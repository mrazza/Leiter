namespace Leiter.Algorithms.Segmentation.Internal;

using System;
using System.Collections.Generic;
using System.Threading;
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
    private const int SequentialCellCancellationCheckMask = 63;

    private static Lab32[] ValidateAndCopyPixels(
        ReadOnlySpan<Lab32> pixels,
        int width,
        int height,
        double spatialCellWidth,
        double rangeCellWidth,
        CancellationToken cancellationToken)
    {
        ValidateArguments(pixels.Length, width, height, spatialCellWidth, rangeCellWidth, sampleStride: 1);
        cancellationToken.ThrowIfCancellationRequested();
        return pixels.IsEmpty ? [] : pixels.ToArray();
    }

    private static void ValidateArguments(
        int pixelLength,
        int width,
        int height,
        double spatialCellWidth,
        double rangeCellWidth,
        int sampleStride)
    {
        Guard.ThrowIfNegativeDimensions(width, height);
        Guard.ThrowIfNotFinitePositive(spatialCellWidth);
        Guard.ThrowIfNotFinitePositive(rangeCellWidth);
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleStride, 1);

        long expectedLength = (long)width * height;
        if (pixelLength != expectedLength)
        {
            throw new ArgumentException(
                $"Pixel span length ({pixelLength}) must equal width * height ({expectedLength}).",
                "pixels");
        }
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
        Size gridSize,
        out int totalPointCount)
    {
        int gridWidth = gridSize.Width;
        int gridHeight = gridSize.Height;
        var offsets = new int[gridSize.Count];
        totalPointCount = 0;

        for (int cellY = 0; cellY < gridHeight; cellY++)
        {
            int rowSampleCount = ySpans[cellY].SampleCount;
            int rowBase = gridSize.IndexOf(0, cellY);
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
        Size imageSize,
        double rangeCellWidth,
        int sampleStride,
        Size gridSize,
        AxisCellSpan[] xSpans,
        AxisCellSpan[] ySpans,
        int[] cellPointStartOffsets,
        PackedPoint[] points,
        bool buildInParallel,
        CancellationToken cancellationToken)
    {
        int spatialCellCount = gridSize.Count;
        var perCellBuckets = new ColorBucket[spatialCellCount][];

        if (buildInParallel)
        {
            Func<int, ParallelLoopState, CellBuildScratch, CellBuildScratch> body = (cellIndex, _, scratch) =>
            {
                perCellBuckets[cellIndex] = BuildCellAtIndex(
                    sourcePixels,
                    imageSize,
                    rangeCellWidth,
                    sampleStride,
                    gridSize,
                    xSpans,
                    ySpans,
                    cellPointStartOffsets,
                    points,
                    cellIndex,
                    scratch);
                return scratch;
            };

            if (cancellationToken.CanBeCanceled)
            {
                Parallel.For(
                    0,
                    spatialCellCount,
                    new ParallelOptions { CancellationToken = cancellationToken },
                    static () => new CellBuildScratch(),
                    body,
                    static _ => { });
            }
            else
            {
                Parallel.For(
                    0,
                    spatialCellCount,
                    static () => new CellBuildScratch(),
                    body,
                    static _ => { });
            }
        }
        else
        {
            var scratch = new CellBuildScratch();
            for (int cellIndex = 0; cellIndex < spatialCellCount; cellIndex++)
            {
                if ((cellIndex & SequentialCellCancellationCheckMask) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                perCellBuckets[cellIndex] = BuildCellAtIndex(
                    sourcePixels,
                    imageSize,
                    rangeCellWidth,
                    sampleStride,
                    gridSize,
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
        Size imageSize,
        double rangeCellWidth,
        int sampleStride,
        Size gridSize,
        AxisCellSpan[] xSpans,
        AxisCellSpan[] ySpans,
        int[] cellPointStartOffsets,
        PackedPoint[] points,
        int cellIndex,
        CellBuildScratch scratch)
    {
        Coord cellCoord = gridSize.CoordOf(cellIndex);
        AxisCellSpan xSpan = xSpans[cellCoord.X];
        AxisCellSpan ySpan = ySpans[cellCoord.Y];
        if (xSpan.SampleCount == 0 || ySpan.SampleCount == 0)
            return [];

        return BuildSpatialCell(
            sourcePixels,
            imageSize,
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
        Size imageSize,
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

        CountCellColors(sourcePixels, imageSize, rangeCellWidth, xSpan, ySpan, sampleStride, colorCounts, sortedKeys, cellSamples);
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
        Size imageSize,
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
            int rowStart = imageSize.IndexOf(0, y);
            for (int x = xSpan.FirstSample; x < xSpan.ExclusiveEnd; x += sampleStride)
            {
                int pixelIndex = rowStart + x;
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
            cellBuckets[i] = new ColorBucket(key, runningOffset, count);
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
