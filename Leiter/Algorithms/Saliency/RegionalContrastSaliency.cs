namespace Leiter.Algorithms.Saliency;

using System;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Saliency.Internal;
using Leiter.Core;
using Leiter.Pixels;

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
    /// Additionally, border regions (regions with more than 10% of their pixels inside the 2.5% outer image border)
    /// can be identified and suppressed to <c>0.0</c> after optional color-space smoothing so border regions do not
    /// darken interior regions sharing a color. This is enabled by default.
    /// </remarks>
    /// <param name="image">The input image.</param>
    /// <param name="segmentation">The segmentation to use.</param>
    /// <param name="options">Optional regional contrast post-processing parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The saliency map where each pixel has a value between [0, 1].</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="segmentation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="image"/> and <paramref name="segmentation"/> dimensions do not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> contains invalid parameters.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Matrix<DoublePixel> ComputeSaliency(
        IReadOnlyMatrix<Rgb8> image,
        IDisjointSet segmentation,
        RegionalContrastSaliencyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.ThrowIfSizeMismatch(image, segmentation);
        RegionalContrastSaliencyOptions resolvedOptions = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0)
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        RegionGraph graph = segmentation.ToRegionGraph();
        cancellationToken.ThrowIfCancellationRequested();
        return ComputeSaliencyCore(image, graph, resolvedOptions, cancellationToken);
    }

    /// <summary>
    /// Computes the saliency map for the given image based on a precomputed <see cref="RegionGraph"/>.
    /// </summary>
    /// <param name="image">The input image.</param>
    /// <param name="regionGraph">The precomputed region adjacency graph matching <paramref name="image"/> dimensions.</param>
    /// <param name="options">Optional regional contrast post-processing parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The saliency map where each pixel has a value between [0, 1].</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="regionGraph"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="image"/> and <paramref name="regionGraph"/> dimensions do not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> contains invalid parameters.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Matrix<DoublePixel> ComputeSaliency(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        RegionalContrastSaliencyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(regionGraph);

        RegionalContrastSaliencyOptions resolvedOptions = OptionsResolver.Resolve(options);
        Guard.ThrowIfSizeMismatch(image.Size, regionGraph.Size, nameof(regionGraph));
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0)
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        return ComputeSaliencyCore(image, regionGraph, resolvedOptions, cancellationToken);
    }

    private static Matrix<DoublePixel> ComputeSaliencyCore(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph graph,
        RegionalContrastSaliencyOptions resolvedOptions,
        CancellationToken cancellationToken)
    {
        bool enableSmoothing = resolvedOptions.EnableSmoothing;
        bool computeBorderRegions = resolvedOptions.ComputeBorderRegions;
        int regionCount = graph.RegionCount;
        int width = image.Width;
        int height = image.Height;
        IReadOnlyMatrix<int> labels = graph.Labels;

        var (colorQuantizedImage, _, rgbToLabLookupTable) = SaliencyColorSmoother.QuantizeForSaliency(image);
        cancellationToken.ThrowIfCancellationRequested();

        var regionCounts = new int[regionCount];
        var centroidSums = new NormalizedCoord[regionCount];
        var centerDistanceSums = new double[regionCount];
        var borderPixelCounts = computeBorderRegions ? new int[regionCount] : null;
        var regionHistograms = new DictionaryHistogram<Rgb8>[regionCount];
        for (int r = 0; r < regionCount; r++)
        {
            regionHistograms[r] = new DictionaryHistogram<Rgb8>();
        }

        const double regionInBorderPercentage = 0.1;
        const double borderPercentage = 0.025;
        double borderPixelWidth = width * borderPercentage;
        double borderPixelHeight = height * borderPercentage;
        double maxX = width - borderPixelWidth;
        double maxY = height - borderPixelHeight;
        var imageCenter = new NormalizedCoord(0.5, 0.5);
        double invWidth = 1.0 / width;
        double invHeight = 1.0 / height;

        ReadOnlySpan<Rgb8> colorSpan = default;
        bool hasSpans = labels.TryGetReadOnlySpan(out ReadOnlySpan<int> labelSpan) &&
                        colorQuantizedImage.TryGetReadOnlySpan(out colorSpan);

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int rowBase = y * width;
            double normY = y * invHeight;
            bool isRowInBorder = computeBorderRegions && (y < borderPixelHeight || y >= maxY);

            for (int x = 0; x < width; x++)
            {
                int label = hasSpans ? labelSpan[rowBase + x] : labels[x, y];
                Rgb8 color = hasSpans ? colorSpan[rowBase + x] : colorQuantizedImage[x, y];

                var normalizedCoord = new NormalizedCoord(x * invWidth, normY);
                regionCounts[label]++;
                centroidSums[label] += normalizedCoord;
                centerDistanceSums[label] += normalizedCoord.Distance(imageCenter);
                regionHistograms[label].Increment(color);

                if (borderPixelCounts is not null && (isRowInBorder || x < borderPixelWidth || x >= maxX))
                {
                    borderPixelCounts[label]++;
                }
            }
        }

        var regionCentroids = new NormalizedCoord[regionCount];
        var avgRegionDistances = new double[regionCount];
        for (int r = 0; r < regionCount; r++)
        {
            int count = regionCounts[r];
            if (count > 0)
            {
                double invCount = 1.0 / count;
                regionCentroids[r] = centroidSums[r] * invCount;
                avgRegionDistances[r] = centerDistanceSums[r] * invCount;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        PairwiseLabDistanceTable distanceTable = PairwiseLabDistanceTable.Build(rgbToLabLookupTable);

        var processedRegions = new (int Total, (double Density, int DistanceIndex)[] ColorData)[regionCount];
        for (int r = 0; r < regionCount; r++)
        {
            DictionaryHistogram<Rgb8> histogram = regionHistograms[r];
            var colorData = new (double Density, int DistanceIndex)[histogram.BucketCount];
            int idx = 0;
            foreach (Rgb8 bucket in histogram.Buckets)
            {
                colorData[idx++] = (histogram.GetDensity(bucket), distanceTable.GetIndex(bucket));
            }

            processedRegions[r] = (histogram.Total, colorData);
        }

        var rawSaliency = new double[regionCount];
        const double sigmaSquared = 0.4;
        double centerPriorWeight = resolvedOptions.CenterPriorWeight;
        ParallelOptions? parallelOptions = cancellationToken.CanBeCanceled
            ? new ParallelOptions { CancellationToken = cancellationToken }
            : null;

        void ComputeRegionContrast(int r)
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
                    ReadOnlySpan<double> distanceRow = distanceTable.GetRow(colorData[c].DistanceIndex);
                    for (int oc = 0; oc < otherColorData.Length; oc++)
                    {
                        distance += bucketDensity * otherColorData[oc].Density * distanceRow[otherColorData[oc].DistanceIndex];
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
        }

        if (parallelOptions is not null)
        {
            Parallel.For(0, regionCount, parallelOptions, ComputeRegionContrast);
        }
        else
        {
            Parallel.For(0, regionCount, ComputeRegionContrast);
        }

        double[] finalSaliency;
        if (enableSmoothing)
        {
            int colorCount = distanceTable.Count;
            var colorSaliencies = new double[colorCount];
            var colorPixelCounts = new int[colorCount];

            for (int y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    int label = hasSpans ? labelSpan[rowBase + x] : labels[x, y];
                    Rgb8 color = hasSpans ? colorSpan[rowBase + x] : colorQuantizedImage[x, y];
                    int colorIndex = distanceTable.GetIndex(color);
                    colorSaliencies[colorIndex] += rawSaliency[label];
                    colorPixelCounts[colorIndex]++;
                }
            }

            for (int c = 0; c < colorCount; c++)
            {
                if (colorPixelCounts[c] > 0)
                {
                    colorSaliencies[c] /= colorPixelCounts[c];
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            SaliencyColorSmoother.SmoothColorSaliency(colorSaliencies, distanceTable, neighborDivisor: 10);

            finalSaliency = new double[regionCount];

            void ProjectSmoothedRegion(int r)
            {
                var colorData = processedRegions[r].ColorData;
                double regionSaliency = 0.0;
                for (int c = 0; c < colorData.Length; c++)
                {
                    regionSaliency += colorData[c].Density * colorSaliencies[colorData[c].DistanceIndex];
                }

                finalSaliency[r] = regionSaliency;
            }

            if (parallelOptions is not null)
            {
                Parallel.For(0, regionCount, parallelOptions, ProjectSmoothedRegion);
            }
            else
            {
                Parallel.For(0, regionCount, ProjectSmoothedRegion);
            }
        }
        else
        {
            finalSaliency = rawSaliency;
        }

        if (borderPixelCounts is not null)
        {
            for (int r = 0; r < regionCount; r++)
            {
                if (borderPixelCounts[r] > regionCounts[r] * regionInBorderPercentage)
                {
                    finalSaliency[r] = 0.0;
                }
            }
        }

        if (!SaliencyColorSmoother.TryGetMinMaxRange(finalSaliency.AsSpan(), out double minSaliency, out double saliencyRange))
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return labels.Map<int, DoublePixel>(label => (finalSaliency[label] - minSaliency) / saliencyRange);
    }
}
