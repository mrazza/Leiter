namespace Leiter.Tests.Algorithms.Internal;

using System;
using System.Threading;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="ParallelRunner"/>.
/// </summary>
public class ParallelRunnerTests
{
    [Fact]
    public void For_And_ForChunks_ShouldVisitAllIndicesAndValidateArguments()
    {
        var visited = new int[512];
        ParallelRunner.For(visited.Length, CancellationToken.None, i => visited[i] = i + 1);
        for (int i = 0; i < visited.Length; i++)
            Assert.Equal(i + 1, visited[i]);

        var chunkVisited = new int[1000];
        ParallelRunner.ForChunks(
            chunkVisited.Length,
            CancellationToken.None,
            (start, end) =>
            {
                for (int i = start; i < end; i++)
                    chunkVisited[i] = i + 2;
            },
            minParallelCount: 64);

        for (int i = 0; i < chunkVisited.Length; i++)
            Assert.Equal(i + 2, chunkVisited[i]);

        ParallelRunner.For(0, CancellationToken.None, _ => Assert.Fail("Should not invoke on 0 count."));
        ParallelRunner.ForChunks(0, CancellationToken.None, (_, _) => Assert.Fail("Should not invoke on 0 count."));
        Assert.Empty(ParallelRunner.MapChunks(0, CancellationToken.None, (_, _) => 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.For(-1, CancellationToken.None, _ => { }));
        Assert.Throws<ArgumentNullException>(() => ParallelRunner.For(1, CancellationToken.None, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.ForChunks(-1, CancellationToken.None, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => ParallelRunner.ForChunks(1, CancellationToken.None, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.ForChunks(1, CancellationToken.None, (_, _) => { }, minParallelCount: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.MapChunks(-1, CancellationToken.None, (_, _) => 0));
        Assert.Throws<ArgumentNullException>(() => ParallelRunner.MapChunks<int>(1, CancellationToken.None, null!));
    }

    [Fact]
    public void MapChunks_ShouldReturnOrderedChunkResultsAndHonorCancellation()
    {
        int[] partialSums = ParallelRunner.MapChunks(
            2000,
            CancellationToken.None,
            (start, end) =>
            {
                int sum = 0;
                for (int i = start; i < end; i++)
                    sum += i;
                return sum;
            },
            minParallelCount: 64);

        int total = 0;
        for (int i = 0; i < partialSums.Length; i++)
            total += partialSums[i];
        Assert.Equal(1999 * 2000 / 2, total);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => ParallelRunner.For(10, cts.Token, _ => { }));
        Assert.Throws<OperationCanceledException>(() => ParallelRunner.ForChunks(10, cts.Token, (_, _) => { }));
        Assert.Throws<OperationCanceledException>(() => ParallelRunner.MapChunks(10, cts.Token, (_, _) => 0));
    }

    [Fact]
    public void ForRows_AndMapRows_ShouldCoverAllRowsAndValidateArguments()
    {
        ParallelRunner.ForRows(new Size(0, 5), CancellationToken.None, (_, _) => Assert.Fail("Should not invoke on empty width."));
        ParallelRunner.ForRows(new Size(5, 0), CancellationToken.None, (_, _) => Assert.Fail("Should not invoke on empty height."));
        Assert.Empty(ParallelRunner.MapRows(new Size(0, 5), CancellationToken.None, (_, _) => 1));

        // Below minHeight falls back to single strip; above thresholds visits every row exactly once.
        Size thinWide = new(10_000, 4);
        int thinStrips = 0;
        ParallelRunner.ForRows(thinWide, CancellationToken.None, (start, end) =>
        {
            Assert.Equal(0, start);
            Assert.Equal(4, end);
            Interlocked.Increment(ref thinStrips);
        }, minParallelPixels: 100, minHeight: 8);
        Assert.Equal(1, thinStrips);

        Size grid = new(20, 64);
        var rowHits = new int[grid.Height];
        ParallelRunner.ForRows(
            grid,
            CancellationToken.None,
            (startRow, endRow) =>
            {
                for (int y = startRow; y < endRow; y++)
                    Interlocked.Increment(ref rowHits[y]);
            },
            minParallelPixels: 128,
            minHeight: 8);

        for (int y = 0; y < grid.Height; y++)
            Assert.Equal(1, rowHits[y]);

        int[] rowSums = ParallelRunner.MapRows(
            grid,
            CancellationToken.None,
            (startRow, endRow) =>
            {
                int sum = 0;
                for (int y = startRow; y < endRow; y++)
                    sum += y;
                return sum;
            },
            minParallelPixels: 128,
            minHeight: 8);

        int total = 0;
        for (int i = 0; i < rowSums.Length; i++)
            total += rowSums[i];
        Assert.Equal(grid.Height * (grid.Height - 1) / 2, total);

        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.ForRows(new Size(-1, 4), CancellationToken.None, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => ParallelRunner.ForRows(grid, CancellationToken.None, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.ForRows(grid, CancellationToken.None, (_, _) => { }, minParallelPixels: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.ForRows(grid, CancellationToken.None, (_, _) => { }, minHeight: 1));
        Assert.Throws<ArgumentNullException>(() => ParallelRunner.MapRows<int>(grid, CancellationToken.None, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.MapRows(grid, CancellationToken.None, (_, _) => 0, minParallelPixels: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.MapRows(grid, CancellationToken.None, (_, _) => 0, minHeight: 1));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Throws<OperationCanceledException>(() => ParallelRunner.ForRows(grid, cts.Token, (_, _) => { }));
        Assert.Throws<OperationCanceledException>(() => ParallelRunner.MapRows(grid, cts.Token, (_, _) => 0));
    }

    [Fact]
    public void ChunkBounds_AndPrefixSumTransposedBuckets_ShouldBeExactAndDeterministic()
    {
        int totalCount = 1003;
        int chunkCount = ParallelRunner.ResolveChunkCount(totalCount);
        Assert.InRange(chunkCount, 2, 128);

        int expectedStart = 0;
        for (int c = 0; c < chunkCount; c++)
        {
            (int start, int end) = ParallelRunner.GetChunkBounds(c, chunkCount, totalCount);
            Assert.Equal(expectedStart, start);
            Assert.True(end > start);
            expectedStart = end;
        }
        Assert.Equal(totalCount, expectedStart);

        // Jagged overload: 3 chunks x 4 buckets
        int[][] chunkCounts =
        [
            [2, 0, 1, 3],
            [1, 4, 0, 2],
            [3, 1, 2, 0],
        ];
        int[] bucketOffsets = ParallelRunner.PrefixSumTransposedBuckets(chunkCounts, 4);
        Assert.Equal([0, 6, 11, 14, 19], bucketOffsets);
        Assert.Equal([0, 6, 11, 14], chunkCounts[0]);
        Assert.Equal([2, 6, 12, 17], chunkCounts[1]);
        Assert.Equal([3, 10, 12, 19], chunkCounts[2]);

        // Flat overload: 3 chunks x 4 buckets
        int[] flatCounts =
        [
            2, 0, 1, 3,
            1, 4, 0, 2,
            3, 1, 2, 0,
        ];
        int[] flatBucketOffsets = ParallelRunner.PrefixSumTransposedBuckets(flatCounts, 3, 4);
        Assert.Equal(bucketOffsets, flatBucketOffsets);
        Assert.Equal([0, 6, 11, 14, 2, 6, 12, 17, 3, 10, 12, 19], flatCounts);

        Assert.Throws<ArgumentNullException>(() => ParallelRunner.PrefixSumTransposedBuckets((int[][])null!, 2));
        Assert.Throws<ArgumentNullException>(() => ParallelRunner.PrefixSumTransposedBuckets((int[])null!, 1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.PrefixSumTransposedBuckets(new int[1][], -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.PrefixSumTransposedBuckets([], -1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ParallelRunner.PrefixSumTransposedBuckets([], 2, -1));
        Assert.Throws<ArgumentException>(() => ParallelRunner.PrefixSumTransposedBuckets([[1]], 2));
        Assert.Throws<ArgumentException>(() => ParallelRunner.PrefixSumTransposedBuckets(new int[3], 2, 2));
    }
}
