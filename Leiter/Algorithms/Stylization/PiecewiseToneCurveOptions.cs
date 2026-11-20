namespace Leiter.Algorithms.Stylization;

using System;
using Leiter.Algorithms;
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
    private const double MinContrastDelta = -0.5;
    private const double MaxContrastDelta = 1.0;
    private const double MaxKneeShift = 0.06;
    private const double MaxRelativeKneeSlack = 0.5;
    private const double MaxInwardKneeFraction = 0.25;

    /// <summary>
    /// Gets the default <see cref="PiecewiseToneCurveOptions"/> instance.
    /// </summary>
    public static PiecewiseToneCurveOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="PiecewiseToneCurveOptions"/> with knee breakpoints gently adapted to the lightness dispersion of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when <paramref name="image"/> is empty; otherwise a validated <see cref="PiecewiseToneCurveOptions"/> instance tuned for <paramref name="image"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static PiecewiseToneCurveOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = OptionTuning.DefaultReferenceDimension) =>
        OptionTuning.TuneForImage<PiecewiseToneCurveOptions>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="PiecewiseToneCurveOptions"/> tuned from <paramref name="baseline"/> with knee breakpoints gently adapted to the lightness dispersion of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="baseline">The baseline tone curve configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <paramref name="baseline"/> unchanged when <paramref name="image"/> is empty; otherwise a validated <see cref="PiecewiseToneCurveOptions"/> instance tuned for <paramref name="image"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static PiecewiseToneCurveOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        PiecewiseToneCurveOptions baseline,
        double referenceDimension = OptionTuning.DefaultReferenceDimension) =>
        OptionTuning.TuneForImage(image, baseline, referenceDimension);

    /// <summary>
    /// Creates <see cref="PiecewiseToneCurveOptions"/> tuned from a cached <see cref="ImageAnalysis"/> profile.
    /// </summary>
    /// <param name="analysis">The precomputed image analysis profile.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when the analyzed image is empty; otherwise a validated <see cref="PiecewiseToneCurveOptions"/> instance tuned for <paramref name="analysis"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static PiecewiseToneCurveOptions ForAnalysis(
        ImageAnalysis analysis,
        double referenceDimension = OptionTuning.DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis<PiecewiseToneCurveOptions>(analysis, referenceDimension);

    /// <summary>
    /// Creates <see cref="PiecewiseToneCurveOptions"/> tuned from <paramref name="baseline"/> using a cached <see cref="ImageAnalysis"/> profile.
    /// </summary>
    /// <param name="analysis">The precomputed image analysis profile.</param>
    /// <param name="baseline">The baseline tone curve configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <paramref name="baseline"/> unchanged when the analyzed image is empty; otherwise a validated <see cref="PiecewiseToneCurveOptions"/> instance tuned for <paramref name="analysis"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static PiecewiseToneCurveOptions ForAnalysis(
        ImageAnalysis analysis,
        PiecewiseToneCurveOptions baseline,
        double referenceDimension = OptionTuning.DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis(analysis, baseline, referenceDimension);

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
        if (profile.IsEmpty)
        {
            return baseline;
        }

        double contrastDelta = Math.Clamp(
            (profile.LabStats.StdDevL - OptionTuning.ReferenceStdDevL) / OptionTuning.ReferenceStdDevL,
            MinContrastDelta,
            MaxContrastDelta);

        double shadowDelta;
        double highlightDelta;
        if (contrastDelta < 0.0)
        {
            shadowDelta = Math.Max(MaxKneeShift * contrastDelta, -MaxRelativeKneeSlack * baseline.ShadowKnee);
            highlightDelta = Math.Min(-MaxKneeShift * contrastDelta, MaxRelativeKneeSlack * (1.0 - baseline.HighlightKnee));
        }
        else
        {
            double maxInwardShift = Math.Min(MaxKneeShift, MaxInwardKneeFraction * (baseline.HighlightKnee - baseline.ShadowKnee));
            shadowDelta = maxInwardShift * contrastDelta;
            highlightDelta = -maxInwardShift * contrastDelta;
        }

        return baseline with
        {
            ShadowKnee = baseline.ShadowKnee + shadowDelta,
            HighlightKnee = baseline.HighlightKnee + highlightDelta
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
