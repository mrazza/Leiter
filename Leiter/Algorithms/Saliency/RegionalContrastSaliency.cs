namespace Leiter.Algorithms.Saliency;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    /// <param name="options">Optional regional contrast post-processing parameters.</param>
    /// <returns>The saliency map where each pixel has a value between [0, 1].</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="segmentation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="image"/> and <paramref name="segmentation"/> dimensions do not match.</exception>
    public static Matrix<DoublePixel> ComputeSaliency(
        IReadOnlyMatrix<Rgb8> image,
        IDisjointSet segmentation,
        RegionalContrastSaliencyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(segmentation);

        RegionalContrastSaliencyOptions resolvedOptions = options ?? RegionalContrastSaliencyOptions.Default;
        resolvedOptions.Validate();
        bool enableSmoothing = resolvedOptions.EnableSmoothing;
        bool computeBorderRegions = resolvedOptions.ComputeBorderRegions;

        if (image.Count == 0)
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        Guard.ThrowIfSizeMismatch(image, segmentation);

        RegionGraph graph = RegionGraph.FromDisjointSet(segmentation);
        int regionCount = graph.RegionCount;
        IReadOnlyMatrix<int> labels = graph.Labels;
        IReadOnlyList<Region<Coord>> regions = graph.Regions;

        var (colorQuantizedImage, _, rgbToLabLookupTable) = PaletteQuantizer.QuantizeAndTruncate(
            image,
            static color => UniformQuantizer.QuantizeToMidpoint(color, 12),
            static color => color.ToLab32(RgbColorSpace.SRgb));

        var regionCentroids = new NormalizedCoord[regionCount];
        var avgRegionDistances = new double[regionCount];
        for (int r = 0; r < regionCount; r++)
        {
            Region<Coord> region = regions[r];
            NormalizedCoord sum = new(0, 0);
            double sumDist = 0.0;
            foreach (Coord coord in region.Pixels)
            {
                NormalizedCoord normalizedCoord = image.NormalizeCoord(coord);
                sum += normalizedCoord;
                NormalizedCoord centered = normalizedCoord - 0.5;
                sumDist += Math.Sqrt((centered.X * centered.X) + (centered.Y * centered.Y));
            }

            double invCount = 1.0 / region.Pixels.Count;
            regionCentroids[r] = sum * invCount;
            avgRegionDistances[r] = sumDist * invCount;
        }

        var regionHistograms = new DictionaryHistogram<Rgb8>[regionCount];
        for (int r = 0; r < regionCount; r++)
        {
            regionHistograms[r] = new DictionaryHistogram<Rgb8>();
        }

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                regionHistograms[labels[x, y]].Increment(colorQuantizedImage[x, y]);
            }
        }

        int rgbToIndexLookupTableIndex = 0;
        Dictionary<Rgb8, int> rgbToIndexLookupTable = rgbToLabLookupTable.ToDictionary(entry => entry.Key, _ => rgbToIndexLookupTableIndex++);
        double[] labDistanceLookupTable = rgbToLabLookupTable.SelectMany(
            color1 => rgbToLabLookupTable.Select(color2 => color1.Value.Distance(color2.Value))).ToArray();

        var processedRegions = new (int Total, (Rgb8 Color, double Density, int DistanceIndex)[] ColorData)[regionCount];
        for (int r = 0; r < regionCount; r++)
        {
            DictionaryHistogram<Rgb8> histogram = regionHistograms[r];
            var colorData = new (Rgb8 Color, double Density, int DistanceIndex)[histogram.BucketCount];
            int idx = 0;
            foreach (Rgb8 bucket in histogram.Buckets)
            {
                colorData[idx++] = (bucket, histogram.GetDensity(bucket), rgbToIndexLookupTable[bucket]);
            }

            processedRegions[r] = (histogram.Total(), colorData);
        }

        var rawSaliency = new double[regionCount];
        int colorCount = rgbToIndexLookupTable.Count;
        const double sigmaSquared = 0.4;
        double centerPriorWeight = resolvedOptions.CenterPriorWeight;

        Parallel.For(0, regionCount, r =>
        {
            NormalizedCoord currCentroid = regionCentroids[r];
            var colorData = processedRegions[r].ColorData;
            double currSaliency = 0.0;

            for (int other = 0; other < regionCount; other++)
            {
                if (r == other)
                {
                    continue;
                }

                var (otherTotal, otherColorData) = processedRegions[other];
                double distance = 0.0;

                for (int c = 0; c < colorData.Length; c++)
                {
                    double bucketDensity = colorData[c].Density;
                    int initialBucketOffset = colorData[c].DistanceIndex * colorCount;
                    for (int oc = 0; oc < otherColorData.Length; oc++)
                    {
                        distance += bucketDensity * otherColorData[oc].Density * labDistanceLookupTable[initialBucketOffset + otherColorData[oc].DistanceIndex];
                    }
                }

                double spatialDistance = currCentroid.Distance(regionCentroids[other]);
                currSaliency += Math.Exp(spatialDistance / -sigmaSquared) * otherTotal * distance;
            }

            if (centerPriorWeight > 0.0)
            {
                double currAverageRegionDistance = avgRegionDistances[r];
                rawSaliency[r] = Math.Exp(-centerPriorWeight * currAverageRegionDistance * currAverageRegionDistance) * currSaliency;
            }
            else
            {
                rawSaliency[r] = currSaliency;
            }
        });

        var isBorderRegion = new bool[regionCount];
        if (computeBorderRegions)
        {
            const double REGION_IN_BORDER_PERCENTAGE = 0.1;
            const double BORDER_PERCENTAGE = 0.025;
            double borderPixelWidth = image.Width * BORDER_PERCENTAGE;
            double borderPixelHeight = image.Height * BORDER_PERCENTAGE;
            double maxX = image.Width - borderPixelWidth;
            double maxY = image.Height - borderPixelHeight;

            for (int r = 0; r < regionCount; r++)
            {
                Region<Coord> region = regions[r];
                int borderPixels = 0;
                foreach (Coord pixel in region.Pixels)
                {
                    if (pixel.X < borderPixelWidth || pixel.X >= maxX || pixel.Y < borderPixelHeight || pixel.Y >= maxY)
                    {
                        borderPixels++;
                    }
                }

                if (borderPixels > region.Pixels.Count * REGION_IN_BORDER_PERCENTAGE)
                {
                    isBorderRegion[r] = true;
                    rawSaliency[r] = 0.0;
                }
            }
        }

        double[] finalSaliency;
        if (enableSmoothing)
        {
            Dictionary<Rgb8, (double Saliency, int Count)> intermediateColorSaliencyMap = [];
            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    int label = labels[x, y];
                    Rgb8 color = colorQuantizedImage[x, y];

                    if (!intermediateColorSaliencyMap.TryGetValue(color, out var colorSaliencyEntry))
                    {
                        colorSaliencyEntry = (0.0, 0);
                    }

                    intermediateColorSaliencyMap[color] = (colorSaliencyEntry.Saliency + rawSaliency[label], colorSaliencyEntry.Count + 1);
                }
            }

            Dictionary<Rgb8, double> colorSaliencyMap = intermediateColorSaliencyMap.ToDictionary(entry => entry.Key, entry => entry.Value.Saliency / entry.Value.Count);
            SaliencyColorSmoother.SmoothColorSaliency(colorSaliencyMap, rgbToLabLookupTable, neighborDivisor: 10);

            finalSaliency = new double[regionCount];
            Parallel.For(0, regionCount, r =>
            {
                DictionaryHistogram<Rgb8> histogram = regionHistograms[r];
                double regionSaliency = 0.0;
                foreach (var bucket in histogram)
                {
                    regionSaliency += histogram.GetDensity(bucket.Key) * colorSaliencyMap[bucket.Key];
                }

                finalSaliency[r] = regionSaliency;
            });

            if (computeBorderRegions)
            {
                for (int r = 0; r < regionCount; r++)
                {
                    if (isBorderRegion[r])
                    {
                        finalSaliency[r] = 0.0;
                    }
                }
            }
        }
        else
        {
            finalSaliency = rawSaliency;
        }

        if (!SaliencyColorSmoother.TryGetMinMaxRange(finalSaliency, out double minSaliency, out double saliencyRange))
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        return labels.Map<int, DoublePixel>(label => (finalSaliency[label] - minSaliency) / saliencyRange);
    }
}
