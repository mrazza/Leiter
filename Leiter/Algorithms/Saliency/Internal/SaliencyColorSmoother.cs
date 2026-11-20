namespace Leiter.Algorithms.Saliency.Internal;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Leiter.Algorithms.Quantization;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Shared color-space saliency smoothing and quantization helpers for contrast-based saliency algorithms.
/// </summary>
internal static class SaliencyColorSmoother
{
    internal const int DefaultQuantizationLevels = 12;

    private readonly record struct NeighborEntry(int EntryIndex, double Distance, int PackedColor);

    /// <summary>
    /// Quantizes <paramref name="image"/> into <see cref="DefaultQuantizationLevels"/> buckets per channel,
    /// truncates to the most frequent buckets, and builds the CIELAB lookup table used by saliency algorithms.
    /// </summary>
    public static QuantizedPalette<Rgb8, Lab32> QuantizeForSaliency(IReadOnlyMatrix<Rgb8> image) =>
        PaletteQuantizer.QuantizeAndTruncate(
            image,
            static color => UniformQuantizer.QuantizeToMidpoint(color, DefaultQuantizationLevels),
            static color => color.ToLab32(RgbColorSpace.SRgb));

    /// <summary>
    /// Smooths dense per-palette-color saliency values in place across nearest neighbors using <paramref name="distanceTable"/>.
    /// </summary>
    /// <param name="colorSaliencies">The dense per-color saliency array indexed consistently with <paramref name="distanceTable"/>.</param>
    /// <param name="distanceTable">Precomputed pairwise CIEDE2000 distance table matching <paramref name="colorSaliencies"/> length.</param>
    /// <param name="neighborDivisor">Divisor applied to the active palette size to select the neighbor count.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="distanceTable"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="neighborDivisor"/> is less than or equal to zero.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="colorSaliencies"/> length does not match <see cref="PairwiseLabDistanceTable.Count"/>.</exception>
    public static void SmoothColorSaliency(
        Span<double> colorSaliencies,
        PairwiseLabDistanceTable distanceTable,
        int neighborDivisor)
    {
        ArgumentNullException.ThrowIfNull(distanceTable);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(neighborDivisor, 0);

        if (colorSaliencies.Length != distanceTable.Count)
        {
            throw new ArgumentException(
                $"Color saliency span length {colorSaliencies.Length} must match distance table count {distanceTable.Count}.",
                nameof(colorSaliencies));
        }

        int activeCount = colorSaliencies.Length;
        int nearestColorCount = activeCount / neighborDivisor;
        if (nearestColorCount <= 1)
        {
            return;
        }

        var tableIndices = new int[activeCount];
        var packedColors = new int[activeCount];
        for (int i = 0; i < activeCount; i++)
        {
            tableIndices[i] = i;
            packedColors[i] = PackRgbKey(distanceTable.GetColor(i));
        }

        double[] unsmoothedValues = colorSaliencies.ToArray();
        SmoothCore(unsmoothedValues, tableIndices, packedColors, distanceTable, nearestColorCount, colorSaliencies);
    }

    /// <summary>
    /// Smooths per-color saliency values across nearest neighbors in CIELAB space.
    /// </summary>
    /// <param name="colorSaliencyMap">The per-color saliency map to smooth in place.</param>
    /// <param name="rgbToLabLookupTable">Precomputed Lab colors for keys in <paramref name="colorSaliencyMap"/>.</param>
    /// <param name="neighborDivisor">Divisor applied to the active palette size to select the neighbor count.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="colorSaliencyMap"/> or <paramref name="rgbToLabLookupTable"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="neighborDivisor"/> is less than or equal to zero.</exception>
    public static void SmoothColorSaliency(
        Dictionary<Rgb8, double> colorSaliencyMap,
        IReadOnlyDictionary<Rgb8, Lab32> rgbToLabLookupTable,
        int neighborDivisor)
    {
        ArgumentNullException.ThrowIfNull(colorSaliencyMap);
        ArgumentNullException.ThrowIfNull(rgbToLabLookupTable);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(neighborDivisor, 0);

        int nearestColorCount = colorSaliencyMap.Count / neighborDivisor;
        if (nearestColorCount <= 1)
        {
            return;
        }

        PairwiseLabDistanceTable distanceTable = PairwiseLabDistanceTable.Build(rgbToLabLookupTable);
        SmoothColorSaliency(colorSaliencyMap, distanceTable, neighborDivisor);
    }

