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
internal sealed class DisjointSetForest
{
    private readonly int[] parents;

    public DisjointSetForest(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        parents = new int[count];
        ParallelRunner.ForChunks(
            count,
            CancellationToken.None,
            (start, end) => Array.Fill(parents, -1, start, end - start));
    }

    public int Count => parents.Length;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsRoot(int index)
    {
        Debug.Assert((uint)index < (uint)parents.Length);
        return parents[index] < 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetRootSize(int rootIndex)
    {
        Debug.Assert((uint)rootIndex < (uint)parents.Length);
        Debug.Assert(parents[rootIndex] < 0);
        return -parents[rootIndex];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int FindReadOnly(int index)
    {
        Debug.Assert((uint)index < (uint)parents.Length);
        int current = index;
        while (parents[current] >= 0)
            current = parents[current];

        return current;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Find(int index)
    {
        int root = FindReadOnly(index);
        int current = index;
        while (current != root)
        {
            int next = parents[current];
            parents[current] = root;
            current = next;
        }

        return root;
    }

    public int Union(int first, int second)
    {
        int rootFirst = Find(first);
        int rootSecond = Find(second);
        if (rootFirst == rootSecond)
            return rootFirst;

        return MergeRootsUnchecked(rootFirst, rootSecond, out _);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int MergeRootsUnchecked(int firstRoot, int secondRoot, out int absorbedRoot)
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
}
