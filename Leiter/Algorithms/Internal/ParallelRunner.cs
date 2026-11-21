namespace Leiter.Algorithms.Internal;

using System;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Core;

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

    /// <summary>
    /// Partitions the rows of <paramref name="size"/> into horizontal <c>[startRow, endRow)</c> strips,
    /// parallelizing only when both total pixel count and row count meet their respective thresholds.
    /// </summary>
    public static void ForRows(
        Size size,
        CancellationToken cancellationToken,
        Action<int, int> body,
        int minParallelPixels = DefaultMinParallelCount,
        int minHeight = 8)
    {
        Guard.ThrowIfNegative(size);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentOutOfRangeException.ThrowIfLessThan(minParallelPixels, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minHeight, 2);
        cancellationToken.ThrowIfCancellationRequested();

        if (size.IsEmpty)
            return;

        int minRowParallel = size.Area >= minParallelPixels && size.Height >= minHeight ? 2 : size.Height + 1;
        ForChunks(size.Height, cancellationToken, body, minParallelCount: minRowParallel);
    }

    /// <summary>
    /// Partitions the rows of <paramref name="size"/> into horizontal <c>[startRow, endRow)</c> strips in parallel
    /// and returns one result per strip in deterministic top-to-bottom order.
    /// </summary>
    public static TResult[] MapRows<TResult>(
        Size size,
        CancellationToken cancellationToken,
        Func<int, int, TResult> mapStrip,
        int minParallelPixels = DefaultMinParallelCount,
        int minHeight = 8)
    {
        Guard.ThrowIfNegative(size);
        ArgumentNullException.ThrowIfNull(mapStrip);
        ArgumentOutOfRangeException.ThrowIfLessThan(minParallelPixels, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(minHeight, 2);
        cancellationToken.ThrowIfCancellationRequested();

        if (size.IsEmpty)
            return [];

        int minRowParallel = size.Area >= minParallelPixels && size.Height >= minHeight ? 2 : size.Height + 1;
        return MapChunks(size.Height, cancellationToken, mapStrip, minParallelCount: minRowParallel);
    }

    /// <summary>
    /// Converts per-chunk bucket histograms into exclusive in-place scatter write cursors in transposed
    /// <c>(bucket, chunk)</c> order and returns the global <c>bucketOffsets</c> array of length <paramref name="bucketCount"/> + 1.
    /// </summary>
    public static int[] PrefixSumTransposedBuckets(int[][] chunkCounts, int bucketCount)
    {
        ArgumentNullException.ThrowIfNull(chunkCounts);
        ArgumentOutOfRangeException.ThrowIfNegative(bucketCount);

        int chunkCount = chunkCounts.Length;
        for (int c = 0; c < chunkCount; c++)
        {
            ArgumentNullException.ThrowIfNull(chunkCounts[c]);
            if (chunkCounts[c].Length < bucketCount)
                throw new ArgumentException($"Chunk histogram {c} must contain at least {bucketCount} buckets.", nameof(chunkCounts));
        }

        var bucketOffsets = new int[bucketCount + 1];
        int runningTotal = 0;
        for (int bucket = 0; bucket < bucketCount; bucket++)
        {
            bucketOffsets[bucket] = runningTotal;
            for (int c = 0; c < chunkCount; c++)
            {
                int count = chunkCounts[c][bucket];
                chunkCounts[c][bucket] = runningTotal;
                runningTotal += count;
            }
        }

        bucketOffsets[bucketCount] = runningTotal;
        return bucketOffsets;
    }

    /// <summary>
    /// Converts flat row-major per-chunk bucket histograms into exclusive in-place scatter write cursors in
    /// transposed <c>(bucket, chunk)</c> order and returns the global <c>bucketOffsets</c> array of length <paramref name="bucketCount"/> + 1.
    /// </summary>
    public static int[] PrefixSumTransposedBuckets(int[] chunkBucketCounts, int chunkCount, int bucketCount)
    {
        ArgumentNullException.ThrowIfNull(chunkBucketCounts);
        ArgumentOutOfRangeException.ThrowIfNegative(chunkCount);
        ArgumentOutOfRangeException.ThrowIfNegative(bucketCount);

        if ((long)chunkCount * bucketCount > chunkBucketCounts.Length)
            throw new ArgumentException("Flat bucket count buffer is smaller than chunkCount * bucketCount.", nameof(chunkBucketCounts));

        var bucketOffsets = new int[bucketCount + 1];
        int runningTotal = 0;
        for (int bucket = 0; bucket < bucketCount; bucket++)
        {
            bucketOffsets[bucket] = runningTotal;
            for (int c = 0; c < chunkCount; c++)
            {
                int index = (c * bucketCount) + bucket;
                int count = chunkBucketCounts[index];
                chunkBucketCounts[index] = runningTotal;
                runningTotal += count;
            }
        }

        bucketOffsets[bucketCount] = runningTotal;
        return bucketOffsets;
    }

    internal static int ResolveChunkCount(int totalCount) =>
        Math.Min(totalCount, Math.Clamp(Environment.ProcessorCount * 4, 2, 128));

    internal static (int Start, int End) GetChunkBounds(int chunkIndex, int chunkCount, int totalCount)
    {
        int start = (int)((long)chunkIndex * totalCount / chunkCount);
        int end = (int)((long)(chunkIndex + 1) * totalCount / chunkCount);
        return (start, end);
    }
}
