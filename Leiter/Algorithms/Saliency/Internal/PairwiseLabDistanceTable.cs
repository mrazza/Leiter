namespace Leiter.Algorithms.Saliency.Internal;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Leiter.Pixels;

/// <summary>
/// Precomputes and caches symmetric pairwise CIEDE2000 color distances across a quantized <see cref="Rgb8"/> palette.
/// </summary>
internal sealed class PairwiseLabDistanceTable
{
    private readonly double[] distances;
    private readonly Rgb8[] colors;
    private readonly Dictionary<Rgb8, int> colorToIndex;

    private PairwiseLabDistanceTable(double[] distances, Rgb8[] colors, Dictionary<Rgb8, int> colorToIndex)
    {
        this.distances = distances;
        this.colors = colors;
        this.colorToIndex = colorToIndex;
    }

    /// <summary>
    /// Gets the number of palette colors indexed by this table.
    /// </summary>
    public int Count => colors.Length;

    /// <summary>
    /// Gets the symmetric CIEDE2000 distance between palette colors at indices <paramref name="i"/> and <paramref name="j"/>.
    /// </summary>
    public double this[int i, int j] => distances[(i * colors.Length) + j];

    /// <summary>
    /// Builds a symmetric pairwise CIEDE2000 distance table from <paramref name="rgbToLabLookupTable"/>,
    /// evaluating only the upper triangle in parallel.
    /// </summary>
    /// <param name="rgbToLabLookupTable">Mapping from quantized <see cref="Rgb8"/> colors to their CIELAB values.</param>
    /// <returns>A populated <see cref="PairwiseLabDistanceTable"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rgbToLabLookupTable"/> is <see langword="null"/>.</exception>
    public static PairwiseLabDistanceTable Build(IReadOnlyDictionary<Rgb8, Lab32> rgbToLabLookupTable)
    {
        ArgumentNullException.ThrowIfNull(rgbToLabLookupTable);

        int count = rgbToLabLookupTable.Count;
        var colors = new Rgb8[count];
        var labColors = new Lab32[count];
        var colorToIndex = new Dictionary<Rgb8, int>(count);

        int idx = 0;
        foreach (var entry in rgbToLabLookupTable)
        {
            colors[idx] = entry.Key;
            labColors[idx] = entry.Value;
            colorToIndex[entry.Key] = idx;
            idx++;
        }

        var distances = new double[count * count];
        if (count > 1)
        {
            Parallel.For(0, count, i =>
            {
                Lab32 ci = labColors[i];
                int rowOffset = i * count;
                for (int j = i + 1; j < count; j++)
                {
                    double distance = ci.Distance(labColors[j]);
                    distances[rowOffset + j] = distance;
                    distances[(j * count) + i] = distance;
                }
            });
        }

        return new PairwiseLabDistanceTable(distances, colors, colorToIndex);
    }

    /// <summary>
    /// Gets the palette color at <paramref name="index"/>.
    /// </summary>
    public Rgb8 GetColor(int index) => colors[index];

    /// <summary>
    /// Gets the dense zero-based table index associated with <paramref name="color"/>.
    /// </summary>
    public int GetIndex(Rgb8 color) => colorToIndex[color];

    /// <summary>
    /// Attempts to resolve the dense zero-based table index associated with <paramref name="color"/>.
    /// </summary>
    public bool TryGetIndex(Rgb8 color, out int index) => colorToIndex.TryGetValue(color, out index);

    /// <summary>
    /// Returns a contiguous read-only span of distances from palette color <paramref name="index"/> to all palette colors.
    /// </summary>
    public ReadOnlySpan<double> GetRow(int index) => distances.AsSpan(index * colors.Length, colors.Length);

    /// <summary>
    /// Gets the CIEDE2000 distance between two palette colors <paramref name="left"/> and <paramref name="right"/>.
    /// </summary>
    public double GetDistance(Rgb8 left, Rgb8 right) => this[GetIndex(left), GetIndex(right)];
}
