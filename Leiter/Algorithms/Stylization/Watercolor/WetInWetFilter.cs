namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using Leiter.Algorithms;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
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
    /// <param name="distanceField">The normalized saliency distance field.</param>
    /// <param name="salientMask">Optional boolean salient mask; if <see langword="null"/>, pixels with distance &lt;= 1e-6 are considered salient.</param>
    /// <param name="options">Optional wet-in-wet configuration options.</param>
    /// <returns>A <see cref="WetInWetResult"/> containing the filtered image, wet area mask, and qualifying boundaries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="regionGraph"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="regionGraph"/>, <paramref name="distanceField"/>, or <paramref name="salientMask"/> do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static WetInWetResult ApplyDetailed(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        IReadOnlyMatrix<bool>? salientMask = null,
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
                image.ToSequentialMatrix(),
                new MaskMatrix(image.Size),
                new bool[boundaryCount]);
        }

        IReadOnlyMatrix<bool> effectiveSalientMask;
        if (salientMask is not null)
        {
            effectiveSalientMask = salientMask;
        }
        else
        {
            var mask = new MaskMatrix(image.Size);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    double d = distanceField[x, y].Value;
                    if (double.IsFinite(d) && d <= 1e-6)
                    {
                        mask[x, y] = true;
                    }
                }
            }

            effectiveSalientMask = mask;
        }

        RegionColorStatistics[] stats = RegionColorStatistics.Compute(regionGraph, image, effectiveSalientMask);

        var (gradX, gradY, _) = SpatialGradients.ComputeColorGradients(image);
        var (avgGradX, avgGradY) = ComputeAverageRegionGradients(regionGraph, gradX, gradY);

        bool[] qualifying = new bool[boundaryCount];
        bool hasQualifying = false;

        for (int bi = 0; bi < boundaryCount; bi++)
        {
            RegionBoundary b = regionGraph.Boundaries[bi];
            bool isSalientA = stats[b.First].SalientFraction >= opts.SalientOverlapThreshold;
            bool isSalientB = stats[b.Second].SalientFraction >= opts.SalientOverlapThreshold;

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
                image.ToSequentialMatrix(),
                new MaskMatrix(image.Size),
                qualifying);
        }

        int width = image.Width;
        int height = image.Height;
        int totalPixels = width * height;

        int maxBand = Math.Max(opts.ScatterRadius, (int)Math.Ceiling(Math.Max(opts.MajorRadius, opts.MinorRadius)));

        int[] nearestDist = new int[totalPixels];
        Array.Fill(nearestDist, int.MaxValue);

        int[] nearestBoundary = new int[totalPixels];
        Array.Fill(nearestBoundary, -1);

        var orientations = new SequentialMatrix<Vector2D>(width, height);
        var queue = new Queue<int>();

        for (int bi = 0; bi < boundaryCount; bi++)
        {
            if (!qualifying[bi])
            {
                continue;
            }

            RegionBoundary b = regionGraph.Boundaries[bi];
            int brighterId = stats[b.First].MeanIntensity >= stats[b.Second].MeanIntensity ? b.First : b.Second;
            int darkerId = brighterId == b.First ? b.Second : b.First;

            EnqueueBoundaryPixels(b.FirstBorderPixels.Span, bi, brighterId, darkerId, regionGraph.Labels, width, height, nearestDist, nearestBoundary, orientations, queue);
            EnqueueBoundaryPixels(b.SecondBorderPixels.Span, bi, brighterId, darkerId, regionGraph.Labels, width, height, nearestDist, nearestBoundary, orientations, queue);
        }

        while (queue.Count > 0)
        {
            int curIdx = queue.Dequeue();
            int curDist = nearestDist[curIdx];
            if (curDist >= maxBand)
            {
                continue;
            }

            int bi = nearestBoundary[curIdx];
            RegionBoundary b = regionGraph.Boundaries[bi];
            int curX = curIdx % width;
            int curY = curIdx / width;
            Vector2D curOrientation = orientations[curX, curY];

            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = curY + dy;
                if (ny < 0 || ny >= height)
                {
                    continue;
                }

                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    int nx = curX + dx;
                    if (nx < 0 || nx >= width)
                    {
                        continue;
                    }

                    int nLabel = regionGraph.Labels[nx, ny];
                    if (nLabel != b.First && nLabel != b.Second)
                    {
                        continue;
                    }

                    int nIdx = (ny * width) + nx;
                    int nDist = curDist + 1;

                    if (nDist < nearestDist[nIdx])
                    {
                        nearestDist[nIdx] = nDist;
                        nearestBoundary[nIdx] = bi;
                        orientations[nx, ny] = curOrientation;
                        queue.Enqueue(nIdx);
                    }
                }
            }
        }

        Matrix<Rgb8> seeded = image.ToSequentialMatrix();
        var wetAreaMask = new MaskMatrix(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = (y * width) + x;
                int dist = nearestDist[idx];
                if (dist <= maxBand)
                {
                    wetAreaMask[x, y] = true;

                    if (dist <= opts.ScatterRadius)
                    {
                        int bi = nearestBoundary[idx];
                        RegionBoundary b = regionGraph.Boundaries[bi];
                        int brighterId = stats[b.First].MeanIntensity >= stats[b.Second].MeanIntensity ? b.First : b.Second;
                        int darkerId = brighterId == b.First ? b.Second : b.First;

                        if (regionGraph.Labels[x, y] == brighterId)
                        {
                            double hash = HashSplitMix64(opts.RandomSeed, x, y);
                            if (hash < opts.SeedProbability)
                            {
                                seeded[x, y] = stats[darkerId].MeanRgb.ToRgb8();
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
    /// Applies wet-in-wet edge bleeding to an image and returns the filtered RGB result matrix.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="regionGraph">The region adjacency graph representing the image partition.</param>
    /// <param name="distanceField">The normalized saliency distance field.</param>
    /// <param name="salientMask">Optional boolean salient mask; if <see langword="null"/>, pixels with distance &lt;= 1e-6 are considered salient.</param>
    /// <param name="options">Optional wet-in-wet configuration options.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the edge-bled image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="regionGraph"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="regionGraph"/>, <paramref name="distanceField"/>, or <paramref name="salientMask"/> do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        IReadOnlyMatrix<bool>? salientMask = null,
        WetInWetOptions? options = null) =>
        ApplyDetailed(image, regionGraph, distanceField, salientMask, options).Image;

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

    private static void EnqueueBoundaryPixels(
        ReadOnlySpan<Coord> borderPixels,
        int boundaryIndex,
        int brighterId,
        int darkerId,
        IReadOnlyMatrix<int> labels,
        int width,
        int height,
        int[] nearestDist,
        int[] nearestBoundary,
        SequentialMatrix<Vector2D> orientations,
        Queue<int> queue)
    {
        for (int i = 0; i < borderPixels.Length; i++)
        {
            Coord p = borderPixels[i];
            int idx = (p.Y * width) + p.X;
            if (nearestDist[idx] > 0)
            {
                Vector2D normal = ComputeBoundaryNormal(p, brighterId, darkerId, labels, width, height);
                nearestDist[idx] = 0;
                nearestBoundary[idx] = boundaryIndex;
                orientations[p] = normal;
                queue.Enqueue(idx);
            }
        }
    }

    private static Vector2D ComputeBoundaryNormal(
        Coord p,
        int brighterId,
        int darkerId,
        IReadOnlyMatrix<int> labels,
        int width,
        int height)
    {
        int pLabel = labels[p];
        double sumX = 0.0;
        double sumY = 0.0;

        for (int dy = -1; dy <= 1; dy++)
        {
            int ny = p.Y + dy;
            if (ny < 0 || ny >= height)
            {
                continue;
            }

            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                {
                    continue;
                }

                int nx = p.X + dx;
                if (nx < 0 || nx >= width)
                {
                    continue;
                }

                int qLabel = labels[nx, ny];
                if (pLabel == brighterId && qLabel == darkerId)
                {
                    sumX += p.X - nx;
                    sumY += p.Y - ny;
                }
                else if (pLabel == darkerId && qLabel == brighterId)
                {
                    sumX += nx - p.X;
                    sumY += ny - p.Y;
                }
            }
        }

        var normal = new Vector2D(sumX, sumY).Normalize();
        return normal == Vector2D.Zero ? new Vector2D(1.0, 0.0) : normal;
    }

    private static double HashSplitMix64(int seed, int x, int y)
    {
        ulong z = ((ulong)(uint)seed * 0x9E3779B97F4A7C15UL)
                ^ ((ulong)(uint)x * 0xBF58476D1CE4E5B9UL)
                ^ ((ulong)(uint)y * 0x94D049BB133111EBUL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        return (z >> 11) * (1.0 / 9007199254740992.0);
    }
}
