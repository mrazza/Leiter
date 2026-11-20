namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Saliency;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Implements wet-in-wet edge bleeding for watercolor stylization (Wang et al. 2014, Sec. 3.4).
/// Bleeds pigments across qualifying low-contrast boundaries by scattering pigment seeds from darker to brighter regions
/// and smoothing the wet region with an oriented elliptical Gaussian filter.
/// </summary>
public static class WetInWetFilter
{
    /// <summary>
    /// Applies wet-in-wet edge bleeding to an image and returns a detailed <see cref="WetInWetResult"/>
    /// containing the filtered image, the binary wet area mask, and qualifying boundary classifications.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="regionGraph">The region adjacency graph representing the image partition.</param>
    /// <param name="salientMask">Optional boolean salient mask; when <see langword="null"/>, no regions are treated as salient.</param>
    /// <param name="options">Optional wet-in-wet configuration options.</param>
    /// <returns>A <see cref="WetInWetResult"/> containing the filtered image, wet area mask, and qualifying boundaries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="regionGraph"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="regionGraph"/> or <paramref name="salientMask"/> do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static WetInWetResult ApplyDetailed(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<bool>? salientMask = null,
        WetInWetOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(regionGraph);
        Guard.ThrowIfSizeMismatch(image.Size, regionGraph.Size, nameof(regionGraph));
        Guard.ThrowIfSizeMismatchIfNotNull(image, salientMask);

        WetInWetOptions opts = options ?? WetInWetOptions.Default;
        opts.Validate();
        OrientedEllipticalFilterOptions filterOpts = opts.EllipticalFilter ?? WetInWetOptions.DefaultEllipticalFilter;

        int boundaryCount = regionGraph.Boundaries.Count;
        if (image.Count == 0 || opts.Mode == WetInWetMode.Disabled || boundaryCount == 0)
        {
            return new WetInWetResult(
                image.Clone(),
                new MaskMatrix(image.Size),
                new bool[boundaryCount]);
        }

        bool[]? salientRegions = salientMask is null ? null : regionGraph.ComputeRegionFlags(salientMask, opts.SalientOverlapThreshold);
        RegionColorStatistics<Rgb64>[] stats = RegionColorStatistics.Compute(regionGraph, image);

        var (avgGradX, avgGradY) = ComputeAverageRegionGradients(regionGraph, image);

        bool[] qualifying = new bool[boundaryCount];
        bool hasQualifying = false;

        for (int bi = 0; bi < boundaryCount; bi++)
        {
            RegionBoundary b = regionGraph.Boundaries[bi];
            bool isSalientA = salientRegions is not null && salientRegions[b.First];
            bool isSalientB = salientRegions is not null && salientRegions[b.Second];

            if (opts.Mode == WetInWetMode.ExcludeSalientBoundaries && (isSalientA != isSalientB))
            {
                qualifying[bi] = false;
                continue;
            }

            if (opts.Mode == WetInWetMode.ExcludeSalientRegions && (isSalientA && isSalientB))
            {
                qualifying[bi] = false;
                continue;
            }

            double intensityDiff = Math.Abs(stats[b.First].MeanIntensity - stats[b.Second].MeanIntensity);
            if (intensityDiff < opts.MinIntensityDifference)
            {
                qualifying[bi] = false;
                continue;
            }

            bool gradRule = (avgGradX[b.First] >= opts.ColorGradientThreshold && avgGradY[b.First] >= opts.ColorGradientThreshold) ||
                            (avgGradX[b.Second] >= opts.ColorGradientThreshold && avgGradY[b.Second] >= opts.ColorGradientThreshold);

            double hueDiff = ColorMetrics.HueDifferenceDegrees(stats[b.First].MeanHueDegrees, stats[b.Second].MeanHueDegrees);
            bool hueRule = (isSalientA || isSalientB)
                ? (hueDiff < opts.SalientHueThresholdDegrees)
                : (hueDiff < opts.NonSalientHueThresholdDegrees);

            bool qualifies = gradRule || hueRule;
            qualifying[bi] = qualifies;
            if (qualifies)
            {
                hasQualifying = true;
            }
        }

        if (!hasQualifying)
        {
            return new WetInWetResult(
                image.Clone(),
                new MaskMatrix(image.Size),
                qualifying);
        }

        int width = image.Width;
        int height = image.Height;
        int maxBand = Math.Max(opts.ScatterRadius, (int)Math.Ceiling(Math.Max(filterOpts.MajorRadius, filterOpts.MinorRadius)));

        RegionBoundaryBand band = regionGraph.ComputeBoundaryBand(
            maxBand,
            boundaryIndex => qualifying[boundaryIndex],
            restrictToIncidentRegions: true);

