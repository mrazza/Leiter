namespace Leiter.Algorithms.DataStructures;

using System.Collections.Immutable;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Disjoint Set Union (DSU) data structure, also known as Union-Find or Merge-Find.
/// </summary>
public interface IDisjointSet : IReadOnlyMatrix<LongPixel>
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

        /// <summary>
        /// Gets the indices of the pixels in the partition.
        /// </summary>
        /// <returns>The indices of the pixels in the partition.</returns>
        IEnumerable<int> GetIndices();

        /// <summary>
        /// Gets the coordinates of the pixels in the partition.
        /// </summary>
        /// <returns>The coordinates of the pixels in the partition.</returns>
        IEnumerable<Coord> GetCoords();
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
    /// Returns a materialized matrix where each pixel is a unique identifier representing the partition it belongs to.
    /// </summary>
    /// <remarks>
    /// Pixels in the same partition (disjoin set) will have the same value. Pixels with different values
    /// are, necessarily, in different partitions.
    /// 
    /// As a result of the materialization, this method is preferred over treating the DisjointSet as a matrix for
    /// computations where many lookups will be required or you will need to iterate more than once.
    /// </remarks>
    /// <returns>A materialized matrix where each pixel is a unique identifier representing the partition it belongs to.</returns>
    Matrix<LongPixel> ToMatrix();

    /// <summary>
    /// Returns a materialized set of regions, where each region is a set of pixels that are in the same partition.
    /// </summary>
    /// <remarks>
    /// As a result of the materialization, this method is preferred over getting paritions and iterating over the
    /// {GetCoords()} or {GetIndices()} methods if multiple reads will take place.
    /// </remarks>
    /// <returns>A materialized set of regions, where each region is a set of pixels that are in the same partition.</returns>
    IImmutableSet<Region<Coord>> ToRegions();
}