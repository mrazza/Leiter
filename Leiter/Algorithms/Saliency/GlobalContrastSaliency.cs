namespace Leiter.Algorithms.Saliency;

using System;
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
    /// Computes the saliency map for the given image using the specified options.
    /// </summary>
    /// <remarks>
    /// A value of 1 indicates a salient pixel, while a value of 0 indicates a non-salient pixel.
    ///
    /// Optionally, color space smoothing can be enabled via <see cref="GlobalContrastSaliencyOptions.EnableSmoothing"/>
    /// to smooth out saliency values across similar colors in the image, reducing saliency noise near quantized
    /// color bucket boundaries. This is enabled by default.
    /// </remarks>
    /// <param name="image">The input image.</param>
    /// <param name="options">Optional global contrast post-processing parameters.</param>
    /// <returns>The saliency map where each pixel has a value between [0, 1].</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    public static Matrix<DoublePixel> ComputeSaliency(
        IReadOnlyMatrix<Rgb8> image,
        GlobalContrastSaliencyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        GlobalContrastSaliencyOptions resolvedOptions = OptionsResolver.Resolve(options);

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
        var colorSaliencies = new double[colorCount];
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

            colorSaliencies[i] = weightedSum * invTotalPixels;
        }

        if (resolvedOptions.EnableSmoothing)
        {
            SaliencyColorSmoother.SmoothColorSaliency(colorSaliencies, distanceTable, neighborDivisor: 4);
        }

        // Map the saliency values back to the original image and normalize the values to [0, 1].
        if (!SaliencyColorSmoother.TryGetMinMaxRange(colorSaliencies.AsSpan(), out double minSaliency, out double saliencyRange))
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        for (int i = 0; i < colorSaliencies.Length; i++)
        {
            colorSaliencies[i] = (colorSaliencies[i] - minSaliency) / saliencyRange;
        }

        return paletteImage.Map<DoublePixel>(pixel => colorSaliencies[distanceTable.GetIndex(pixel)]);
    }
}
