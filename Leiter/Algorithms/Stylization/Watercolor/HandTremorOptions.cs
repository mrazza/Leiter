namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
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
public sealed record HandTremorOptions(
    double Amplitude = 3.0,
    double SalientAmplitudeScale = 0.65,
    double SimilarHueThresholdDegrees = 30.0,
    double OverlapDarkening = 0.5,
    Rgb8? PaperColor = null,
    PerlinNoiseOptions? PrimaryNoise = null,
    PerlinNoiseOptions? SecondaryNoise = null)
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default tremor amplitude and noise frequencies apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="HandTremorOptions"/> instance.
    /// </summary>
    public static HandTremorOptions Default { get; } = new();

    internal static readonly PerlinNoiseOptions DefaultPrimaryNoise = new(Seed: 101, Frequency: 0.04, Octaves: 3);

    internal static readonly PerlinNoiseOptions DefaultSecondaryNoise = new(Seed: 202, Frequency: 0.04, Octaves: 3);

    /// <summary>
    /// Creates <see cref="HandTremorOptions"/> with displacement amplitude and Perlin noise frequencies scaled for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="HandTremorOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static HandTremorOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        HandTremorOptions defaults = Default;
        if (totalPixels == 0)
        {
            return defaults with
            {
                PrimaryNoise = DefaultPrimaryNoise,
                SecondaryNoise = DefaultSecondaryNoise
            };
        }

        return defaults with
        {
            Amplitude = defaults.Amplitude * s,
            PrimaryNoise = PerlinNoiseOptions.ForSize(size, DefaultPrimaryNoise, referenceDimension),
            SecondaryNoise = PerlinNoiseOptions.ForSize(size, DefaultSecondaryNoise, referenceDimension)
        };
    }

    /// <summary>
    /// Creates <see cref="HandTremorOptions"/> tuned for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image whose dimensions guide scaling.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="HandTremorOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static HandTremorOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        return ForSize(image.Size, referenceDimension);
    }

    internal static HandTremorOptions ForProfile(in ImageTuningProfile profile, double referenceDimension) =>
        ForSize(profile.ImageSize, referenceDimension);

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

        PrimaryNoise?.Validate();
        SecondaryNoise?.Validate();
    }
}
