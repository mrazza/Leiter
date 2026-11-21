namespace Leiter.Algorithms.Saliency;

using System;
using System.Threading;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Morphology;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Extracts and filters boolean foreground masks from continuous [0, 1] saliency maps.
/// </summary>
public static class SaliencyMask
{
    /// <summary>
    /// Computes a binarization threshold in [0, 1] for the given saliency map using the specified method.
    /// </summary>
    /// <param name="saliencyMap">The continuous [0, 1] saliency map.</param>
    /// <param name="method">The thresholding strategy.</param>
    /// <param name="fixedThreshold">The fixed threshold in [0, 1] used when <paramref name="method"/> is <see cref="SaliencyThresholdMethod.Fixed"/>.</param>
    /// <returns>The computed threshold in [0, 1].</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="saliencyMap"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="method"/> is invalid or <paramref name="fixedThreshold"/> is outside [0, 1].</exception>
    public static double ComputeThreshold(
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        SaliencyThresholdMethod method = SaliencyThresholdMethod.Otsu,
        double fixedThreshold = SaliencyMaskOptions.DefaultFixedThreshold)
    {
        ArgumentNullException.ThrowIfNull(saliencyMap);
        Guard.ThrowIfUndefinedEnum(method);
        Guard.ThrowIfNotFiniteInRange(fixedThreshold, 0.0, 1.0);

        if (saliencyMap.Count == 0)
            return 0.0;

        return method switch
        {
            SaliencyThresholdMethod.Fixed => fixedThreshold,
            SaliencyThresholdMethod.AdaptiveMean => ComputeAdaptiveMeanThreshold(saliencyMap),
            _ => ComputeOtsuThreshold(saliencyMap),
        };
    }

    private delegate void SpanVisitor<TState>(scoped ReadOnlySpan<DoublePixel> span, ref TState state)
        where TState : allows ref struct;

    private ref struct OtsuHistogramAccumulator(Span<int> histogram)
    {
        public Span<int> Histogram = histogram;
        public double TotalSum;
    }

    /// <summary>
    /// Thresholds a continuous [0, 1] saliency map and filters connected foreground components into a boolean mask.
    /// </summary>
    /// <param name="saliencyMap">The continuous [0, 1] saliency map.</param>
    /// <param name="options">Optional thresholding and connected-component filtering parameters.</param>
    /// <returns>A <see cref="MaskMatrix"/> with <see langword="true"/> for salient foreground pixels and <see langword="false"/> for background pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="saliencyMap"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter in <paramref name="options"/> is outside its valid range.</exception>
    public static MaskMatrix Binarize(
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        SaliencyMaskOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(saliencyMap);

        SaliencyMaskOptions resolvedOptions = OptionsResolver.Resolve(options);

        double threshold = ComputeThreshold(saliencyMap, resolvedOptions.Method, resolvedOptions.FixedThreshold);
        MaskMatrix rawMask = saliencyMap.ToMaskMatrix(pixel => ScalarMath.ClampUnitOrDefault(pixel.Value) > threshold);
        return BinaryMorphology.FilterConnectedComponents(
            rawMask,
            resolvedOptions.RetentionPolicy,
            resolvedOptions.MinAreaFraction,
            resolvedOptions.Connectivity);
    }

    private static double ComputeAdaptiveMeanThreshold(IReadOnlyMatrix<DoublePixel> saliencyMap)
    {
        double sum = 0.0;
        if (saliencyMap is SequentialMatrix<DoublePixel> sequential &&
            saliencyMap.Count >= ParallelRunner.DefaultMinParallelCount)
        {
            DoublePixel[] raw = sequential.RawData;
            double[] chunkSums = ParallelRunner.MapChunks(
                raw.Length,
                CancellationToken.None,
                (start, end) =>
                {
                    double local = 0.0;
                    for (int i = start; i < end; i++)
                        local += ScalarMath.ClampUnitOrDefault(raw[i].Value);
                    return local;
                });

            for (int i = 0; i < chunkSums.Length; i++)
                sum += chunkSums[i];
        }
        else
        {
            ForEachSpan(
                saliencyMap,
                ref sum,
                static (scoped ReadOnlySpan<DoublePixel> span, ref double acc) =>
                {
                    for (int i = 0; i < span.Length; i++)
                        acc += ScalarMath.ClampUnitOrDefault(span[i].Value);
                });
        }

        double mean = sum / saliencyMap.Count;
        return Math.Clamp(2.0 * mean, 0.0, 0.95);
    }

