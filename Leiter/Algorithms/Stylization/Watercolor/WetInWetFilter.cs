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
/// Implements wet-in-wet edge bleeding for watercolor stylization (Bousseau et al. 2006).
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

        if (regionGraph.Size != image.Size)
        {
            throw new ArgumentException("RegionGraph dimensions must match image dimensions.", nameof(regionGraph));
        }

        if (salientMask is not null && salientMask.Size != image.Size)
        {
            throw new ArgumentException("SalientMask dimensions must match image dimensions.", nameof(salientMask));
        }

        WetInWetOptions opts = options ?? new WetInWetOptions();
        opts.Validate();

        int boundaryCount = regionGraph.Boundaries.Count;
        if (image.Count == 0 || opts.Mode == WetInWetMode.Disabled || boundaryCount == 0)
        {
            return new WetInWetResult(
                image.Clone(),
                new MaskMatrix(image.Size),
                new bool[boundaryCount]);
        }

        double[]? salientFractions = salientMask is null ? null : regionGraph.ComputeRegionFractions(salientMask);
        RegionColorStatistics<Rgb64>[] stats = RegionColorStatistics.Compute(regionGraph, image);

        var (gradX, gradY, _) = SpatialGradients.ComputeColorGradients(image);
        var (avgGradX, avgGradY) = ComputeAverageRegionGradients(regionGraph, gradX, gradY);

        bool[] qualifying = new bool[boundaryCount];
        bool hasQualifying = false;

        for (int bi = 0; bi < boundaryCount; bi++)
        {
            RegionBoundary b = regionGraph.Boundaries[bi];
            bool isSalientA = salientFractions is not null && salientFractions[b.First] >= opts.SalientOverlapThreshold;
            bool isSalientB = salientFractions is not null && salientFractions[b.Second] >= opts.SalientOverlapThreshold;

            if (opts.Mode == WetInWetMode.ExcludeSalientBoundaries && (isSalientA != isSalientB))
            {
                qualifying[bi] = false;
                continue;
            }

            if (opts.Mode == WetInWetMode.ExcludeSalientRegions && (isSalientA || isSalientB))
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
        int maxBand = Math.Max(opts.ScatterRadius, (int)Math.Ceiling(Math.Max(opts.MajorRadius, opts.MinorRadius)));

        RegionBoundaryBand band = regionGraph.ComputeBoundaryBand(
            maxBand,
            boundaryIndex => qualifying[boundaryIndex],
            restrictToIncidentRegions: true);

        var orientations = new SequentialMatrix<Vector2D>(width, height);
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
                        int darkerId = brighterId == b.First ? b.Second : b.First;

                        if (regionGraph.Labels[x, y] == brighterId)
                        {
                            double hash = SplitMix64.HashToUnitDouble(opts.RandomSeed, x, y);
                            if (hash < opts.SeedProbability)
                            {
                                seeded[x, y] = stats[darkerId].MeanColor.ToRgb8();
                            }
                        }
                    }
                }
            }
        }

        Matrix<Rgb8> filtered = OrientedEllipticalFilter.Apply(
            seeded,
            orientations,
            new OrientedEllipticalFilterOptions(opts.MajorRadius, opts.MinorRadius),
            wetAreaMask);

        return new WetInWetResult(filtered, wetAreaMask, qualifying);
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

        if (regionGraph.Size != image.Size)
        {
            throw new ArgumentException("RegionGraph dimensions must match image dimensions.", nameof(regionGraph));
        }

        if (distanceField.Size != image.Size)
        {
            throw new ArgumentException("DistanceField dimensions must match image dimensions.", nameof(distanceField));
        }

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
        IReadOnlyMatrix<Rgb64> gradX,
        IReadOnlyMatrix<Rgb64> gradY)
    {
        int regionCount = regionGraph.RegionCount;
        var avgGradX = new double[regionCount];
        var avgGradY = new double[regionCount];

        for (int r = 0; r < regionCount; r++)
        {
            Region<Coord> region = regionGraph.Regions[r];
            double sumX = 0.0;
            double sumY = 0.0;
            foreach (Coord coord in region.Pixels)
            {
                sumX += SpatialGradients.ChannelNorm(gradX[coord]);
                sumY += SpatialGradients.ChannelNorm(gradY[coord]);
            }

            double invCount = 1.0 / region.Pixels.Count;
            avgGradX[r] = sumX * invCount;
            avgGradY[r] = sumY * invCount;
        }

        return (avgGradX, avgGradY);
    }
}
