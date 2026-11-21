namespace Leiter.Algorithms.Quantization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Core;

/// <summary>
/// Provides histogram-based, coverage-truncated palette quantization for pixel matrices.
/// </summary>
public static class PaletteQuantizer
{
    /// <summary>
    /// Quantizes an image using <paramref name="bucketizer"/>, retains the most frequent buckets needed
    /// to cover <paramref name="coverageFraction"/> of pixels, and remaps rare buckets to their nearest
    /// retained bucket using <typeparamref name="TPixel"/> distance.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the image and quantized palette.</typeparam>
    /// <param name="image">The input image matrix to quantize.</param>
    /// <param name="bucketizer">Maps each input pixel to its initial quantization bucket.</param>
    /// <param name="coverageFraction">The cumulative pixel coverage fraction in <c>(0, 1]</c> of frequent buckets to retain.</param>
    /// <returns>The palette-mapped image, truncated histogram, and identity metric lookup table.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="bucketizer"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="coverageFraction"/> is NaN, less than or equal to 0.0, or greater than 1.0.</exception>
    public static QuantizedPalette<TPixel, TPixel> QuantizeAndTruncate<TPixel>(
        IReadOnlyMatrix<TPixel> image,
        Func<TPixel, TPixel> bucketizer,
        double coverageFraction = 0.95)
        where TPixel : struct, IPixel<TPixel>
        => QuantizeAndTruncate(image, bucketizer, static pixel => pixel, coverageFraction);

    /// <summary>
    /// Quantizes an image using <paramref name="bucketizer"/>, retains the most frequent buckets needed
    /// to cover <paramref name="coverageFraction"/> of pixels, and remaps rare buckets to their nearest
    /// retained bucket in the metric space defined by <paramref name="metricSelector"/>.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type of the image and quantized palette.</typeparam>
    /// <typeparam name="TMetric">The metric pixel type used to compute perceptual distances between buckets.</typeparam>
    /// <param name="image">The input image matrix to quantize.</param>
    /// <param name="bucketizer">Maps each input pixel to its initial quantization bucket.</param>
    /// <param name="metricSelector">Projects each unique bucket value into the distance metric space.</param>
    /// <param name="coverageFraction">The cumulative pixel coverage fraction in <c>(0, 1]</c> of frequent buckets to retain.</param>
    /// <returns>The palette-mapped image, truncated histogram, and precomputed metric lookup table.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="bucketizer"/>, or <paramref name="metricSelector"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="coverageFraction"/> is NaN, less than or equal to 0.0, or greater than 1.0.</exception>
    public static QuantizedPalette<TPixel, TMetric> QuantizeAndTruncate<TPixel, TMetric>(
        IReadOnlyMatrix<TPixel> image,
        Func<TPixel, TPixel> bucketizer,
        Func<TPixel, TMetric> metricSelector,
        double coverageFraction = 0.95)
        where TPixel : struct, IPixel<TPixel>
        where TMetric : struct, IPixel<TMetric>
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(bucketizer);
        ArgumentNullException.ThrowIfNull(metricSelector);

        Guard.ThrowIfNotFiniteInHalfOpenRange(coverageFraction, 0.0, 1.0);

        if (image.Count == 0)
        {
            return new QuantizedPalette<TPixel, TMetric>(
                new SequentialMatrix<TPixel>(image.Size),
                new DictionaryHistogram<TPixel>(),
                new Dictionary<TPixel, TMetric>());
        }

        Matrix<TPixel> quantizedInput = image.Map(bucketizer);
        TPixel[] quantizedRaw = quantizedInput is SequentialMatrix<TPixel> sequentialQuantized
            ? sequentialQuantized.RawData
            : quantizedInput.ToArray();

        Dictionary<TPixel, BucketEntry>[] chunkHistograms = ParallelRunner.MapChunks(
            quantizedRaw.Length,
            CancellationToken.None,
            (start, end) =>
            {
                var local = new Dictionary<TPixel, BucketEntry>();
                for (int i = start; i < end; i++)
                {
                    ref BucketEntry entry = ref CollectionsMarshal.GetValueRefOrAddDefault(local, quantizedRaw[i], out bool exists);
                    if (!exists)
                    {
                        entry.Count = 1;
                        entry.FirstSeen = i;
                    }
                    else
                    {
                        entry.Count++;
                    }
                }

                return local;
            });

