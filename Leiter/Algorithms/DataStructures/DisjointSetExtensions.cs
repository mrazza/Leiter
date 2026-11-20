namespace Leiter.Algorithms.DataStructures;

using Leiter.Core;

/// <summary>
/// Extension methods for <see cref="IDisjointSet"/> segmentations.
/// </summary>
public static class DisjointSetExtensions
{
    /// <summary>
    /// Converts this disjoint-set segmentation into a deterministic <see cref="RegionGraph"/>.
    /// </summary>
    /// <param name="segmentation">The segmented pixel partition.</param>
    /// <param name="connectivity">The grid connectivity used to detect neighboring regions.</param>
    /// <returns>A populated <see cref="RegionGraph"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="segmentation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="connectivity"/> is invalid.</exception>
    public static RegionGraph ToRegionGraph(
        this IDisjointSet segmentation,
        GridConnectivity connectivity = GridConnectivity.EightConnected)
    {
        ArgumentNullException.ThrowIfNull(segmentation);
        return RegionGraph.FromDisjointSet(segmentation, connectivity);
    }
}
