namespace Leiter.Algorithms.AntiAliasing;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for Fast Approximate Anti-Aliasing (FXAA 3.11).
/// </summary>
/// <param name="RelativeContrastThreshold">The relative contrast threshold determining edge sensitivity (default 0.125; must be finite and within (0, 1]).</param>
/// <param name="AbsoluteContrastThreshold">The minimum contrast threshold preventing dark region noise processing (default 0.0625; must be finite and non-negative).</param>
/// <param name="SubpixelBlendStrength">The strength of subpixel anti-aliasing blending (default 0.75; must be finite and within [0, 1]).</param>
/// <param name="MaxSpanSearchSteps">The maximum number of tangent search steps along an edge span (default 8; must be at least 1).</param>
public sealed record FxaaOptions(
    double RelativeContrastThreshold = 0.125,
    double AbsoluteContrastThreshold = 0.0625,
    double SubpixelBlendStrength = 0.75,
    int MaxSpanSearchSteps = 8) : IProfileTunableOptions<FxaaOptions>
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default FXAA span steps apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="FxaaOptions"/> instance.
    /// </summary>
    public static FxaaOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="FxaaOptions"/> with <see cref="MaxSpanSearchSteps"/> scaled proportionally to <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="FxaaOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static FxaaOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="FxaaOptions"/> by scaling <paramref name="baseline"/>'s <see cref="MaxSpanSearchSteps"/> proportionally to <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline FXAA configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="FxaaOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static FxaaOptions ForSize(
        Size size,
        FxaaOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        if (totalPixels == 0)
        {
            return baseline;
        }

        int maxSteps = Math.Max(64, baseline.MaxSpanSearchSteps);
        return baseline with
        {
            MaxSpanSearchSteps = Math.Clamp(OptionTuning.RoundPositiveInt(baseline.MaxSpanSearchSteps * s), 4, maxSteps)
        };
    }

    /// <summary>
    /// Creates <see cref="FxaaOptions"/> tuned for both the dimensions and luminance profile of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="FxaaOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static FxaaOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage<FxaaOptions>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="FxaaOptions"/> tuned from <paramref name="baseline"/> for the dimensions and luminance profile of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="baseline">The baseline FXAA configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="FxaaOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static FxaaOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        FxaaOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage(image, baseline, referenceDimension);

    static FxaaOptions IProfileTunableOptions<FxaaOptions>.ForProfile(
        in ImageTuningProfile profile,
        FxaaOptions baseline,
        double referenceDimension) =>
        ForProfile(in profile, baseline, referenceDimension);

    internal static FxaaOptions ForProfile(
        in ImageTuningProfile profile,
        FxaaOptions baseline,
        double referenceDimension)
    {
        FxaaOptions scaled = ForSize(profile.ImageSize, baseline, referenceDimension);
        if (profile.IsEmpty || profile.LabStats.MeanL >= 35.0)
        {
            return scaled;
        }

        double t = Math.Clamp(profile.LabStats.MeanL / 35.0, 0.0, 1.0);
        double lowKeyFloor = Math.Min(0.0312, scaled.AbsoluteContrastThreshold);
        double absThreshold = ScalarMath.Lerp(lowKeyFloor, scaled.AbsoluteContrastThreshold, t);
        return scaled with { AbsoluteContrastThreshold = absThreshold };
    }

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid range.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFiniteInHalfOpenRange(RelativeContrastThreshold, 0.0, 1.0);
        Guard.ThrowIfNotFiniteNonNegative(AbsoluteContrastThreshold);
        Guard.ThrowIfNotFiniteInRange(SubpixelBlendStrength, 0.0, 1.0);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxSpanSearchSteps, 1);
    }
}
