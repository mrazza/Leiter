namespace Leiter.Algorithms.Saliency;

using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Quantization;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// A regional contrast saliency algorithm.
/// </summary>
/// <remarks>
/// This algorithm computes per-region saliency based on an image and a segmentation.
/// This implements the "Region Based Contrast" saliency algorithm described in Section 4
/// of Global Contrast based Salient Region Detection by Ming-Ming Cheng, et al.
/// </remarks>
public static class RegionalContrastSaliency
{
    /// <summary>
    /// Computes the saliency map for the given image based on the provided segmentation.
    /// </summary>
    /// <remarks>
    /// A value of 1 indicates a salient pixel, while a value of 0 indicates a non-salient pixel.
    /// 
    /// Optionally, color space smoothing can be enabled to smooth out saliency values across
    /// similar colors in the image. This helps produce more uniformity in saliency across regions
    /// with similar colors. This is enabled by default.
    /// 
    /// Additionally, border regions can be computed and rejected from the saliency map.
    /// The idea being that regions whose pixels are primarily on the border of the image
    /// are unlikely to be salient regions. This is enabled by default.
    /// </remarks>
    /// <param name="image">The input image.</param>
    /// <param name="segmentation">The segmentation to use.</param>
    /// <param name="enableSmoothing">Whether to enable region-based saliency smoothing.</param>
    /// <param name="computeBorderRegions">Whether to compute border regions.</param>
    /// <returns>The saliency map where each pixel has a value between [0, 1].</returns>
    public static Matrix<DoublePixel> ComputeSaliency(Matrix<Rgb8> image, IDisjointSet segmentation, bool enableSmoothing = true, bool computeBorderRegions = true)
    {
        if (image.Count == 0)
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        var (colorQuantizedImage, _, rgbToLabLookupTable) = PaletteQuantizer.QuantizeAndTruncate(
            image,
            static color => UniformQuantizer.QuantizeToMidpoint(color, 12),
            static color => color.ToLab32(RgbColorSpace.sRGB));
        var segmentationMatrix = segmentation.ToMatrix();
        var regions = segmentation.ToRegions();

        var normalizedRegionCentroids = regions.ToDictionary(
            region => region.Id,
            region => region.Pixels.Select(pixel => image.NormalizeCoord(pixel)).Aggregate(
                new NormalizedCoord(0, 0),
                (current, next) => current + next,
                (regionCentroid) => new NormalizedCoord(regionCentroid.X / region.Pixels.Count, regionCentroid.Y / region.Pixels.Count)));
        var normalizedAverageRegionDistance = regions.ToDictionary(
            region => region.Id,
            region => region.Pixels.Select(pixel => image.NormalizeCoord(pixel) - 0.5).Select(
                normalizedCoord => Math.Sqrt(normalizedCoord.X * normalizedCoord.X + normalizedCoord.Y * normalizedCoord.Y)).Average());

        Dictionary<long, IHistogram<Rgb8>> regionColorHistograms = new(regions.Count);
        foreach (var (Segment, Color) in segmentationMatrix.Zip(colorQuantizedImage, (segment, color) => (Segment: segment, Color: color)))
        {
            if (!regionColorHistograms.TryGetValue(Segment.Value, out IHistogram<Rgb8>? histogram))
            {
                histogram = new DictionaryHistogram<Rgb8>();
                regionColorHistograms.Add(Segment.Value, histogram);
            }
            histogram.Increment(Color);
        }

        // Computing region-based saliency requires many nested loops. As a result, precomputation of
        // critical information dramatically improves performance.
        // We precompute the following:
        // 1. A lookup table for RGB to LAB conversion.
        // 2. A lookup table for the distance between all possible color pairs.
        // 3. The collection of region information that we need to iterate over.
        //
        // Note that these loops are so tight that Dictionary<>/hash map lookups end up being expensive.
        // As a result, we construct a single dictionary to resolve an RGB value to an array index and
        // use array lookups using that index instead of dictionaries for lookups within the loop.
        int rgbToIndexLookupTableIndex = 0;
        Dictionary<Rgb8, int> rgbToIndexLookupTable = rgbToLabLookupTable.ToDictionary(entry => entry.Key, entry => rgbToIndexLookupTableIndex++);
        double[] labDistanceLookupTable = rgbToLabLookupTable.SelectMany(
            color1 => rgbToLabLookupTable.Select(color2 => color1.Value.Distance(color2.Value))).ToArray();
        (long Segment, int Total, List<(Rgb8 Color, double Density, int DistanceIndex)> ColorData)[] processedRegions =
            regionColorHistograms.Select(entry => (
                    Segment: entry.Key,
                    Total: entry.Value.Total(),
                    ColorData: entry.Value.Buckets.Select(bucket => (Color: bucket, Density: entry.Value.GetDensity(bucket), DistanceIndex: rgbToIndexLookupTable[bucket])).ToList())
                )
                .ToArray();

        Dictionary<long, double> saliency = new(regions.Count);
        Parallel.ForEach(processedRegions, (value) =>
        {
            var (Segment, Total, ColorData) = value;
            var currCentroid = normalizedRegionCentroids[Segment];
            double currSaliency = 0.0;
            foreach (var (OtherSegment, OtherTotal, OtherColorData) in processedRegions)
            {
                if (Segment == OtherSegment)
                {
                    continue;
                }

                var distance = 0.0;
                foreach (var currColorData in ColorData)
                {
                    var bucketDestiny = currColorData.Density;
                    var initialBucketOffset = currColorData.DistanceIndex * rgbToIndexLookupTable.Count;
                    foreach (var currOtherColorData in OtherColorData)
                    {
                        distance += bucketDestiny * currOtherColorData.Density * labDistanceLookupTable[initialBucketOffset + currOtherColorData.DistanceIndex];
                    }
                }

                var sigmaSquared = 0.4;
                var spacialDistance = currCentroid.Distance(normalizedRegionCentroids[OtherSegment]);
                currSaliency += Math.Exp(spacialDistance / (-sigmaSquared)) * OtherTotal * distance;
            }

            var currAverageRegionDistance = normalizedAverageRegionDistance[Segment];
            lock (saliency)
            {
                saliency.Add(Segment, Math.Exp(-9 * currAverageRegionDistance * currAverageRegionDistance) * currSaliency);
            }
        });

        HashSet<long> borderRegions = [];
        if (computeBorderRegions)
        {
            const double REGION_IN_BORDER_PERCENTAGE = .1;
            const double BORDER_PERCENTAGE = .025;
            var borderPixelWidth = image.Width * BORDER_PERCENTAGE;
            var borderPixelHeight = image.Height * BORDER_PERCENTAGE;
            var maxX = image.Width - borderPixelWidth;
            var maxY = image.Height - borderPixelHeight;
            borderRegions = regions.Where(
                region => region.Pixels.Where(pixel => pixel.X < borderPixelWidth || pixel.X >= maxX || pixel.Y < borderPixelHeight || pixel.Y >= maxY).Count() > region.Pixels.Count * REGION_IN_BORDER_PERCENTAGE)
                .Select(region => region.Id)
                .ToHashSet();

            // Zero border regions before smoothing so border background regions do not bleed saliency
            // into foreground colors during color-space smoothing.
            foreach (var borderId in borderRegions)
            {
                saliency[borderId] = 0.0;
            }
        }

        var finalSaliencyMap = saliency;
        if (enableSmoothing)
        {
            Dictionary<Rgb8, (double Saliency, int Count)> intermediateColorSaliencyMap = [];
            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    var segment = segmentationMatrix[x, y];

                    if (!intermediateColorSaliencyMap.TryGetValue(colorQuantizedImage[x, y], out var colorSaliencyEntry))
                    {
                        colorSaliencyEntry = (0.0, 0);
                    }
                    intermediateColorSaliencyMap[colorQuantizedImage[x, y]] = (colorSaliencyEntry.Saliency + saliency[segment.Value], colorSaliencyEntry.Count + 1);
                }
            }

