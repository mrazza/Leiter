namespace Leiter.Algorithms.Saliency;

using System.Collections.Generic;
using System.Linq;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Quantization;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Global contrast saliency algorithm.
/// </summary>
/// <remarks>
/// This algorithm computes per-pixel saliency based on contrast with the rest of the image.
/// This is implemented via the histogram-based global contrast saliency algorithm described in
/// section 3 of Global Contrast based Salient Region Detection by Ming-Ming Cheng, et al.
/// </remarks>
public static class GlobalContrastSaliency
{
    /// <summary>
    /// Computes the saliency map for the given image.
    /// </summary>
    /// <remarks>
    /// A value of 1 indicates a salient pixel, while a value of 0 indicates a non-salient pixel.
    /// 
    /// Optionally, color space smoothing can be enabled to smooth out saliency values across
    /// similar colors in the image. This helps reduce saliency noise in areas of the image with color
    /// variation near the boundary of quantized color buckets. This is enabled by default.
    /// </remarks>
    /// <param name="image">The input image.</param>
    /// <param name="enableColorSpaceSmoothing">Whether to enable color space smoothing.</param>
    /// <returns>The saliency map where each pixel has a value between [0, 1].</returns>
    public static Matrix<DoublePixel> ComputeSaliency(Matrix<Rgb8> image, bool enableColorSpaceSmoothing = true)
    {
        // Quantize the image into 12 buckets per channel and compute the number of pixels in each bucket
        // across the image.
        Matrix<Rgb8> quantizedInput = image.Map(color => UniformQuantizer.QuantizeToMidpoint(color, 12));
        DictionaryHistogram<Rgb8> histogram = new();
        foreach (var quantizedPixel in quantizedInput)
        {
            histogram.Increment(quantizedPixel);
        }

        // Create a lookup table for the Lab values of each bucket (this is a significant performance
        // optimization given that Lab32 conversions are expensive).
        Dictionary<Rgb8, Lab32> rgbToLabLookupTable = histogram.ToDictionary(bucket => bucket.Key, bucket => bucket.Key.ToLab32(RgbColorSpace.sRGB));

        // Truncate the histogram to the top 95% of pixels and map the remaining pixels to the nearest bucket.
        DictionaryHistogram<Rgb8> truncatedHistogram = new();
        Dictionary<Rgb8, Rgb8> truncatedColors = new();
        int pixelCount = 0;
        int pixelCutOff = (int)(image.Count * 0.95);
        foreach (var bucket in histogram.OrderByDescending(bucket => bucket.Value))
        {
            if (pixelCount <= pixelCutOff)
            {
                truncatedHistogram.Increment(bucket.Key, bucket.Value);
                pixelCount += bucket.Value;
            }
            else
            {
                var newKey = truncatedHistogram.OrderBy(targetBucket => rgbToLabLookupTable[bucket.Key].Distance(rgbToLabLookupTable[targetBucket.Key])).First().Key;
                truncatedHistogram.Increment(newKey, bucket.Value);
                truncatedColors[bucket.Key] = newKey;
            }
        }

        // Compute the saliency for each bucket based on the distance to all other buckets.
        Dictionary<Rgb8, double> saliencyMap = [];
        foreach (var bucket in truncatedHistogram)
        {
            Lab32 bucketColor = rgbToLabLookupTable[bucket.Key];
            saliencyMap[bucket.Key] =
                truncatedHistogram.Where(targetBucket => targetBucket.Key != bucket.Key)
                    .Sum(targetBucket => bucketColor.Distance(rgbToLabLookupTable[targetBucket.Key]) * targetBucket.Value) / image.Count;
        }

        if (enableColorSpaceSmoothing)
        {
            int nearestColorCount = truncatedHistogram.BucketCount / 4;
            var unsmoothedSaliencyMap = new Dictionary<Rgb8, double>(saliencyMap);
            foreach (var bucket in unsmoothedSaliencyMap)
            {
                // Computes [c, D(c, c sub i)] for m nearest neighbors of bucket.Key.
                var nearestNeighbors = unsmoothedSaliencyMap.Keys
                    .Where(targetBucket => targetBucket != bucket.Key) // TODO: Unclear if we should include ourselves here or not;
                                                                       // paper seems to imply OTHER so we're skipping ourselves.
                    .Select(key => (color: key, distance: rgbToLabLookupTable[bucket.Key].Distance(rgbToLabLookupTable[key])))
                    .OrderBy(neighbor => neighbor.distance).Take(nearestColorCount).ToList();

                // Computes T = sum(D(c, c sub i)).
                double T = nearestNeighbors.Sum(neighbor => neighbor.distance);

                // Computes (1 / ((m - 1) * T)) * sum((T - D(c, c sub i)) * S[c sub i]).
                saliencyMap[bucket.Key] = (1.0 / ((nearestColorCount - 1) * T)) * nearestNeighbors.Sum(neighbor => (T - neighbor.distance) * unsmoothedSaliencyMap[neighbor.color]);
            }
        }

        // In order to prepare the final image, we need to map the truncated colors back to the original colors.
        foreach (var color in truncatedColors)
        {
            saliencyMap[color.Key] = saliencyMap[color.Value];
        }

        // Map the saliency values back to the original image and normalize the values to [0, 1].
        double minSaliency = saliencyMap.Values.Min();
        double saliencyRange = saliencyMap.Values.Max() - minSaliency;
        return quantizedInput.Map<DoublePixel>((pixel) => (saliencyMap[pixel] - minSaliency) / saliencyRange);
    }
}