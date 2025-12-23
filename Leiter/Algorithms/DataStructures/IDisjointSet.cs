namespace Leiter.Algorithms.DataStructures;

using System.Collections.Immutable;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Disjoint Set Union (DSU) data structure, also known as Union-Find or Merge-Find.
/// </summary>
public interface IDisjointSet
{
    public interface IPartition
    {
        /// <summary>
        /// The index of the root of the partition.
        /// </summary>
        int RootIndex { get; }

        /// <summary>
        /// The size of the partition.
        /// </summary>
        int Size { get; }
    }

    /// <summary>
    /// Gets the region of pixels that are in the specified partition.
    /// </summary>
    /// <param name="partition">The partition to get the region for.</param>
    /// <returns>The region of pixels that are in the specified partition.</returns>
    Region<Coord> GetRegion(IPartition partition);

    /// <summary>
    /// Gets the region of pixels that are in the partition containing the specified coordinate.
    /// </summary>
    /// <param name="coord">The coordinate to get the region for.</param>
    /// <returns>The region of pixels that are in the partition containing the specified coordinate.</returns>
    Region<Coord> GetRegion(Coord coord);

    /// <summary>
    /// Gets the region of pixels that are in the partition containing the specified index.
    /// </summary>
    /// <param name="index">The index in the matrix to get the region for.</param>
    /// <returns>The region of pixels that are in the partition containing the specified index.</returns>
    Region<Coord> GetRegion(int index);

    /// <summary>
    /// Returns a matrix where each pixel is a unique identifier representing the partition it belongs to.
    /// </summary>
    /// <remarks>
    /// Pixels in the same partition (disjoin set) will have the same value. Pixels with different values
    /// are, necessarily, in different partitions.
    /// </remarks>
    /// <returns>A matrix where each pixel is a unique identifier representing the partition it belongs to.</returns>
    Matrix<LongPixel> AsMatrix();

    /// <summary>
    /// Returns a set of regions, where each region is a set of pixels that are in the same partition.
    /// </summary>
    /// <returns>A set of regions, where each region is a set of pixels that are in the same partition.</returns>
    IImmutableSet<Region<Coord>> AsRegions();
}