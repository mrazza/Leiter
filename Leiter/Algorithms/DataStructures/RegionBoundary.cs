namespace Leiter.Algorithms.DataStructures;

using Leiter.Core;

/// <summary>
/// Represents the shared spatial boundary between two adjacent regions in a <see cref="RegionGraph"/>,
/// storing deduplicated border pixel coordinates on both sides in row-major order.
/// </summary>
/// <param name="First">The smaller dense region identifier (<c>First &lt; Second</c>).</param>
/// <param name="Second">The larger dense region identifier.</param>
/// <param name="FirstBorderPixels">Deduplicated pixel coordinates inside <paramref name="First"/> that border <paramref name="Second"/>.</param>
/// <param name="SecondBorderPixels">Deduplicated pixel coordinates inside <paramref name="Second"/> that border <paramref name="First"/>.</param>
public readonly record struct RegionBoundary(
    int First,
    int Second,
    ReadOnlyMemory<Coord> FirstBorderPixels,
    ReadOnlyMemory<Coord> SecondBorderPixels) : IUndirectedEdge<int>
{
    /// <summary>
    /// Determines whether <paramref name="regionId"/> is one of the two regions bounding this edge.
    /// </summary>
    /// <param name="regionId">The dense region identifier to test.</param>
    /// <returns><see langword="true"/> if <paramref name="regionId"/> equals <see cref="First"/> or <see cref="Second"/>; otherwise <see langword="false"/>.</returns>
    public bool IsIncidentTo(int regionId) => regionId == First || regionId == Second;

    /// <summary>
    /// Returns the identifier of the region on the opposite side of this boundary from <paramref name="regionId"/>.
    /// </summary>
    /// <param name="regionId">One of <see cref="First"/> or <see cref="Second"/>.</param>
    /// <returns>The opposite region identifier.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="regionId"/> does not belong to this boundary.</exception>
    public int GetOther(int regionId)
    {
        if (regionId == First)
            return Second;
        if (regionId == Second)
            return First;

        throw new ArgumentOutOfRangeException(nameof(regionId), regionId, $"Region {regionId} is not incident to boundary ({First}, {Second}).");
    }

    /// <summary>
    /// Returns the deduplicated border pixel coordinates belonging to <paramref name="regionId"/> along this boundary.
    /// </summary>
    /// <param name="regionId">One of <see cref="First"/> or <see cref="Second"/>.</param>
    /// <returns>The border pixel coordinates on the requested side of the boundary.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="regionId"/> does not belong to this boundary.</exception>
    public ReadOnlyMemory<Coord> GetBorderPixelsFor(int regionId)
    {
        if (regionId == First)
            return FirstBorderPixels;
        if (regionId == Second)
            return SecondBorderPixels;

        throw new ArgumentOutOfRangeException(nameof(regionId), regionId, $"Region {regionId} is not incident to boundary ({First}, {Second}).");
    }

    /// <summary>
    /// Converts this topological boundary into a weighted <see cref="UndirectedGraphEdge{T}"/> over dense region identifiers.
    /// </summary>
    /// <param name="weight">The scalar edge weight to assign.</param>
    /// <returns>A weighted undirected graph edge connecting <see cref="First"/> and <see cref="Second"/>.</returns>
    public UndirectedGraphEdge<int> ToEdge(double weight = 0.0) => new(First, Second, weight);
}
