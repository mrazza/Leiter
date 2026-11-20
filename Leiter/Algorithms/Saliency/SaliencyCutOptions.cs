namespace Leiter.Algorithms.Saliency;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for Cheng et al.'s iterative <see cref="SaliencyCut"/> salient object segmentation.
/// </summary>
/// <param name="MaxOuterIterations">Maximum number of trimap dilation/erosion refinement iterations (<c>≥ 1</c>).</param>
/// <param name="TrimapBandRadius">Morphological dilation/erosion radius in pixels used to construct the unknown boundary band (<c>≥ 1</c>).</param>
/// <param name="GrabCut">Optional inner GrabCut solver parameters; when <see langword="null"/>, defaults to <see cref="DefaultGrabCut"/> (<c>Iterations = 2</c>, <c>MixtureComponentCount = 5</c>, <c>SmoothnessWeight = 50.0</c>).</param>
/// <param name="Mask">Optional initial binarization and connected-component retention parameters; when <see langword="null"/>, defaults to <see cref="DefaultMask"/> (fixed threshold <c>70 / 255</c> and <see cref="ComponentRetentionPolicy.MinimumAreaFraction"/> with <c>0.01</c>).</param>
/// <param name="MaxWorkingDimension">Optional maximum width or height for working resolution; <see langword="null"/> runs at full native resolution.</param>
public sealed record SaliencyCutOptions(
    int MaxOuterIterations = 4,
    int TrimapBandRadius = 10,
    GrabCutOptions? GrabCut = null,
    SaliencyMaskOptions? Mask = null,
    int? MaxWorkingDimension = null) : ISizeTunableOptions<SaliencyCutOptions>, IProfileTunableOptions<SaliencyCutOptions>
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default band radii and smoothness weights apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default inner GrabCut options used when <c>GrabCut</c> is omitted or <see langword="null"/>.
    /// </summary>
    public static GrabCutOptions DefaultGrabCut { get; } = GrabCutOptions.Default with { Iterations = 2 };

    /// <summary>
    /// Gets the default initial binarization and component retention options used when <c>Mask</c> is omitted or <see langword="null"/>.
    /// </summary>
    public static SaliencyMaskOptions DefaultMask { get; } = SaliencyMaskOptions.Default with { Method = SaliencyThresholdMethod.Fixed };

    /// <summary>
    /// Gets the default <see cref="SaliencyCutOptions"/> instance.
    /// </summary>
    public static SaliencyCutOptions Default { get; } = new();

    /// <summary>
    /// Gets the inner GrabCut solver parameters (defaults to <see cref="DefaultGrabCut"/>).
    /// </summary>
    public GrabCutOptions GrabCut
    {
        get;
        init => field = value ?? DefaultGrabCut;
    } = GrabCut ?? DefaultGrabCut;

    /// <summary>
    /// Gets the initial binarization and connected-component retention parameters (defaults to <see cref="DefaultMask"/>).
    /// </summary>
    public SaliencyMaskOptions Mask
    {
        get;
        init => field = value ?? DefaultMask;
    } = Mask ?? DefaultMask;

    /// <summary>
    /// Creates <see cref="SaliencyCutOptions"/> with trimap band radius and inner GrabCut smoothness scaled linearly for full-resolution execution on <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="SaliencyCutOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static SaliencyCutOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="SaliencyCutOptions"/> by scaling <paramref name="baseline"/> trimap band radius and inner GrabCut smoothness
    /// linearly for <paramref name="size"/> in full-resolution coordinates while preserving <see cref="MaxWorkingDimension"/>.
    /// </summary>
    /// <remarks>
    /// When <see cref="MaxWorkingDimension"/> is non-null and exceeded by an input image at execution time,
    /// <see cref="SaliencyCut.Extract"/> proportionally rescales both <see cref="TrimapBandRadius"/> and
    /// <see cref="GrabCutOptions.SmoothnessWeight"/> from full-resolution coordinates to the working resolution.
    /// </remarks>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline SaliencyCut configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="SaliencyCutOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static SaliencyCutOptions ForSize(
        Size size,
        SaliencyCutOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateSizeTuning(size, baseline, referenceDimension);
        var (s, _, totalPixels) = OptionTuning.ComputeScalesUnchecked(size, referenceDimension);
        if (totalPixels == 0)
        {
            return baseline;
        }

        return baseline with
        {
            TrimapBandRadius = ScaleTrimapBandRadius(size, baseline.TrimapBandRadius, s),
            GrabCut = GrabCutOptions.ForSize(size, baseline.GrabCut, referenceDimension)
        };
    }

    /// <summary>
    /// Creates <see cref="SaliencyCutOptions"/> tuned for the dimensions and color complexity of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="SaliencyCutOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static SaliencyCutOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage<SaliencyCutOptions>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="SaliencyCutOptions"/> tuned from <paramref name="baseline"/> for the dimensions and color complexity of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="baseline">The baseline SaliencyCut configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="SaliencyCutOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static SaliencyCutOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        SaliencyCutOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage(image, baseline, referenceDimension);

    /// <summary>
    /// Creates <see cref="SaliencyCutOptions"/> tuned from defaults for the pre-analyzed image dimensions and statistics in <paramref name="analysis"/>.
    /// </summary>
    /// <param name="analysis">The cached image analysis profile to tune for.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="SaliencyCutOptions"/> instance tuned for <paramref name="analysis"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static SaliencyCutOptions ForAnalysis(
        ImageAnalysis analysis,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis<SaliencyCutOptions>(analysis, referenceDimension);

    /// <summary>
    /// Creates <see cref="SaliencyCutOptions"/> tuned from <paramref name="baseline"/> for the pre-analyzed image dimensions and statistics in <paramref name="analysis"/>.
    /// </summary>
    /// <param name="analysis">The cached image analysis profile to tune for.</param>
    /// <param name="baseline">The baseline SaliencyCut configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="SaliencyCutOptions"/> instance tuned from <paramref name="baseline"/> for <paramref name="analysis"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static SaliencyCutOptions ForAnalysis(
        ImageAnalysis analysis,
        SaliencyCutOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis(analysis, baseline, referenceDimension);

    static SaliencyCutOptions IProfileTunableOptions<SaliencyCutOptions>.ForProfile(
        in ImageTuningProfile profile,
        SaliencyCutOptions baseline,
        double referenceDimension) =>
        ForProfile(in profile, baseline, referenceDimension);

    internal static SaliencyCutOptions ForProfile(
        in ImageTuningProfile profile,
        SaliencyCutOptions baseline,
        double referenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScalesUnchecked(profile.ImageSize, referenceDimension);
        if (totalPixels == 0 || profile.IsEmpty)
        {
            return baseline;
        }

        return baseline with
        {
            TrimapBandRadius = ScaleTrimapBandRadius(profile.ImageSize, baseline.TrimapBandRadius, s),
            GrabCut = GrabCutOptions.ForProfile(in profile, baseline.GrabCut, referenceDimension)
        };
    }

    private static int ScaleTrimapBandRadius(Size size, int baselineBandRadius, double linearScale)
    {
        int maxBand = Math.Max(1, Math.Min(size.Width, size.Height) / 3);
        return Math.Clamp(OptionTuning.RoundPositiveInt(baselineBandRadius * linearScale), 1, maxBand);
    }

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any numeric, enum, or nested option parameter is invalid.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxOuterIterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(TrimapBandRadius, 1);
        GrabCut.Validate();
        Mask.Validate();
        if (MaxWorkingDimension is int maxDim)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDim, 2, nameof(MaxWorkingDimension));
    }
}
