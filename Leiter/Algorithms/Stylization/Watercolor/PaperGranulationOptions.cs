namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Noise;
using Leiter.Core;

/// <summary>
/// Configures parameters for watercolor paper granulation and pigment settling (Bousseau et al. 2006).
/// </summary>
/// <param name="DensityStrength">The density adjustment multiplier scaling pigment accumulation in paper valleys and depletion on peaks (default 0.3; must be finite and non-negative).</param>
/// <param name="WarpAmplitude">The spatial micro-warp amplitude multiplier displacing pigment along paper height gradients (default 0.6; must be finite and non-negative).</param>
/// <param name="GrainNoise">Optional Perlin noise options for procedural paper fiber synthesis. If <see langword="null"/>, default grain noise parameters are used.</param>
public sealed record PaperGranulationOptions(
    double DensityStrength = 0.3,
    double WarpAmplitude = 0.6,
    PerlinNoiseOptions? GrainNoise = null) : ISizeTunableOptions<PaperGranulationOptions>
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default grain frequency and micro-warp amplitude apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="PaperGranulationOptions"/> instance.
    /// </summary>
    public static PaperGranulationOptions Default { get; } = new();

    /// <summary>
    /// Gets the default procedural paper grain Perlin noise options used when <see cref="GrainNoise"/> is <see langword="null"/>.
    /// </summary>
    public static PerlinNoiseOptions DefaultGrainNoise { get; } = new(
        Seed: 707,
        Frequency: 0.18,
        Octaves: 4,
        Lacunarity: 2.0,
        Persistence: 0.55);

    /// <summary>
    /// Creates <see cref="PaperGranulationOptions"/> with <see cref="WarpAmplitude"/> scaled linearly and <see cref="GrainNoise"/> frequency scaled inversely to linear dimension for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when <paramref name="size"/> is empty; otherwise a validated <see cref="PaperGranulationOptions"/> instance tuned for <paramref name="size"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static PaperGranulationOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="PaperGranulationOptions"/> by scaling <paramref name="baseline"/> warp amplitude linearly and grain frequency inversely to linear dimension for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline paper granulation configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <paramref name="baseline"/> unchanged when <paramref name="size"/> is empty; otherwise a validated <see cref="PaperGranulationOptions"/> instance tuned for <paramref name="size"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static PaperGranulationOptions ForSize(
        Size size,
        PaperGranulationOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateSizeTuning(size, baseline, referenceDimension);
        return ForSizeUnchecked(size, baseline, referenceDimension);
    }

    internal static PaperGranulationOptions ForSizeUnchecked(
        Size size,
        PaperGranulationOptions baseline,
        double referenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScalesUnchecked(size, referenceDimension);
        if (totalPixels == 0)
        {
            return baseline;
        }

        PerlinNoiseOptions grainBaseline = baseline.GrainNoise ?? DefaultGrainNoise;
        return baseline with
        {
            WarpAmplitude = baseline.WarpAmplitude == 0.0 ? 0.0 : OptionTuning.ScaleFiniteNonNegative(baseline.WarpAmplitude * s),
            GrainNoise = PerlinNoiseOptions.ForSize(size, grainBaseline, referenceDimension)
        };
    }

    /// <summary>
    /// Creates <see cref="PaperGranulationOptions"/> tuned for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <param name="image">The input image whose dimensions guide scaling.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when <paramref name="image"/> is empty; otherwise a validated <see cref="PaperGranulationOptions"/> instance tuned for <paramref name="image"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static PaperGranulationOptions ForImage<T>(
        IReadOnlyMatrix<T> image,
        double referenceDimension = DefaultReferenceDimension)
        where T : struct =>
        OptionTuning.TuneForSizeImage<PaperGranulationOptions, T>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="PaperGranulationOptions"/> by scaling <paramref name="baseline"/> for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <param name="image">The input image whose dimensions guide scaling.</param>
    /// <param name="baseline">The baseline paper granulation configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <paramref name="baseline"/> unchanged when <paramref name="image"/> is empty; otherwise a validated <see cref="PaperGranulationOptions"/> instance tuned for <paramref name="image"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static PaperGranulationOptions ForImage<T>(
        IReadOnlyMatrix<T> image,
        PaperGranulationOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
        where T : struct =>
        OptionTuning.TuneForSizeImage(image, baseline, referenceDimension);

    /// <summary>
    /// Creates <see cref="PaperGranulationOptions"/> tuned for the dimensions recorded in a cached <see cref="ImageAnalysis"/> profile.
    /// </summary>
    /// <param name="analysis">The precomputed image analysis profile.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when the analyzed image is empty; otherwise a validated <see cref="PaperGranulationOptions"/> instance tuned for <paramref name="analysis"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static PaperGranulationOptions ForAnalysis(
        ImageAnalysis analysis,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForSizeAnalysis<PaperGranulationOptions>(analysis, referenceDimension);

    /// <summary>
    /// Creates <see cref="PaperGranulationOptions"/> by scaling <paramref name="baseline"/> for the dimensions recorded in a cached <see cref="ImageAnalysis"/> profile.
    /// </summary>
    /// <param name="analysis">The precomputed image analysis profile.</param>
    /// <param name="baseline">The baseline paper granulation configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <paramref name="baseline"/> unchanged when the analyzed image is empty; otherwise a validated <see cref="PaperGranulationOptions"/> instance tuned for <paramref name="analysis"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static PaperGranulationOptions ForAnalysis(
        ImageAnalysis analysis,
        PaperGranulationOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForSizeAnalysis(analysis, baseline, referenceDimension);

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite, negative, or contains invalid noise options.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFiniteNonNegative(DensityStrength);
        Guard.ThrowIfNotFiniteNonNegative(WarpAmplitude);

        GrainNoise?.Validate();
    }
}
