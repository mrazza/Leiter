namespace Leiter.Algorithms.Saliency;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
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

    private readonly record struct RegionColorBucket(int Count, double Density, int DistanceIndex);

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

        PairwiseLabDistanceTable distanceTable = PairwiseLabDistanceTable.Build(rgbToLabLookupTable);
        cancellationToken.ThrowIfCancellationRequested();

        var regionCounts = new int[regionCount];
        var regionCentroids = new NormalizedCoord[regionCount];
        var avgRegionDistances = new double[regionCount];
        var borderPixelCounts = computeBorderRegions ? new int[regionCount] : null;
        var processedRegions = new (int Total, RegionColorBucket[] ColorData)[regionCount];

        const double regionInBorderPercentage = 0.1;
        const double borderPercentage = 0.025;
        double borderPixelWidth = width * borderPercentage;
        double borderPixelHeight = height * borderPercentage;
        double maxX = width - borderPixelWidth;
        double maxY = height - borderPixelHeight;
        var imageCenter = new NormalizedCoord(0.5, 0.5);
        double invWidth = 1.0 / width;
        double invHeight = 1.0 / height;

        Rgb8[]? colorRaw = colorQuantizedImage is SequentialMatrix<Rgb8> seqColors ? seqColors.RawData : null;
        int colorCount = distanceTable.Count;

        ParallelRunner.For(regionCount, cancellationToken, r =>
        {
            HashSet<Coord> pixels = graph.Regions[r].Pixels;
            int count = pixels.Count;
            regionCounts[r] = count;
            if (count == 0)
            {
                processedRegions[r] = (0, []);
                return;
            }

            NormalizedCoord centroidSum = default;
            double centerDistanceSum = 0.0;
            int borderCount = 0;
            var localHist = new Dictionary<int, int>();

            foreach (Coord coord in pixels)
            {
                Rgb8 color = colorRaw is not null
                    ? colorRaw[(coord.Y * width) + coord.X]
                    : colorQuantizedImage[coord.X, coord.Y];

                int colorIndex = distanceTable.GetIndex(color);
                ref int bucketCount = ref CollectionsMarshal.GetValueRefOrAddDefault(localHist, colorIndex, out _);
                bucketCount++;

                var normalizedCoord = new NormalizedCoord(coord.X * invWidth, coord.Y * invHeight);
                centroidSum += normalizedCoord;
                centerDistanceSum += normalizedCoord.Distance(imageCenter);

                if (borderPixelCounts is not null &&
                    (coord.Y < borderPixelHeight || coord.Y >= maxY || coord.X < borderPixelWidth || coord.X >= maxX))
                {
                    borderCount++;
                }
            }

            double invCount = 1.0 / count;
            regionCentroids[r] = centroidSum * invCount;
            avgRegionDistances[r] = centerDistanceSum * invCount;
            if (borderPixelCounts is not null)
                borderPixelCounts[r] = borderCount;

            var colorData = new RegionColorBucket[localHist.Count];
            int idx = 0;
            foreach (var kvp in localHist)
                colorData[idx++] = new RegionColorBucket(kvp.Value, kvp.Value * invCount, kvp.Key);

            processedRegions[r] = (count, colorData);
        });

        cancellationToken.ThrowIfCancellationRequested();

        var rawSaliency = new double[regionCount];
        const double sigmaSquared = 0.4;
        double centerPriorWeight = resolvedOptions.CenterPriorWeight;

        ParallelRunner.For(regionCount, cancellationToken, r =>
        {
            NormalizedCoord currCentroid = regionCentroids[r];
            RegionColorBucket[] colorData = processedRegions[r].ColorData;
            double currSaliency = 0.0;

            for (int other = 0; other < regionCount; other++)
            {
                if (r == other)
                    continue;

                var (otherTotal, otherColorData) = processedRegions[other];
                double distance = 0.0;

                for (int c = 0; c < colorData.Length; c++)
                {
                    double bucketDensity = colorData[c].Density;
                    ReadOnlySpan<double> distanceRow = distanceTable.GetRow(colorData[c].DistanceIndex);
                    for (int oc = 0; oc < otherColorData.Length; oc++)
                        distance += bucketDensity * otherColorData[oc].Density * distanceRow[otherColorData[oc].DistanceIndex];
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

        double[] finalSaliency;
        if (enableSmoothing)
        {
            var colorSaliencies = new double[colorCount];
            var colorPixelCounts = new int[colorCount];

            // Aggregate per-color raw saliency directly across processed region histograms (O(R * B) instead of O(W * H)).
            for (int r = 0; r < regionCount; r++)
            {
                double regionSaliency = rawSaliency[r];
                RegionColorBucket[] colorData = processedRegions[r].ColorData;
                for (int c = 0; c < colorData.Length; c++)
                {
                    RegionColorBucket entry = colorData[c];
                    colorSaliencies[entry.DistanceIndex] += regionSaliency * entry.Count;
                    colorPixelCounts[entry.DistanceIndex] += entry.Count;
                }
            }

            for (int c = 0; c < colorCount; c++)
            {
                if (colorPixelCounts[c] > 0)
                    colorSaliencies[c] /= colorPixelCounts[c];
            }

            cancellationToken.ThrowIfCancellationRequested();
            SaliencyColorSmoother.SmoothColorSaliency(colorSaliencies, distanceTable, neighborDivisor: 10);

            finalSaliency = new double[regionCount];
            ParallelRunner.For(regionCount, cancellationToken, r =>
            {
                RegionColorBucket[] colorData = processedRegions[r].ColorData;
                double regionSaliency = 0.0;
                for (int c = 0; c < colorData.Length; c++)
                    regionSaliency += colorData[c].Density * colorSaliencies[colorData[c].DistanceIndex];

                finalSaliency[r] = regionSaliency;
            });
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
                    finalSaliency[r] = 0.0;
            }
        }

        if (!SaliencyColorSmoother.TryGetMinMaxRange(finalSaliency.AsSpan(), out double minSaliency, out double saliencyRange))
            return new SequentialMatrix<DoublePixel>(image.Size);

        cancellationToken.ThrowIfCancellationRequested();
        var normalizedRegionSaliency = new DoublePixel[regionCount];
        for (int r = 0; r < regionCount; r++)
            normalizedRegionSaliency[r] = (finalSaliency[r] - minSaliency) / saliencyRange;

        return labels.Map<int, DoublePixel>(label => normalizedRegionSaliency[label]);
    }
}
