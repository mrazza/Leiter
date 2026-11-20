namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Threading.Tasks;
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

        WetInWetOptions opts = OptionsResolver.Resolve(options, WetInWetOptions.Default);
        return ApplyDetailedCore(image, regionGraph, salientMask, precomputedSalientFractions: null, opts);
    }

    internal static WetInWetResult ApplyDetailedCore(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<bool>? salientMask,
        double[]? precomputedSalientFractions,
        WetInWetOptions opts)
    {
        OrientedEllipticalFilterOptions filterOpts = opts.EllipticalFilter ?? WetInWetOptions.DefaultEllipticalFilter;

        int boundaryCount = regionGraph.Boundaries.Count;
        if (image.Count == 0 || opts.Mode == WetInWetMode.Disabled || boundaryCount == 0)
        {
            return CreateUnmodifiedResult(image, boundaryCount);
        }

        bool[]? salientRegions = precomputedSalientFractions is not null
            ? RegionGraph.ComputeRegionFlags(precomputedSalientFractions, opts.SalientOverlapThreshold)
            : salientMask is null
                ? null
                : regionGraph.ComputeRegionFlags(salientMask, opts.SalientOverlapThreshold);
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

        Size size = image.Size;
        int width = size.Width;
        int height = size.Height;
        var boundaryPolarity = new (int BrighterId, int DarkerId)[boundaryCount];
        for (int bi = 0; bi < boundaryCount; bi++)
        {
            if (!qualifying[bi])
                continue;

            RegionBoundary b = regionGraph.Boundaries[bi];
            int brighterId = stats[b.First].MeanIntensity >= stats[b.Second].MeanIntensity ? b.First : b.Second;
            int darkerId = brighterId == b.First ? b.Second : b.First;
            boundaryPolarity[bi] = (brighterId, darkerId);
        }

        double filterRadius = Math.Ceiling(Math.Max(filterOpts.MajorRadius, filterOpts.MinorRadius));
        int maxBand = Math.Max(opts.ScatterRadius, (int)Math.Min(filterRadius, width + height));

        RegionBoundaryBand band = regionGraph.ComputeBoundaryBand(
            maxBand,
            boundaryIndex => qualifying[boundaryIndex],
            restrictToIncidentRegions: true);

        var orientations = new SequentialMatrix<Vector2D>(size);
        var seedDarkerColors = new Rgb8[size.Count];
        ReadOnlySpan<GridNeighbor> graphNeighbors = GridNeighborhood.GetAll(regionGraph.Connectivity);
        ReadOnlySpan<Coord> seedPixels = band.SeedPixels.Span;
        for (int s = 0; s < seedPixels.Length; s++)
        {
            Coord p = seedPixels[s];
            int bi = band.NearestBoundaryIndices[p];
            (int brighterId, int darkerId) = boundaryPolarity[bi];

            Vector2D normal = regionGraph.ComputeBoundaryNormal(p, brighterId, darkerId);
            orientations[p] = normal == Vector2D.Zero ? new Vector2D(1.0, 0.0) : normal;

            int pIdx = size.IndexOf(p);
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
                    if (size.Contains(q) && regionGraph.Labels[q] == darkerId)
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
        var wetAreaMask = new MaskMatrix(size);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int dist = band.Distances[x, y];
                if (dist <= maxBand)
                {
                    int bi = band.NearestBoundaryIndices[x, y];
                    int brighterId = boundaryPolarity[bi].BrighterId;
                    bool isBrighterSide = regionGraph.Labels[x, y] == brighterId;

                    if (!isBrighterSide && dist > 1)
                    {
                        continue;
                    }

                    wetAreaMask[x, y] = true;
                    if (dist > 0)
                    {
                        orientations[x, y] = orientations[band.NearestSeedIndices[x, y]];
                    }

                    if (isBrighterSide && dist <= opts.ScatterRadius)
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

        Matrix<Rgb8> filtered = OrientedEllipticalFilter.ApplyCore(
            seeded,
            orientations,
            filterOpts,
            wetAreaMask);

        var composited = new SequentialMatrix<Rgb8>(size);
        Parallel.For(0, height, y =>
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
                int brighterId = boundaryPolarity[bi].BrighterId;
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
        });

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

        WetInWetOptions opts = OptionsResolver.Resolve(options, WetInWetOptions.Default);
        int boundaryCount = regionGraph.Boundaries.Count;
        if (image.Count == 0 || opts.Mode == WetInWetMode.Disabled || boundaryCount == 0)
        {
            return CreateUnmodifiedResult(image, boundaryCount);
        }

        MaskMatrix salientMask = SaliencyDistanceField.ExtractSalientMask(distanceField);
        return ApplyDetailedCore(image, regionGraph, salientMask, precomputedSalientFractions: null, opts);
    }

    private static WetInWetResult CreateUnmodifiedResult(IReadOnlyMatrix<Rgb8> image, int boundaryCount) =>
        new(
            image.Clone(),
            new MaskMatrix(image.Size),
            new bool[boundaryCount]);

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
        Size size = image.Size;
        int regionCount = regionGraph.RegionCount;
        var avgGradX = new double[regionCount];
        var avgGradY = new double[regionCount];
        if (regionCount == 0)
            return (avgGradX, avgGradY);

        IReadOnlyMatrix<int> labels = regionGraph.Labels;
        (double w0, double w1, double w2, double scale) = SpatialGradients.GetKernelWeights(GradientOperator.Sobel);
        double normalizedScale = scale / 255.0;

        Parallel.For(0, regionCount, r =>
        {
            ReadOnlySpan<Coord> pixels = regionGraph.GetRegionPixels(r);
            int count = regionGraph.GetRegionPixelCount(r);
            if (count == 0)
                return;

            double sumX = 0.0;
            double sumY = 0.0;
            for (int i = 0; i < pixels.Length; i++)
            {
                Coord coord = pixels[i];
                int x = coord.X;
                int y = coord.Y;
                Rgb8 center = image[coord];

                var (gx, gy) = SpatialGradients.ComputeRgb8GradientAt(
                    SampleRegionClamped(image, labels, size, x - 1, y - 1, r, center),
                    SampleRegionClamped(image, labels, size, x, y - 1, r, center),
                    SampleRegionClamped(image, labels, size, x + 1, y - 1, r, center),
                    SampleRegionClamped(image, labels, size, x - 1, y, r, center),
                    SampleRegionClamped(image, labels, size, x + 1, y, r, center),
                    SampleRegionClamped(image, labels, size, x - 1, y + 1, r, center),
                    SampleRegionClamped(image, labels, size, x, y + 1, r, center),
                    SampleRegionClamped(image, labels, size, x + 1, y + 1, r, center),
                    w0,
                    w1,
                    w2,
                    normalizedScale);

                sumX += SpatialGradients.ChannelNorm(gx);
                sumY += SpatialGradients.ChannelNorm(gy);
            }

            double invCount = 1.0 / count;
            avgGradX[r] = sumX * invCount;
            avgGradY[r] = sumY * invCount;
        });

        return (avgGradX, avgGradY);
    }

    private static Rgb8 SampleRegionClamped(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<int> labels,
        Size size,
        int x,
        int y,
        int regionLabel,
        Rgb8 fallback) =>
        (size.Contains(x, y) && labels[x, y] == regionLabel)
            ? image[x, y]
            : fallback;
}
