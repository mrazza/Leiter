namespace Leiter.Algorithms.Segmentation.Internal;

using System;
using System.Collections.Generic;
using Leiter.Pixels;

/// <summary>
/// Accelerates 5D joint spatial-color Mean-Shift mode queries using a Compressed Sparse Row (CSR) hash lattice.
/// </summary>
internal sealed class MeanShiftSpatialLattice
{
    /// <summary>
    /// Represents a 5D discrete integer cell coordinate in joint spatial and CIELAB color space.
    /// </summary>
    private readonly record struct CellKey5D(int X, int Y, int L, int A, int B)
    {
        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(X, Y, L, A, B);
    }

    private readonly record struct CellSpan(int Offset, int Count);

    private readonly double spatialCellWidth;
    private readonly double rangeCellWidth;
    private readonly int width;
    private readonly int height;
    private readonly Dictionary<CellKey5D, CellSpan> cellSpans;
    private readonly int[] pixelIndices;

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
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 0);
        if (!double.IsFinite(spatialCellWidth) || spatialCellWidth <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(spatialCellWidth), spatialCellWidth, "Spatial cell width must be a finite positive number.");
        if (!double.IsFinite(rangeCellWidth) || rangeCellWidth <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(rangeCellWidth), rangeCellWidth, "Range cell width must be a finite positive number.");

        this.width = width;
        this.height = height;
        this.spatialCellWidth = spatialCellWidth;
        this.rangeCellWidth = rangeCellWidth;

        if (pixels.Length == 0 || width == 0 || height == 0)
        {
            cellSpans = new Dictionary<CellKey5D, CellSpan>();
            pixelIndices = [];
            return;
        }

        // Pass 1: Count occurrences of each 5D cell
        var cellCounts = new Dictionary<CellKey5D, int>();
        for (int p = 0; p < pixels.Length; p++)
        {
            int x = p % width;
            int y = p / width;
            CellKey5D key = GetCellKey(x, y, pixels[p]);
            if (!cellCounts.TryGetValue(key, out int count))
            {
                cellCounts[key] = 1;
            }
            else
            {
                cellCounts[key] = count + 1;
            }
        }

        // Prefix sum / offset computation
        cellSpans = new Dictionary<CellKey5D, CellSpan>(cellCounts.Count);
        var writeOffsets = new Dictionary<CellKey5D, int>(cellCounts.Count);
        int runningOffset = 0;
        foreach (KeyValuePair<CellKey5D, int> kvp in cellCounts)
        {
            cellSpans[kvp.Key] = new CellSpan(runningOffset, kvp.Value);
            writeOffsets[kvp.Key] = runningOffset;
            runningOffset += kvp.Value;
        }

        // Pass 2: Scatter pixel indices into the CSR flat array
        pixelIndices = new int[pixels.Length];
        for (int p = 0; p < pixels.Length; p++)
        {
            int x = p % width;
            int y = p / width;
            CellKey5D key = GetCellKey(x, y, pixels[p]);
            int dest = writeOffsets[key]++;
            pixelIndices[dest] = p;
        }
    }

    private CellKey5D GetCellKey(double x, double y, Lab32 color)
    {
        int cx = (int)Math.Floor(x / spatialCellWidth);
        int cy = (int)Math.Floor(y / spatialCellWidth);
        int cl = (int)Math.Floor(color.L / rangeCellWidth);
        int ca = (int)Math.Floor(color.A / rangeCellWidth);
        int cb = (int)Math.Floor(color.B / rangeCellWidth);
        return new CellKey5D(cx, cy, cl, ca, cb);
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
        if (pixelIndices.Length == 0)
            return current;

        int curX = (int)Math.Floor(current.X / spatialCellWidth);
        int curY = (int)Math.Floor(current.Y / spatialCellWidth);
        int curL = (int)Math.Floor(current.Color.L / rangeCellWidth);
        int curA = (int)Math.Floor(current.Color.A / rangeCellWidth);
        int curB = (int)Math.Floor(current.Color.B / rangeCellWidth);

        double sumX = 0;
        double sumY = 0;
        double sumL = 0;
        double sumA = 0;
        double sumB = 0;
        int count = 0;

        for (int dxCell = -1; dxCell <= 1; dxCell++)
        {
            int cellX = curX + dxCell;
            for (int dyCell = -1; dyCell <= 1; dyCell++)
            {
                int cellY = curY + dyCell;
                for (int dlCell = -1; dlCell <= 1; dlCell++)
                {
                    int cellL = curL + dlCell;
                    for (int daCell = -1; daCell <= 1; daCell++)
                    {
                        int cellA = curA + daCell;
                        for (int dbCell = -1; dbCell <= 1; dbCell++)
                        {
                            int cellB = curB + dbCell;
                            var key = new CellKey5D(cellX, cellY, cellL, cellA, cellB);
                            if (cellSpans.TryGetValue(key, out CellSpan span))
                            {
                                int end = span.Offset + span.Count;
                                for (int i = span.Offset; i < end; i++)
                                {
                                    int idx = pixelIndices[i];
                                    int px = idx % width;
                                    int py = idx / width;
                                    double dx = px - current.X;
                                    double dy = py - current.Y;
                                    double distSqSpatial = (dx * dx) + (dy * dy);
                                    if (distSqSpatial <= spatialRadiusSq)
                                    {
                                        Lab32 pixelColor = sourceLab[idx];
                                        if (pixelColor.EuclideanDistanceSquared(current.Color) <= rangeRadiusSq)
                                        {
                                            sumX += px;
                                            sumY += py;
                                            sumL += pixelColor.L;
                                            sumA += pixelColor.A;
                                            sumB += pixelColor.B;
                                            count++;
                                        }
                                    }
                                }
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
}
