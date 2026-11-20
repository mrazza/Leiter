namespace Leiter.Algorithms.Segmentation.Internal;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Accelerates 5D joint spatial-color Mean-Shift mode queries using a 2D spatial grid of sorted 3D color buckets.
/// </summary>
internal sealed class MeanShiftSpatialLattice
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

    private readonly record struct AxisCellSpan(int FirstSample, int ExclusiveEnd, int SampleCount);

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ColorBucket(int CellL, int CellA, int CellB, int PointOffset, int PointCount);

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

    private sealed class CellBuildScratch
    {
        public readonly Dictionary<ColorKey3D, int> ColorCounts = new();
        public readonly List<ColorKey3D> SortedKeys = [];
        public readonly Dictionary<ColorKey3D, int> WriteOffsets = new();

        public void Clear()
        {
            ColorCounts.Clear();
            SortedKeys.Clear();
            WriteOffsets.Clear();
        }
    }

    private const int ParallelBuildPixelThreshold = 65536;

    private readonly double spatialCellWidth;
    private readonly double rangeCellWidth;
    private readonly int gridWidth;
    private readonly int gridHeight;
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any dimension or cell width is non-positive or non-finite.</exception>
    public MeanShiftSpatialLattice(
        ReadOnlySpan<Lab32> pixels,
        int width,
        int height,
        double spatialCellWidth,
        double rangeCellWidth)
        : this(
            pixels.Length == 0 || width <= 0 || height <= 0 ? [] : pixels.ToArray(),
            width,
            height,
            spatialCellWidth,
            rangeCellWidth,
            buildInParallel: pixels.Length >= ParallelBuildPixelThreshold)
    {
    }

    internal static MeanShiftSpatialLattice Create(
        Lab32[] pixels,
        int width,
        int height,
        double spatialCellWidth,
        double rangeCellWidth,
        int sampleStride = 1)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        return new MeanShiftSpatialLattice(
            pixels,
            width,
            height,
            spatialCellWidth,
            rangeCellWidth,
            buildInParallel: pixels.Length >= ParallelBuildPixelThreshold,
            sampleStride: sampleStride);
    }

    private MeanShiftSpatialLattice(
        Lab32[] sourcePixels,
        int width,
        int height,
        double spatialCellWidth,
        double rangeCellWidth,
        bool buildInParallel,
        int sampleStride = 1)
    {
        ValidateArguments(width, height, spatialCellWidth, rangeCellWidth, sampleStride);

        this.spatialCellWidth = spatialCellWidth;
        this.rangeCellWidth = rangeCellWidth;

        if (sourcePixels.Length == 0 || width == 0 || height == 0)
        {
            gridWidth = 0;
            gridHeight = 0;
            spatialCellOffsets = [];
            colorBuckets = [];
            points = [];
            return;
        }

        AxisCellSpan[] xSpans = ComputeAxisCellSpans(width, spatialCellWidth, sampleStride);
        AxisCellSpan[] ySpans = ComputeAxisCellSpans(height, spatialCellWidth, sampleStride);
        gridWidth = xSpans.Length;
        gridHeight = ySpans.Length;

        int[] cellPointStartOffsets = ComputeCellPointOffsets(xSpans, ySpans, out int totalPointCount);
        points = new PackedPoint[totalPointCount];

        ColorBucket[][] perCellBuckets = BuildAllSpatialCells(
            sourcePixels,
            width,
            rangeCellWidth,
            sampleStride,
            xSpans,
            ySpans,
            cellPointStartOffsets,
            points,
            buildInParallel);

        colorBuckets = FlattenColorBuckets(perCellBuckets, out spatialCellOffsets);
    }

    /// <summary>
    /// Computes one Mean-Shift mode-seeking step using the 5D spatial-range lattice.
    /// </summary>
    /// <param name="sourceLab">The source CIELAB pixel buffer.</param>
    /// <param name="current">The current mode coordinate.</param>
    /// <param name="spatialRadiusSq">The squared spatial radius threshold.</param>
    /// <param name="rangeRadiusSq">The squared color range radius threshold.</param>
    /// <returns>The updated mode coordinate, or <paramref name="current"/> if no points were within the window.</returns>
    public MeanShiftMode ComputeMeanStep(
        ReadOnlySpan<Lab32> sourceLab,
        MeanShiftMode current,
        double spatialRadiusSq,
        double rangeRadiusSq)
    {
        _ = sourceLab;
        if (points.Length == 0)
            return current;

        double queryX = current.X;
        double queryY = current.Y;
        Lab32 queryColor = current.Color;

        int centerCellX = (int)Math.Floor(queryX / spatialCellWidth);
        int centerCellY = (int)Math.Floor(queryY / spatialCellWidth);
        int minSpatialCellX = Math.Max(0, centerCellX - 1);
        int maxSpatialCellX = Math.Min(gridWidth - 1, centerCellX + 1);
        int minSpatialCellY = Math.Max(0, centerCellY - 1);
        int maxSpatialCellY = Math.Min(gridHeight - 1, centerCellY + 1);
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
            int cellRowOffset = cellY * gridWidth;
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
            if (bucket.CellL > colorWindow.MaxL)
                break;
            if (!colorWindow.ContainsChroma(bucket.CellA, bucket.CellB))
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

    private static void ValidateArguments(
        int width,
        int height,
        double spatialCellWidth,
        double rangeCellWidth,
        int sampleStride)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 0);
        Guard.ThrowIfNotFinitePositive(spatialCellWidth);
        Guard.ThrowIfNotFinitePositive(rangeCellWidth);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleStride, 1);
    }

    private static AxisCellSpan[] ComputeAxisCellSpans(int axisLength, double cellWidth, int sampleStride)
    {
        int cellCount = (int)Math.Floor((axisLength - 1) / cellWidth) + 1;
        var minCoords = new int[cellCount];
        var exclusiveEnds = new int[cellCount];
        Array.Fill(minCoords, -1);

        for (int coord = 0; coord < axisLength; coord++)
        {
            int cellCoord = (int)Math.Floor(coord / cellWidth);
            if (minCoords[cellCoord] < 0)
                minCoords[cellCoord] = coord;
            exclusiveEnds[cellCoord] = coord + 1;
        }

        var spans = new AxisCellSpan[cellCount];
        for (int cellCoord = 0; cellCoord < cellCount; cellCoord++)
        {
            int minCoord = minCoords[cellCoord];
            if (minCoord < 0)
                continue;

            int exclusiveEnd = exclusiveEnds[cellCoord];
            int firstSample = ((minCoord + sampleStride - 1) / sampleStride) * sampleStride;
            int sampleCount = firstSample < exclusiveEnd
                ? ((exclusiveEnd - 1 - firstSample) / sampleStride) + 1
                : 0;
            spans[cellCoord] = new AxisCellSpan(firstSample, exclusiveEnd, sampleCount);
        }

        return spans;
    }

    private static int[] ComputeCellPointOffsets(
        AxisCellSpan[] xSpans,
        AxisCellSpan[] ySpans,
        out int totalPointCount)
    {
        int gridWidth = xSpans.Length;
        int gridHeight = ySpans.Length;
        var offsets = new int[gridWidth * gridHeight];
        totalPointCount = 0;

        for (int cellY = 0; cellY < gridHeight; cellY++)
        {
            int rowSampleCount = ySpans[cellY].SampleCount;
            int rowBase = cellY * gridWidth;
            for (int cellX = 0; cellX < gridWidth; cellX++)
            {
                int cellIndex = rowBase + cellX;
                offsets[cellIndex] = totalPointCount;
                totalPointCount += rowSampleCount * xSpans[cellX].SampleCount;
            }
        }

        return offsets;
    }

    private static ColorBucket[][] BuildAllSpatialCells(
        Lab32[] sourcePixels,
        int width,
        double rangeCellWidth,
        int sampleStride,
        AxisCellSpan[] xSpans,
        AxisCellSpan[] ySpans,
        int[] cellPointStartOffsets,
        PackedPoint[] points,
        bool buildInParallel)
    {
        int gridWidth = xSpans.Length;
        int spatialCellCount = gridWidth * ySpans.Length;
        var perCellBuckets = new ColorBucket[spatialCellCount][];

        if (buildInParallel)
        {
            Parallel.For(
                0,
                spatialCellCount,
                static () => new CellBuildScratch(),
                (cellIndex, _, scratch) =>
                {
                    perCellBuckets[cellIndex] = BuildCellAtIndex(
                        sourcePixels,
                        width,
                        rangeCellWidth,
                        sampleStride,
                        gridWidth,
                        xSpans,
                        ySpans,
                        cellPointStartOffsets,
                        points,
                        cellIndex,
                        scratch);
                    return scratch;
                },
                static _ => { });
        }
        else
        {
            var scratch = new CellBuildScratch();
            for (int cellIndex = 0; cellIndex < spatialCellCount; cellIndex++)
            {
                perCellBuckets[cellIndex] = BuildCellAtIndex(
                    sourcePixels,
                    width,
                    rangeCellWidth,
                    sampleStride,
                    gridWidth,
                    xSpans,
                    ySpans,
                    cellPointStartOffsets,
                    points,
                    cellIndex,
                    scratch);
            }
        }

        return perCellBuckets;
    }

    private static ColorBucket[] BuildCellAtIndex(
        Lab32[] sourcePixels,
        int width,
        double rangeCellWidth,
        int sampleStride,
        int gridWidth,
        AxisCellSpan[] xSpans,
        AxisCellSpan[] ySpans,
        int[] cellPointStartOffsets,
        PackedPoint[] points,
        int cellIndex,
        CellBuildScratch scratch)
    {
        AxisCellSpan xSpan = xSpans[cellIndex % gridWidth];
        AxisCellSpan ySpan = ySpans[cellIndex / gridWidth];
        if (xSpan.SampleCount == 0 || ySpan.SampleCount == 0)
            return [];

        return BuildSpatialCell(
            sourcePixels,
            width,
            rangeCellWidth,
            xSpan,
            ySpan,
            sampleStride,
            cellPointStartOffsets[cellIndex],
            points,
            scratch);
    }

    private static ColorBucket[] BuildSpatialCell(
        Lab32[] sourcePixels,
        int width,
        double rangeCellWidth,
        AxisCellSpan xSpan,
        AxisCellSpan ySpan,
        int sampleStride,
        int cellPointOffset,
        PackedPoint[] points,
        CellBuildScratch scratch)
    {
        scratch.Clear();
        Dictionary<ColorKey3D, int> colorCounts = scratch.ColorCounts;
        List<ColorKey3D> sortedKeys = scratch.SortedKeys;

        CountCellColors(sourcePixels, width, rangeCellWidth, xSpan, ySpan, sampleStride, colorCounts, sortedKeys);
        if (sortedKeys.Count == 0)
            return [];

        sortedKeys.Sort();

        Dictionary<ColorKey3D, int> writeOffsets = scratch.WriteOffsets;
        ColorBucket[] cellBuckets = BuildSortedBucketSpans(sortedKeys, colorCounts, cellPointOffset, writeOffsets);
        ScatterCellPoints(sourcePixels, width, rangeCellWidth, xSpan, ySpan, sampleStride, writeOffsets, points);
        return cellBuckets;
    }

    private static void CountCellColors(
        Lab32[] sourcePixels,
        int width,
        double rangeCellWidth,
        AxisCellSpan xSpan,
        AxisCellSpan ySpan,
        int sampleStride,
        Dictionary<ColorKey3D, int> colorCounts,
        List<ColorKey3D> sortedKeys)
    {
        for (int y = ySpan.FirstSample; y < ySpan.ExclusiveEnd; y += sampleStride)
        {
            int rowStart = y * width;
            for (int x = xSpan.FirstSample; x < xSpan.ExclusiveEnd; x += sampleStride)
            {
                int pixelIndex = rowStart + x;
                if ((uint)pixelIndex >= (uint)sourcePixels.Length)
                    continue;

                ColorKey3D colorKey = QuantizeColor(sourcePixels[pixelIndex], rangeCellWidth);
                if (!colorCounts.TryGetValue(colorKey, out int count))
                {
                    colorCounts[colorKey] = 1;
                    sortedKeys.Add(colorKey);
                }
                else
                {
                    colorCounts[colorKey] = count + 1;
                }
            }
        }
    }

    private static ColorBucket[] BuildSortedBucketSpans(
        List<ColorKey3D> sortedKeys,
        Dictionary<ColorKey3D, int> colorCounts,
        int cellPointOffset,
        Dictionary<ColorKey3D, int> writeOffsets)
    {
        var cellBuckets = new ColorBucket[sortedKeys.Count];
        int runningOffset = cellPointOffset;

        for (int i = 0; i < sortedKeys.Count; i++)
        {
            ColorKey3D key = sortedKeys[i];
            int count = colorCounts[key];
            cellBuckets[i] = new ColorBucket(key.L, key.A, key.B, runningOffset, count);
            writeOffsets[key] = runningOffset;
            runningOffset += count;
        }

        return cellBuckets;
    }

    private static void ScatterCellPoints(
        Lab32[] sourcePixels,
        int width,
        double rangeCellWidth,
        AxisCellSpan xSpan,
        AxisCellSpan ySpan,
        int sampleStride,
        Dictionary<ColorKey3D, int> writeOffsets,
        PackedPoint[] points)
    {
        for (int y = ySpan.FirstSample; y < ySpan.ExclusiveEnd; y += sampleStride)
        {
            int rowStart = y * width;
            for (int x = xSpan.FirstSample; x < xSpan.ExclusiveEnd; x += sampleStride)
            {
                int pixelIndex = rowStart + x;
                if ((uint)pixelIndex >= (uint)sourcePixels.Length)
                    continue;

                Lab32 color = sourcePixels[pixelIndex];
                ColorKey3D colorKey = QuantizeColor(color, rangeCellWidth);
                int destinationIndex = writeOffsets[colorKey];
                writeOffsets[colorKey] = destinationIndex + 1;
                points[destinationIndex] = new PackedPoint(x, y, color);
            }
        }
    }

    private static ColorBucket[] FlattenColorBuckets(ColorBucket[][] perCellBuckets, out int[] spatialCellOffsets)
    {
        int cellCount = perCellBuckets.Length;
        spatialCellOffsets = new int[cellCount + 1];

        int totalBuckets = 0;
        for (int i = 0; i < cellCount; i++)
        {
            spatialCellOffsets[i] = totalBuckets;
            totalBuckets += perCellBuckets[i].Length;
        }

        spatialCellOffsets[cellCount] = totalBuckets;
        var flatBuckets = new ColorBucket[totalBuckets];
        for (int i = 0; i < cellCount; i++)
        {
            ColorBucket[] cell = perCellBuckets[i];
            if (cell.Length > 0)
            {
                cell.CopyTo(flatBuckets, spatialCellOffsets[i]);
            }
        }

        return flatBuckets;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ColorKey3D QuantizeColor(Lab32 color, double rangeCellWidth) =>
        new(
            (int)Math.Floor(color.L / rangeCellWidth),
            (int)Math.Floor(color.A / rangeCellWidth),
            (int)Math.Floor(color.B / rangeCellWidth));

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
            if (buckets[midpoint].CellL < minLightnessCell)
                low = midpoint + 1;
            else
                high = midpoint;
        }

        return low;
    }
}
