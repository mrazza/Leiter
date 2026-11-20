namespace Leiter.Algorithms.Saliency;

using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;

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
        Guard.ThrowIfSizeMismatch(image, saliencyMap);

        SaliencyCutOptions resolvedOptions = OptionsResolver.Resolve(options);

        if (image.Count == 0)
            return new MaskMatrix(image.Size);

        if (resolvedOptions.MaxWorkingDimension is int maxDimension &&
            Math.Max(image.Width, image.Height) > maxDimension)
        {
            return ExtractAtScaledResolution(image, saliencyMap, resolvedOptions, maxDimension);
        }

        return ExtractCore(image, saliencyMap, resolvedOptions);
    }

    private static MaskMatrix ExtractAtScaledResolution(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        SaliencyCutOptions options,
        int maxDimension)
    {
        double scale = (double)maxDimension / Math.Max(image.Width, image.Height);
        var workingSize = new Size(
            OptionTuning.RoundPositiveInt(image.Width * scale),
            OptionTuning.RoundPositiveInt(image.Height * scale));

        Matrix<Rgb8> smallImage = image.ResizeBox(workingSize);
        Matrix<DoublePixel> smallSaliency = saliencyMap.ResizeBox(workingSize);
        int scaledBandRadius = OptionTuning.RoundPositiveInt(options.TrimapBandRadius * scale);

        GrabCutOptions grabCutOptions = options.GrabCut ?? SaliencyCutOptions.DefaultGrabCut;
        GrabCutOptions scaledGrabCut = grabCutOptions with
        {
            SmoothnessWeight = OptionTuning.ScaleFiniteNonNegative(grabCutOptions.SmoothnessWeight * scale)
        };
        SaliencyCutOptions workingOptions = options with
        {
            TrimapBandRadius = scaledBandRadius,
            GrabCut = scaledGrabCut
        };

        MaskMatrix smallMask = ExtractCore(smallImage, smallSaliency, workingOptions);
        return smallMask.ResizeMaskNearest(image.Size);
    }

    private static MaskMatrix BinarizeInitialSeed(
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        SaliencyCutOptions options,
        out int foregroundCount)
    {
        SaliencyMaskOptions maskOptions = options.Mask ?? SaliencyCutOptions.DefaultMask;
        MaskMatrix candidate = SaliencyMask.Binarize(saliencyMap, maskOptions);

        foregroundCount = candidate.CountTrue();
        if ((foregroundCount == 0 || foregroundCount == saliencyMap.Count) && maskOptions.Method != SaliencyThresholdMethod.Otsu)
        {
            candidate = SaliencyMask.Binarize(saliencyMap, maskOptions with { Method = SaliencyThresholdMethod.Otsu });
            foregroundCount = candidate.CountTrue();
        }

        return candidate;
    }

    private static MaskMatrix ExtractCore(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        SaliencyCutOptions options)
    {
        MaskMatrix candidateMask = BinarizeInitialSeed(saliencyMap, options, out int seedFgCount);
        if (seedFgCount == 0 || seedFgCount == image.Count)
            return candidateMask;

        GrabCutOptions grabCutOptions = options.GrabCut ?? SaliencyCutOptions.DefaultGrabCut;
        SaliencyMaskOptions maskOptions = options.Mask ?? SaliencyCutOptions.DefaultMask;
        int bandRadius = options.TrimapBandRadius;

        TrimapMatrix trimap = TrimapMatrix.FromCandidateMask(candidateMask);
        var session = GrabCut.CreateSession(image, grabCutOptions);
        MaskMatrix? previousMask = null;

        for (int outer = 0; outer < options.MaxOuterIterations; outer++)
        {
            MaskMatrix rawMask = session.Run(trimap);
            MaskMatrix currentMask = BinaryMorphology.FilterConnectedComponents(
                rawMask,
                maskOptions.RetentionPolicy,
                maskOptions.MinAreaFraction,
                maskOptions.Connectivity);

            int currentFgCount = currentMask.CountTrue();
            if (currentFgCount == 0 && previousMask is null)
            {
                currentMask = candidateMask;
                currentFgCount = seedFgCount;
            }

            if (currentFgCount == 0 || currentFgCount == image.Count)
                return previousMask ?? currentMask;

            if (previousMask is not null && previousMask.ContentEquals(currentMask))
                return currentMask;

            previousMask = currentMask;

            if (outer == options.MaxOuterIterations - 1)
                break;

            if (!TryBuildRefinedTrimap(currentMask, bandRadius, out TrimapMatrix? nextTrimap))
                return currentMask;

            trimap = nextTrimap!;
        }

        return previousMask!;
    }

    private static bool TryBuildRefinedTrimap(
        MaskMatrix currentMask,
        int bandRadius,
        out TrimapMatrix? refinedTrimap)
    {
        MaskMatrix dilated = BinaryMorphology.Dilate(currentMask, bandRadius, StructuringElementShape.Disk);
        if (dilated.CountTrue() == currentMask.Count)
        {
            refinedTrimap = null;
            return false;
        }

        int erodeRadius = Math.Max(1, bandRadius / 4);
        MaskMatrix eroded = BinaryMorphology.Erode(currentMask, erodeRadius, StructuringElementShape.Disk);
        refinedTrimap = TrimapMatrix.FromConfidenceBands(currentMask, eroded, dilated);
        return true;
    }
}
