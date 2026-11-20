namespace Leiter.Algorithms.Saliency;

using System;
using System.Collections.Generic;
using System.Linq;
using Leiter.Pixels;

/// <summary>
/// Shared color-space saliency smoothing helper for contrast-based saliency algorithms.
/// </summary>
internal static class SaliencyColorSmoother
{
    /// <summary>
    /// Smooths per-color saliency values across nearest neighbors in CIELAB space.
    /// </summary>
    /// <param name="colorSaliencyMap">The per-color saliency map to smooth in place.</param>
    /// <param name="rgbToLabLookupTable">Precomputed Lab colors for keys in <paramref name="colorSaliencyMap"/>.</param>
    /// <param name="neighborDivisor">Divisor applied to the active palette size to select the neighbor count.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="neighborDivisor"/> is less than or equal to zero.</exception>
    public static void SmoothColorSaliency(
        Dictionary<Rgb8, double> colorSaliencyMap,
        IReadOnlyDictionary<Rgb8, Lab32> rgbToLabLookupTable,
        int neighborDivisor)
    {
        if (neighborDivisor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(neighborDivisor), "Neighbor divisor must be positive.");
        }

        int nearestColorCount = colorSaliencyMap.Count / neighborDivisor;
        if (nearestColorCount <= 1)
        {
            return;
        }

        var unsmoothed = new Dictionary<Rgb8, double>(colorSaliencyMap);
        foreach (var bucket in unsmoothed)
        {
            // Computes [c, D(c, c sub i)] for m nearest neighbors of bucket.Key.
            // Note that we include our own color here. The paper says "other colors" and "other pixels"
            // but the equations seem to explicitly include the color or pixel under evaluation and so
            // we do so here.
            Lab32 bucketLab = rgbToLabLookupTable[bucket.Key];
            var nearestNeighbors = unsmoothed.Keys
                .Select(key => (color: key, distance: bucketLab.Distance(rgbToLabLookupTable[key])))
                .OrderBy(neighbor => neighbor.distance)
                .ThenBy(neighbor => neighbor.color.GetHashCode())
                .Take(nearestColorCount)
                .ToList();

            // Computes T = sum(D(c, c sub i)).
            double T = nearestNeighbors.Sum(n => n.distance);
            if (T <= 0.0)
            {
                continue;
            }

            // Computes (1 / ((m - 1) * T)) * sum((T - D(c, c sub i)) * S[c sub i]).
            colorSaliencyMap[bucket.Key] = (1.0 / ((nearestColorCount - 1) * T)) *
                nearestNeighbors.Sum(n => (T - n.distance) * unsmoothed[n.color]);
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
