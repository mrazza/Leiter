namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Noise;
using Leiter.Core;

/// <summary>
/// Configures parameters for watercolor turbulence flow and pigment dispersion (Bousseau et al. 2006).
/// </summary>
/// <param name="Intensity">The turbulence flow intensity multiplier scaling noise density offsets (default 0.35; must be finite and non-negative).</param>
/// <param name="NoiseOptions">Optional Perlin noise options for generating the fractal turbulence field. If <see langword="null"/>, default flow noise parameters are used.</param>
public sealed record TurbulenceFlowOptions(
    double Intensity = 0.35,
    PerlinNoiseOptions? NoiseOptions = null) : ISizeTunableOptions<TurbulenceFlowOptions>
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default turbulence noise frequency applies unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="TurbulenceFlowOptions"/> instance.
    /// </summary>
    public static TurbulenceFlowOptions Default { get; } = new();

    /// <summary>
    /// Gets the default fractal Perlin noise options used when <see cref="NoiseOptions"/> is <see langword="null"/>.
    /// </summary>
    public static PerlinNoiseOptions DefaultNoise { get; } = new(
        Seed: 404,
        Frequency: 0.015,
        Octaves: 4,
        Lacunarity: 2.0,
        Persistence: 0.5);

    /// <summary>
    /// Creates <see cref="TurbulenceFlowOptions"/> with fractal flow <see cref="NoiseOptions"/> scaled for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="TurbulenceFlowOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static TurbulenceFlowOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="TurbulenceFlowOptions"/> by scaling <paramref name="baseline"/> fractal flow <see cref="NoiseOptions"/> for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline turbulence flow configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="TurbulenceFlowOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static TurbulenceFlowOptions ForSize(
        Size size,
        TurbulenceFlowOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        var (_, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        if (totalPixels == 0)
        {
            return baseline;
        }

        PerlinNoiseOptions noiseBaseline = baseline.NoiseOptions ?? DefaultNoise;
        return baseline with
        {
            NoiseOptions = PerlinNoiseOptions.ForSize(size, noiseBaseline, referenceDimension)
        };
    }

    /// <summary>
    /// Creates <see cref="TurbulenceFlowOptions"/> tuned for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <param name="image">The input image whose dimensions guide scaling.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="TurbulenceFlowOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static TurbulenceFlowOptions ForImage<T>(
        IReadOnlyMatrix<T> image,
        double referenceDimension = DefaultReferenceDimension)
        where T : struct =>
        OptionTuning.TuneForSizeImage<TurbulenceFlowOptions, T>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="TurbulenceFlowOptions"/> by scaling <paramref name="baseline"/> for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <param name="image">The input image whose dimensions guide scaling.</param>
    /// <param name="baseline">The baseline turbulence flow configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="TurbulenceFlowOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static TurbulenceFlowOptions ForImage<T>(
        IReadOnlyMatrix<T> image,
        TurbulenceFlowOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
        where T : struct =>
        OptionTuning.TuneForSizeImage(image, baseline, referenceDimension);

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite, negative, or contains invalid noise options.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFiniteNonNegative(Intensity);

        NoiseOptions?.Validate();
    }
}