        var orientations = new SequentialMatrix<Vector2D>(width, height);
        var seedDarkerColors = new Rgb8[width * height];
        ReadOnlySpan<GridNeighbor> graphNeighbors = GridNeighborhood.GetAll(regionGraph.Connectivity);
        ReadOnlySpan<Coord> seedPixels = band.SeedPixels.Span;
        for (int s = 0; s < seedPixels.Length; s++)
        {
            Coord p = seedPixels[s];
            int bi = band.NearestBoundaryIndices[p];
            RegionBoundary b = regionGraph.Boundaries[bi];
            int brighterId = stats[b.First].MeanIntensity >= stats[b.Second].MeanIntensity ? b.First : b.Second;
            int darkerId = brighterId == b.First ? b.Second : b.First;

            Vector2D normal = regionGraph.ComputeBoundaryNormal(p, brighterId, darkerId);
            orientations[p] = normal == Vector2D.Zero ? new Vector2D(1.0, 0.0) : normal;

            int pIdx = (p.Y * width) + p.X;
            if (regionGraph.Labels[p] == darkerId)
            {
                seedDarkerColors[pIdx] = image[p];
            }
            else
            {
                int sumR = 0;
                int sumG = 0;
                int sumB = 0;
                int count = 0;
                for (int n = 0; n < graphNeighbors.Length; n++)
                {
                    Coord q = p + graphNeighbors[n].Offset;
                    if (regionGraph.Labels.Contains(q) && regionGraph.Labels[q] == darkerId)
                    {
                        Rgb8 qCol = image[q];
                        sumR += qCol.R;
                        sumG += qCol.G;
                        sumB += qCol.B;
                        count++;
                    }
                }

                seedDarkerColors[pIdx] = count > 0
                    ? new Rgb8(
                        Rgb8.ClampRoundedByte((double)sumR / count),
                        Rgb8.ClampRoundedByte((double)sumG / count),
                        Rgb8.ClampRoundedByte((double)sumB / count))
                    : stats[darkerId].MeanColor.ToRgb8();
            }
        }

