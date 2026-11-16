namespace Leiter.Algorithms.DataStructures;

using Leiter.Core;

/// <summary>
/// Represents the shared spatial boundary between two adjacent regions in a <see cref="RegionGraph"/>,
/// storing deduplicated border pixel coordinates on both sides in row-major order.
/// </summary>
/// <param name="FirstRegionId">The smaller dense region identifier (<c>FirstRegionId &lt; SecondRegionId</c>).</param>
/// <param name="SecondRegionId">The larger dense region identifier.</param>
/// <param name="FirstBorderPixels">Deduplicated pixel coordinates inside <paramref name="FirstRegionId"/> that border <paramref name="SecondRegionId"/>.</param>
/// <param name="SecondBorderPixels">Deduplicated pixel coordinates inside <paramref name="SecondRegionId"/> that border <paramref name="FirstRegionId"/>.</param>
public readonly record struct RegionBoundary(
    int FirstRegionId,
    int SecondRegionId,
    ReadOnlyMemory<Coord> FirstBorderPixels,
    ReadOnlyMemory<Coord> SecondBorderPixels)
{
    /// <summary>
    /// Returns the identifier of the region on the opposite side of this boundary from <paramref name="regionId"/>.
    /// </summary>
    /// <param name="regionId">One of <see cref="FirstRegionId"/> or <see cref="SecondRegionId"/>.</param>
    /// <returns>The opposite region identifier.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="regionId"/> does not belong to this boundary.</exception>
    public int GetOtherRegionId(int regionId)
    {
        if (regionId == FirstRegionId)
            return SecondRegionId;
        if (regionId == SecondRegionId)
            return FirstRegionId;

        throw new ArgumentOutOfRangeException(nameof(regionId), regionId, $"Region {regionId} is not incident to boundary ({FirstRegionId}, {SecondRegionId}).");
    }

    /// <summary>
    /// Returns the deduplicated border pixel coordinates belonging to <paramref name="regionId"/> along this boundary.
    /// </summary>
    /// <param name="regionId">One of <see cref="FirstRegionId"/> or <see cref="SecondRegionId"/>.</param>
    /// <returns>The border pixel coordinates on the requested side of the boundary.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="regionId"/> does not belong to this boundary.</exception>
    public ReadOnlyMemory<Coord> GetBorderPixelsFor(int regionId)
    {
        if (regionId == FirstRegionId)
            return FirstBorderPixels;
        if (regionId == SecondRegionId)
            return SecondBorderPixels;

        throw new ArgumentOutOfRangeException(nameof(regionId), regionId, $"Region {regionId} is not incident to boundary ({FirstRegionId}, {SecondRegionId}).");
    }
}
