namespace Leiter.Algorithms.DataStructures;

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Disjoint Set Union (DSU) data structure, also known as Union-Find or Merge-Find.
/// </summary>
/// <remarks>
/// This is written explicitly for the purposes of placing individual pixels into disjoin, non-overlapping
/// sets and maintaining arbitrary data associated with each set.
/// 
/// Roots/Partition IDs are arbitrary integers. They are not guaranteed to be sequential or in any particular order.
/// The only guarantee is that pixels in the same set will have the same root/partition ID.
/// 
/// For background on the datastructure, see: https://en.wikipedia.org/wiki/Disjoint-set_data_structure
/// </remarks>
/// <typeparam name="T">The arbitrary data type to be associated with each partition.</typeparam>
public class DisjointSet<T> : IDisjointSet
    where T : struct
{
    /// <summary>
    /// Represents a snapshot of the current state of a partition within this disjoint set.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Partition : IDisjointSet.IPartition
    {
        internal Partition(DisjointSet<T> disjointSet, int rootIndex, int size, T value)
        {
            this.disjointSet = disjointSet;
            this.RootIndex = rootIndex;
            this.Size = size;
            this.Value = value;
        }

        private readonly DisjointSet<T> disjointSet;

        /// <inheritdoc/>
        public int RootIndex { get; }

        /// <inheritdoc/>
        public int Size { get; }

        /// <summary>
        /// Gets the value associated with this partition.
        /// </summary>
        public T Value { get; }

        /// <inheritdoc/>
        public IEnumerable<Coord> GetCoords()
        {
            foreach (int index in disjointSet.EnumeratePartitionIndices(RootIndex))
                yield return disjointSet.image.CoordFromIndex(index);
        }

        /// <inheritdoc/>
        public IEnumerable<int> GetIndices()
            => disjointSet.EnumeratePartitionIndices(RootIndex);
    }


    /// <summary>
    /// Represents an element within the disjoint set, storing its forest links and associated value.
    /// </summary>
    /// <param name="SelfIndex">The linear index of this element in the matrix.</param>
    /// <param name="ParentIndex">The index of the parent element in the forest tree for path compression.</param>
    /// <param name="NextIndex">The index of the next element in the circular singly-linked list for allocation-free partition traversal.</param>
    /// <param name="Size">The number of elements in the tree rooted at this element, used for union-by-size rank optimization.</param>
    /// <param name="Value">The arbitrary user data associated with this partition root.</param>
    [StructLayout(LayoutKind.Sequential)]
    private record struct Element(int SelfIndex, int ParentIndex, int NextIndex, int Size, T Value);

    private readonly IUntypedMatrix image;
    private readonly Element[] elements;

    /// <inheritdoc/>
    public int Count => image.Count;

    /// <inheritdoc/>
    public int Width => image.Width;

    /// <inheritdoc/>
    public int Height => image.Height;

    /// <inheritdoc/>
    public Size Size => image.Size;

    /// <inheritdoc/>
    public LongPixel this[int index] => FindRootReadOnly(index);

    /// <inheritdoc/>
    public LongPixel this[Coord coord] => FindRootReadOnly(image.IndexFromCoord(coord));

    /// <inheritdoc/>
    public LongPixel this[int x, int y] => FindRootReadOnly(image.IndexFromCoord(new Coord(x, y)));

    /// <summary>
    /// Initializes a new instance of the <see cref="DisjointSet{T}" /> class.
    /// </summary>
    /// <param name="image">The image to segment into disjoint sets.</param>
    /// <param name="initialValueFunc">The function to be used to initialize the value of each partition.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="initialValueFunc"/> is <see langword="null"/>.</exception>
    public DisjointSet(IUntypedMatrix image, Func<int, T> initialValueFunc)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(initialValueFunc);
        this.image = image;
        elements = new Element[image.Count];
        for (int index = 0; index < image.Count; index++)
        {
            elements[index].SelfIndex = index;
            elements[index].ParentIndex = index;
            elements[index].NextIndex = index;
            elements[index].Size = 1;
            elements[index].Value = initialValueFunc(index);
        }
    }

    /// <summary>
    /// Gets a snapshot of the partition containing the specified coordinate.
    /// </summary>
    /// <param name="coord">The coordinate to get the partition for.</param>
    /// <returns>A snapshot of the partition containing the specified coordinate.</returns>
    public Partition GetPartition(Coord coord)
    {
        return GetPartition(image.IndexFromCoord(coord));
    }

    /// <summary>
    /// Gets a snapshot of the partition containing the specified index.
    /// </summary>
    /// <param name="index">The index in the matrix to get the partition for.</param>
    /// <returns>A snapshot of the partition containing the specified index.</returns>
    public Partition GetPartition(int index)
    {
        var rootIndex = Find(index);
        return new Partition(this, rootIndex, elements[rootIndex].Size, elements[rootIndex].Value);
    }

    /// <inheritdoc/>
    public Region<Coord> GetRegion(IDisjointSet.IPartition partition)
    {
        return GetRegion(partition.RootIndex);
    }

    /// <inheritdoc/>
    public Region<Coord> GetRegion(Coord coord)
    {
        return GetRegion(image.IndexFromCoord(coord));
    }

    /// <inheritdoc/>
    public Region<Coord> GetRegion(int index)
    {
        int rootIndex = FindRootReadOnly(index);
        var region = new Region<Coord>(elements[rootIndex].Size);
        int currIndex = rootIndex;
        do
        {
            region.Pixels.Add(image.CoordFromIndex(currIndex));
            currIndex = elements[currIndex].NextIndex;
        } while (currIndex != rootIndex);
        return region;
    }

    private IEnumerable<int> EnumeratePartitionIndices(int rootIndex)
    {
        int currIndex = rootIndex;
        do
        {
            yield return currIndex;
            currIndex = elements[currIndex].NextIndex;
        } while (currIndex != rootIndex);
    }

    /// <summary>
    /// Unions the two specified partitions.
    /// </summary>
    /// <param name="firstPartition">The first partition to union.</param>
    /// <param name="secondPartition">The second partition to union.</param>
    /// <param name="unionValue">The value to assign to the unioned partition.</param>
    /// <returns>The partition that the two partitions were unioned into.</returns>
    /// <exception cref="ArgumentException">Thrown when the two partitions are the same.</exception>
    public Partition Union(Partition firstPartition, Partition secondPartition, T unionValue) =>
        UnionCore(firstPartition.RootIndex, secondPartition.RootIndex, unionValue, nameof(secondPartition));

    /// <summary>
    /// Unions the two partitions containing the specified coordinates.
    /// </summary>
    /// <param name="firstCoord">The first coordinate to union.</param>
    /// <param name="secondCoord">The second coordinate to union.</param>
    /// <param name="unionValue">The value to assign to the unioned partition.</param>
    /// <returns>The partition that the two partitions were unioned into.</returns>
    /// <exception cref="ArgumentException">Thrown when the two coordinates are already in the same partition.</exception>
    public Partition Union(Coord firstCoord, Coord secondCoord, T unionValue) =>
        UnionCore(image.IndexFromCoord(firstCoord), image.IndexFromCoord(secondCoord), unionValue, nameof(secondCoord));

    /// <summary>
    /// Unions the two partitions containing the specified indices.
    /// </summary>
    /// <param name="firstIndex">The first index to union.</param>
    /// <param name="secondIndex">The second index to union.</param>
    /// <param name="unionValue">The value to assign to the unioned partition.</param>
    /// <returns>The partition that the two partitions were unioned into.</returns>
    /// <exception cref="ArgumentException">Thrown when the two indices are already in the same partition.</exception>
    public Partition Union(int firstIndex, int secondIndex, T unionValue) =>
        UnionCore(firstIndex, secondIndex, unionValue, nameof(secondIndex));

    private Partition UnionCore(int firstIndex, int secondIndex, T unionValue, string secondParamName)
    {
        int firstSetRoot = Find(firstIndex);
        int secondSetRoot = Find(secondIndex);
        if (firstSetRoot == secondSetRoot)
            throw new ArgumentException("The two elements are already in the same set.", secondParamName);

        return MergeRoots(firstSetRoot, secondSetRoot, unionValue);
    }

    /// <summary>
    /// Attempts to union the sets containing <paramref name="firstIndex"/> and <paramref name="secondIndex"/>.
    /// </summary>
    /// <param name="firstIndex">The linear index of an element in the first set.</param>
    /// <param name="secondIndex">The linear index of an element in the second set.</param>
    /// <param name="unionValue">The value to assign to the merged set.</param>
    /// <param name="mergedPartition">When this method returns, contains the merged partition, or the existing partition if both elements already belong to the same set.</param>
    /// <returns><see langword="true"/> if two distinct sets were merged; <see langword="false"/> if both indices already belonged to the same set.</returns>
    public bool TryUnion(int firstIndex, int secondIndex, T unionValue, out Partition mergedPartition)
    {
        int firstSetRoot = Find(firstIndex);
        int secondSetRoot = Find(secondIndex);
        if (firstSetRoot == secondSetRoot)
        {
            mergedPartition = GetPartition(firstSetRoot);
            return false;
        }

        mergedPartition = MergeRoots(firstSetRoot, secondSetRoot, unionValue);
        return true;
    }

    private Partition MergeRoots(int firstSetRoot, int secondSetRoot, T unionValue)
    {
        if (elements[firstSetRoot].Size < elements[secondSetRoot].Size)
        {
            (firstSetRoot, secondSetRoot) = (secondSetRoot, firstSetRoot);
        }

        elements[secondSetRoot].ParentIndex = firstSetRoot;
        elements[firstSetRoot].Size += elements[secondSetRoot].Size;
        (elements[firstSetRoot].NextIndex, elements[secondSetRoot].NextIndex) = (elements[secondSetRoot].NextIndex, elements[firstSetRoot].NextIndex);
        elements[firstSetRoot].Value = unionValue;
        return GetPartition(firstSetRoot);
    }

    /// <inheritdoc/>
    public Matrix<LongPixel> ToMatrix()
    {
        var matrix = new SequentialMatrix<LongPixel>(image.Size);
        for (int pixelIndex = 0; pixelIndex < image.Count; pixelIndex++)
        {
            matrix[pixelIndex] = FindRootReadOnly(pixelIndex);
        }
        return matrix;
    }

    /// <inheritdoc/>
    public IImmutableSet<Region<Coord>> ToRegions()
    {
        var components = new Dictionary<int, Region<Coord>>();
        for (int pixelIndex = 0; pixelIndex < image.Count; pixelIndex++)
        {
            int root = FindRootReadOnly(pixelIndex);

            if (!components.TryGetValue(root, out var region))
            {
                region = new Region<Coord>()
                {
                    Id = root
                };
                components[root] = region;
            }
            region.Pixels.Add(image.CoordFromIndex(pixelIndex));
        }

        return components.Values.ToImmutableHashSet();
    }

    /// <summary>
    /// Finds the root partition index for the specified element index, applying path compression.
    /// </summary>
    /// <param name="index">The index of the element whose root is to be found.</param>
    /// <returns>The root index of the partition containing <paramref name="index"/>.</returns>
    private int Find(int index)
    {
        int root = FindRootReadOnly(index);

        int current = index;
        while (current != root)
        {
            int next = elements[current].ParentIndex;
            elements[current].ParentIndex = root;
            current = next;
        }

        return root;
    }

    private int FindRootReadOnly(int index)
    {
        int current = index;
        while (elements[current].ParentIndex != current)
        {
            current = elements[current].ParentIndex;
        }

        return current;
    }

    /// <inheritdoc/>
    public LongPixel GetElement(int index) => FindRootReadOnly(index);

    /// <inheritdoc/>
    public LongPixel GetElement(int x, int y) => FindRootReadOnly(image.IndexFromCoord(new Coord(x, y)));

    /// <summary>
    /// Converts a linear index into the corresponding 2D matrix coordinate.
    /// </summary>
    /// <param name="index">The zero-based linear index.</param>
    /// <returns>The 2D coordinate corresponding to <paramref name="index"/>.</returns>
    public Coord CoordFromIndex(int index) => image.CoordFromIndex(index);

    /// <summary>
    /// Converts a 2D matrix coordinate into the corresponding linear index.
    /// </summary>
    /// <param name="coord">The 2D coordinate in the matrix.</param>
    /// <returns>The zero-based linear index corresponding to <paramref name="coord"/>.</returns>
    public int IndexFromCoord(Coord coord) => image.IndexFromCoord(coord);

    /// <inheritdoc/>
    public IEnumerator<LongPixel> GetEnumerator()
    {
        for (int pixelIndex = 0; pixelIndex < image.Count; pixelIndex++)
        {
            yield return FindRootReadOnly(pixelIndex);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>
/// Factory methods for creating <see cref="DisjointSet{T}"/> instances.
/// </summary>
public static class DisjointSet
{
    /// <summary>
    /// Creates a <see cref="DisjointSet{T}"/> initialized with singleton byte partitions (value <c>0</c>) for each element in <paramref name="matrix"/>.
    /// </summary>
    /// <param name="matrix">The matrix whose dimensions and element count define the disjoint sets.</param>
    /// <returns>A new <see cref="DisjointSet{T}"/> of <see cref="byte"/> containing singleton partitions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <see langword="null"/>.</exception>
    public static DisjointSet<byte> CreateSingletons(IUntypedMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return new DisjointSet<byte>(matrix, static _ => 0);
    }
}
