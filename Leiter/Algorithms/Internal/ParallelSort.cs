namespace Leiter.Algorithms.Internal;

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

/// <summary>
/// Provides deterministic parallel sample sort for large struct arrays using devirtualized generic comparers.
/// </summary>
internal static class ParallelSort
{
    public const int DefaultParallelThreshold = 65_536;

    /// <summary>
    /// Sorts <paramref name="source"/> according to <paramref name="comparer"/> and returns the sorted array,
    /// using parallel sample sort above <paramref name="parallelThreshold"/>.
    /// </summary>
    public static T[] SortAndReturn<T, TComparer>(
        T[] source,
        TComparer comparer,
        CancellationToken cancellationToken = default,
        int parallelThreshold = DefaultParallelThreshold)
        where TComparer : struct, IComparer<T>
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(parallelThreshold, 1);
        cancellationToken.ThrowIfCancellationRequested();

        int length = source.Length;
        if (length <= 1)
            return source;

        if (length < parallelThreshold || Environment.ProcessorCount <= 1)
        {
            source.AsSpan().Sort(comparer);
            return source;
        }

        int workerCount = Math.Min(length, Math.Clamp(Environment.ProcessorCount, 2, 64));
        int bucketCount = Math.Clamp(workerCount * 4, 8, 256);
        T[] splitters = SelectSplitters(source, comparer, bucketCount);

        var workerBucketCounts = new int[workerCount * bucketCount];
        ParallelRunner.For(workerCount, cancellationToken, worker =>
        {
            (int start, int end) = GetWorkerSlice(worker, workerCount, length);
            int rowBase = worker * bucketCount;
            for (int i = start; i < end; i++)
            {
                int bucket = FindBucket(source[i], splitters, comparer);
                workerBucketCounts[rowBase + bucket]++;
            }
        });

        cancellationToken.ThrowIfCancellationRequested();

        var workerBucketOffsets = new int[workerCount * bucketCount];
        var bucketOffsets = new int[bucketCount + 1];
        int runningOffset = 0;
        for (int bucket = 0; bucket < bucketCount; bucket++)
        {
            bucketOffsets[bucket] = runningOffset;
            for (int worker = 0; worker < workerCount; worker++)
            {
                int index = (worker * bucketCount) + bucket;
                workerBucketOffsets[index] = runningOffset;
                runningOffset += workerBucketCounts[index];
            }
        }
        bucketOffsets[bucketCount] = runningOffset;

        var destination = new T[length];
        ParallelRunner.For(workerCount, cancellationToken, worker =>
        {
            (int start, int end) = GetWorkerSlice(worker, workerCount, length);
            Span<int> cursors = stackalloc int[bucketCount];
            workerBucketOffsets.AsSpan(worker * bucketCount, bucketCount).CopyTo(cursors);

            for (int i = start; i < end; i++)
            {
                T item = source[i];
                int bucket = FindBucket(item, splitters, comparer);
                destination[cursors[bucket]++] = item;
            }
        });

        cancellationToken.ThrowIfCancellationRequested();

        ParallelRunner.For(bucketCount, cancellationToken, bucket =>
        {
            int start = bucketOffsets[bucket];
            int count = bucketOffsets[bucket + 1] - start;
            if (count > 1)
                destination.AsSpan(start, count).Sort(comparer);
        });

        return destination;
    }

    private static T[] SelectSplitters<T, TComparer>(T[] source, TComparer comparer, int bucketCount)
        where TComparer : struct, IComparer<T>
    {
        int sampleCount = Math.Min(source.Length, bucketCount * 16);
        var samples = new T[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            int sourceIndex = (int)((long)i * source.Length / sampleCount);
            samples[i] = source[sourceIndex];
        }

        samples.AsSpan().Sort(comparer);

        var splitters = new T[bucketCount - 1];
        for (int i = 0; i < splitters.Length; i++)
        {
            int sampleIndex = (int)((long)(i + 1) * sampleCount / bucketCount);
            splitters[i] = samples[sampleIndex];
        }

        return splitters;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FindBucket<T, TComparer>(T item, ReadOnlySpan<T> splitters, TComparer comparer)
        where TComparer : struct, IComparer<T>
    {
        int low = 0;
        int high = splitters.Length;
        while (low < high)
        {
            int mid = low + ((high - low) >> 1);
            if (comparer.Compare(item, splitters[mid]) < 0)
                high = mid;
            else
                low = mid + 1;
        }

        return low;
    }

    private static (int Start, int End) GetWorkerSlice(int worker, int workerCount, int length)
    {
        int start = (int)((long)worker * length / workerCount);
        int end = (int)((long)(worker + 1) * length / workerCount);
        return (start, end);
    }
}
