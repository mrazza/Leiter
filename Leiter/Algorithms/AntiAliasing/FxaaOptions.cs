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
    int MaxSpanSearchSteps = 8)
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
    public static FxaaOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        FxaaOptions defaults = Default;
        if (totalPixels == 0)
        {
            return defaults;
        }

        return defaults with
        {
            MaxSpanSearchSteps = Math.Clamp(OptionTuning.RoundPositiveInt(defaults.MaxSpanSearchSteps * s), 4, 64)
        };
    }

    /// <summary>
    /// Creates <see cref="FxaaOptions"/> tuned for both the dimensions and luminance profile of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="FxaaOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static FxaaOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        ImageTuningProfile profile = ImageTuningProfile.Analyze(image);
        return ForProfile(in profile, referenceDimension);
    }

    internal static FxaaOptions ForProfile(in ImageTuningProfile profile, double referenceDimension)
    {
        FxaaOptions scaled = ForSize(profile.ImageSize, referenceDimension);
        if (profile.IsEmpty || profile.LabStats.MeanL >= 35.0)
        {
            return scaled;
        }

        double t = Math.Clamp(profile.LabStats.MeanL / 35.0, 0.0, 1.0);
        double absThreshold = ScalarMath.Lerp(0.0312, scaled.AbsoluteContrastThreshold, t);
        return scaled with { AbsoluteContrastThreshold = absThreshold };
    }

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid range.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFinitePositive(RelativeContrastThreshold);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(RelativeContrastThreshold, 1.0);
        Guard.ThrowIfNotFiniteNonNegative(AbsoluteContrastThreshold);
        Guard.ThrowIfNotFiniteInRange(SubpixelBlendStrength, 0.0, 1.0);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxSpanSearchSteps, 1);
    }
}