        var histogram = new DictionaryHistogram<TPixel>();
        var firstSeenOrder = new Dictionary<TPixel, int>();
        for (int c = 0; c < chunkHistograms.Length; c++)
        {
            foreach (var kvp in chunkHistograms[c])
            {
                histogram.Increment(kvp.Key, kvp.Value.Count);
                if (!firstSeenOrder.TryGetValue(kvp.Key, out int existingFirst) || kvp.Value.FirstSeen < existingFirst)
                    firstSeenOrder[kvp.Key] = kvp.Value.FirstSeen;
            }
        }

        Dictionary<TPixel, TMetric> rawMetricLookup = histogram.ToDictionary(
            bucket => bucket.Key,
            bucket => metricSelector(bucket.Key));

        DictionaryHistogram<TPixel> paletteHistogram = new();
        Dictionary<TPixel, TMetric> metricLookup = new();
        Dictionary<TPixel, TPixel> remap = new();
        var retainedBuckets = new List<RetainedBucket<TPixel, TMetric>>();

        int targetPixelCount = (int)Math.Ceiling(image.Count * coverageFraction);
        int pixelCount = 0;
        foreach (var bucket in histogram.OrderByDescending(b => b.Value).ThenBy(b => firstSeenOrder[b.Key]))
        {
            if (pixelCount < targetPixelCount || paletteHistogram.BucketCount == 0)
            {
                TMetric metric = rawMetricLookup[bucket.Key];
                paletteHistogram.Increment(bucket.Key, bucket.Value);
                metricLookup[bucket.Key] = metric;
                retainedBuckets.Add(new RetainedBucket<TPixel, TMetric>(bucket.Key, metric, firstSeenOrder[bucket.Key]));
                pixelCount += bucket.Value;
            }
            else
            {
                TMetric rareMetric = rawMetricLookup[bucket.Key];
                TPixel nearestRetainedColor = FindNearestRetainedBucket(rareMetric, retainedBuckets);
                paletteHistogram.Increment(nearestRetainedColor, bucket.Value);
                remap[bucket.Key] = nearestRetainedColor;
            }
        }

        Matrix<TPixel> paletteImage = remap.Count > 0
            ? quantizedInput.Map(c => remap.TryGetValue(c, out var mapped) ? mapped : c)
            : quantizedInput;

        return new QuantizedPalette<TPixel, TMetric>(paletteImage, paletteHistogram, metricLookup);
    }

    private struct BucketEntry
    {
        public int Count;
        public int FirstSeen;
    }

    private readonly record struct RetainedBucket<TPixel, TMetric>(TPixel Bucket, TMetric Metric, int FirstSeen);

    private static TPixel FindNearestRetainedBucket<TPixel, TMetric>(
        TMetric rareMetric,
        List<RetainedBucket<TPixel, TMetric>> retainedBuckets)
        where TPixel : struct, IPixel<TPixel>
        where TMetric : struct, IPixel<TMetric>
    {
        TPixel bestBucket = retainedBuckets[0].Bucket;
        double bestDistance = rareMetric.Distance(retainedBuckets[0].Metric);
        int bestFirstSeen = retainedBuckets[0].FirstSeen;

        for (int i = 1; i < retainedBuckets.Count; i++)
        {
            RetainedBucket<TPixel, TMetric> candidate = retainedBuckets[i];
            double distance = rareMetric.Distance(candidate.Metric);
            int cmp = distance.CompareTo(bestDistance);
            if (cmp < 0 || (cmp == 0 && candidate.FirstSeen < bestFirstSeen))
            {
                bestDistance = distance;
                bestBucket = candidate.Bucket;
                bestFirstSeen = candidate.FirstSeen;
            }
        }

        return bestBucket;
    }
}
