namespace Leiter.Algorithms.Segmentation.Internal;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Accelerates 5D joint spatial-color Mean-Shift mode queries using a 2D spatial grid of sorted 3D color buckets.
/// </summary>
internal sealed partial class MeanShiftSpatialLattice
{
    private readonly record struct ColorKey3D(int L, int A, int B) : IComparable<ColorKey3D>
    {
        public int CompareTo(ColorKey3D other)
        {
            int comparison = L.CompareTo(other.L);
            if (comparison != 0)
                return comparison;
            comparison = A.CompareTo(other.A);
            return comparison != 0 ? comparison : B.CompareTo(other.B);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ColorBucket(ColorKey3D Key, int PointOffset, int PointCount);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PackedPoint(int x, int y, Lab32 color)
    {
        public readonly int X = x;
        public readonly int Y = y;
        public readonly Lab32 Color = color;
    }

    private readonly record struct ColorCellWindow(int MinL, int MaxL, int MinA, int MaxA, int MinB, int MaxB)
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ContainsChroma(int cellA, int cellB) =>
            cellA >= MinA && cellA <= MaxA && cellB >= MinB && cellB <= MaxB;
    }

    private readonly double spatialCellWidth;
    private readonly double rangeCellWidth;
    private readonly Size gridSize;
    private readonly int[] spatialCellOffsets;
    private readonly ColorBucket[] colorBuckets;
    private readonly PackedPoint[] points;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeanShiftSpatialLattice"/> class.
    /// </summary>
    /// <param name="pixels">The flat row-major CIELAB pixel span.</param>
    /// <param name="width">The width of the image lattice in pixels.</param>
    /// <param name="height">The height of the image lattice in pixels.</param>
    /// <param name="spatialCellWidth">The width of each spatial lattice cell.</param>
    /// <param name="rangeCellWidth">The width of each color range lattice cell.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any dimension is negative, or any cell width is non-positive or non-finite.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="pixels"/> length does not equal <paramref name="width"/> * <paramref name="height"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public MeanShiftSpatialLattice(
        ReadOnlySpan<Lab32> pixels,
        int width,
        int height,
        double spatialCellWidth,
        double rangeCellWidth,
        CancellationToken cancellationToken = default)
        : this(
            ValidateAndCopyPixels(pixels, width, height, spatialCellWidth, rangeCellWidth, cancellationToken),
            width,
            height,
            spatialCellWidth,
            rangeCellWidth,
            buildInParallel: pixels.Length >= ParallelBuildPixelThreshold,
            skipValidation: true,
            cancellationToken: cancellationToken)
    {
    }

    internal static MeanShiftSpatialLattice Create(
        Lab32[] pixels,
        int width,
        int height,
        double spatialCellWidth,
        double rangeCellWidth,
        int sampleStride = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        return new MeanShiftSpatialLattice(
            pixels,
            width,
            height,
            spatialCellWidth,
            rangeCellWidth,
            buildInParallel: pixels.Length >= ParallelBuildPixelThreshold,
            sampleStride: sampleStride,
            cancellationToken: cancellationToken);
    }

