namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for the wet-in-wet watercolor edge bleeding filter.
/// </summary>
/// <param name="Mode">The wet-in-wet boundary selection mode (default <see cref="WetInWetMode.Default"/>).</param>
/// <param name="ColorGradientThreshold">The threshold for region-averaged color gradient magnitude required to qualify a boundary (default 0.1; must be finite and non-negative).</param>
/// <param name="SalientHueThresholdDegrees">The maximum hue difference in degrees required to qualify a boundary incident to a salient region (default 20.0; must be finite and within [0.0, 180.0]).</param>
/// <param name="NonSalientHueThresholdDegrees">The maximum hue difference in degrees required to qualify a boundary between non-salient regions (default 90.0; must be finite and within [0.0, 180.0]).</param>
/// <param name="MinIntensityDifference">The minimum HSI intensity difference between adjacent regions required for bleeding (default 0.02; must be finite and non-negative).</param>
/// <param name="SalientOverlapThreshold">The minimum fraction of salient pixels required for a region to be classified as salient (default 0.5; must be finite and within [0.0, 1.0]).</param>
/// <param name="ScatterRadius">The maximum pixel distance from qualifying boundaries for pigment seed scattering (default 4; must be at least 1).</param>
/// <param name="SeedProbability">The probability in [0.0, 1.0] of depositing a pigment seed at a candidate pixel (default 0.45; must be finite and within [0.0, 1.0]).</param>
/// <param name="EllipticalFilter">Optional oriented elliptical Gaussian smoothing configuration; when <see langword="null"/>, defaults to wet-in-wet soft feathering radii (<c>MajorRadius = 6.0, MinorRadius = 2.5, SigmaScale = 0.55</c>).</param>
/// <param name="Seed">The pseudo-random seed used for deterministic pigment seed scattering (default 1337).</param>
public sealed record WetInWetOptions(
    WetInWetMode Mode = WetInWetMode.Default,
    double ColorGradientThreshold = 0.1,
    double SalientHueThresholdDegrees = 20.0,
    double NonSalientHueThresholdDegrees = 90.0,
    double MinIntensityDifference = 0.02,
    double SalientOverlapThreshold = 0.5,
    int ScatterRadius = 4,
    double SeedProbability = 0.45,
    OrientedEllipticalFilterOptions? EllipticalFilter = null,
    int Seed = 1337) : IProfileTunableOptions<WetInWetOptions>, ISizeTunableOptions<WetInWetOptions>
{
    private const double MinScaledGradientThreshold = 0.005;
    private const double MaxScaledGradientThreshold = 0.5;

    /// <summary>
    /// Gets the default oriented elliptical smoothing options used when <see cref="EllipticalFilter"/> is <see langword="null"/>.
    /// </summary>
    public static OrientedEllipticalFilterOptions DefaultEllipticalFilter { get; } =
        new(MajorRadius: 6.0, MinorRadius: 2.5, SigmaScale: 0.55);

    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default bleed radii and gradient thresholds apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="WetInWetOptions"/> instance.
    /// </summary>
    public static WetInWetOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="WetInWetOptions"/> with scatter radius, elliptical filter radii, and color gradient threshold scaled for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when <paramref name="size"/> is empty; otherwise a validated <see cref="WetInWetOptions"/> instance tuned for <paramref name="size"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static WetInWetOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="WetInWetOptions"/> by scaling <paramref name="baseline"/> scatter radius, elliptical filter radii, and color gradient threshold for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline wet-in-wet configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <paramref name="baseline"/> unchanged when <paramref name="size"/> is empty; otherwise a validated <see cref="WetInWetOptions"/> instance tuned for <paramref name="size"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static WetInWetOptions ForSize(
        Size size,
        WetInWetOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateSizeTuning(size, baseline, referenceDimension);
        return ForSizeUnchecked(size, baseline, referenceDimension);
    }

    internal static WetInWetOptions ForSizeUnchecked(
        Size size,
        WetInWetOptions baseline,
        double referenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScalesUnchecked(size, referenceDimension);
        if (totalPixels == 0)
        {
            return baseline;
        }

        OrientedEllipticalFilterOptions filterBaseline = baseline.EllipticalFilter ?? DefaultEllipticalFilter;
        double minGradient = Math.Min(MinScaledGradientThreshold, baseline.ColorGradientThreshold);
        double maxGradient = Math.Max(MaxScaledGradientThreshold, baseline.ColorGradientThreshold);
        double colorGradientThreshold = baseline.ColorGradientThreshold == 0.0
            ? 0.0
            : OptionTuning.ClampFinite(baseline.ColorGradientThreshold / s, minGradient, maxGradient);

        return baseline with
        {
            ScatterRadius = OptionTuning.RoundPositiveInt(baseline.ScatterRadius * s),
            EllipticalFilter = OrientedEllipticalFilterOptions.ForSize(size, filterBaseline, referenceDimension),
            ColorGradientThreshold = colorGradientThreshold
        };
    }

    /// <summary>
    /// Creates <see cref="WetInWetOptions"/> tuned for both the dimensions and scene clutter of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when <paramref name="image"/> is empty; otherwise a validated <see cref="WetInWetOptions"/> instance tuned for <paramref name="image"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static WetInWetOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage<WetInWetOptions>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="WetInWetOptions"/> tuned from <paramref name="baseline"/> for both the dimensions and scene clutter of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="baseline">The baseline wet-in-wet configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <paramref name="baseline"/> unchanged when <paramref name="image"/> is empty; otherwise a validated <see cref="WetInWetOptions"/> instance tuned for <paramref name="image"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static WetInWetOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        WetInWetOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage(image, baseline, referenceDimension);

    /// <summary>
    /// Creates <see cref="WetInWetOptions"/> tuned from a cached <see cref="ImageAnalysis"/> profile.
    /// </summary>
    /// <param name="analysis">The precomputed image analysis profile.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when the analyzed image is empty; otherwise a validated <see cref="WetInWetOptions"/> instance tuned for <paramref name="analysis"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static WetInWetOptions ForAnalysis(
        ImageAnalysis analysis,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis<WetInWetOptions>(analysis, referenceDimension);

    /// <summary>
    /// Creates <see cref="WetInWetOptions"/> tuned from <paramref name="baseline"/> using a cached <see cref="ImageAnalysis"/> profile.
    /// </summary>
    /// <param name="analysis">The precomputed image analysis profile.</param>
    /// <param name="baseline">The baseline wet-in-wet configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <paramref name="baseline"/> unchanged when the analyzed image is empty; otherwise a validated <see cref="WetInWetOptions"/> instance tuned for <paramref name="analysis"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static WetInWetOptions ForAnalysis(
        ImageAnalysis analysis,
        WetInWetOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis(analysis, baseline, referenceDimension);

    static WetInWetOptions IProfileTunableOptions<WetInWetOptions>.ForProfile(
        in ImageTuningProfile profile,
        WetInWetOptions baseline,
        double referenceDimension) =>
        ForProfile(in profile, baseline, referenceDimension);

    internal static WetInWetOptions ForProfile(
        in ImageTuningProfile profile,
        WetInWetOptions baseline,
        double referenceDimension)
    {
        WetInWetOptions scaled = ForSizeUnchecked(profile.ImageSize, baseline, referenceDimension);
        if (profile.IsEmpty)
        {
            return scaled;
        }

        double minGradient = Math.Min(MinScaledGradientThreshold, scaled.ColorGradientThreshold);
        double maxGradient = Math.Max(MaxScaledGradientThreshold, scaled.ColorGradientThreshold);
        return scaled with
        {
            ColorGradientThreshold = OptionTuning.ClampFinite(
                scaled.ColorGradientThreshold * profile.ClutterFactor,
                minGradient,
                maxGradient)
        };
    }

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid range.</exception>
    public void Validate()
    {
        Guard.ThrowIfUndefinedEnum(Mode);
        Guard.ThrowIfNotFiniteNonNegative(ColorGradientThreshold);
        Guard.ThrowIfNotFiniteInRange(SalientHueThresholdDegrees, 0.0, 180.0);
        Guard.ThrowIfNotFiniteInRange(NonSalientHueThresholdDegrees, 0.0, 180.0);
        Guard.ThrowIfNotFiniteNonNegative(MinIntensityDifference);
        Guard.ThrowIfNotFiniteInRange(SalientOverlapThreshold, 0.0, 1.0);
        ArgumentOutOfRangeException.ThrowIfLessThan(ScatterRadius, 1);
        Guard.ThrowIfNotFiniteInRange(SeedProbability, 0.0, 1.0);
        EllipticalFilter?.Validate();
    }
}
