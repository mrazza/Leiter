namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for watercolor edge darkening (Bousseau et al. 2006).
/// </summary>
/// <param name="Intensity">The darkening intensity multiplier scaling edge pigment density (default 1.8; must be finite and non-negative).</param>
/// <param name="BlurSigma">The standard deviation for Gaussian smoothing of the gradient edge map (default 0.8; must be finite and non-negative).</param>
/// <param name="GradientOperator">The spatial gradient operator kernel used to compute edge magnitude (default <see cref="GradientOperator.Sobel"/>).</param>
public sealed record EdgeDarkeningOptions(
    double Intensity = 1.8,
    double BlurSigma = 0.8,
    GradientOperator GradientOperator = GradientOperator.Sobel) : IValidatableOptions
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default blur sigma and intensity apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="EdgeDarkeningOptions"/> instance.
    /// </summary>
    public static EdgeDarkeningOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="EdgeDarkeningOptions"/> with <see cref="BlurSigma"/> scaled linearly and <see cref="Intensity"/> compensated for Sobel impulse attenuation at <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EdgeDarkeningOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static EdgeDarkeningOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        EdgeDarkeningOptions defaults = Default;
        if (totalPixels == 0)
        {
            return defaults;
        }

        double impulseCompensation = Math.Sqrt(((s * s) + 0.5) / 1.5);
        return defaults with
        {
            Intensity = defaults.Intensity * impulseCompensation,
            BlurSigma = defaults.BlurSigma * s
        };
    }

    /// <summary>
    /// Creates <see cref="EdgeDarkeningOptions"/> tuned for both the dimensions and color spread of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EdgeDarkeningOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static EdgeDarkeningOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        ImageTuningProfile profile = ImageTuningProfile.Analyze(image);
        return ForProfile(in profile, referenceDimension);
    }

    internal static EdgeDarkeningOptions ForProfile(in ImageTuningProfile profile, double referenceDimension)
    {
        EdgeDarkeningOptions scaled = ForSize(profile.ImageSize, referenceDimension);
        if (profile.IsEmpty)
        {
            return scaled;
        }

        return scaled with
        {
            Intensity = scaled.Intensity / Math.Clamp(profile.ColorSpreadFactor, 0.85, 1.20)
        };
    }

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite, negative, or not a defined enum value.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFiniteNonNegative(Intensity);
        Guard.ThrowIfNotFiniteNonNegative(BlurSigma);
        Guard.ThrowIfUndefinedEnum(GradientOperator);
    }
}
