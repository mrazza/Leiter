namespace Leiter.Algorithms.Segmentation;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for <see cref="GrabCut"/> foreground/background segmentation.
/// </summary>
/// <param name="Iterations">The maximum number of alternating GMM-update and graph-cut iterations (<c>≥ 1</c>).</param>
/// <param name="MixtureComponentCount">The number of Gaussian components in each foreground/background GMM (<c>≥ 1</c>).</param>
/// <param name="SmoothnessWeight">The contrast-sensitive pairwise boundary smoothness weight γ (<c>≥ 0</c>).</param>
public sealed record GrabCutOptions(
    int Iterations = 5,
    int MixtureComponentCount = 5,
    double SmoothnessWeight = 50.0) : ISizeTunableOptions<GrabCutOptions>, IProfileTunableOptions<GrabCutOptions>
{
    private const double LowDispersionThreshold = 14.0;
    private const double HighDispersionThreshold = 30.0;
    private const double MinColorSpreadClamp = 0.80;
    private const double MaxColorSpreadClamp = 1.25;

    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default smoothness weights apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="GrabCutOptions"/> instance.
    /// </summary>
    public static GrabCutOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="GrabCutOptions"/> with <see cref="SmoothnessWeight"/> scaled linearly to <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="GrabCutOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static GrabCutOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="GrabCutOptions"/> by scaling <paramref name="baseline"/>'s <see cref="SmoothnessWeight"/> linearly to <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline GrabCut configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="GrabCutOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static GrabCutOptions ForSize(
        Size size,
        GrabCutOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateSizeTuning(size, baseline, referenceDimension);
        return ScaleCore(size, baseline, referenceDimension, applyProfile: false, profile: default);
    }

    /// <summary>
    /// Creates <see cref="GrabCutOptions"/> tuned for the dimensions and color complexity of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="GrabCutOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static GrabCutOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage<GrabCutOptions>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="GrabCutOptions"/> tuned from <paramref name="baseline"/> for the dimensions and color complexity of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="baseline">The baseline GrabCut configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="GrabCutOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static GrabCutOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        GrabCutOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage(image, baseline, referenceDimension);

    /// <summary>
    /// Creates <see cref="GrabCutOptions"/> tuned from default GrabCut parameters using pre-computed <paramref name="analysis"/>.
    /// </summary>
    /// <param name="analysis">The pre-computed image analysis profile.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="GrabCutOptions"/> instance tuned for <paramref name="analysis"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static GrabCutOptions ForAnalysis(
        ImageAnalysis analysis,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis<GrabCutOptions>(analysis, referenceDimension);

    /// <summary>
    /// Creates <see cref="GrabCutOptions"/> tuned from <paramref name="baseline"/> using pre-computed <paramref name="analysis"/>.
    /// </summary>
    /// <param name="analysis">The pre-computed image analysis profile.</param>
    /// <param name="baseline">The baseline GrabCut configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="GrabCutOptions"/> instance tuned for <paramref name="analysis"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static GrabCutOptions ForAnalysis(
        ImageAnalysis analysis,
        GrabCutOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis(analysis, baseline, referenceDimension);

    static GrabCutOptions IProfileTunableOptions<GrabCutOptions>.ForProfile(
        in ImageTuningProfile profile,
        GrabCutOptions baseline,
        double referenceDimension) =>
        ForProfile(in profile, baseline, referenceDimension);

    internal static GrabCutOptions ForProfile(
        in ImageTuningProfile profile,
        GrabCutOptions baseline,
        double referenceDimension) =>
        ScaleCore(profile.ImageSize, baseline, referenceDimension, applyProfile: !profile.IsEmpty, in profile);

    private static GrabCutOptions ScaleCore(
        Size size,
        GrabCutOptions baseline,
        double referenceDimension,
        bool applyProfile,
        in ImageTuningProfile profile)
    {
        var (linearScale, _, totalPixels) = OptionTuning.ComputeScalesUnchecked(size, referenceDimension);
        if (totalPixels == 0)
        {
            return baseline;
        }

        if (!applyProfile)
        {
            return baseline with
            {
                SmoothnessWeight = OptionTuning.ScaleFiniteNonNegative(baseline.SmoothnessWeight * linearScale)
            };
        }

        int componentDelta = profile.LabDispersion < LowDispersionThreshold
            ? -1
            : profile.LabDispersion > HighDispersionThreshold
                ? 1
                : 0;
        int components = Math.Max(1, baseline.MixtureComponentCount + componentDelta);
        double colorFactor = Math.Sqrt(Math.Clamp(profile.ColorSpreadFactor, MinColorSpreadClamp, MaxColorSpreadClamp));
        double smoothness = OptionTuning.ScaleFiniteNonNegative(baseline.SmoothnessWeight * (linearScale / colorFactor));

        return baseline with
        {
            MixtureComponentCount = components,
            SmoothnessWeight = smoothness
        };
    }

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is outside its valid range.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Iterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MixtureComponentCount, 1);
        Guard.ThrowIfNotFiniteNonNegative(SmoothnessWeight);
    }
}
