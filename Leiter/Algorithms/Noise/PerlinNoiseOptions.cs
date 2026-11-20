namespace Leiter.Algorithms.Noise;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Core;

/// <summary>
/// Configures parameters for Perlin gradient noise and fractal field synthesis.
/// </summary>
/// <param name="Seed">The pseudo-random seed used to initialize the permutation table (default 1337).</param>
/// <param name="Frequency">The base spatial frequency multiplier for coordinate scaling (default 0.05; must be finite and greater than 0.0).</param>
/// <param name="Octaves">The number of fractal noise octaves to blend (default 4; must be between 1 and 16 inclusive).</param>
/// <param name="Lacunarity">The frequency multiplier per octave step (default 2.0; must be finite and greater than 0.0).</param>
/// <param name="Persistence">The amplitude decay multiplier per octave step (default 0.5; must be finite and non-negative).</param>
public sealed record PerlinNoiseOptions(
    int Seed = 1337,
    double Frequency = 0.05,
    int Octaves = 4,
    double Lacunarity = 2.0,
    double Persistence = 0.5) : ISizeTunableOptions<PerlinNoiseOptions>
{
    /// <summary>
    /// The maximum supported number of fractal noise octaves.
    /// </summary>
    internal const int MaxOctaves = 16;

    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default frequencies apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="PerlinNoiseOptions"/> instance.
    /// </summary>
    public static PerlinNoiseOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="PerlinNoiseOptions"/> with the base frequency scaled inversely to the linear dimension of <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target matrix dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="PerlinNoiseOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static PerlinNoiseOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="PerlinNoiseOptions"/> by scaling <paramref name="baseline"/>'s frequency inversely to the linear dimension of <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target matrix dimensions.</param>
    /// <param name="baseline">The baseline noise configuration defined at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="PerlinNoiseOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static PerlinNoiseOptions ForSize(
        Size size,
        PerlinNoiseOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        if (totalPixels == 0)
        {
            return baseline;
        }

        return baseline with
        {
            Frequency = OptionTuning.ClampFrequency(baseline.Frequency / s)
        };
    }

    /// <summary>
    /// Creates <see cref="PerlinNoiseOptions"/> scaled for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <param name="image">The target image matrix whose dimensions guide frequency scaling.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="PerlinNoiseOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static PerlinNoiseOptions ForImage<T>(
        IReadOnlyMatrix<T> image,
        double referenceDimension = DefaultReferenceDimension)
        where T : struct =>
        OptionTuning.TuneForSizeImage<PerlinNoiseOptions, T>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="PerlinNoiseOptions"/> by scaling <paramref name="baseline"/> for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <param name="image">The target image matrix whose dimensions guide frequency scaling.</param>
    /// <param name="baseline">The baseline noise configuration defined at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="PerlinNoiseOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static PerlinNoiseOptions ForImage<T>(
        IReadOnlyMatrix<T> image,
        PerlinNoiseOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
        where T : struct =>
        OptionTuning.TuneForSizeImage(image, baseline, referenceDimension);

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid range.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFinitePositive(Frequency);
        ArgumentOutOfRangeException.ThrowIfLessThan(Octaves, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Octaves, MaxOctaves);
        Guard.ThrowIfNotFinitePositive(Lacunarity);
        Guard.ThrowIfNotFiniteNonNegative(Persistence);
    }
}
