namespace Leiter.Algorithms.Saliency;

using System.Collections.Generic;
using System.Linq;
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

        if (image.Count == 0)
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        // Quantize the image into 12 buckets per channel and truncate the histogram to the top 95% of pixels.
        var (paletteImage, truncatedHistogram, rgbToLabLookupTable) = PaletteQuantizer.QuantizeAndTruncate(
            image,
            static color => UniformQuantizer.QuantizeToMidpoint(color, 12),
            static color => color.ToLab32(RgbColorSpace.SRgb));

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
            SaliencyColorSmoother.SmoothColorSaliency(saliencyMap, rgbToLabLookupTable, neighborDivisor: 4);
        }

        if (saliencyMap.Count == 0)
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        // Map the saliency values back to the original image and normalize the values to [0, 1].
        double minSaliency = saliencyMap.Values.Min();
        double saliencyRange = saliencyMap.Values.Max() - minSaliency;
        if (saliencyRange <= 0.0)
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        return paletteImage.Map<DoublePixel>(pixel => (saliencyMap[pixel] - minSaliency) / saliencyRange);
    }
}