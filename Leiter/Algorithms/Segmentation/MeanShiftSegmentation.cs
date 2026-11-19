namespace Leiter.Algorithms.Segmentation;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Provides Mean-Shift filtering and image segmentation algorithms operating in joint spatial and CIELAB color feature space.
/// </summary>
public static class MeanShiftSegmentation
{
    /// <summary>
    /// The spatial radius threshold above which the 5D Compressed Sparse Row (CSR) spatial lattice is used for mode seeking.
    /// </summary>
    internal const int LatticeRadiusThreshold = 16;

    /// <summary>
    /// Performs edge-preserving Mean-Shift smoothing on a CIELAB image.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to filter.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>A filtered <see cref="Matrix{Lab32}"/> containing the converged mode color at each pixel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static Matrix<Lab32> Filter(IReadOnlyMatrix<Lab32> labImage, MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(labImage);
        var resolvedOptions = options ?? MeanShiftOptions.Default;
        resolvedOptions.Validate();

        if (labImage.Count == 0)
            return new SequentialMatrix<Lab32>(labImage.Width, labImage.Height);

        (MeanShiftMode[] modes, _) = SeekModes(labImage, resolvedOptions);
        return BuildFilteredLabMatrix(labImage.Width, labImage.Height, modes);
    }

    /// <summary>
    /// Performs edge-preserving Mean-Shift smoothing on an 8-bit sRGB image.
    /// </summary>
    /// <param name="image">The input sRGB image to filter.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>A filtered <see cref="Matrix{Rgb8}"/> containing the converged mode color at each pixel converted to sRGB.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static Matrix<Rgb8> Filter(IReadOnlyMatrix<Rgb8> image, MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var resolvedOptions = options ?? MeanShiftOptions.Default;
        resolvedOptions.Validate();

        if (image.Count == 0)
            return new SequentialMatrix<Rgb8>(image.Width, image.Height);

        Matrix<Lab32> labMatrix = image.Map(static pixel => pixel.ToLab32(RgbColorSpace.SRgb));
        (MeanShiftMode[] modes, _) = SeekModes(labMatrix, resolvedOptions);
        return BuildFilteredLabMatrix(image.Width, image.Height, modes).Map(static pixel => pixel.ToRgb8(RgbColorSpace.SRgb));
    }

    /// <summary>
    /// Simultaneously performs edge-preserving Mean-Shift smoothing and region segmentation on a CIELAB image,
    /// sharing a single mode-seeking pass.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to filter and segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>A tuple containing the filtered <see cref="Matrix{Lab32}"/> image and the partitioned <see cref="IDisjointSet"/> regions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static (Matrix<Lab32> FilteredImage, IDisjointSet Segmentation) FilterAndSegment(
        IReadOnlyMatrix<Lab32> labImage,
        MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(labImage);
        var resolvedOptions = options ?? MeanShiftOptions.Default;
        resolvedOptions.Validate();

        if (labImage.Count == 0)
        {
            return (
                new SequentialMatrix<Lab32>(labImage.Width, labImage.Height),
                new DisjointSet<byte>(labImage, static _ => 0));
        }

        (MeanShiftMode[] modes, Lab32[] sourceLab) = SeekModes(labImage, resolvedOptions);
        Matrix<Lab32> filtered = BuildFilteredLabMatrix(labImage.Width, labImage.Height, modes);
        IDisjointSet segmentation = SegmentFromModes(labImage, sourceLab, modes, resolvedOptions);
        return (filtered, segmentation);
    }

    /// <summary>
    /// Simultaneously performs edge-preserving Mean-Shift smoothing and region segmentation on an 8-bit sRGB image,
    /// sharing a single mode-seeking pass.
    /// </summary>
    /// <param name="image">The input sRGB image to filter and segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>A tuple containing the filtered <see cref="Matrix{Rgb8}"/> image and the partitioned <see cref="IDisjointSet"/> regions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static (Matrix<Rgb8> FilteredImage, IDisjointSet Segmentation) FilterAndSegment(
        IReadOnlyMatrix<Rgb8> image,
        MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var resolvedOptions = options ?? MeanShiftOptions.Default;
        resolvedOptions.Validate();

        if (image.Count == 0)
        {
            return (
                new SequentialMatrix<Rgb8>(image.Width, image.Height),
                new DisjointSet<byte>(image, static _ => 0));
        }

        Matrix<Lab32> labMatrix = image.Map(static pixel => pixel.ToLab32(RgbColorSpace.SRgb));
        (MeanShiftMode[] modes, Lab32[] sourceLab) = SeekModes(labMatrix, resolvedOptions);
        Matrix<Rgb8> filtered = BuildFilteredLabMatrix(image.Width, image.Height, modes).Map(static pixel => pixel.ToRgb8(RgbColorSpace.SRgb));
        IDisjointSet segmentation = SegmentFromModes(image, sourceLab, modes, resolvedOptions);
        return (filtered, segmentation);
    }

    private static Matrix<Lab32> BuildFilteredLabMatrix(int width, int height, MeanShiftMode[] modes)
    {
        var resultData = new Lab32[modes.Length];
        for (int i = 0; i < resultData.Length; i++)
        {
            resultData[i] = modes[i].Color;
        }

        return new SequentialMatrix<Lab32>(width, height, resultData);
    }

    /// <summary>
    /// Segments a CIELAB image into disjoint sets of pixels using Mean-Shift mode clustering and region pruning.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>An <see cref="IDisjointSet"/> representing the partitioned regions of the image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static IDisjointSet Segment(IReadOnlyMatrix<Lab32> labImage, MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(labImage);
        var resolvedOptions = options ?? MeanShiftOptions.Default;
        resolvedOptions.Validate();

        if (labImage.Count == 0)
            return new DisjointSet<byte>(labImage, static _ => 0);

        (MeanShiftMode[] modes, Lab32[] sourceLab) = SeekModes(labImage, resolvedOptions);
        return SegmentFromModes(labImage, sourceLab, modes, resolvedOptions);
    }

    /// <summary>
    /// Segments an 8-bit sRGB image into disjoint sets of pixels using Mean-Shift mode clustering and region pruning.
    /// </summary>
    /// <param name="image">The input sRGB image to segment.</param>
    /// <param name="options">The Mean-Shift configuration options, or <see langword="null"/> to use default options.</param>
    /// <returns>An <see cref="IDisjointSet"/> representing the partitioned regions of the image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static IDisjointSet Segment(IReadOnlyMatrix<Rgb8> image, MeanShiftOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        var resolvedOptions = options ?? MeanShiftOptions.Default;
        resolvedOptions.Validate();

        if (image.Count == 0)
            return new DisjointSet<byte>(image, static _ => 0);

        Matrix<Lab32> labMatrix = image.Map(static pixel => pixel.ToLab32(RgbColorSpace.SRgb));
        (MeanShiftMode[] modes, Lab32[] sourceLab) = SeekModes(labMatrix, resolvedOptions);
        return SegmentFromModes(image, sourceLab, modes, resolvedOptions);
    }

    private static (MeanShiftMode[] Modes, Lab32[] SourceLab) SeekModes(
        IReadOnlyMatrix<Lab32> labImage,
        MeanShiftOptions options)
    {
        int width = labImage.Width;
        int height = labImage.Height;
        int pixelCount = width * height;
        Lab32[] sourceLab = labImage.ToArray();

        var modes = new MeanShiftMode[pixelCount];
        if (pixelCount == 0)
            return (modes, sourceLab);

        int hs = options.SpatialRadius;
        double hr = options.RangeRadius;
        double hsSq = hs * hs;
        double hrSq = hr * hr;
        double convergenceEpsSq = options.ConvergenceEpsilon * options.ConvergenceEpsilon;
        int maxIterations = options.MaxIterations;

        int sampleStride = (hs >= 20) ? Math.Clamp(hs / 10, 2, 4) : 1;
        MeanShiftSpatialLattice? lattice = null;
        if (hs > LatticeRadiusThreshold)
        {
            lattice = MeanShiftSpatialLattice.Create(sourceLab, width, height, hs, hr, sampleStride);
        }

        int coarseStride = (hs >= 12 && width >= 8 && height >= 8)
            ? Math.Clamp(hs / 6, 2, 4)
            : 1;

        if (coarseStride == 1)
        {
            Parallel.For(0, height, y =>
            {
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    modes[rowStart + x] = SeekSingleMode(
                        sourceLab,
                        width,
                        height,
                        x,
                        y,
                        hs,
                        hsSq,
                        hrSq,
                        convergenceEpsSq,
                        maxIterations,
                        lattice);
                }
            });

            return (modes, sourceLab);
        }

        int[] coarseXs = BuildCoarseCoordinates(width, coarseStride);
        int[] coarseYs = BuildCoarseCoordinates(height, coarseStride);

        Parallel.For(0, coarseYs.Length, yi =>
        {
            int cy = coarseYs[yi];
            int rowStart = cy * width;
            for (int xi = 0; xi < coarseXs.Length; xi++)
            {
                int cx = coarseXs[xi];
                modes[rowStart + cx] = SeekSingleMode(
                    sourceLab,
                    width,
                    height,
                    cx,
                    cy,
                    hs,
                    hsSq,
                    hrSq,
                    convergenceEpsSq,
                    maxIterations,
                    lattice);
            }
        });

        double modeColorTolSq = 0.25 * hrSq;
        double sourceColorTolSq = 0.25 * hrSq;
        double cornerMatchTolSq = 0.1225 * hrSq;

        Parallel.For(0, height, y =>
        {
            int y0;
            int y1;
            double ty;
            bool isCoarseY;
            if (y == height - 1)
            {
                y0 = y1 = y;
                ty = 0.0;
                isCoarseY = true;
            }
            else
            {
                y0 = (y / coarseStride) * coarseStride;
                y1 = Math.Min(y0 + coarseStride, height - 1);
                ty = (y1 > y0) ? (double)(y - y0) / (y1 - y0) : 0.0;
                isCoarseY = (y == y0);
            }

            int row0 = y0 * width;
            int row1 = y1 * width;
            int rowStart = y * width;

            for (int x = 0; x < width; x++)
            {
                int x0;
                int x1;
                double tx;
                bool isCoarseX;
                if (x == width - 1)
                {
                    x0 = x1 = x;
                    tx = 0.0;
                    isCoarseX = true;
                }
                else
                {
                    x0 = (x / coarseStride) * coarseStride;
                    x1 = Math.Min(x0 + coarseStride, width - 1);
                    tx = (x1 > x0) ? (double)(x - x0) / (x1 - x0) : 0.0;
                    isCoarseX = (x == x0);
                }

                if (isCoarseX && isCoarseY)
                    continue;

                MeanShiftMode m00 = modes[row0 + x0];
                MeanShiftMode m10 = modes[row0 + x1];
                MeanShiftMode m01 = modes[row1 + x0];
                MeanShiftMode m11 = modes[row1 + x1];

                double w00 = (1.0 - tx) * (1.0 - ty);
                double w10 = tx * (1.0 - ty);
                double w01 = (1.0 - tx) * ty;
                double w11 = tx * ty;

                Lab32 c00 = sourceLab[row0 + x0];
                Lab32 c10 = sourceLab[row0 + x1];
                Lab32 c01 = sourceLab[row1 + x0];
                Lab32 c11 = sourceLab[row1 + x1];
                Lab32 pixelColor = sourceLab[rowStart + x];

                if (m00.Color.EuclideanDistanceSquared(m10.Color) <= modeColorTolSq &&
                    m00.Color.EuclideanDistanceSquared(m01.Color) <= modeColorTolSq &&
                    m00.Color.EuclideanDistanceSquared(m11.Color) <= modeColorTolSq &&
                    m00.SpatialDistanceSquared(m10) <= hsSq &&
                    m00.SpatialDistanceSquared(m01) <= hsSq &&
                    m00.SpatialDistanceSquared(m11) <= hsSq)
                {
                    var interpSrc = new Lab32(
                        (float)((w00 * c00.L) + (w10 * c10.L) + (w01 * c01.L) + (w11 * c11.L)),
                        (float)((w00 * c00.A) + (w10 * c10.A) + (w01 * c01.A) + (w11 * c11.A)),
                        (float)((w00 * c00.B) + (w10 * c10.B) + (w01 * c01.B) + (w11 * c11.B)));

                    if (pixelColor.EuclideanDistanceSquared(interpSrc) <= sourceColorTolSq &&
                        pixelColor.EuclideanDistanceSquared(m00.Color) <= hrSq)
                    {
                        modes[rowStart + x] = new MeanShiftMode(
                            (w00 * m00.X) + (w10 * m10.X) + (w01 * m01.X) + (w11 * m11.X),
                            (w00 * m00.Y) + (w10 * m10.Y) + (w01 * m01.Y) + (w11 * m11.Y),
                            new Lab32(
                                (float)((w00 * m00.Color.L) + (w10 * m10.Color.L) + (w01 * m01.Color.L) + (w11 * m11.Color.L)),
                                (float)((w00 * m00.Color.A) + (w10 * m10.Color.A) + (w01 * m01.Color.A) + (w11 * m11.Color.A)),
                                (float)((w00 * m00.Color.B) + (w10 * m10.Color.B) + (w01 * m01.Color.B) + (w11 * m11.Color.B))));
                        continue;
                    }
                }
                else if (!isCoarseX && !isCoarseY)
                {
                    int matchCount = 0;
                    MeanShiftMode refMode = default;
                    bool allMatchingAgree = true;
                    double sumW = 0.0;
                    double sumX = 0.0;
                    double sumY = 0.0;
                    double sumL = 0.0;
                    double sumA = 0.0;
                    double sumB = 0.0;

                    AccumulateMatchingCorner(pixelColor, c00, m00, w00, cornerMatchTolSq, hrSq, modeColorTolSq, hsSq, ref matchCount, ref refMode, ref allMatchingAgree, ref sumW, ref sumX, ref sumY, ref sumL, ref sumA, ref sumB);
                    AccumulateMatchingCorner(pixelColor, c10, m10, w10, cornerMatchTolSq, hrSq, modeColorTolSq, hsSq, ref matchCount, ref refMode, ref allMatchingAgree, ref sumW, ref sumX, ref sumY, ref sumL, ref sumA, ref sumB);
                    AccumulateMatchingCorner(pixelColor, c01, m01, w01, cornerMatchTolSq, hrSq, modeColorTolSq, hsSq, ref matchCount, ref refMode, ref allMatchingAgree, ref sumW, ref sumX, ref sumY, ref sumL, ref sumA, ref sumB);
                    AccumulateMatchingCorner(pixelColor, c11, m11, w11, cornerMatchTolSq, hrSq, modeColorTolSq, hsSq, ref matchCount, ref refMode, ref allMatchingAgree, ref sumW, ref sumX, ref sumY, ref sumL, ref sumA, ref sumB);

                    if (matchCount >= 2 && allMatchingAgree && sumW > 0.0)
                    {
                        double invW = 1.0 / sumW;
                        modes[rowStart + x] = new MeanShiftMode(
                            sumX * invW,
                            sumY * invW,
                            new Lab32((float)(sumL * invW), (float)(sumA * invW), (float)(sumB * invW)));
                        continue;
                    }
                }

                modes[rowStart + x] = SeekSingleMode(
                    sourceLab,
                    width,
                    height,
                    x,
                    y,
                    hs,
                    hsSq,
                    hrSq,
                    convergenceEpsSq,
                    maxIterations,
                    lattice);
            }
        });

        return (modes, sourceLab);
    }

    private static void AccumulateMatchingCorner(
        Lab32 pixelColor,
        Lab32 cornerSrc,
        MeanShiftMode cornerMode,
        double weight,
        double cornerMatchTolSq,
        double hrSq,
        double modeColorTolSq,
        double hsSq,
        ref int matchCount,
        ref MeanShiftMode refMode,
        ref bool allMatchingAgree,
        ref double sumW,
        ref double sumX,
        ref double sumY,
        ref double sumL,
        ref double sumA,
        ref double sumB)
    {
        if (!allMatchingAgree)
            return;

        if (pixelColor.EuclideanDistanceSquared(cornerSrc) <= cornerMatchTolSq &&
            pixelColor.EuclideanDistanceSquared(cornerMode.Color) <= hrSq)
        {
            if (matchCount == 0)
            {
                refMode = cornerMode;
            }
            else if (cornerMode.Color.EuclideanDistanceSquared(refMode.Color) > modeColorTolSq ||
                     cornerMode.SpatialDistanceSquared(refMode) > hsSq)
            {
                allMatchingAgree = false;
                return;
            }

            matchCount++;
            sumW += weight;
            sumX += weight * cornerMode.X;
            sumY += weight * cornerMode.Y;
            sumL += weight * cornerMode.Color.L;
            sumA += weight * cornerMode.Color.A;
            sumB += weight * cornerMode.Color.B;
        }
    }

    private static int[] BuildCoarseCoordinates(int length, int stride)
    {
        int gridCount = ((length - 1) / stride) + 1;
        bool appendLast = ((length - 1) % stride) != 0;
        var coords = new int[gridCount + (appendLast ? 1 : 0)];
        for (int i = 0; i < gridCount; i++)
        {
            coords[i] = i * stride;
        }

        if (appendLast)
        {
            coords[^1] = length - 1;
        }

        return coords;
    }

    private static MeanShiftMode SeekSingleMode(
        Lab32[] sourceLab,
        int width,
        int height,
        int x,
        int y,
        int hs,
        double hsSq,
        double hrSq,
        double convergenceEpsSq,
        int maxIterations,
        MeanShiftSpatialLattice? lattice)
    {
        var current = new MeanShiftMode(x, y, sourceLab[(y * width) + x]);

        for (int iter = 0; iter < maxIterations; iter++)
        {
            MeanShiftMode next = (lattice is null)
                ? ComputeDirectMeanStep(sourceLab, width, height, current, hsSq, hrSq, hs)
                : lattice.ComputeMeanStep(sourceLab, current, hsSq, hrSq);

            double shiftDistSq = current.SpatialDistanceSquared(next) + next.Color.EuclideanDistanceSquared(current.Color);
            if (shiftDistSq <= convergenceEpsSq)
            {
                current = next;
                break;
            }

            current = next;
        }

        return current;
    }

    private static MeanShiftMode ComputeDirectMeanStep(
        ReadOnlySpan<Lab32> sourceLab,
        int width,
        int height,
        MeanShiftMode current,
        double spatialRadiusSq,
        double rangeRadiusSq,
        int spatialRadius)
    {
        int minY = Math.Max(0, (int)Math.Floor(current.Y - spatialRadius));
        int maxY = Math.Min(height - 1, (int)Math.Ceiling(current.Y + spatialRadius));

        double sumX = 0;
        double sumY = 0;
        double sumL = 0;
        double sumA = 0;
        double sumB = 0;
        int count = 0;

        for (int y = minY; y <= maxY; y++)
        {
            double dy = y - current.Y;
            double dySq = dy * dy;
            if (dySq > spatialRadiusSq)
                continue;

            double dxSpan = Math.Sqrt(spatialRadiusSq - dySq);
            int rowMinX = Math.Max(0, (int)Math.Floor(current.X - dxSpan));
            int rowMaxX = Math.Min(width - 1, (int)Math.Ceiling(current.X + dxSpan));
            int rowOffset = y * width;
            for (int x = rowMinX; x <= rowMaxX; x++)
            {
                double dx = x - current.X;
                double distSqSpatial = (dx * dx) + dySq;
                if (distSqSpatial <= spatialRadiusSq)
                {
                    Lab32 pixelColor = sourceLab[rowOffset + x];
                    if (pixelColor.EuclideanDistanceSquared(current.Color) <= rangeRadiusSq)
                    {
                        sumX += x;
                        sumY += y;
                        sumL += pixelColor.L;
                        sumA += pixelColor.A;
                        sumB += pixelColor.B;
                        count++;
                    }
                }
            }
        }

        if (count == 0)
            return current;

        double invCount = 1.0 / count;
        return new MeanShiftMode(
            sumX * invCount,
            sumY * invCount,
            new Lab32((float)(sumL * invCount), (float)(sumA * invCount), (float)(sumB * invCount)));
    }

    private static IDisjointSet SegmentFromModes(
        IUntypedMatrix targetImage,
        ReadOnlySpan<Lab32> sourceLab,
        MeanShiftMode[] modes,
        MeanShiftOptions options)
    {
        int width = targetImage.Width;
        int height = targetImage.Height;
        int pixelCount = width * height;
        var disjointSet = new DisjointSet<byte>(targetImage, static _ => 0);

        if (pixelCount <= 1)
            return disjointSet;

        double hs = options.SpatialRadius;
        double hr = options.RangeRadius;
        double hsSq = hs * hs;
        double hrSq = hr * hr;
        double seedBoundSq = (1.5 * hr) * (1.5 * hr);

        // Order pixels from flat density-basin interiors (lowest local mode gradient) to boundary transitions
        ReadOnlySpan<GridNeighbor> cardinalNeighbors = GridNeighborhood.All4Connected;
        var gradSq = new double[pixelCount];
        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                int p = rowStart + x;
                Lab32 centerColor = modes[p].Color;
                double maxDiffSq = 0.0;
                for (int n = 0; n < cardinalNeighbors.Length; n++)
                {
                    int nx = x + cardinalNeighbors[n].Offset.X;
                    int ny = y + cardinalNeighbors[n].Offset.Y;
                    if ((uint)nx < (uint)width && (uint)ny < (uint)height)
                    {
                        double diffSq = centerColor.EuclideanDistanceSquared(modes[(ny * width) + nx].Color);
                        if (diffSq > maxDiffSq)
                            maxDiffSq = diffSq;
                    }
                }

                gradSq[p] = maxDiffSq;
            }
        }

        const int BucketCount = 1024;
        var bucketCounts = new int[BucketCount];
        for (int p = 0; p < pixelCount; p++)
        {
            double g = gradSq[p];
            int bucket = double.IsFinite(g) ? Math.Clamp((int)(g * 4.0), 0, BucketCount - 1) : BucketCount - 1;
            bucketCounts[bucket]++;
        }

        var bucketOffsets = new int[BucketCount];
        int running = 0;
        for (int b = 0; b < BucketCount; b++)
        {
            bucketOffsets[b] = running;
            running += bucketCounts[b];
        }

        var orderedPixels = new int[pixelCount];
        for (int p = 0; p < pixelCount; p++)
        {
            double g = gradSq[p];
            int bucket = double.IsFinite(g) ? Math.Clamp((int)(g * 4.0), 0, BucketCount - 1) : BucketCount - 1;
            orderedPixels[bucketOffsets[bucket]++] = p;
        }

        // Centroid-bounded BFS basin clustering: prevents transitive single-linkage chaining across smooth gradients
        ReadOnlySpan<GridNeighbor> allNeighbors = GridNeighborhood.GetAll(options.Connectivity);
        var visited = new bool[pixelCount];
        var queue = new Queue<int>();

        for (int i = 0; i < orderedPixels.Length; i++)
        {
            int seed = orderedPixels[i];
            if (visited[seed])
                continue;

            visited[seed] = true;
            queue.Enqueue(seed);
            var seedPart = disjointSet.GetPartition(seed);
            Lab32 seedColor = modes[seed].Color;
            double sumL = seedColor.L;
            double sumA = seedColor.A;
            double sumB = seedColor.B;
            int count = 1;

            while (queue.Count > 0)
            {
                int u = queue.Dequeue();
                int ux = u % width;
                int uy = u / width;
                MeanShiftMode uMode = modes[u];

                double invCount = 1.0 / count;
                var regionMean = new Lab32((float)(sumL * invCount), (float)(sumA * invCount), (float)(sumB * invCount));

                for (int n = 0; n < allNeighbors.Length; n++)
                {
                    int nx = ux + allNeighbors[n].Offset.X;
                    int ny = uy + allNeighbors[n].Offset.Y;
                    if ((uint)nx >= (uint)width || (uint)ny >= (uint)height)
                        continue;

                    int q = (ny * width) + nx;
                    if (visited[q])
                        continue;

                    MeanShiftMode qMode = modes[q];
                    if (uMode.SpatialDistanceSquared(qMode) <= hsSq &&
                        uMode.Color.EuclideanDistanceSquared(qMode.Color) <= hrSq &&
                        qMode.Color.EuclideanDistanceSquared(regionMean) <= hrSq &&
                        qMode.Color.EuclideanDistanceSquared(seedColor) <= seedBoundSq)
                    {
                        visited[q] = true;
                        seedPart = disjointSet.Union(seedPart, disjointSet.GetPartition(q), 0);
                        sumL += qMode.Color.L;
                        sumA += qMode.Color.A;
                        sumB += qMode.Color.B;
                        count++;
                        invCount = 1.0 / count;
                        regionMean = new Lab32((float)(sumL * invCount), (float)(sumA * invCount), (float)(sumB * invCount));
                        queue.Enqueue(q);
                    }
                }
            }
        }

        // Small region pruning
        int minRegionSize = options.MinRegionSize;
        if (minRegionSize > 1 && pixelCount > 1)
        {
            ReadOnlySpan<GridNeighbor> forward = GridNeighborhood.GetForward(options.Connectivity);
            PruneSmallRegions(disjointSet, sourceLab, width, height, minRegionSize, forward);
        }

        return disjointSet;
    }

    private static void PruneSmallRegions(
        DisjointSet<byte> disjointSet,
        ReadOnlySpan<Lab32> sourceLab,
        int width,
        int height,
        int minRegionSize,
        ReadOnlySpan<GridNeighbor> forward)
    {
        int pixelCount = width * height;
        var sumL = new double[pixelCount];
        var sumA = new double[pixelCount];
        var sumB = new double[pixelCount];

        for (int i = 0; i < pixelCount; i++)
        {
            int root = disjointSet.GetPartition(i).RootIndex;
            Lab32 c = sourceLab[i];
            sumL[root] += c.L;
            sumA[root] += c.A;
            sumB[root] += c.B;
        }

        Lab32 GetMeanLab(int root)
        {
            var part = disjointSet.GetPartition(root);
            double invSize = 1.0 / part.Size;
            return new Lab32((float)(sumL[root] * invSize), (float)(sumA[root] * invSize), (float)(sumB[root] * invSize));
        }

        var seenPairs = new HashSet<ulong>();
        var candidateEdges = new List<UndirectedGraphEdge<int>>();

        while (true)
        {
            seenPairs.Clear();
            candidateEdges.Clear();

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * width;
                for (int x = 0; x < width; x++)
                {
                    int p = rowStart + x;
                    int rootP = disjointSet.GetPartition(p).RootIndex;

                    for (int n = 0; n < forward.Length; n++)
                    {
                        int nx = x + forward[n].Offset.X;
                        int ny = y + forward[n].Offset.Y;
                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                        {
                            int q = ny * width + nx;
                            int rootQ = disjointSet.GetPartition(q).RootIndex;

                            if (rootP != rootQ)
                            {
                                int r1 = rootP;
                                int r2 = rootQ;
                                if (r1 > r2)
                                {
                                    int temp = r1;
                                    r1 = r2;
                                    r2 = temp;
                                }

                                ulong pairKey = ((ulong)(uint)r1 << 32) | (ulong)(uint)r2;
                                if (seenPairs.Add(pairKey))
                                {
                                    var part1 = disjointSet.GetPartition(r1);
                                    var part2 = disjointSet.GetPartition(r2);
                                    if (part1.Size < minRegionSize || part2.Size < minRegionSize)
                                    {
                                        Lab32 mean1 = GetMeanLab(r1);
                                        Lab32 mean2 = GetMeanLab(r2);
                                        candidateEdges.Add(new UndirectedGraphEdge<int>
                                        {
                                            First = r1,
                                            Second = r2,
                                            Weight = mean1.EuclideanDistance(mean2)
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (candidateEdges.Count == 0)
                break;

            var edges = candidateEdges.ToArray();
            Array.Sort(edges, CompareRegionEdges);

            bool mergedInPass = false;
            for (int i = 0; i < edges.Length; i++)
            {
                UndirectedGraphEdge<int> edge = edges[i];
                var p1 = disjointSet.GetPartition(edge.First);
                var p2 = disjointSet.GetPartition(edge.Second);

                if (p1.RootIndex != p2.RootIndex && (p1.Size < minRegionSize || p2.Size < minRegionSize))
                {
                    int oldRoot1 = p1.RootIndex;
                    int oldRoot2 = p2.RootIndex;
                    var merged = disjointSet.Union(p1, p2, 0);
                    int newRoot = merged.RootIndex;
                    int absorbedRoot = (newRoot == oldRoot1) ? oldRoot2 : oldRoot1;
                    sumL[newRoot] += sumL[absorbedRoot];
                    sumA[newRoot] += sumA[absorbedRoot];
                    sumB[newRoot] += sumB[absorbedRoot];
                    mergedInPass = true;
                }
            }

            if (!mergedInPass)
                break;
        }
    }

    private static int CompareRegionEdges(UndirectedGraphEdge<int> a, UndirectedGraphEdge<int> b)
    {
        int cmp = a.Weight.CompareTo(b.Weight);
        if (cmp != 0) return cmp;
        cmp = a.First.CompareTo(b.First);
        if (cmp != 0) return cmp;
        return a.Second.CompareTo(b.Second);
    }
}