            Dictionary<Rgb8, double> colorSaliencyMap = intermediateColorSaliencyMap.ToDictionary(entry => entry.Key, entry => entry.Value.Saliency / entry.Value.Count);
            SaliencyColorSmoother.SmoothColorSaliency(colorSaliencyMap, rgbToLabLookupTable, neighborDivisor: 10);

            finalSaliencyMap = new Dictionary<long, double>(regions.Count);
            Parallel.ForEach(regionColorHistograms, (value) =>
            {
                var (Segment, Histogram) = value;
                var regionSaliency = 0.0;
                foreach (var bucket in Histogram)
                {
                    regionSaliency += Histogram.GetDensity(bucket.Key) * colorSaliencyMap[bucket.Key];
                }

                lock (finalSaliencyMap)
                {
                    finalSaliencyMap.Add(Segment, regionSaliency);
                }
            });

            if (computeBorderRegions)
            {
                foreach (var borderId in borderRegions)
                {
                    finalSaliencyMap[borderId] = 0.0;
                }
            }
        }

        if (finalSaliencyMap.Count == 0)
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        double minSaliency = finalSaliencyMap.Values.Min();
        double saliencyRange = finalSaliencyMap.Values.Max() - minSaliency;
        if (saliencyRange <= 0.0)
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        return segmentationMatrix.Map<DoublePixel>(segment => (finalSaliencyMap[segment.Value] - minSaliency) / saliencyRange);
    }
}