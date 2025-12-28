namespace Leiter.Algorithms.Saliency;

using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Quantization;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

public static class RegionalContrastSaliency
{
    private record struct RegionCentroid(double X, double Y)
    {
        public readonly double Distance(RegionCentroid other)
        {
            return Math.Sqrt(Math.Pow(this.X - other.X, 2) + Math.Pow(this.Y - other.Y, 2));
        }
    };

    public static Matrix<DoublePixel> ComputeSaliency(Matrix<Rgb8> image, bool enableSmoothing, bool computeBorderRegions)
    {
        var colorQuantizedImage = image.Map(color => UniformQuantizer.QuantizeToMidpoint(color, 12));
        var segmentation = EgbiSegmentation.Segment(Blur.GaussianBlur(image, 0.8f, 2).Map(pixel => pixel.ToLab32(RgbColorSpace.sRGB)), kFactor: 100, minSegmentSize: 50);
        var segmentationMatrix = segmentation.ToMatrix();
        var regions = segmentation.ToRegions();
        var regionCentroids = regions.ToDictionary(
            region => region.Id,
            region => region.Pixels.Select(pixel => new RegionCentroid((double)pixel.X / image.Width, (double)pixel.Y / image.Height)).Aggregate(
                new RegionCentroid(0, 0),
                (current, next) => new RegionCentroid(current.X + next.X, current.Y + next.Y),
                (regionCentroid) => new RegionCentroid(regionCentroid.X / region.Pixels.Count, regionCentroid.Y / region.Pixels.Count)));
        var averageRegionDistance = regions.ToDictionary(
            region => region.Id,
            region => region.Pixels.Select(pixel => Math.Sqrt(Math.Pow(((double)pixel.X / image.Width) - .5, 2) + Math.Pow(((double)pixel.Y / image.Height) - .5, 2))).Average());
        Dictionary<long, IHistogram<Rgb8>> histograms = new();

        foreach (var (Segment, Color) in segmentationMatrix.Zip(colorQuantizedImage, (segment, color) => (Segment: segment, Color: color)))
        {
            if (!histograms.TryGetValue(Segment.Value, out IHistogram<Rgb8>? histogram))
            {
                histogram = new DictionaryHistogram<Rgb8>();
                histograms.Add(Segment.Value, histogram);
            }
            histogram.Increment(Color);
        }
        Console.WriteLine("Segment histograms complete.");

        Dictionary<Rgb8, Lab32> rgbToLabLookupTable = histograms.Values.SelectMany(histogram => histogram.Select(bucket => bucket.Key)).Distinct().ToDictionary(bucket => bucket, bucket => bucket.ToLab32(RgbColorSpace.sRGB));
        Console.WriteLine("RGB to Lab lookup table complete.");

        Dictionary<long, double> saliency = new();
        Parallel.ForEach(histograms, (value) =>
        {
            var (Segment, Histogram) = value;
            double currSaliency = 0.0;
            foreach (var (OtherSegment, OtherHistogram) in histograms)
            {
                if (Segment == OtherSegment)
                {
                    continue;
                }

                var distance = 0.0;
                foreach (var bucket in Histogram)
                {
                    var bucketLab = rgbToLabLookupTable[bucket.Key];
                    var bucketDestiny = Histogram.GetDensity(bucket.Key);
                    foreach (var otherBucket in OtherHistogram)
                    {
                        distance += bucketDestiny * OtherHistogram.GetDensity(otherBucket.Key) * bucketLab.Distance(rgbToLabLookupTable[otherBucket.Key]);
                    }
                }

                var sigmaSquared = 0.4;
                var spacialDistance = regionCentroids[Segment].Distance(regionCentroids[OtherSegment]);
                currSaliency += Math.Exp(spacialDistance / (-sigmaSquared)) * OtherHistogram.Total() * distance;
            }

            var averageRegionDistanceSquared = averageRegionDistance[Segment] * averageRegionDistance[Segment];
            lock (saliency)
            {
                saliency.Add(Segment, Math.Exp(-9 * averageRegionDistanceSquared) * currSaliency);
            }
            Console.Write($"\r{new string(' ', Console.BufferWidth)}\rSaliency computed for segment\t{Segment}\t(Size: {Histogram.Total()})\t{saliency.Count}/{histograms.Count} - {(int)Math.Round((double)saliency.Count / histograms.Count * 100)}%");
        });
        Console.WriteLine("\nSaliency computed for all segments.");

        var finalSaliencyMap = saliency;
        HashSet<long> borderRegions = new();

        if (computeBorderRegions)
        {
            var regionInBorderPercentage = .1;
            var borderPixelWidth = image.Width * .025;
            var borderPixelHeight = image.Height * .025;
            borderRegions = regions.Where(
                region => region.Pixels.Where(pixel => pixel.X < borderPixelWidth || pixel.X >= image.Width - borderPixelWidth || pixel.Y < borderPixelHeight || pixel.Y >= image.Height - borderPixelHeight).Count() > region.Pixels.Count * regionInBorderPercentage)
                .Select(region => region.Id)
                .ToHashSet();
        }

        // BEGIN SMOOTHING
        if (enableSmoothing)
        {
            Dictionary<Rgb8, (double Saliency, int Count)> intermediateColorSaliencyMap = new();
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

            finalSaliencyMap = new Dictionary<long, double>();
            Parallel.ForEach(histograms, (value) =>
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
        // END SMOOTHING

        double minSaliency = finalSaliencyMap.Values.Min();
        double saliencyRange = finalSaliencyMap.Values.Max() - minSaliency;

        return segmentationMatrix.Map<DoublePixel>(segment => computeBorderRegions && borderRegions.Contains(segment.Value) ? 0.0 : (finalSaliencyMap[segment.Value] - minSaliency) / saliencyRange);
    }
}