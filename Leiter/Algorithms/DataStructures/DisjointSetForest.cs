namespace Leiter.Algorithms.DataStructures;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Leiter.Algorithms.Internal;

/// <summary>
/// Compact union-by-size disjoint-set forest backed by a single negative-size parent array (4 bytes/element).
/// </summary>
/// <remarks>
/// Root nodes store the negated component size (<c>-size &lt; 0</c>); non-root nodes store their non-negative
/// parent index (<c>parent &gt;= 0</c>). Equal-size merges keep the first root as the surviving representative.
/// </remarks>
public sealed class DisjointSetForest
{
    private readonly int[] parents;

    /// <summary>
    /// Initializes a new <see cref="DisjointSetForest"/> with <paramref name="count"/> singleton sets (<c>0</c> through <c>count - 1</c>).
    /// </summary>
    /// <param name="count">The non-negative number of elements in the forest.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    public DisjointSetForest(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        parents = new int[count];
        ParallelRunner.ForChunks(
            count,
            CancellationToken.None,
            (start, end) => Array.Fill(parents, -1, start, end - start));
    }

    /// <summary>
    /// Gets the total number of elements managed by this forest.
    /// </summary>
    public int Count => parents.Length;

    /// <summary>
    /// Determines whether <paramref name="index"/> is currently the root representative of its set.
    /// </summary>
    /// <param name="index">The zero-based element index in <c>[0, Count)</c>.</param>
    /// <returns><see langword="true"/> if <paramref name="index"/> is a root; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    public bool IsRoot(int index)
    {
        ValidateIndex(index);
        return IsRootUnchecked(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsRootUnchecked(int index)
    {
        Debug.Assert((uint)index < (uint)parents.Length);
        return parents[index] < 0;
    }

    /// <summary>
    /// Gets the number of elements in the set represented by <paramref name="rootIndex"/>.
    /// </summary>
    /// <param name="rootIndex">The zero-based index of a root element in <c>[0, Count)</c>.</param>
    /// <returns>The positive size of the set rooted at <paramref name="rootIndex"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rootIndex"/> is outside <c>[0, Count)</c>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="rootIndex"/> is not currently a set root.</exception>
    public int GetRootSize(int rootIndex)
    {
        ValidateIndex(rootIndex);
        int value = parents[rootIndex];
        if (value >= 0)
            throw new ArgumentException($"Index {rootIndex} is not a root.", nameof(rootIndex));

        return -value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetRootSizeUnchecked(int rootIndex)
    {
        Debug.Assert((uint)rootIndex < (uint)parents.Length);
        Debug.Assert(parents[rootIndex] < 0);
        return -parents[rootIndex];
    }

    /// <summary>
    /// Gets the number of elements in the set containing <paramref name="index"/> without performing path compression.
    /// </summary>
    /// <param name="index">The zero-based element index in <c>[0, Count)</c>.</param>
    /// <returns>The positive size of the set containing <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    public int GetSize(int index)
    {
        ValidateIndex(index);
        return -parents[FindReadOnlyUnchecked(index)];
    }

    /// <summary>
    /// Finds the root representative of the set containing <paramref name="index"/> without mutating parent links.
    /// </summary>
    /// <remarks>
    /// Suitable for concurrent read-only queries after all unions have completed.
    /// </remarks>
    /// <param name="index">The zero-based element index in <c>[0, Count)</c>.</param>
    /// <returns>The root representative index for the set containing <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    public int FindReadOnly(int index)
    {
        ValidateIndex(index);
        return FindReadOnlyUnchecked(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FindReadOnlyUnchecked(int index)
    {
        Debug.Assert((uint)index < (uint)parents.Length);
        int current = index;
        while (parents[current] >= 0)
            current = parents[current];

        return current;
    }

    /// <summary>
    /// Finds the root representative of the set containing <paramref name="index"/>, applying path compression.
    /// </summary>
    /// <param name="index">The zero-based element index in <c>[0, Count)</c>.</param>
    /// <returns>The root representative index for the set containing <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is outside <c>[0, Count)</c>.</exception>
    public int Find(int index)
    {
        ValidateIndex(index);
        return FindUnchecked(index);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int FindUnchecked(int index)
    {
        int root = FindReadOnlyUnchecked(index);
        int current = index;
        while (current != root)
        {
            int next = parents[current];
            parents[current] = root;
            current = next;
        }

        return root;
    }

    /// <summary>
    /// Merges the sets containing <paramref name="first"/> and <paramref name="second"/> by size and returns the surviving root representative.
    /// </summary>
    /// <remarks>
    /// If <paramref name="first"/> and <paramref name="second"/> already belong to the same set, returns their existing root without modification.
    /// When merging distinct sets of equal size, the root of <paramref name="first"/> survives.
    /// </remarks>
    /// <param name="first">A zero-based index in the first set.</param>
    /// <param name="second">A zero-based index in the second set.</param>
    /// <returns>The surviving root representative index after merging.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="first"/> or <paramref name="second"/> is outside <c>[0, Count)</c>.</exception>
    public int Union(int first, int second)
    {
        ValidateIndex(first);
        ValidateIndex(second);
        return UnionUnchecked(first, second);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int UnionUnchecked(int first, int second)
    {
        int rootFirst = FindUnchecked(first);
        int rootSecond = FindUnchecked(second);
        if (rootFirst == rootSecond)
            return rootFirst;

        return MergeRootsUnchecked(rootFirst, rootSecond, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int MergeRootsUnchecked(int firstRoot, int secondRoot, out int absorbedRoot)
    {
        Debug.Assert((uint)firstRoot < (uint)parents.Length);
        Debug.Assert((uint)secondRoot < (uint)parents.Length);
        Debug.Assert(firstRoot != secondRoot);
        Debug.Assert(parents[firstRoot] < 0);
        Debug.Assert(parents[secondRoot] < 0);

        // Negative values store -size, so a strictly larger signed integer indicates a strictly smaller set.
        if (parents[firstRoot] > parents[secondRoot])
            (firstRoot, secondRoot) = (secondRoot, firstRoot);

        parents[firstRoot] += parents[secondRoot];
        parents[secondRoot] = firstRoot;
        absorbedRoot = secondRoot;
        return firstRoot;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ValidateIndex(int index, [CallerArgumentExpression(nameof(index))] string? paramName = null)
    {
        if ((uint)index >= (uint)parents.Length)
            throw new ArgumentOutOfRangeException(paramName, index, $"Index {index} must be within [0, {parents.Length}).");
    }
}
