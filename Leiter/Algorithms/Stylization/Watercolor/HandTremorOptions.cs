namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Noise;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for the hand tremor perturbation filter in watercolor stylization.
/// </summary>
/// <param name="Amplitude">The maximum displacement amplitude in pixels (default 3.0; must be finite and non-negative).</param>
/// <param name="SalientAmplitudeScale">The minimum displacement amplitude scale applied inside salient regions (default 0.65; must be finite and within [0.0, 1.0]).</param>
/// <param name="SimilarHueThresholdDegrees">The maximum hue difference in degrees for boundaries to be classified as similar hue (Group 2) rather than contrasting (Group 3) (default 30.0; must be finite and within [0.0, 180.0]).</param>
/// <param name="OverlapDarkening">The blend factor for pigment darkening in stroke overlap regions (default 0.5; must be finite and within [0.0, 1.0]).</param>
/// <param name="PaperColor">The paper background color revealed in gap regions between contrasting strokes (default null; resolves to (250, 248, 242) if null).</param>
/// <param name="PrimaryNoise">Optional configuration for the primary Perlin noise displacement field.</param>
/// <param name="SecondaryNoise">Optional configuration for the secondary Perlin noise displacement field.</param>
/// <param name="MinContrastingChroma">The minimum normalized RGB chroma (<c>max(R,G,B) - min(R,G,B)</c> in [0.0, 1.0]) required on both adjacent regions to emit contrasting-hue paper gaps and overlaps (default 0.08; set to 0.0 to allow gaps on neutral/achromatic boundaries).</param>
/// <param name="PaperGapBias">The normalized boundary-normal bias in [-1.0, 1.0] controlling white paper-gap coverage on contrasting boundaries (default 0.0; positive values pull washes apart to widen gaps, negative values push washes together to suppress gaps and increase overlaps).</param>
/// <param name="PaperGapOpacity">The opacity in [0.0, 1.0] used to blend <see cref="PaperColor"/> inside contrasting paper gaps (default 1.0; 0.0 leaves underlying image pixels unchanged).</param>
public sealed record HandTremorOptions(
    double Amplitude = 3.0,
    double SalientAmplitudeScale = 0.65,
    double SimilarHueThresholdDegrees = 30.0,
    double OverlapDarkening = 0.5,
    Rgb8? PaperColor = null,
    PerlinNoiseOptions? PrimaryNoise = null,
    PerlinNoiseOptions? SecondaryNoise = null,
    double MinContrastingChroma = 0.08,
    double PaperGapBias = 0.0,
    double PaperGapOpacity = 1.0) : ISizeTunableOptions<HandTremorOptions>
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default tremor amplitude and noise frequencies apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="HandTremorOptions"/> instance.
    /// </summary>
    public static HandTremorOptions Default { get; } = new();

    /// <summary>
    /// Gets the default paper background color (<c>(250, 248, 242)</c>) used when <see cref="PaperColor"/> is <see langword="null"/>.
    /// </summary>
    public static Rgb8 DefaultPaperColor => new(250, 248, 242);

    /// <summary>
    /// Gets the default primary Perlin noise options used when <see cref="PrimaryNoise"/> is <see langword="null"/>.
    /// </summary>
    public static PerlinNoiseOptions DefaultPrimaryNoise { get; } = new(Seed: 101, Frequency: 0.04, Octaves: 3);

    /// <summary>
    /// Gets the default secondary Perlin noise options used when <see cref="SecondaryNoise"/> is <see langword="null"/>.
    /// </summary>
    public static PerlinNoiseOptions DefaultSecondaryNoise { get; } = new(Seed: 202, Frequency: 0.04, Octaves: 3);

    /// <summary>
    /// Creates <see cref="HandTremorOptions"/> with displacement amplitude and Perlin noise frequencies scaled for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when <paramref name="size"/> is empty; otherwise a validated <see cref="HandTremorOptions"/> instance tuned for <paramref name="size"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static HandTremorOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="HandTremorOptions"/> by scaling <paramref name="baseline"/> displacement amplitude and Perlin noise frequencies for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline hand tremor configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <paramref name="baseline"/> unchanged when <paramref name="size"/> is empty; otherwise a validated <see cref="HandTremorOptions"/> instance tuned for <paramref name="size"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static HandTremorOptions ForSize(
        Size size,
        HandTremorOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateSizeTuning(size, baseline, referenceDimension);
        return ForSizeUnchecked(size, baseline, referenceDimension);
    }

    internal static HandTremorOptions ForSizeUnchecked(
        Size size,
        HandTremorOptions baseline,
        double referenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScalesUnchecked(size, referenceDimension);
        if (totalPixels == 0)
        {
            return baseline;
        }

        PerlinNoiseOptions primaryBaseline = baseline.PrimaryNoise ?? DefaultPrimaryNoise;
        PerlinNoiseOptions secondaryBaseline = baseline.SecondaryNoise ?? DefaultSecondaryNoise;

        return baseline with
        {
            Amplitude = baseline.Amplitude == 0.0 ? 0.0 : OptionTuning.ScaleFiniteNonNegative(baseline.Amplitude * s),
            PrimaryNoise = PerlinNoiseOptions.ForSize(size, primaryBaseline, referenceDimension),
            SecondaryNoise = PerlinNoiseOptions.ForSize(size, secondaryBaseline, referenceDimension)
        };
    }

    /// <summary>
    /// Creates <see cref="HandTremorOptions"/> tuned for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <param name="image">The input image whose dimensions guide scaling.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when <paramref name="image"/> is empty; otherwise a validated <see cref="HandTremorOptions"/> instance tuned for <paramref name="image"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static HandTremorOptions ForImage<T>(
        IReadOnlyMatrix<T> image,
        double referenceDimension = DefaultReferenceDimension)
        where T : struct =>
        OptionTuning.TuneForSizeImage<HandTremorOptions, T>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="HandTremorOptions"/> by scaling <paramref name="baseline"/> for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <param name="image">The input image whose dimensions guide scaling.</param>
    /// <param name="baseline">The baseline hand tremor configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <paramref name="baseline"/> unchanged when <paramref name="image"/> is empty; otherwise a validated <see cref="HandTremorOptions"/> instance tuned for <paramref name="image"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static HandTremorOptions ForImage<T>(
        IReadOnlyMatrix<T> image,
        HandTremorOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
        where T : struct =>
        OptionTuning.TuneForSizeImage(image, baseline, referenceDimension);

    /// <summary>
    /// Creates <see cref="HandTremorOptions"/> tuned for the dimensions recorded in a cached <see cref="ImageAnalysis"/> profile.
    /// </summary>
    /// <param name="analysis">The precomputed image analysis profile.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when the analyzed image is empty; otherwise a validated <see cref="HandTremorOptions"/> instance tuned for <paramref name="analysis"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static HandTremorOptions ForAnalysis(
        ImageAnalysis analysis,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForSizeAnalysis<HandTremorOptions>(analysis, referenceDimension);

    /// <summary>
    /// Creates <see cref="HandTremorOptions"/> by scaling <paramref name="baseline"/> for the dimensions recorded in a cached <see cref="ImageAnalysis"/> profile.
    /// </summary>
    /// <param name="analysis">The precomputed image analysis profile.</param>
    /// <param name="baseline">The baseline hand tremor configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <paramref name="baseline"/> unchanged when the analyzed image is empty; otherwise a validated <see cref="HandTremorOptions"/> instance tuned for <paramref name="analysis"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static HandTremorOptions ForAnalysis(
        ImageAnalysis analysis,
        HandTremorOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForSizeAnalysis(analysis, baseline, referenceDimension);

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid range.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFiniteNonNegative(Amplitude);
        Guard.ThrowIfNotFiniteInRange(SalientAmplitudeScale, 0.0, 1.0);
        Guard.ThrowIfNotFiniteInRange(SimilarHueThresholdDegrees, 0.0, 180.0);
        Guard.ThrowIfNotFiniteInRange(OverlapDarkening, 0.0, 1.0);
        Guard.ThrowIfNotFiniteInRange(MinContrastingChroma, 0.0, 1.0);
        Guard.ThrowIfNotFiniteInRange(PaperGapBias, -1.0, 1.0);
        Guard.ThrowIfNotFiniteInRange(PaperGapOpacity, 0.0, 1.0);

        PrimaryNoise?.Validate();
        SecondaryNoise?.Validate();
    }
}
