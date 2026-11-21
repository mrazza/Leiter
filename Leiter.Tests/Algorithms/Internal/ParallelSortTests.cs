namespace Leiter.Tests.Algorithms.Internal;

using System;
using System.Collections.Generic;
using System.Threading;
using Leiter.Algorithms.Internal;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="ParallelSort"/>.
/// </summary>
public class ParallelSortTests
{
    private readonly struct IntComparer : IComparer<int>
    {
        public int Compare(int x, int y) => x.CompareTo(y);
    }

    private readonly struct EdgeLike(double weight, ulong tieBreaker)
    {
        public readonly double Weight = weight;
        public readonly ulong TieBreaker = tieBreaker;
    }

    private readonly struct EdgeLikeComparer : IComparer<EdgeLike>
    {
        public int Compare(EdgeLike x, EdgeLike y)
        {
            int cmp = x.Weight.CompareTo(y.Weight);
            return cmp != 0 ? cmp : x.TieBreaker.CompareTo(y.TieBreaker);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(200_000)]
    public void SortAndReturn_RandomInputs_ShouldMatchArraySortExactly(int count)
    {
        var rng = new Random(12345 + count);
        var source = new EdgeLike[count];
        for (int i = 0; i < count; i++)
        {
            source[i] = new EdgeLike(rng.NextDouble(), (ulong)i);
        }

        var expected = (EdgeLike[])source.Clone();
        Array.Sort(expected, (a, b) => new EdgeLikeComparer().Compare(a, b));

        EdgeLike[] actual = ParallelSort.SortAndReturn(source, new EdgeLikeComparer());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SortAndReturn_AdversarialDistributions_ShouldSortExactly()
    {
        // Reverse-sorted above threshold
        const int count = 150_000;
        var reversed = new int[count];
        for (int i = 0; i < count; i++)
            reversed[i] = count - i;

        int[] sortedReversed = ParallelSort.SortAndReturn(reversed, new IntComparer());
        for (int i = 0; i < count; i++)
            Assert.Equal(i + 1, sortedReversed[i]);

        // All-equal input
        var allEqual = new int[count];
        Array.Fill(allEqual, 42);
        int[] sortedEqual = ParallelSort.SortAndReturn(allEqual, new IntComparer());
        Assert.All(sortedEqual, v => Assert.Equal(42, v));

        // Heavy duplicates (skewed distribution)
        var rng = new Random(99);
        var skewed = new int[count];
        for (int i = 0; i < count; i++)
            skewed[i] = i % 5 == 0 ? 7 : rng.Next(0, 50);

        var expectedSkewed = (int[])skewed.Clone();
        Array.Sort(expectedSkewed);
        int[] actualSkewed = ParallelSort.SortAndReturn(skewed, new IntComparer());
        Assert.Equal(expectedSkewed, actualSkewed);
    }

    [Fact]
    public void SortAndReturn_ValidationAndCancellation_ShouldBehaveCorrectly()
    {
        Assert.Throws<ArgumentNullException>(() => ParallelSort.SortAndReturn<int, IntComparer>(null!, new IntComparer()));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelSort.SortAndReturn([1, 2], new IntComparer(), parallelThreshold: 0));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            ParallelSort.SortAndReturn(new int[10], new IntComparer(), cts.Token));
    }
}