    private MeanShiftSpatialLattice(
        Lab32[] sourcePixels,
        int width,
        int height,
        double spatialCellWidth,
        double rangeCellWidth,
        bool buildInParallel,
        int sampleStride = 1,
        bool skipValidation = false,
        CancellationToken cancellationToken = default)
    {
        if (!skipValidation)
            ValidateArguments(sourcePixels.Length, width, height, spatialCellWidth, rangeCellWidth, sampleStride);

        cancellationToken.ThrowIfCancellationRequested();

        this.spatialCellWidth = spatialCellWidth;
        this.rangeCellWidth = rangeCellWidth;

        if (sourcePixels.Length == 0 || width == 0 || height == 0)
        {
            gridSize = new Size(0, 0);
            spatialCellOffsets = [];
            colorBuckets = [];
            points = [];
            return;
        }

        AxisCellSpan[] xSpans = ComputeAxisCellSpans(width, spatialCellWidth, sampleStride);
        AxisCellSpan[] ySpans = ComputeAxisCellSpans(height, spatialCellWidth, sampleStride);
        gridSize = new Size(xSpans.Length, ySpans.Length);

        int[] cellPointStartOffsets = ComputeCellPointOffsets(xSpans, ySpans, gridSize, out int totalPointCount);
        points = new PackedPoint[totalPointCount];

        ColorBucket[][] perCellBuckets = BuildAllSpatialCells(
            sourcePixels,
            new Size(width, height),
            rangeCellWidth,
            sampleStride,
            gridSize,
            xSpans,
            ySpans,
            cellPointStartOffsets,
            points,
            buildInParallel,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        colorBuckets = FlattenColorBuckets(perCellBuckets, out spatialCellOffsets);
    }

    /// <summary>
    /// Computes one Mean-Shift mode-seeking step using the 5D spatial-range lattice.
    /// </summary>
    /// <param name="current">The current mode coordinate.</param>
    /// <param name="spatialRadiusSq">The squared spatial radius threshold.</param>
    /// <param name="rangeRadiusSq">The squared color range radius threshold.</param>
    /// <returns>The updated mode coordinate, or <paramref name="current"/> if no points were within the window.</returns>
    public MeanShiftMode ComputeMeanStep(
        MeanShiftMode current,
        double spatialRadiusSq,
        double rangeRadiusSq)
    {
        if (points.Length == 0)
            return current;

        double queryX = current.X;
        double queryY = current.Y;
        Lab32 queryColor = current.Color;

        int centerCellX = (int)Math.Floor(queryX / spatialCellWidth);
        int centerCellY = (int)Math.Floor(queryY / spatialCellWidth);
        int minSpatialCellX = Math.Max(0, centerCellX - 1);
        int maxSpatialCellX = Math.Min(gridSize.Width - 1, centerCellX + 1);
        int minSpatialCellY = Math.Max(0, centerCellY - 1);
        int maxSpatialCellY = Math.Min(gridSize.Height - 1, centerCellY + 1);
        if (minSpatialCellX > maxSpatialCellX || minSpatialCellY > maxSpatialCellY)
            return current;

        ColorKey3D centerColorCell = QuantizeColor(queryColor, rangeCellWidth);
        var colorWindow = new ColorCellWindow(
            centerColorCell.L - 1,
            centerColorCell.L + 1,
            centerColorCell.A - 1,
            centerColorCell.A + 1,
            centerColorCell.B - 1,
            centerColorCell.B + 1);

        var accumulator = default(MeanShiftModeAccumulator);
        for (int cellY = minSpatialCellY; cellY <= maxSpatialCellY; cellY++)
        {
            int cellRowOffset = gridSize.IndexOf(0, cellY);
            for (int cellX = minSpatialCellX; cellX <= maxSpatialCellX; cellX++)
            {
                AccumulateSpatialCell(
                    cellRowOffset + cellX,
                    queryX,
                    queryY,
                    queryColor,
                    in colorWindow,
                    spatialRadiusSq,
                    rangeRadiusSq,
                    ref accumulator);
            }
        }

        return accumulator.ToModeOrFallback(current);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AccumulateSpatialCell(
        int cellIndex,
        double queryX,
        double queryY,
        Lab32 queryColor,
        in ColorCellWindow colorWindow,
        double spatialRadiusSq,
        double rangeRadiusSq,
        ref MeanShiftModeAccumulator accumulator)
    {
        int bucketStart = spatialCellOffsets[cellIndex];
        int bucketEnd = spatialCellOffsets[cellIndex + 1];
        if (bucketStart >= bucketEnd)
            return;

        int firstBucket = FindFirstBucketAtOrAboveLightness(colorBuckets, bucketStart, bucketEnd, colorWindow.MinL);
        for (int bucketIndex = firstBucket; bucketIndex < bucketEnd; bucketIndex++)
        {
            ColorBucket bucket = colorBuckets[bucketIndex];
            if (bucket.Key.L > colorWindow.MaxL)
                break;
            if (!colorWindow.ContainsChroma(bucket.Key.A, bucket.Key.B))
                continue;

            int pointEnd = bucket.PointOffset + bucket.PointCount;
            for (int pointIndex = bucket.PointOffset; pointIndex < pointEnd; pointIndex++)
            {
                ref readonly PackedPoint point = ref points[pointIndex];
                double deltaX = point.X - queryX;
                double deltaY = point.Y - queryY;
                if ((deltaX * deltaX) + (deltaY * deltaY) > spatialRadiusSq)
                    continue;

                if (point.Color.EuclideanDistanceSquared(queryColor) <= rangeRadiusSq)
                {
                    accumulator.Add(point.X, point.Y, point.Color);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ColorKey3D QuantizeColor(Lab32 color, double rangeCellWidth) =>
        new(
            QuantizeChannel(color.L, rangeCellWidth),
            QuantizeChannel(color.A, rangeCellWidth),
            QuantizeChannel(color.B, rangeCellWidth));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int QuantizeChannel(float value, double rangeCellWidth) =>
        (int)Math.Clamp(Math.Floor(value / rangeCellWidth), int.MinValue + 1.0, int.MaxValue - 1.0);

    private static int FindFirstBucketAtOrAboveLightness(
        ColorBucket[] buckets,
        int startIndex,
        int endIndex,
        int minLightnessCell)
    {
        int low = startIndex;
        int high = endIndex;
        while (low < high)
        {
            int midpoint = low + ((high - low) >> 1);
            if (buckets[midpoint].Key.L < minLightnessCell)
                low = midpoint + 1;
            else
                high = midpoint;
        }

        return low;
    }
}
