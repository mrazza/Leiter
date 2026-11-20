namespace Leiter.Algorithms.Segmentation.Internal;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Leiter.Core;
using Leiter.Pixels;

internal sealed partial class MeanShiftSpatialLattice
{
    private readonly record struct AxisCellSpan(int FirstSample, int ExclusiveEnd, int SampleCount);

    private sealed class CellBuildScratch
    {
        public readonly Dictionary<ColorKey3D, int> ColorCounts = new();
        public readonly List<ColorKey3D> SortedKeys = [];
        public readonly Dictionary<ColorKey3D, int> WriteOffsets = new();
        public readonly List<(int X, int Y, Lab32 Color, ColorKey3D Key)> CellSamples = [];

        public void Clear()
        {
            ColorCounts.Clear();
            SortedKeys.Clear();
            WriteOffsets.Clear();
            CellSamples.Clear();
        }
    }

    private const int ParallelBuildPixelThreshold = 65536;

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
        List<(int X, int Y, Lab32 Color, ColorKey3D Key)> cellSamples = scratch.CellSamples;

        CountCellColors(sourcePixels, width, rangeCellWidth, xSpan, ySpan, sampleStride, colorCounts, sortedKeys, cellSamples);
        if (sortedKeys.Count == 0)
            return [];

        sortedKeys.Sort();

        Dictionary<ColorKey3D, int> writeOffsets = scratch.WriteOffsets;
        ColorBucket[] cellBuckets = BuildSortedBucketSpans(sortedKeys, colorCounts, cellPointOffset, writeOffsets);
        ScatterCellPoints(cellSamples, writeOffsets, points);
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
        List<ColorKey3D> sortedKeys,
        List<(int X, int Y, Lab32 Color, ColorKey3D Key)> cellSamples)
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
                cellSamples.Add((x, y, color, colorKey));
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
        List<(int X, int Y, Lab32 Color, ColorKey3D Key)> cellSamples,
        Dictionary<ColorKey3D, int> writeOffsets,
        PackedPoint[] points)
    {
        for (int i = 0; i < cellSamples.Count; i++)
        {
            (int x, int y, Lab32 color, ColorKey3D colorKey) = cellSamples[i];
            int destinationIndex = writeOffsets[colorKey];
            writeOffsets[colorKey] = destinationIndex + 1;
            points[destinationIndex] = new PackedPoint(x, y, color);
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
}
