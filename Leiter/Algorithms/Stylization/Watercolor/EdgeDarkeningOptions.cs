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
    GradientOperator GradientOperator = GradientOperator.Sobel) : IProfileTunableOptions<EdgeDarkeningOptions>
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
    public static EdgeDarkeningOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="EdgeDarkeningOptions"/> by scaling <paramref name="baseline"/> blur sigma linearly and compensating intensity for Sobel impulse attenuation at <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline edge darkening configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EdgeDarkeningOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static EdgeDarkeningOptions ForSize(
        Size size,
        EdgeDarkeningOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        if (totalPixels == 0)
        {
            return baseline;
        }

        double impulseCompensation = Math.Sqrt(((s * s) + 0.5) / 1.5);
        return baseline with
        {
            Intensity = baseline.Intensity * impulseCompensation,
            BlurSigma = baseline.BlurSigma * s
        };
    }

    /// <summary>
    /// Creates <see cref="EdgeDarkeningOptions"/> tuned for both the dimensions and color spread of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EdgeDarkeningOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static EdgeDarkeningOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage<EdgeDarkeningOptions>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="EdgeDarkeningOptions"/> tuned from <paramref name="baseline"/> for both the dimensions and color spread of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="baseline">The baseline edge darkening configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EdgeDarkeningOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static EdgeDarkeningOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        EdgeDarkeningOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage(image, baseline, referenceDimension);

    static EdgeDarkeningOptions IProfileTunableOptions<EdgeDarkeningOptions>.ForProfile(
        in ImageTuningProfile profile,
        EdgeDarkeningOptions baseline,
        double referenceDimension) =>
        ForProfile(in profile, baseline, referenceDimension);

    internal static EdgeDarkeningOptions ForProfile(
        in ImageTuningProfile profile,
        EdgeDarkeningOptions baseline,
        double referenceDimension)
    {
        EdgeDarkeningOptions scaled = ForSize(profile.ImageSize, baseline, referenceDimension);
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
