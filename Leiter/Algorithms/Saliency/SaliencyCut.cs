namespace Leiter.Algorithms.Saliency;

using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for Cheng et al.'s iterative <see cref="SaliencyCut"/> salient object segmentation.
/// </summary>
/// <param name="InitialThreshold">Initial fixed threshold in [0, 1] used on the saliency map (default <c>70 / 255</c> per Cheng et al. Sec. 5.1).</param>
/// <param name="MaxOuterIterations">Maximum number of trimap dilation/erosion refinement iterations (<c>≥ 1</c>).</param>
/// <param name="InnerGrabCutIterations">Number of GMM/min-cut iterations executed per outer trimap step (<c>≥ 1</c>).</param>
/// <param name="TrimapBandRadius">Morphological dilation/erosion radius in pixels used to construct the unknown boundary band (<c>≥ 1</c>).</param>
/// <param name="MixtureComponentCount">Number of Gaussian components per foreground/background GMM (<c>≥ 1</c>).</param>
/// <param name="SmoothnessWeight">Contrast-sensitive pairwise boundary smoothness weight γ (<c>≥ 0</c>).</param>
/// <param name="RetentionPolicy">Connected-component cleanup policy applied to initial and refined masks.</param>
/// <param name="MinAreaFraction">Minimum component area fraction in [0, 1] when <paramref name="RetentionPolicy"/> is <see cref="ComponentRetentionPolicy.MinimumAreaFraction"/>.</param>
/// <param name="MaxWorkingDimension">Optional maximum width or height for working resolution; <see langword="null"/> runs at full native resolution.</param>
public sealed record SaliencyCutOptions(
    double InitialThreshold = 70.0 / 255.0,
    int MaxOuterIterations = 4,
    int InnerGrabCutIterations = 2,
    int TrimapBandRadius = 10,
    int MixtureComponentCount = 5,
    double SmoothnessWeight = 50.0,
    ComponentRetentionPolicy RetentionPolicy = ComponentRetentionPolicy.MinimumAreaFraction,
    double MinAreaFraction = 0.01,
    int? MaxWorkingDimension = null)
{
    internal void Validate()
    {
        if (!double.IsFinite(InitialThreshold) || InitialThreshold < 0.0 || InitialThreshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(InitialThreshold), InitialThreshold, "InitialThreshold must be finite and within [0, 1].");
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxOuterIterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(InnerGrabCutIterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(TrimapBandRadius, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MixtureComponentCount, 1);
        if (!double.IsFinite(SmoothnessWeight) || SmoothnessWeight < 0.0)
            throw new ArgumentOutOfRangeException(nameof(SmoothnessWeight), SmoothnessWeight, "SmoothnessWeight must be finite and non-negative.");
        if (!Enum.IsDefined(RetentionPolicy))
            throw new ArgumentOutOfRangeException(nameof(RetentionPolicy), RetentionPolicy, "Unsupported component retention policy.");
        if (!double.IsFinite(MinAreaFraction) || MinAreaFraction < 0.0 || MinAreaFraction > 1.0)
            throw new ArgumentOutOfRangeException(nameof(MinAreaFraction), MinAreaFraction, "MinAreaFraction must be finite and within [0, 1].");
        if (MaxWorkingDimension is int maxDim)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDim, 2, nameof(MaxWorkingDimension));
    }
}

/// <summary>
/// Implements Cheng et al.'s iterative GrabCut-based unsupervised salient object extraction (<c>SaliencyCut</c>).
/// </summary>
public static class SaliencyCut
{
    /// <summary>
    /// Extracts a boolean foreground mask from an image and its continuous [0, 1] saliency map
    /// using iterative trimap dilation/erosion refinement and GrabCut optimization.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="saliencyMap">The continuous [0, 1] saliency map matching <paramref name="image"/> dimensions.</param>
    /// <param name="options">Optional tuning parameters (defaults to full-resolution Cheng et al. parameters with multi-object area retention).</param>
    /// <returns>A <see cref="MaskMatrix"/> with <see langword="true"/> for extracted salient object pixels and <see langword="false"/> for background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="saliencyMap"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="saliencyMap"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> contains invalid values.</exception>
    public static MaskMatrix Extract(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        SaliencyCutOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(saliencyMap);

        if (image.Size != saliencyMap.Size)
            throw new ArgumentException("Image and saliency map dimensions must match.", nameof(saliencyMap));

        SaliencyCutOptions resolvedOptions = options ?? new SaliencyCutOptions();
        resolvedOptions.Validate();

        if (image.Count == 0)
            return new MaskMatrix(image.Size);

        if (resolvedOptions.MaxWorkingDimension is int maxDimension &&
            Math.Max(image.Width, image.Height) > maxDimension)
        {
            double scale = (double)maxDimension / Math.Max(image.Width, image.Height);
            int workingWidth = Math.Max(1, (int)Math.Round(image.Width * scale));
            int workingHeight = Math.Max(1, (int)Math.Round(image.Height * scale));
            var workingSize = new Size(workingWidth, workingHeight);

            Matrix<Rgb8> smallImage = DownsampleBilinear(image, workingSize);
            Matrix<DoublePixel> smallSaliency = DownsampleBilinear(saliencyMap, workingSize);
            int scaledBandRadius = Math.Max(1, (int)Math.Round(resolvedOptions.TrimapBandRadius * scale));

            MaskMatrix smallMask = ExtractCore(smallImage, smallSaliency, resolvedOptions, scaledBandRadius);
            return UpsampleMaskNearest(smallMask, image.Size);
        }

        return ExtractCore(image, saliencyMap, resolvedOptions, resolvedOptions.TrimapBandRadius);
    }

