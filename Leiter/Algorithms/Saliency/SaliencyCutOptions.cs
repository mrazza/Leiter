namespace Leiter.Algorithms.Saliency;

using System;
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
/// <param name="GrabCut">Optional inner GrabCut solver parameters; when <see langword="null"/>, defaults to <c>Iterations = 2</c>, <c>MixtureComponentCount = 5</c>, <c>SmoothnessWeight = 50.0</c>.</param>
/// <param name="Mask">Optional initial binarization and connected-component retention parameters; when <see langword="null"/>, defaults to fixed threshold <c>70 / 255</c> and <see cref="ComponentRetentionPolicy.MinimumAreaFraction"/> with <c>0.01</c>.</param>
/// <param name="MaxWorkingDimension">Optional maximum width or height for working resolution; <see langword="null"/> runs at full native resolution.</param>
public sealed record SaliencyCutOptions(
    int MaxOuterIterations = 4,
    int TrimapBandRadius = 10,
    GrabCutOptions? GrabCut = null,
    SaliencyMaskOptions? Mask = null,
    int? MaxWorkingDimension = null) : IProfileTunableOptions<SaliencyCutOptions>
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default band radii and smoothness weights apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="SaliencyCutOptions"/> instance.
    /// </summary>
    public static SaliencyCutOptions Default { get; } = new();

    /// <summary>
    /// Gets the default inner GrabCut options used when <see cref="GrabCut"/> is <see langword="null"/>.
    /// </summary>
    public static GrabCutOptions DefaultGrabCut { get; } = new(Iterations: 2, MixtureComponentCount: 5, SmoothnessWeight: 50.0);

    /// <summary>
    /// Gets the default initial binarization and component retention options used when <see cref="Mask"/> is <see langword="null"/>.
    /// </summary>
    public static SaliencyMaskOptions DefaultMask { get; } = new(
        Method: SaliencyThresholdMethod.Fixed,
        FixedThreshold: SaliencyMaskOptions.DefaultFixedThreshold,
        RetentionPolicy: ComponentRetentionPolicy.MinimumAreaFraction,
        MinAreaFraction: SaliencyMaskOptions.DefaultMinAreaFraction);

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
    /// Creates <see cref="SaliencyCutOptions"/> by scaling <paramref name="baseline"/> trimap band radius and inner GrabCut smoothness for <paramref name="size"/>.
    /// </summary>
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
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        if (totalPixels == 0)
        {
            return baseline;
        }

        int maxBand = Math.Max(1, Math.Min(size.Width, size.Height) / 3);
        int bandRadius = Math.Clamp(OptionTuning.RoundPositiveInt(baseline.TrimapBandRadius * s), 1, maxBand);
        GrabCutOptions grabCutBaseline = baseline.GrabCut ?? DefaultGrabCut;

        return baseline with
        {
            TrimapBandRadius = bandRadius,
            GrabCut = GrabCutOptions.ForSize(size, grabCutBaseline, referenceDimension),
            Mask = baseline.Mask ?? DefaultMask
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
        SaliencyCutOptions scaled = ForSize(profile.ImageSize, baseline, referenceDimension);
        if (profile.IsEmpty)
        {
            return scaled;
        }

        GrabCutOptions grabCutBaseline = baseline.GrabCut ?? DefaultGrabCut;
        return scaled with
        {
            GrabCut = GrabCutOptions.ForProfile(in profile, grabCutBaseline, referenceDimension)
        };
    }

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any numeric, enum, or nested option parameter is invalid.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxOuterIterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(TrimapBandRadius, 1);
        GrabCut?.Validate();
        Mask?.Validate();
        if (MaxWorkingDimension is int maxDim)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDim, 2, nameof(MaxWorkingDimension));
    }
}
