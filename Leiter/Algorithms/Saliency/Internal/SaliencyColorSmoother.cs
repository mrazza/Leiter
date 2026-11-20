namespace Leiter.Algorithms.Saliency.Internal;

using System;
using System.Collections.Generic;
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

        int nearestColorCount = colorSaliencyMap.Count / neighborDivisor;
        if (nearestColorCount <= 1)
        {
            return;
        }

        int activeCount = colorSaliencyMap.Count;
        var entries = new (Rgb8 Color, int TableIndex, double UnsmoothedValue, int Hash)[activeCount];
        int entryIndex = 0;
        foreach (var bucket in colorSaliencyMap)
        {
            entries[entryIndex++] = (bucket.Key, distanceTable.GetIndex(bucket.Key), bucket.Value, bucket.Key.GetHashCode());
        }

        var neighbors = new (int EntryIndex, double Distance, int Hash)[activeCount];
        for (int i = 0; i < activeCount; i++)
        {
            ReadOnlySpan<double> distanceRow = distanceTable.GetRow(entries[i].TableIndex);
            for (int j = 0; j < activeCount; j++)
            {
                neighbors[j] = (j, distanceRow[entries[j].TableIndex], entries[j].Hash);
            }

            Array.Sort(neighbors, static (a, b) =>
            {
                int cmp = a.Distance.CompareTo(b.Distance);
                return cmp != 0 ? cmp : a.Hash.CompareTo(b.Hash);
            });

            // Computes T = sum(D(c, c sub i)).
            // Note that we include our own color here. The paper says "other colors" and "other pixels"
            // but the equations seem to explicitly include the color or pixel under evaluation and so
            // we do so here.
            double T = 0.0;
            for (int k = 0; k < nearestColorCount; k++)
            {
                T += neighbors[k].Distance;
            }

            if (T <= 0.0)
            {
                continue;
            }

            // Computes (1 / ((m - 1) * T)) * sum((T - D(c, c sub i)) * S[c sub i]).
            double weightedSum = 0.0;
            for (int k = 0; k < nearestColorCount; k++)
            {
                weightedSum += (T - neighbors[k].Distance) * entries[neighbors[k].EntryIndex].UnsmoothedValue;
            }

            colorSaliencyMap[entries[i].Color] = (1.0 / ((nearestColorCount - 1) * T)) * weightedSum;
        }
    }

    /// <summary>
    /// Computes the minimum value and positive span <c>(max - min)</c> across <paramref name="values"/>.
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="values"/> is non-empty and <paramref name="range"/> &gt; 0; otherwise <see langword="false"/>.</returns>
    public static bool TryGetMinMaxRange(ReadOnlySpan<double> values, out double min, out double range)
    {
        if (values.IsEmpty)
        {
            min = 0.0;
            range = 0.0;
            return false;
        }

        double minVal = values[0];
        double maxVal = minVal;
        for (int i = 1; i < values.Length; i++)
        {
            double val = values[i];
            if (val < minVal)
                minVal = val;
            if (val > maxVal)
                maxVal = val;
        }

        min = minVal;
        range = maxVal - minVal;
        return range > 0.0;
    }

    /// <summary>
    /// Computes the minimum value and positive span <c>(max - min)</c> across <paramref name="values"/> in a single pass.
    /// </summary>
    /// <returns><see langword="true"/> if <paramref name="values"/> is non-empty and <paramref name="range"/> &gt; 0; otherwise <see langword="false"/>.</returns>
    public static bool TryGetMinMaxRange(IEnumerable<double> values, out double min, out double range)
    {
        using IEnumerator<double> enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            min = 0.0;
            range = 0.0;
            return false;
        }

        double minVal = enumerator.Current;
        double maxVal = minVal;
        while (enumerator.MoveNext())
        {
            double val = enumerator.Current;
            if (val < minVal)
                minVal = val;
            if (val > maxVal)
                maxVal = val;
        }

        min = minVal;
        range = maxVal - minVal;
        return range > 0.0;
    }
}