    private static MaskMatrix ExtractCore(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        SaliencyCutOptions options,
        int bandRadius)
    {
        MaskMatrix candidateMask = SaliencyMask.Binarize(
            saliencyMap,
            SaliencyThresholdMethod.Fixed,
            options.InitialThreshold,
            options.RetentionPolicy,
            options.MinAreaFraction);

        int fgCount = candidateMask.CountTrue();
        if (fgCount == 0 || fgCount == image.Count)
        {
            candidateMask = SaliencyMask.Binarize(
                saliencyMap,
                SaliencyThresholdMethod.Otsu,
                options.InitialThreshold,
                options.RetentionPolicy,
                options.MinAreaFraction);

            fgCount = candidateMask.CountTrue();
            if (fgCount == 0 || fgCount == image.Count)
                return candidateMask;
        }

        var trimap = new TrimapMatrix(image.Size);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                if (candidateMask[x, y])
                    trimap[x, y] = TrimapLabel.ProbableForeground;
            }
        }

        var session = new GrabCutSession(image, options.MixtureComponentCount, options.SmoothnessWeight);
        MaskMatrix? previousMask = null;

        for (int outer = 0; outer < options.MaxOuterIterations; outer++)
        {
            MaskMatrix rawMask = session.Run(trimap, options.InnerGrabCutIterations);
            MaskMatrix currentMask = SaliencyMask.FilterConnectedComponents(
                rawMask,
                options.RetentionPolicy,
                options.MinAreaFraction);

            int currentFgCount = currentMask.CountTrue();
            if (currentFgCount == 0 && previousMask is null)
            {
                currentMask = candidateMask;
                currentFgCount = fgCount;
            }

            if (currentFgCount == 0 || currentFgCount == image.Count)
                return previousMask ?? currentMask;

            if (previousMask is not null && MasksEqual(previousMask, currentMask))
                return currentMask;

            previousMask = currentMask;

            if (outer == options.MaxOuterIterations - 1)
                break;

            MaskMatrix dilated = BinaryMorphology.Dilate(currentMask, bandRadius, StructuringElementShape.Disk);
            if (dilated.CountTrue() == image.Count)
                return currentMask;

            int erodeRadius = Math.Clamp(bandRadius / 4, 1, 3);
            MaskMatrix eroded = BinaryMorphology.Erode(currentMask, erodeRadius, StructuringElementShape.Disk);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    if (!dilated[x, y])
                        trimap[x, y] = TrimapLabel.Background;
                    else if (eroded[x, y])
                        trimap[x, y] = TrimapLabel.Foreground;
                    else if (currentMask[x, y])
                        trimap[x, y] = TrimapLabel.ProbableForeground;
                    else
                        trimap[x, y] = TrimapLabel.ProbableBackground;
                }
            }
        }

        return previousMask!;
    }

    private static bool MasksEqual(IReadOnlyMatrix<bool> left, IReadOnlyMatrix<bool> right)
    {
        for (int y = 0; y < left.Height; y++)
        {
            for (int x = 0; x < left.Width; x++)
            {
                if (left[x, y] != right[x, y])
                    return false;
            }
        }
        return true;
    }

    private static Matrix<T> DownsampleBilinear<T>(IReadOnlyMatrix<T> source, Size targetSize)
        where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
    {
        var result = new SequentialMatrix<T>(targetSize);
        int srcWidth = source.Width;
        int srcHeight = source.Height;

        Parallel.For(0, targetSize.Height, y =>
        {
            double srcY = Math.Clamp((((y + 0.5) * srcHeight) / targetSize.Height) - 0.5, 0.0, srcHeight - 1);
            int y0 = (int)Math.Floor(srcY);
            int y1 = Math.Min(srcHeight - 1, y0 + 1);
            double fy = srcY - y0;

            for (int x = 0; x < targetSize.Width; x++)
            {
                double srcX = Math.Clamp((((x + 0.5) * srcWidth) / targetSize.Width) - 0.5, 0.0, srcWidth - 1);
                int x0 = (int)Math.Floor(srcX);
                int x1 = Math.Min(srcWidth - 1, x0 + 1);
                double fx = srcX - x0;

                double w00 = (1.0 - fx) * (1.0 - fy);
                double w10 = fx * (1.0 - fy);
                double w01 = (1.0 - fx) * fy;
                double w11 = fx * fy;

                var blended =
                    (source[x0, y0].ToDoubleVector() * w00) +
                    (source[x1, y0].ToDoubleVector() * w10) +
                    (source[x0, y1].ToDoubleVector() * w01) +
                    (source[x1, y1].ToDoubleVector() * w11);

                result[x, y] = T.FromDoubleVector(blended);
            }
        });

        return result;
    }

    private static MaskMatrix UpsampleMaskNearest(IReadOnlyMatrix<bool> source, Size targetSize)
    {
        var result = new MaskMatrix(targetSize);
        int srcWidth = source.Width;
        int srcHeight = source.Height;

        Parallel.For(0, targetSize.Height, y =>
        {
            int srcY = Math.Clamp((int)((long)y * srcHeight / targetSize.Height), 0, srcHeight - 1);
            for (int x = 0; x < targetSize.Width; x++)
            {
                int srcX = Math.Clamp((int)((long)x * srcWidth / targetSize.Width), 0, srcWidth - 1);
                result[x, y] = source[srcX, srcY];
            }
        });

        return result;
    }
}