    /// <summary>
    /// Smooths per-color saliency values across nearest neighbors using a precomputed <see cref="PairwiseLabDistanceTable"/>.
    /// </summary>
    /// <param name="colorSaliencyMap">The per-color saliency map to smooth in place.</param>
    /// <param name="distanceTable">Precomputed pairwise CIEDE2000 distance table covering all keys in <paramref name="colorSaliencyMap"/>.</param>
    /// <param name="neighborDivisor">Divisor applied to the active palette size to select the neighbor count.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="colorSaliencyMap"/> or <paramref name="distanceTable"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="neighborDivisor"/> is less than or equal to zero.</exception>
    public static void SmoothColorSaliency(
        Dictionary<Rgb8, double> colorSaliencyMap,
        PairwiseLabDistanceTable distanceTable,
        int neighborDivisor)
    {
        ArgumentNullException.ThrowIfNull(colorSaliencyMap);
        ArgumentNullException.ThrowIfNull(distanceTable);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(neighborDivisor, 0);

        int activeCount = colorSaliencyMap.Count;
        int nearestColorCount = activeCount / neighborDivisor;
        if (nearestColorCount <= 1)
        {
            return;
        }

        var keys = new Rgb8[activeCount];
        var tableIndices = new int[activeCount];
        var unsmoothedValues = new double[activeCount];
        var packedColors = new int[activeCount];
        int entryIndex = 0;
        foreach (var bucket in colorSaliencyMap)
        {
            keys[entryIndex] = bucket.Key;
            tableIndices[entryIndex] = distanceTable.GetIndex(bucket.Key);
            unsmoothedValues[entryIndex] = bucket.Value;
            packedColors[entryIndex] = PackRgbKey(bucket.Key);
            entryIndex++;
        }

        var smoothedValues = new double[activeCount];
        SmoothCore(unsmoothedValues, tableIndices, packedColors, distanceTable, nearestColorCount, smoothedValues);

        for (int i = 0; i < activeCount; i++)
        {
            colorSaliencyMap[keys[i]] = smoothedValues[i];
        }
    }

    private static void SmoothCore(
        ReadOnlySpan<double> unsmoothedValues,
        ReadOnlySpan<int> tableIndices,
        ReadOnlySpan<int> packedColors,
        PairwiseLabDistanceTable distanceTable,
        int nearestColorCount,
        Span<double> destination)
    {
        int activeCount = unsmoothedValues.Length;
        var topNeighbors = new NeighborEntry[nearestColorCount];

        for (int i = 0; i < activeCount; i++)
        {
            ReadOnlySpan<double> distanceRow = distanceTable.GetRow(tableIndices[i]);

            for (int j = 0; j < nearestColorCount; j++)
            {
                topNeighbors[j] = new NeighborEntry(j, distanceRow[tableIndices[j]], packedColors[j]);
            }

            if (nearestColorCount < activeCount)
            {
                for (int p = (nearestColorCount / 2) - 1; p >= 0; p--)
                {
                    SiftDownMaxHeap(topNeighbors, p, nearestColorCount);
                }

                for (int j = nearestColorCount; j < activeCount; j++)
                {
                    double distance = distanceRow[tableIndices[j]];
                    int packed = packedColors[j];
                    if (CompareNeighbors(distance, packed, topNeighbors[0]) < 0)
                    {
                        topNeighbors[0] = new NeighborEntry(j, distance, packed);
                        SiftDownMaxHeap(topNeighbors, 0, nearestColorCount);
                    }
                }
            }

            Array.Sort(topNeighbors, CompareNeighbors);

            // Computes totalDistance = sum(D(c, c_i)).
            // Note that we include our own color here. The paper says "other colors" and "other pixels"
            // but the equations seem to explicitly include the color or pixel under evaluation and so
            // we do so here.
            double totalDistance = 0.0;
            for (int k = 0; k < nearestColorCount; k++)
            {
                totalDistance += topNeighbors[k].Distance;
            }

            if (totalDistance <= 0.0)
            {
                destination[i] = unsmoothedValues[i];
                continue;
            }

            // Computes (1 / ((m - 1) * totalDistance)) * sum((totalDistance - D(c, c_i)) * S[c_i]).
            double weightedSum = 0.0;
            for (int k = 0; k < nearestColorCount; k++)
            {
                weightedSum += (totalDistance - topNeighbors[k].Distance) * unsmoothedValues[topNeighbors[k].EntryIndex];
            }

            destination[i] = (1.0 / ((nearestColorCount - 1) * totalDistance)) * weightedSum;
        }
    }