    private static double ComputeOtsuThreshold(IReadOnlyMatrix<DoublePixel> saliencyMap)
    {
        Span<int> histogram = stackalloc int[256];
        histogram.Clear();
        double totalSum = 0.0;

        if (saliencyMap is SequentialMatrix<DoublePixel> sequential &&
            saliencyMap.Count >= ParallelRunner.DefaultMinParallelCount)
        {
            DoublePixel[] raw = sequential.RawData;
            int[][] chunkHistograms = ParallelRunner.MapChunks(
                raw.Length,
                CancellationToken.None,
                (start, end) =>
                {
                    var local = new int[256];
                    for (int i = start; i < end; i++)
                        local[ToHistogramBin(raw[i].Value)]++;
                    return local;
                });

            for (int c = 0; c < chunkHistograms.Length; c++)
            {
                int[] local = chunkHistograms[c];
                for (int bin = 0; bin < 256; bin++)
                    histogram[bin] += local[bin];
            }

            for (int bin = 0; bin < 256; bin++)
                totalSum += (double)bin * histogram[bin];
        }
        else
        {
            var accumulator = new OtsuHistogramAccumulator(histogram);
            ForEachSpan(
                saliencyMap,
                ref accumulator,
                static (scoped ReadOnlySpan<DoublePixel> span, ref OtsuHistogramAccumulator acc) =>
                {
                    for (int i = 0; i < span.Length; i++)
                    {
                        int bin = ToHistogramBin(span[i].Value);
                        acc.Histogram[bin]++;
                        acc.TotalSum += bin;
                    }
                });
            totalSum = accumulator.TotalSum;
        }

        int totalCount = saliencyMap.Count;
        double meanBin = totalSum / totalCount;
        double totalVarianceSum = 0.0;
        for (int bin = 0; bin < 256; bin++)
        {
            int count = histogram[bin];
            if (count > 0)
            {
                double delta = bin - meanBin;
                totalVarianceSum += count * delta * delta;
            }
        }

        if (totalVarianceSum > 0.0)
        {
            int backgroundCount = 0;
            double backgroundSum = 0.0;
            double bestNormalizedVariance = -1.0;
            long bestBinSum = 0;
            int bestBinCount = 0;

            for (int bin = 0; bin < 255; bin++)
            {
                backgroundCount += histogram[bin];
                if (backgroundCount == 0)
                    continue;

                int foregroundCount = totalCount - backgroundCount;
                if (foregroundCount == 0)
                    break;

                backgroundSum += (double)bin * histogram[bin];
                double difference = ((double)totalCount * backgroundSum) - ((double)backgroundCount * totalSum);
                double denominator = (double)totalCount * backgroundCount * foregroundCount * totalVarianceSum;
                double normalizedBetweenVariance = (difference * difference) / denominator;

                if (normalizedBetweenVariance > bestNormalizedVariance + 1e-12)
                {
                    bestNormalizedVariance = normalizedBetweenVariance;
                    bestBinSum = bin;
                    bestBinCount = 1;
                }
                else if (Math.Abs(normalizedBetweenVariance - bestNormalizedVariance) <= 1e-12)
                {
                    bestBinSum += bin;
                    bestBinCount++;
                }
            }

            if (bestBinCount > 0)
            {
                double meanBestBin = (double)bestBinSum / bestBinCount;
                return (meanBestBin + 0.5) / 255.0;
            }
        }

        for (int bin = 0; bin < 256; bin++)
        {
            if (histogram[bin] > 0)
                return bin == 0 ? 1.0 : (bin - 0.5) / 255.0;
        }

        return 0.0;
    }

    private static void ForEachSpan<TState>(
        IReadOnlyMatrix<DoublePixel> matrix,
        ref TState state,
        SpanVisitor<TState> visitor)
        where TState : allows ref struct
    {
        if (matrix.TryGetReadOnlySpan(out ReadOnlySpan<DoublePixel> span))
        {
            visitor(span, ref state);
            return;
        }

        const int chunkCapacity = 256;
        Span<DoublePixel> chunk = stackalloc DoublePixel[chunkCapacity];
        int chunkLength = 0;
        int width = matrix.Width;
        int height = matrix.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                chunk[chunkLength++] = matrix[x, y];
                if (chunkLength == chunkCapacity)
                {
                    visitor(chunk, ref state);
                    chunkLength = 0;
                }
            }
        }

        if (chunkLength > 0)
            visitor(chunk[..chunkLength], ref state);
    }

    private static int ToHistogramBin(double value) =>
        (int)Math.Round(ScalarMath.ClampUnitOrDefault(value) * 255.0);
}
