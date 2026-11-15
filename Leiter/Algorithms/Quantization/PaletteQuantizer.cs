namespace Leiter.Algorithms.Quantization;

using System;
using System.Collections.Generic;
using System.Linq;
using Leiter.Algorithms.DataStructures;
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
    public static QuantizedPalette<TPixel, TPixel> QuantizeAndTruncate<TPixel>(
        Matrix<TPixel> image,
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
    public static QuantizedPalette<TPixel, TMetric> QuantizeAndTruncate<TPixel, TMetric>(
        Matrix<TPixel> image,
        Func<TPixel, TPixel> bucketizer,
        Func<TPixel, TMetric> metricSelector,
        double coverageFraction = 0.95)
        where TPixel : struct, IPixel<TPixel>
        where TMetric : struct, IPixel<TMetric>
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(bucketizer);
        ArgumentNullException.ThrowIfNull(metricSelector);

        if (double.IsNaN(coverageFraction) || coverageFraction is <= 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(coverageFraction), "Coverage fraction must be in the range (0, 1].");
        }

        if (image.Count == 0)
        {
            return new QuantizedPalette<TPixel, TMetric>(
                new SequentialMatrix<TPixel>(image.Size),
                new DictionaryHistogram<TPixel>(),
                new Dictionary<TPixel, TMetric>());
        }

        Matrix<TPixel> quantizedInput = image.Map(bucketizer);
        DictionaryHistogram<TPixel> histogram = new();
        Dictionary<TPixel, int> firstSeenOrder = new();

        int pixelIndex = 0;
        foreach (var quantizedPixel in quantizedInput)
        {
            histogram.Increment(quantizedPixel);
            firstSeenOrder.TryAdd(quantizedPixel, pixelIndex++);
        }

        Dictionary<TPixel, TMetric> rawMetricLookup = histogram.ToDictionary(
            bucket => bucket.Key,
            bucket => metricSelector(bucket.Key));

        DictionaryHistogram<TPixel> paletteHistogram = new();
        Dictionary<TPixel, TMetric> metricLookup = new();
        Dictionary<TPixel, TPixel> remap = new();

        int targetPixelCount = (int)Math.Ceiling(image.Count * coverageFraction);
        int pixelCount = 0;
        foreach (var bucket in histogram.OrderByDescending(b => b.Value).ThenBy(b => firstSeenOrder[b.Key]))
        {
            if (pixelCount < targetPixelCount || paletteHistogram.BucketCount == 0)
            {
                paletteHistogram.Increment(bucket.Key, bucket.Value);
                metricLookup[bucket.Key] = rawMetricLookup[bucket.Key];
                pixelCount += bucket.Value;
            }
            else
            {
                TMetric rareMetric = rawMetricLookup[bucket.Key];
                TPixel nearestRetainedColor = paletteHistogram
                    .OrderBy(target => rareMetric.Distance(metricLookup[target.Key]))
                    .ThenBy(target => firstSeenOrder[target.Key])
                    .First()
                    .Key;
                paletteHistogram.Increment(nearestRetainedColor, bucket.Value);
                remap[bucket.Key] = nearestRetainedColor;
            }
        }

        Matrix<TPixel> paletteImage = remap.Count > 0
            ? quantizedInput.Map(c => remap.TryGetValue(c, out var mapped) ? mapped : c)
            : quantizedInput;

        return new QuantizedPalette<TPixel, TMetric>(paletteImage, paletteHistogram, metricLookup);
    }
}
