namespace Leiter.Algorithms.Saliency;

using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Quantization;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

public static class RegionalContrastSaliency
{
    public static Matrix<DoublePixel> ComputeSaliency(Matrix<Rgb8> image, IDisjointSet segmentation, bool enableSmoothing = true, bool computeBorderRegions = true)
    {
        var colorQuantizedImage = image.Map(color => UniformQuantizer.QuantizeToMidpoint(color, 12));
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
        Dictionary<Rgb8, Lab32> rgbToLabLookupTable = regionColorHistograms.Values.SelectMany(histogram => histogram.Select(bucket => bucket.Key)).Distinct().ToDictionary(bucket => bucket, bucket => bucket.ToLab32(RgbColorSpace.sRGB));
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

        var finalSaliencyMap = saliency;
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
        }

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
            int nearestColorCount = colorSaliencyMap.Count / 10;
            var unsmoothedSaliencyMap = new Dictionary<Rgb8, double>(colorSaliencyMap);
            foreach (var bucket in unsmoothedSaliencyMap)
            {
                // Computes [c, D(c, c sub i)] for m nearest neighbors of bucket.Key.
                // Note that we include our own color here. The paper says "other colors" and "other pixels"
                // but the equations seem to explicitly include the color or pixel under evaluation and so
                // we do so here.
                var nearestNeighbors = unsmoothedSaliencyMap.Keys
                    .Select(key => (color: key, distance: rgbToLabLookupTable[bucket.Key].Distance(rgbToLabLookupTable[key])))
                    .OrderBy(neighbor => neighbor.distance).Take(nearestColorCount).ToList();

                // Computes T = sum(D(c, c sub i)).
                double T = nearestNeighbors.Sum(neighbor => neighbor.distance);

                // Computes (1 / ((m - 1) * T)) * sum((T - D(c, c sub i)) * S[c sub i]).
                colorSaliencyMap[bucket.Key] = (1.0 / ((nearestColorCount - 1) * T)) * nearestNeighbors.Sum(neighbor => (T - neighbor.distance) * unsmoothedSaliencyMap[neighbor.color]);
            }

            finalSaliencyMap = new Dictionary<long, double>(regions.Count);
            Parallel.ForEach(regionColorHistograms, (value) =>
            {
                var (Segment, Histogram) = value;
                var saliency = 0.0;
                foreach (var bucket in Histogram)
                {
                    saliency += Histogram.GetDensity(bucket.Key) * colorSaliencyMap[bucket.Key];
                }

                lock (finalSaliencyMap)
                {
                    finalSaliencyMap.Add(Segment, saliency);
                }
            });
        }

        double minSaliency = finalSaliencyMap.Values.Min();
        double saliencyRange = finalSaliencyMap.Values.Max() - minSaliency;
        return segmentationMatrix.Map<DoublePixel>(segment => computeBorderRegions && borderRegions.Contains(segment.Value) ? 0.0 : (finalSaliencyMap[segment.Value] - minSaliency) / saliencyRange);
    }
}