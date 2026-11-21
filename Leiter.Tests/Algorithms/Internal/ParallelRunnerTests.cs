namespace Leiter.Tests.Algorithms.Internal;

using System;
using System.Threading;
using Leiter.Algorithms.Internal;
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
}
