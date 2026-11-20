namespace Leiter.Algorithms.Saliency;

using System;
using System.Collections.Generic;
using Leiter.Algorithms.Saliency.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Global contrast saliency algorithm.
/// </summary>
/// <remarks>
/// This algorithm computes per-pixel saliency based on contrast with the rest of the image.
/// This is implemented via the histogram-based global contrast saliency algorithm described in
/// Section 3 of Global Contrast based Salient Region Detection by Ming-Ming Cheng, et al.
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    public static Matrix<DoublePixel> ComputeSaliency(IReadOnlyMatrix<Rgb8> image, bool enableColorSpaceSmoothing = true)
    {
        ArgumentNullException.ThrowIfNull(image);
        return ComputeSaliencyCore(image, enableColorSpaceSmoothing);
    }

    /// <summary>
    /// Computes the saliency map for the given image using the specified options.
    /// </summary>
    /// <param name="image">The input image.</param>
    /// <param name="options">Optional global contrast post-processing parameters.</param>
    /// <returns>The saliency map where each pixel has a value between [0, 1].</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    public static Matrix<DoublePixel> ComputeSaliency(IReadOnlyMatrix<Rgb8> image, GlobalContrastSaliencyOptions? options)
    {
        ArgumentNullException.ThrowIfNull(image);
        GlobalContrastSaliencyOptions resolvedOptions = OptionsResolver.Resolve(options);
        return ComputeSaliencyCore(image, resolvedOptions.EnableColorSpaceSmoothing);
    }

    private static Matrix<DoublePixel> ComputeSaliencyCore(IReadOnlyMatrix<Rgb8> image, bool enableColorSpaceSmoothing)
    {
        if (image.Count == 0)
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        // Quantize the image into 12 buckets per channel and truncate the histogram to the top 95% of pixels.
        var (paletteImage, truncatedHistogram, rgbToLabLookupTable) = SaliencyColorSmoother.QuantizeForSaliency(image);
        PairwiseLabDistanceTable distanceTable = PairwiseLabDistanceTable.Build(rgbToLabLookupTable);

        int colorCount = distanceTable.Count;
        var counts = new int[colorCount];
        foreach (var bucket in truncatedHistogram)
        {
            counts[distanceTable.GetIndex(bucket.Key)] = bucket.Value;
        }

        // Compute the saliency for each bucket based on the distance to all other buckets.
        double invTotalPixels = 1.0 / image.Count;
        var saliencyMap = new Dictionary<Rgb8, double>(colorCount);
        for (int i = 0; i < colorCount; i++)
        {
            ReadOnlySpan<double> distances = distanceTable.GetRow(i);
            double weightedSum = 0.0;
            for (int j = 0; j < colorCount; j++)
            {
                if (i != j)
                {
                    weightedSum += distances[j] * counts[j];
                }
            }

            saliencyMap[distanceTable.GetColor(i)] = weightedSum * invTotalPixels;
        }

        if (enableColorSpaceSmoothing)
        {
            SaliencyColorSmoother.SmoothColorSaliency(saliencyMap, distanceTable, neighborDivisor: 4);
        }

        // Map the saliency values back to the original image and normalize the values to [0, 1].
        if (!SaliencyColorSmoother.TryGetMinMaxRange(saliencyMap.Values, out double minSaliency, out double saliencyRange))
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        return paletteImage.Map<DoublePixel>(pixel => (saliencyMap[pixel] - minSaliency) / saliencyRange);
    }
}
