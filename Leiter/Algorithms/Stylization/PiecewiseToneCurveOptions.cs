namespace Leiter.Algorithms.Stylization;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for a piecewise tone reproduction curve with shadow toe, linear midtone, and highlight shoulder.
/// </summary>
/// <param name="MinTone">The output minimum tone floor mapped from input 0.0, in [0.0, 1.0) (default 0.05). Eliminates pure blacks for watercolor substrate simulation.</param>
/// <param name="MaxTone">The output maximum tone ceiling mapped from input 1.0, in (MinTone, 1.0] (default 0.96). Simulates unprinted paper tint without stark pure whites.</param>
/// <param name="ShadowKnee">The input domain breakpoint in (0.0, 1.0) marking the transition from shadow toe easing to linear midtones (default 0.15).</param>
/// <param name="HighlightKnee">The input domain breakpoint in (ShadowKnee, 1.0) marking the transition from linear midtones to highlight shoulder easing (default 0.85).</param>
public sealed record PiecewiseToneCurveOptions(
    double MinTone = 0.05,
    double MaxTone = 0.96,
    double ShadowKnee = 0.15,
    double HighlightKnee = 0.85) : IProfileTunableOptions<PiecewiseToneCurveOptions>
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) accepted for uniform <c>ForSize</c> signatures.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="PiecewiseToneCurveOptions"/> instance.
    /// </summary>
    public static PiecewiseToneCurveOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="PiecewiseToneCurveOptions"/> for <paramref name="size"/>. Tone mapping is resolution-invariant and returns validated default parameters.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="PiecewiseToneCurveOptions"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static PiecewiseToneCurveOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Validates and returns <paramref name="baseline"/> for <paramref name="size"/>. Tone mapping is resolution-invariant.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline tone curve configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>The validated <paramref name="baseline"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static PiecewiseToneCurveOptions ForSize(
        Size size,
        PiecewiseToneCurveOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateSizeAndReference(size, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        return baseline;
    }

    /// <summary>
    /// Creates <see cref="PiecewiseToneCurveOptions"/> with knee breakpoints gently adapted to the lightness dispersion of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="PiecewiseToneCurveOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static PiecewiseToneCurveOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage<PiecewiseToneCurveOptions>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="PiecewiseToneCurveOptions"/> tuned from <paramref name="baseline"/> with knee breakpoints gently adapted to the lightness dispersion of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="baseline">The baseline tone curve configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="PiecewiseToneCurveOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static PiecewiseToneCurveOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        PiecewiseToneCurveOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage(image, baseline, referenceDimension);

    static PiecewiseToneCurveOptions IProfileTunableOptions<PiecewiseToneCurveOptions>.ForProfile(
        in ImageTuningProfile profile,
        PiecewiseToneCurveOptions baseline,
        double referenceDimension) =>
        ForProfile(in profile, baseline, referenceDimension);

    internal static PiecewiseToneCurveOptions ForProfile(
        in ImageTuningProfile profile,
        PiecewiseToneCurveOptions baseline,
        double referenceDimension)
    {
        PiecewiseToneCurveOptions validated = ForSize(profile.ImageSize, baseline, referenceDimension);
        if (profile.IsEmpty)
        {
            return validated;
        }

        const double ReferenceStdDevL = 17.6;
        double contrastDelta = Math.Clamp((profile.LabStats.StdDevL - ReferenceStdDevL) / ReferenceStdDevL, -0.5, 1.0);

        double shadowDelta;
        double highlightDelta;
        if (contrastDelta < 0.0)
        {
            shadowDelta = Math.Max(0.06 * contrastDelta, -0.5 * validated.ShadowKnee);
            highlightDelta = Math.Min(-0.06 * contrastDelta, 0.5 * (1.0 - validated.HighlightKnee));
        }
        else
        {
            double maxInwardShift = Math.Min(0.06, 0.25 * (validated.HighlightKnee - validated.ShadowKnee));
            shadowDelta = maxInwardShift * contrastDelta;
            highlightDelta = -maxInwardShift * contrastDelta;
        }

        return validated with
        {
            ShadowKnee = validated.ShadowKnee + shadowDelta,
            HighlightKnee = validated.HighlightKnee + highlightDelta
        };
    }

    /// <summary>
    /// Validates that all tone curve parameters are finite, satisfy output bounds 0.0 &lt;= MinTone &lt; MaxTone &lt;= 1.0, and satisfy input knots 0.0 &lt; ShadowKnee &lt; HighlightKnee &lt; 1.0.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid domain.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFiniteNonNegative(MinTone);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(MinTone, 1.0);

        Guard.ThrowIfNotFiniteInRange(MaxTone, 0.0, 1.0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaxTone, MinTone);

        Guard.ThrowIfNotFinitePositive(ShadowKnee);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(ShadowKnee, 1.0);

        Guard.ThrowIfNotFinite(HighlightKnee);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(HighlightKnee, ShadowKnee);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(HighlightKnee, 1.0);
    }
}
