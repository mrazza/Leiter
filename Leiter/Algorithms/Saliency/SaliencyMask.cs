namespace Leiter.Algorithms.Saliency;

using Leiter.Algorithms.DataStructures;
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
        double fixedThreshold = 70.0 / 255.0)
    {
        ArgumentNullException.ThrowIfNull(saliencyMap);
        if (!Enum.IsDefined(method))
            throw new ArgumentOutOfRangeException(nameof(method), method, "Unsupported saliency threshold method.");
        if (!double.IsFinite(fixedThreshold) || fixedThreshold < 0.0 || fixedThreshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(fixedThreshold), fixedThreshold, "Threshold must be finite and within [0, 1].");

        if (saliencyMap.Count == 0)
            return 0.0;

        return method switch
        {
            SaliencyThresholdMethod.Fixed => fixedThreshold,
            SaliencyThresholdMethod.AdaptiveMean => ComputeAdaptiveMeanThreshold(saliencyMap),
            _ => ComputeOtsuThreshold(saliencyMap),
        };
    }

    /// <summary>
    /// Thresholds a continuous [0, 1] saliency map and filters connected foreground components into a boolean mask.
    /// </summary>
    /// <param name="saliencyMap">The continuous [0, 1] saliency map.</param>
    /// <param name="method">The thresholding strategy.</param>
    /// <param name="fixedThreshold">The threshold in [0, 1] used when <paramref name="method"/> is <see cref="SaliencyThresholdMethod.Fixed"/>.</param>
    /// <param name="retentionPolicy">The connected-component filtering policy.</param>
    /// <param name="minAreaFraction">The minimum component area fraction in [0, 1] used by <see cref="ComponentRetentionPolicy.MinimumAreaFraction"/>.</param>
    /// <returns>A <see cref="MaskMatrix"/> with <see langword="true"/> for salient foreground pixels and <see langword="false"/> for background pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="saliencyMap"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is outside its valid range.</exception>
    public static MaskMatrix Binarize(
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        SaliencyThresholdMethod method = SaliencyThresholdMethod.Otsu,
        double fixedThreshold = 70.0 / 255.0,
        ComponentRetentionPolicy retentionPolicy = ComponentRetentionPolicy.MinimumAreaFraction,
        double minAreaFraction = 0.01)
    {
        double threshold = ComputeThreshold(saliencyMap, method, fixedThreshold);
        ValidateRetentionArgs(retentionPolicy, minAreaFraction);

        MaskMatrix rawMask = saliencyMap.ToMaskMatrix(pixel => ClampUnit(pixel.Value) > threshold);
        return FilterConnectedComponents(rawMask, retentionPolicy, minAreaFraction);
    }

    /// <summary>
    /// Filters 8-connected foreground components in a boolean mask according to the specified retention policy.
    /// </summary>
    /// <param name="mask">The boolean mask (<see langword="false"/> = background, <see langword="true"/> = foreground).</param>
    /// <param name="retentionPolicy">The connected-component retention rule.</param>
    /// <param name="minAreaFraction">The minimum fraction of total pixels in [0, 1] required when using <see cref="ComponentRetentionPolicy.MinimumAreaFraction"/>.</param>
    /// <returns>A filtered <see cref="MaskMatrix"/> containing only the retained components.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="retentionPolicy"/> is invalid or <paramref name="minAreaFraction"/> is outside [0, 1].</exception>
    public static MaskMatrix FilterConnectedComponents(
        IReadOnlyMatrix<bool> mask,
        ComponentRetentionPolicy retentionPolicy = ComponentRetentionPolicy.MinimumAreaFraction,
        double minAreaFraction = 0.01)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ValidateRetentionArgs(retentionPolicy, minAreaFraction);

        if (mask.Count == 0)
            return new MaskMatrix(mask.Size);

        if (retentionPolicy == ComponentRetentionPolicy.KeepAll)
            return MaskMatrix.From(mask);

        int width = mask.Width;
        int height = mask.Height;
        var filtered = new MaskMatrix(width, height);
        var isForeground = new bool[mask.Count];
        bool hasAnyForeground = false;

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                if (mask[x, y])
                {
                    isForeground[rowBase + x] = true;
                    hasAnyForeground = true;
                }
            }
        }

        if (!hasAnyForeground)
            return filtered;

        var disjointSet = new DisjointSet<byte>(mask, static _ => 0);

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int currentIndex = rowBase + x;
                if (!isForeground[currentIndex])
                    continue;

                var current = new Coord(x, y);
                foreach (GridNeighbor neighbor in GridNeighborhood.Forward8Connected)
                {
                    Coord other = current + neighbor.Offset;
                    if (!mask.Contains(other))
                        continue;

                    int neighborIndex = (other.Y * width) + other.X;
                    if (!isForeground[neighborIndex])
                        continue;

                    var currentPartition = disjointSet.GetPartition(currentIndex);
                    var neighborPartition = disjointSet.GetPartition(neighborIndex);
                    if (currentPartition.RootIndex != neighborPartition.RootIndex)
                        disjointSet.Union(currentPartition, neighborPartition, 0);
                }
            }
        }

        if (retentionPolicy == ComponentRetentionPolicy.LargestComponentOnly)
        {
            int largestRoot = -1;
            int largestSize = 0;

            for (int i = 0; i < isForeground.Length; i++)
            {
                if (!isForeground[i])
                    continue;

                var partition = disjointSet.GetPartition(i);
                if (partition.Size > largestSize)
                {
                    largestSize = partition.Size;
                    largestRoot = partition.RootIndex;
                }
            }

            for (int i = 0; i < isForeground.Length; i++)
            {
                if (isForeground[i] && disjointSet.GetPartition(i).RootIndex == largestRoot)
                    filtered[i] = true;
            }
        }
        else
        {
            int minPixels = Math.Max(1, (int)Math.Ceiling(minAreaFraction * mask.Count));
            for (int i = 0; i < isForeground.Length; i++)
            {
                if (isForeground[i] && disjointSet.GetPartition(i).Size >= minPixels)
                    filtered[i] = true;
            }
        }

        return filtered;
    }

    private static double ComputeAdaptiveMeanThreshold(IReadOnlyMatrix<DoublePixel> saliencyMap)
    {
        double sum = 0.0;
        for (int y = 0; y < saliencyMap.Height; y++)
        {
            for (int x = 0; x < saliencyMap.Width; x++)
                sum += ClampUnit(saliencyMap[x, y].Value);
        }

        double mean = sum / saliencyMap.Count;
        return Math.Clamp(2.0 * mean, 0.0, 0.95);
    }

    private static double ComputeOtsuThreshold(IReadOnlyMatrix<DoublePixel> saliencyMap)
    {
        Span<int> histogram = stackalloc int[256];
        double totalSum = 0.0;

        for (int y = 0; y < saliencyMap.Height; y++)
        {
            for (int x = 0; x < saliencyMap.Width; x++)
            {
                int bin = (int)Math.Round(ClampUnit(saliencyMap[x, y].Value) * 255.0);
                histogram[bin]++;
                totalSum += bin;
            }
        }

        int totalCount = saliencyMap.Count;
        int backgroundCount = 0;
        double backgroundSum = 0.0;
        double bestVariance = -1.0;
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
            double betweenVariance = (difference * difference) / ((double)backgroundCount * foregroundCount);

            if (betweenVariance > bestVariance + 1e-12)
            {
                bestVariance = betweenVariance;
                bestBinSum = bin;
                bestBinCount = 1;
            }
            else if (Math.Abs(betweenVariance - bestVariance) <= 1e-12)
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

        for (int bin = 0; bin < 256; bin++)
        {
            if (histogram[bin] > 0)
                return bin == 0 ? 1.0 : (bin - 0.5) / 255.0;
        }

        return 0.0;
    }

    private static double ClampUnit(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.0;

    private static void ValidateRetentionArgs(ComponentRetentionPolicy retentionPolicy, double minAreaFraction)
    {
        if (!Enum.IsDefined(retentionPolicy))
            throw new ArgumentOutOfRangeException(nameof(retentionPolicy), retentionPolicy, "Unsupported component retention policy.");
        if (!double.IsFinite(minAreaFraction) || minAreaFraction < 0.0 || minAreaFraction > 1.0)
            throw new ArgumentOutOfRangeException(nameof(minAreaFraction), minAreaFraction, "MinAreaFraction must be finite and within [0, 1].");
    }
}
