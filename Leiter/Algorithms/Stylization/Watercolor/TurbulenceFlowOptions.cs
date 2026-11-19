namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Noise;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for watercolor turbulence flow and pigment dispersion (Bousseau et al. 2006).
/// </summary>
/// <param name="Intensity">The turbulence flow intensity multiplier scaling noise density offsets (default 0.35; must be finite and non-negative).</param>
/// <param name="NoiseOptions">Optional Perlin noise options for generating the fractal turbulence field. If <see langword="null"/>, default flow noise parameters are used.</param>
public sealed record TurbulenceFlowOptions(
    double Intensity = 0.35,
    PerlinNoiseOptions? NoiseOptions = null)
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default turbulence noise frequency applies unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="TurbulenceFlowOptions"/> instance.
    /// </summary>
    public static TurbulenceFlowOptions Default { get; } = new();

    internal static readonly PerlinNoiseOptions DefaultNoise = new(
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
    public static TurbulenceFlowOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateSizeAndReference(size, referenceDimension);
        return Default with
        {
            NoiseOptions = PerlinNoiseOptions.ForSize(size, DefaultNoise, referenceDimension)
        };
    }

    /// <summary>
    /// Creates <see cref="TurbulenceFlowOptions"/> tuned for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image whose dimensions guide scaling.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="TurbulenceFlowOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static TurbulenceFlowOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        return ForSize(image.Size, referenceDimension);
    }

    internal static TurbulenceFlowOptions ForProfile(in ImageTuningProfile profile, double referenceDimension) =>
        ForSize(profile.ImageSize, referenceDimension);

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
