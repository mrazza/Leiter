namespace Leiter.Tests.Algorithms.DataStructures;

using System;
using Leiter.Algorithms.DataStructures;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="DisjointSetForest"/>.
/// </summary>
public class DisjointSetForestTests
{
    [Fact]
    public void Constructor_ShouldValidateCountAndInitializeParallelSingletons()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DisjointSetForest(-1));

        var empty = new DisjointSetForest(0);
        Assert.Equal(0, empty.Count);

        var large = new DisjointSetForest(70_000);
        Assert.Equal(70_000, large.Count);

        int[] sampleIndices = [0, 1, 32_768, 65_535, 69_999];
        foreach (int index in sampleIndices)
        {
            Assert.True(large.IsRoot(index));
            Assert.Equal(1, large.GetRootSize(index));
            Assert.Equal(index, large.FindReadOnly(index));
            Assert.Equal(index, large.Find(index));
        }
    }

    [Fact]
    public void Union_AndMergeRootsUnchecked_ShouldPreserveTieBreakingAndPathCompression()
    {
        var forest = new DisjointSetForest(6);

        // Equal size (1 == 1): keeps firstRoot (0), absorbs 1.
        int merged01 = forest.MergeRootsUnchecked(0, 1, out int absorbed01);
        Assert.Equal(0, merged01);
        Assert.Equal(1, absorbed01);
        Assert.True(forest.IsRoot(0));
        Assert.False(forest.IsRoot(1));
        Assert.Equal(2, forest.GetRootSize(0));

        // Unequal size (size[2] == 1 < size[0] == 2): keeps larger root (0), absorbs 2.
        int merged20 = forest.MergeRootsUnchecked(2, 0, out int absorbed20);
        Assert.Equal(0, merged20);
        Assert.Equal(2, absorbed20);
        Assert.Equal(3, forest.GetRootSize(0));

        // Merge {3, 4} -> 3 (size 2), then {3, 5} -> 3 (size 3).
        Assert.Equal(3, forest.Union(3, 4));
        Assert.Equal(3, forest.Union(4, 3)); // Idempotent on same set.
        Assert.Equal(3, forest.Union(4, 5)); // Path compress 4 -> 3, union with 5.
        Assert.Equal(3, forest.GetRootSize(3));

        // Equal size (3 == 3): calling with (3, 0) keeps firstRoot (3).
        int finalRoot = forest.Union(5, 2);
        Assert.Equal(3, finalRoot);
        Assert.Equal(6, forest.GetRootSize(3));

        // Path from 2 was 2 -> 0 -> 3; FindReadOnly reaches 3 without compression, Find compresses 2 -> 3.
        Assert.Equal(3, forest.FindReadOnly(2));
        Assert.Equal(3, forest.Find(2));
        for (int i = 0; i < forest.Count; i++)
            Assert.Equal(3, forest.FindReadOnly(i));
    }

    [Fact]
    public void GetSize_ShouldReturnComponentSizeWithoutPathCompression()
    {
        var forest = new DisjointSetForest(5);
        forest.MergeRootsUnchecked(0, 1, out _); // root 0, size 2
        forest.MergeRootsUnchecked(2, 0, out _); // 2 -> 0, root 0, size 3
        forest.Union(3, 4); // root 3, size 2

        Assert.Equal(3, forest.GetSize(0));
        Assert.Equal(3, forest.GetSize(1));
        Assert.Equal(3, forest.GetSize(2));
        Assert.Equal(2, forest.GetSize(3));
        Assert.Equal(2, forest.GetSize(4));

        // After merging {0,1,2} with {3,4}, all members report size 5.
        int mergedRoot = forest.Union(2, 4);
        for (int i = 0; i < forest.Count; i++)
        {
            Assert.Equal(5, forest.GetSize(i));
            Assert.Equal(i == mergedRoot, forest.IsRoot(i));
        }
    }

    [Fact]
    public void Validation_InvalidIndexOrNonRoot_ShouldThrowExpectedExceptions()
    {
        var forest = new DisjointSetForest(3);
        int survivingRoot = forest.Union(0, 1);
        int nonRoot = survivingRoot == 0 ? 1 : 0;

        Assert.Throws<ArgumentException>("rootIndex", () => forest.GetRootSize(nonRoot));

        int[] invalidIndices = [-1, 3, int.MinValue, int.MaxValue];
        foreach (int bad in invalidIndices)
        {
            Assert.Throws<ArgumentOutOfRangeException>("index", () => forest.IsRoot(bad));
            Assert.Throws<ArgumentOutOfRangeException>("rootIndex", () => forest.GetRootSize(bad));
            Assert.Throws<ArgumentOutOfRangeException>("index", () => forest.GetSize(bad));
            Assert.Throws<ArgumentOutOfRangeException>("index", () => forest.FindReadOnly(bad));
            Assert.Throws<ArgumentOutOfRangeException>("index", () => forest.Find(bad));
            Assert.Throws<ArgumentOutOfRangeException>("first", () => forest.Union(bad, 0));
            Assert.Throws<ArgumentOutOfRangeException>("second", () => forest.Union(0, bad));
        }
    }
}