    private static int PackRgbKey(Rgb8 color) =>
        (color.R << 16) | (color.G << 8) | color.B;

    private static int CompareNeighbors(NeighborEntry left, NeighborEntry right) =>
        CompareNeighbors(left.Distance, left.PackedColor, right);

    private static int CompareNeighbors(double leftDistance, int leftPackedColor, in NeighborEntry right)
    {
        int cmp = leftDistance.CompareTo(right.Distance);
        return cmp != 0 ? cmp : leftPackedColor.CompareTo(right.PackedColor);
    }

    private static void SiftDownMaxHeap(NeighborEntry[] heap, int root, int count)
    {
        NeighborEntry value = heap[root];
        int half = count >> 1;
        int index = root;

        while (index < half)
        {
            int child = (index << 1) + 1;
            int right = child + 1;
            if (right < count && CompareNeighbors(heap[right], heap[child]) > 0)
            {
                child = right;
            }

            if (CompareNeighbors(value, heap[child]) >= 0)
            {
                break;
            }

            heap[index] = heap[child];
            index = child;
        }

        heap[index] = value;
    }

    /// <summary>
    /// Computes the minimum value and positive span <c>(max - min)</c> across <paramref name="values"/>.
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="values"/> is non-empty and <paramref name="range"/> &gt; 0; otherwise <see langword="false"/>.</returns>
    public static bool TryGetMinMaxRange(ReadOnlySpan<double> values, out double min, out double range)
    {
        if (values.IsEmpty)
        {
            return EmptyMinMaxRange(out min, out range);
        }

        double minVal = values[0];
        double maxVal = minVal;
        for (int i = 1; i < values.Length; i++)
        {
            UpdateMinMax(values[i], ref minVal, ref maxVal);
        }

        return CompleteMinMaxRange(minVal, maxVal, out min, out range);
    }

    /// <summary>
    /// Computes the minimum value and positive span <c>(max - min)</c> across <paramref name="values"/> in a single pass.
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="values"/> is non-empty and <paramref name="range"/> &gt; 0; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <see langword="null"/>.</exception>
    public static bool TryGetMinMaxRange(IEnumerable<double> values, out double min, out double range)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values is double[] array)
        {
            return TryGetMinMaxRange(array.AsSpan(), out min, out range);
        }

        if (values is List<double> list)
        {
            return TryGetMinMaxRange(CollectionsMarshal.AsSpan(list), out min, out range);
        }

        using IEnumerator<double> enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return EmptyMinMaxRange(out min, out range);
        }

        double minVal = enumerator.Current;
        double maxVal = minVal;
        while (enumerator.MoveNext())
        {
            UpdateMinMax(enumerator.Current, ref minVal, ref maxVal);
        }

        return CompleteMinMaxRange(minVal, maxVal, out min, out range);
    }

    private static bool EmptyMinMaxRange(out double min, out double range)
    {
        min = 0.0;
        range = 0.0;
        return false;
    }

    private static void UpdateMinMax(double value, ref double minVal, ref double maxVal)
    {
        if (value < minVal)
            minVal = value;
        if (value > maxVal)
            maxVal = value;
    }

    private static bool CompleteMinMaxRange(double minVal, double maxVal, out double min, out double range)
    {
        min = minVal;
        range = maxVal - minVal;
        return range > 0.0;
    }
}
