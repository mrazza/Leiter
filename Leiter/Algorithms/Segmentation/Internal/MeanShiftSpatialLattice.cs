namespace Leiter.Algorithms.Segmentation.Internal;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
            int cmp = L.CompareTo(other.L);
            if (cmp != 0)
                return cmp;
            cmp = A.CompareTo(other.A);
            if (cmp != 0)
                return cmp;
            return B.CompareTo(other.B);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct ColorBucket(int CellL, int CellA, int CellB, int PointOffset, int PointCount);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PackedPoint(int x, int y, float l, float a, float b)
    {
        public readonly int X = x;
        public readonly int Y = y;
        public readonly float L = l;
        public readonly float A = a;
        public readonly float B = b;
    }

    private sealed class CellBuildScratch
    {
        public readonly Dictionary<ColorKey3D, int> Counts = new();
        public readonly List<ColorKey3D> Keys = [];
        public readonly Dictionary<ColorKey3D, int> WriteOffsets = new();

        public void Clear()
        {
            Counts.Clear();
            Keys.Clear();
            WriteOffsets.Clear();
        }
    }

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
            buildInParallel: pixels.Length >= 65536)
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
            buildInParallel: pixels.Length >= 65536,
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
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 0);
        if (!double.IsFinite(spatialCellWidth) || spatialCellWidth <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(spatialCellWidth), spatialCellWidth, "Spatial cell width must be a finite positive number.");
        if (!double.IsFinite(rangeCellWidth) || rangeCellWidth <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(rangeCellWidth), rangeCellWidth, "Range cell width must be a finite positive number.");
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleStride, 1);

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

        gridWidth = (int)Math.Floor((width - 1) / spatialCellWidth) + 1;
        gridHeight = (int)Math.Floor((height - 1) / spatialCellWidth) + 1;

        var xStart = new int[gridWidth];
        var xEnd = new int[gridWidth];
        Array.Fill(xStart, -1);
        for (int x = 0; x < width; x++)
        {
            int cx = (int)Math.Floor(x / spatialCellWidth);
            if (xStart[cx] < 0)
                xStart[cx] = x;
            xEnd[cx] = x + 1;
        }

        var yStart = new int[gridHeight];
        var yEnd = new int[gridHeight];
        Array.Fill(yStart, -1);
        for (int y = 0; y < height; y++)
        {
            int cy = (int)Math.Floor(y / spatialCellWidth);
            if (yStart[cy] < 0)
                yStart[cy] = y;
            yEnd[cy] = y + 1;
        }

        var xFirst = new int[gridWidth];
        var xCount = new int[gridWidth];
        for (int cx = 0; cx < gridWidth; cx++)
        {
            if (xStart[cx] >= 0)
            {
                int firstX = ((xStart[cx] + sampleStride - 1) / sampleStride) * sampleStride;
                xFirst[cx] = firstX;
                if (firstX < xEnd[cx])
                    xCount[cx] = ((xEnd[cx] - 1 - firstX) / sampleStride) + 1;
            }
        }

        var yFirst = new int[gridHeight];
        var yCount = new int[gridHeight];
        for (int cy = 0; cy < gridHeight; cy++)
        {
            if (yStart[cy] >= 0)
            {
                int firstY = ((yStart[cy] + sampleStride - 1) / sampleStride) * sampleStride;
                yFirst[cy] = firstY;
                if (firstY < yEnd[cy])
                    yCount[cy] = ((yEnd[cy] - 1 - firstY) / sampleStride) + 1;
            }
        }

        int spatialCellCount = gridWidth * gridHeight;
        var cellPointStartOffsets = new int[spatialCellCount];
        int totalPointCount = 0;
        for (int cy = 0; cy < gridHeight; cy++)
        {
            int rowSamples = yCount[cy];
            int rowBase = cy * gridWidth;
            for (int cx = 0; cx < gridWidth; cx++)
            {
                int cellIdx = rowBase + cx;
                cellPointStartOffsets[cellIdx] = totalPointCount;
                totalPointCount += rowSamples * xCount[cx];
            }
        }

        points = new PackedPoint[totalPointCount];
        var perCellBuckets = new ColorBucket[spatialCellCount][];

        if (buildInParallel)
        {
            Parallel.For(
                0,
                spatialCellCount,
                static () => new CellBuildScratch(),
                (cellIdx, _, scratch) =>
                {
                    int cx = cellIdx % gridWidth;
                    int cy = cellIdx / gridWidth;
                    if (xCount[cx] == 0 || yCount[cy] == 0)
                    {
                        perCellBuckets[cellIdx] = [];
                        return scratch;
                    }

                    perCellBuckets[cellIdx] = BuildSpatialCell(
                        sourcePixels,
                        width,
                        rangeCellWidth,
                        xFirst[cx],
                        xEnd[cx],
                        yFirst[cy],
                        yEnd[cy],
                        sampleStride,
                        cellPointStartOffsets[cellIdx],
                        points,
                        scratch);
                    return scratch;
                },
                static _ => { });
        }
        else
        {
            var scratch = new CellBuildScratch();
            for (int cy = 0; cy < gridHeight; cy++)
            {
                int rowBase = cy * gridWidth;
                if (yCount[cy] == 0)
                {
                    for (int cx = 0; cx < gridWidth; cx++)
                    {
                        perCellBuckets[rowBase + cx] = [];
                    }

                    continue;
                }

                for (int cx = 0; cx < gridWidth; cx++)
                {
                    int cellIdx = rowBase + cx;
                    if (xCount[cx] == 0)
                    {
                        perCellBuckets[cellIdx] = [];
                        continue;
                    }

                    perCellBuckets[cellIdx] = BuildSpatialCell(
                        sourcePixels,
                        width,
                        rangeCellWidth,
                        xFirst[cx],
                        xEnd[cx],
                        yFirst[cy],
                        yEnd[cy],
                        sampleStride,
                        cellPointStartOffsets[cellIdx],
                        points,
                        scratch);
                }
            }
        }

        spatialCellOffsets = new int[spatialCellCount + 1];
        int totalBuckets = 0;
        for (int i = 0; i < spatialCellCount; i++)
        {
            spatialCellOffsets[i] = totalBuckets;
            totalBuckets += perCellBuckets[i].Length;
        }

        spatialCellOffsets[spatialCellCount] = totalBuckets;
        colorBuckets = new ColorBucket[totalBuckets];
        for (int i = 0; i < spatialCellCount; i++)
        {
            ColorBucket[] cell = perCellBuckets[i];
            if (cell.Length > 0)
            {
                cell.CopyTo(colorBuckets, spatialCellOffsets[i]);
            }
        }
    }

    private static ColorBucket[] BuildSpatialCell(
        Lab32[] sourcePixels,
        int width,
        double rangeCellWidth,
        int xStart,
        int xEnd,
        int yStart,
        int yEnd,
        int sampleStride,
        int cellPointOffset,
        PackedPoint[] points,
        CellBuildScratch scratch)
    {
        scratch.Clear();
        Dictionary<ColorKey3D, int> counts = scratch.Counts;
        List<ColorKey3D> keys = scratch.Keys;

        for (int y = yStart; y < yEnd; y += sampleStride)
        {
            int rowStart = y * width;
            for (int x = xStart; x < xEnd; x += sampleStride)
            {
                int p = rowStart + x;
                if ((uint)p >= (uint)sourcePixels.Length)
                    continue;

                Lab32 c = sourcePixels[p];
                var key = new ColorKey3D(
                    (int)Math.Floor(c.L / rangeCellWidth),
                    (int)Math.Floor(c.A / rangeCellWidth),
                    (int)Math.Floor(c.B / rangeCellWidth));

                if (!counts.TryGetValue(key, out int count))
                {
                    counts[key] = 1;
                    keys.Add(key);
                }
                else
                {
                    counts[key] = count + 1;
                }
            }
        }

        if (keys.Count == 0)
            return [];

        keys.Sort();

        var cellBuckets = new ColorBucket[keys.Count];
        Dictionary<ColorKey3D, int> writeOffsets = scratch.WriteOffsets;
        int offset = cellPointOffset;
        for (int i = 0; i < keys.Count; i++)
        {
            ColorKey3D key = keys[i];
            int count = counts[key];
            cellBuckets[i] = new ColorBucket(key.L, key.A, key.B, offset, count);
            writeOffsets[key] = offset;
            offset += count;
        }

        for (int y = yStart; y < yEnd; y += sampleStride)
        {
            int rowStart = y * width;
            for (int x = xStart; x < xEnd; x += sampleStride)
            {
                int p = rowStart + x;
                if ((uint)p >= (uint)sourcePixels.Length)
                    continue;

                Lab32 c = sourcePixels[p];
                var key = new ColorKey3D(
                    (int)Math.Floor(c.L / rangeCellWidth),
                    (int)Math.Floor(c.A / rangeCellWidth),
                    (int)Math.Floor(c.B / rangeCellWidth));

                int dest = writeOffsets[key];
                writeOffsets[key] = dest + 1;
                points[dest] = new PackedPoint(x, y, c.L, c.A, c.B);
            }
        }

        return cellBuckets;
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

        double curPosX = current.X;
        double curPosY = current.Y;
        float ql = current.Color.L;
        float qa = current.Color.A;
        float qb = current.Color.B;

        int curX = (int)Math.Floor(curPosX / spatialCellWidth);
        int curY = (int)Math.Floor(curPosY / spatialCellWidth);
        int minCellX = Math.Max(0, curX - 1);
        int maxCellX = Math.Min(gridWidth - 1, curX + 1);
        int minCellY = Math.Max(0, curY - 1);
        int maxCellY = Math.Min(gridHeight - 1, curY + 1);
        if (minCellX > maxCellX || minCellY > maxCellY)
            return current;

        int curL = (int)Math.Floor(ql / rangeCellWidth);
        int curA = (int)Math.Floor(qa / rangeCellWidth);
        int curB = (int)Math.Floor(qb / rangeCellWidth);
        int minL = curL - 1;
        int maxL = curL + 1;
        int minA = curA - 1;
        int maxA = curA + 1;
        int minB = curB - 1;
        int maxB = curB + 1;

        double sumX = 0;
        double sumY = 0;
        double sumL = 0;
        double sumA = 0;
        double sumB = 0;
        int count = 0;

        for (int cellY = minCellY; cellY <= maxCellY; cellY++)
        {
            int cellRowOffset = cellY * gridWidth;
            for (int cellX = minCellX; cellX <= maxCellX; cellX++)
            {
                int cellIdx = cellRowOffset + cellX;
                int bStart = spatialCellOffsets[cellIdx];
                int bEnd = spatialCellOffsets[cellIdx + 1];
                if (bStart >= bEnd)
                    continue;

                int lo = LowerBoundByL(colorBuckets, bStart, bEnd, minL);
                for (int b = lo; b < bEnd; b++)
                {
                    ColorBucket bucket = colorBuckets[b];
                    if (bucket.CellL > maxL)
                        break;
                    if (bucket.CellA < minA || bucket.CellA > maxA || bucket.CellB < minB || bucket.CellB > maxB)
                        continue;

                    int pEnd = bucket.PointOffset + bucket.PointCount;
                    for (int i = bucket.PointOffset; i < pEnd; i++)
                    {
                        ref readonly PackedPoint pt = ref points[i];
                        double dx = pt.X - curPosX;
                        double dy = pt.Y - curPosY;
                        if ((dx * dx) + (dy * dy) <= spatialRadiusSq)
                        {
                            double dl = pt.L - ql;
                            double da = pt.A - qa;
                            double db = pt.B - qb;
                            if ((dl * dl) + (da * da) + (db * db) <= rangeRadiusSq)
                            {
                                sumX += pt.X;
                                sumY += pt.Y;
                                sumL += pt.L;
                                sumA += pt.A;
                                sumB += pt.B;
                                count++;
                            }
                        }
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

    private static int LowerBoundByL(ColorBucket[] buckets, int start, int end, int targetL)
    {
        int lo = start;
        int hi = end;
        while (lo < hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            if (buckets[mid].CellL < targetL)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }
}
