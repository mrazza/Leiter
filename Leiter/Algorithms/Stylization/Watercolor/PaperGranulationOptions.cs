namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Noise;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for watercolor paper granulation and pigment settling (Bousseau et al. 2006).
/// </summary>
/// <param name="DensityStrength">The density adjustment multiplier scaling pigment accumulation in paper valleys and depletion on peaks (default 0.3; must be finite and non-negative).</param>
/// <param name="WarpAmplitude">The spatial micro-warp amplitude multiplier displacing pigment along paper height gradients (default 0.6; must be finite and non-negative).</param>
/// <param name="GrainNoiseOptions">Optional Perlin noise options for procedural paper fiber synthesis. If <see langword="null"/>, default grain noise parameters are used.</param>
public sealed record PaperGranulationOptions(
    double DensityStrength = 0.3,
    double WarpAmplitude = 0.6,
    PerlinNoiseOptions? GrainNoiseOptions = null)
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default grain frequency and micro-warp amplitude apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    internal static readonly PerlinNoiseOptions DefaultGrainNoise = new(
        Seed: 707,
        Frequency: 0.35,
        Octaves: 3,
        Lacunarity: 2.1,
        Persistence: 0.55);

    /// <summary>
    /// Creates <see cref="PaperGranulationOptions"/> with <see cref="WarpAmplitude"/> scaled by area ratio and <see cref="GrainNoiseOptions"/> frequency scaled inversely to linear dimension for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="PaperGranulationOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static PaperGranulationOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension)
    {
        var (_, a, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        var defaults = new PaperGranulationOptions();
        if (totalPixels == 0)
        {
            return defaults with
            {
                GrainNoiseOptions = DefaultGrainNoise
            };
        }

        return defaults with
        {
            WarpAmplitude = defaults.WarpAmplitude * a,
            GrainNoiseOptions = PerlinNoiseOptions.ForSize(size, DefaultGrainNoise, referenceDimension)
        };
    }

    /// <summary>
    /// Creates <see cref="PaperGranulationOptions"/> tuned for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image whose dimensions guide scaling.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="PaperGranulationOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static PaperGranulationOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        return ForSize(image.Size, referenceDimension);
    }

    internal static PaperGranulationOptions ForProfile(in ImageTuningProfile profile, double referenceDimension) =>
        ForSize(profile.ImageSize, referenceDimension);

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite, negative, or contains invalid noise options.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(DensityStrength) || DensityStrength < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(DensityStrength), DensityStrength, "DensityStrength must be finite and non-negative.");
        }

        if (!double.IsFinite(WarpAmplitude) || WarpAmplitude < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(WarpAmplitude), WarpAmplitude, "WarpAmplitude must be finite and non-negative.");
        }

        GrainNoiseOptions?.Validate();
    }
}
