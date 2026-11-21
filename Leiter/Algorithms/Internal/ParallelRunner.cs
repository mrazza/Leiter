namespace Leiter.Algorithms.Internal;

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Provides shared cancellation-aware parallel loops and deterministic chunked reductions.
/// </summary>
internal static class ParallelRunner
{
    public const int DefaultMinParallelCount = 65_536;

    /// <summary>
    /// Executes <paramref name="body"/> in parallel for indices in <c>[0, toExclusive)</c>, honoring <paramref name="cancellationToken"/>.
    /// </summary>
    public static void For(int toExclusive, CancellationToken cancellationToken, Action<int> body)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(toExclusive);
        ArgumentNullException.ThrowIfNull(body);
        cancellationToken.ThrowIfCancellationRequested();

        if (toExclusive == 0)
            return;

        if (toExclusive == 1)
        {
            body(0);
            return;
        }

        if (cancellationToken.CanBeCanceled)
            Parallel.For(0, toExclusive, new ParallelOptions { CancellationToken = cancellationToken }, body);
        else
            Parallel.For(0, toExclusive, body);
    }

    /// <summary>
    /// Partitions <c>[0, totalCount)</c> into contiguous <c>[start, end)</c> chunks and invokes <paramref name="body"/> for each chunk.
    /// </summary>
    public static void ForChunks(
        int totalCount,
        CancellationToken cancellationToken,
        Action<int, int> body,
        int minParallelCount = DefaultMinParallelCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentOutOfRangeException.ThrowIfLessThan(minParallelCount, 1);
        cancellationToken.ThrowIfCancellationRequested();

        if (totalCount == 0)
            return;

        if (totalCount < minParallelCount || Environment.ProcessorCount <= 1)
        {
            body(0, totalCount);
            return;
        }

        int chunkCount = ResolveChunkCount(totalCount);
        For(chunkCount, cancellationToken, chunkIndex =>
        {
            (int start, int end) = GetChunkBounds(chunkIndex, chunkCount, totalCount);
            body(start, end);
        });
    }

    /// <summary>
    /// Partitions <c>[0, totalCount)</c> into contiguous chunks in parallel and returns one result per chunk in deterministic chunk order.
    /// </summary>
    public static TResult[] MapChunks<TResult>(
        int totalCount,
        CancellationToken cancellationToken,
        Func<int, int, TResult> mapChunk,
        int minParallelCount = DefaultMinParallelCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        ArgumentNullException.ThrowIfNull(mapChunk);
        ArgumentOutOfRangeException.ThrowIfLessThan(minParallelCount, 1);
        cancellationToken.ThrowIfCancellationRequested();

        if (totalCount == 0)
            return [];

        if (totalCount < minParallelCount || Environment.ProcessorCount <= 1)
            return [mapChunk(0, totalCount)];

        int chunkCount = ResolveChunkCount(totalCount);
        var results = new TResult[chunkCount];
        For(chunkCount, cancellationToken, chunkIndex =>
        {
            (int start, int end) = GetChunkBounds(chunkIndex, chunkCount, totalCount);
            results[chunkIndex] = mapChunk(start, end);
        });

        return results;
    }

    private static int ResolveChunkCount(int totalCount) =>
        Math.Min(totalCount, Math.Clamp(Environment.ProcessorCount * 4, 2, 128));

    private static (int Start, int End) GetChunkBounds(int chunkIndex, int chunkCount, int totalCount)
    {
        int start = (int)((long)chunkIndex * totalCount / chunkCount);
        int end = (int)((long)(chunkIndex + 1) * totalCount / chunkCount);
        return (start, end);
    }
}