        Matrix<Rgb8> seeded = image.Clone();
        var wetAreaMask = new MaskMatrix(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int dist = band.Distances[x, y];
                if (dist <= maxBand)
                {
                    wetAreaMask[x, y] = true;
                    if (dist > 0)
                    {
                        orientations[x, y] = orientations[band.NearestSeedIndices[x, y]];
                    }

                    if (dist <= opts.ScatterRadius)
                    {
                        int bi = band.NearestBoundaryIndices[x, y];
                        RegionBoundary b = regionGraph.Boundaries[bi];
                        int brighterId = stats[b.First].MeanIntensity >= stats[b.Second].MeanIntensity ? b.First : b.Second;

                        if (regionGraph.Labels[x, y] == brighterId)
                        {
                            double falloff = 1.0 - ((double)dist / (opts.ScatterRadius + 1));
                            double effectiveProbability = opts.SeedProbability * falloff * falloff;
                            double hash = SplitMix64.HashToUnitDouble(opts.Seed, x, y);
                            if (hash < effectiveProbability)
                            {
                                seeded[x, y] = seedDarkerColors[band.NearestSeedIndices[x, y]];
                            }
                        }
                    }
                }
            }
        }

        Matrix<Rgb8> filtered = OrientedEllipticalFilter.Apply(
            seeded,
            orientations,
            filterOpts,
            wetAreaMask);

        var composited = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!wetAreaMask[x, y])
                {
                    composited[x, y] = image[x, y];
                    continue;
                }

                int dist = band.Distances[x, y];
                int bi = band.NearestBoundaryIndices[x, y];
                RegionBoundary b = regionGraph.Boundaries[bi];
                int brighterId = stats[b.First].MeanIntensity >= stats[b.Second].MeanIntensity ? b.First : b.Second;
                bool isBrighterSide = regionGraph.Labels[x, y] == brighterId;

                double alpha = isBrighterSide
                    ? ScalarMath.OuterBandSmoothstepWeight(dist, opts.ScatterRadius, maxBand)
                    : dist == 0 ? 0.45 : dist == 1 ? 0.15 : 0.0;

                if (alpha >= 1.0)
                {
                    composited[x, y] = filtered[x, y];
                }
                else if (alpha <= 0.0)
                {
                    composited[x, y] = image[x, y];
                }
                else
                {
                    composited[x, y] = Rgb8.Lerp(image[x, y], filtered[x, y], alpha);
                }
            }
        }

        return new WetInWetResult(composited, wetAreaMask, qualifying);
    }

    /// <summary>
    /// Applies wet-in-wet edge bleeding to an image using a saliency distance field (thresholded at <c>1e-6</c> to identify salient pixels)
    /// and returns a detailed <see cref="WetInWetResult"/>.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="regionGraph">The region adjacency graph representing the image partition.</param>
    /// <param name="distanceField">The normalized saliency distance field.</param>
    /// <param name="options">Optional wet-in-wet configuration options.</param>
    /// <returns>A <see cref="WetInWetResult"/> containing the filtered image, wet area mask, and qualifying boundaries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="regionGraph"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="regionGraph"/> or <paramref name="distanceField"/> do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static WetInWetResult ApplyDetailed(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        WetInWetOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(regionGraph);
        ArgumentNullException.ThrowIfNull(distanceField);
        Guard.ThrowIfSizeMismatch(image.Size, regionGraph.Size, nameof(regionGraph));
        Guard.ThrowIfSizeMismatch(image, distanceField);

        return ApplyDetailed(image, regionGraph, SaliencyDistanceField.ExtractSalientMask(distanceField), options);
    }

    /// <summary>
    /// Applies wet-in-wet edge bleeding to an image and returns the filtered RGB result matrix.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="regionGraph">The region adjacency graph representing the image partition.</param>
    /// <param name="salientMask">Optional boolean salient mask; when <see langword="null"/>, no regions are treated as salient.</param>
    /// <param name="options">Optional wet-in-wet configuration options.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the edge-bled image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="regionGraph"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="regionGraph"/> or <paramref name="salientMask"/> do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<bool>? salientMask = null,
        WetInWetOptions? options = null) =>
        ApplyDetailed(image, regionGraph, salientMask, options).Image;

    /// <summary>
    /// Applies wet-in-wet edge bleeding to an image using a saliency distance field (thresholded at <c>1e-6</c>)
    /// and returns the filtered RGB result matrix.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="regionGraph">The region adjacency graph representing the image partition.</param>
    /// <param name="distanceField">The normalized saliency distance field.</param>
    /// <param name="options">Optional wet-in-wet configuration options.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the edge-bled image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="regionGraph"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="regionGraph"/> or <paramref name="distanceField"/> do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        WetInWetOptions? options = null) =>
        ApplyDetailed(image, regionGraph, distanceField, options).Image;

    private static (double[] AvgGradX, double[] AvgGradY) ComputeAverageRegionGradients(
        RegionGraph regionGraph,
        IReadOnlyMatrix<Rgb8> image)
    {
        int width = image.Width;
        int height = image.Height;
        int regionCount = regionGraph.RegionCount;
        var sumGradX = new double[regionCount];
        var sumGradY = new double[regionCount];
        IReadOnlyMatrix<int> labels = regionGraph.Labels;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int label = labels[x, y];
                Rgb8 center = image[x, y];

                var (gx, gy) = SpatialGradients.ComputeRgb8GradientAt(
                    SampleRegionClamped(image, labels, x - 1, y - 1, width, height, label, center),
                    SampleRegionClamped(image, labels, x, y - 1, width, height, label, center),
                    SampleRegionClamped(image, labels, x + 1, y - 1, width, height, label, center),
                    SampleRegionClamped(image, labels, x - 1, y, width, height, label, center),
                    SampleRegionClamped(image, labels, x + 1, y, width, height, label, center),
                    SampleRegionClamped(image, labels, x - 1, y + 1, width, height, label, center),
                    SampleRegionClamped(image, labels, x, y + 1, width, height, label, center),
                    SampleRegionClamped(image, labels, x + 1, y + 1, width, height, label, center),
                    GradientOperator.Sobel);

                sumGradX[label] += SpatialGradients.ChannelNorm(gx);
                sumGradY[label] += SpatialGradients.ChannelNorm(gy);
            }
        }

        var avgGradX = new double[regionCount];
        var avgGradY = new double[regionCount];
        for (int r = 0; r < regionCount; r++)
        {
            double invCount = 1.0 / regionGraph.Regions[r].Pixels.Count;
            avgGradX[r] = sumGradX[r] * invCount;
            avgGradY[r] = sumGradY[r] * invCount;
        }

        return (avgGradX, avgGradY);
    }

    private static Rgb8 SampleRegionClamped(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<int> labels,
        int x,
        int y,
        int width,
        int height,
        int regionLabel,
        Rgb8 fallback) =>
        ((uint)x < (uint)width && (uint)y < (uint)height && labels[x, y] == regionLabel)
            ? image[x, y]
            : fallback;
}
