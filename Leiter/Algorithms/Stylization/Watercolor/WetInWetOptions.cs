namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for the wet-in-wet watercolor edge bleeding filter.
/// </summary>
/// <param name="Mode">The wet-in-wet boundary selection mode (default <see cref="WetInWetMode.Default"/>).</param>
/// <param name="ColorGradientThreshold">The threshold for region-averaged color gradient magnitude required to qualify a boundary (default 0.1; must be finite and non-negative).</param>
/// <param name="SalientHueThresholdDegrees">The maximum hue difference in degrees required to qualify a boundary incident to a salient region (default 20.0; must be finite and within [0.0, 180.0]).</param>
/// <param name="NonSalientHueThresholdDegrees">The maximum hue difference in degrees required to qualify a boundary between non-salient regions (default 90.0; must be finite and within [0.0, 180.0]).</param>
/// <param name="MinIntensityDifference">The minimum HSI intensity difference between adjacent regions required for bleeding (default 0.02; must be finite and non-negative).</param>
/// <param name="SalientOverlapThreshold">The minimum fraction of salient pixels required for a region to be classified as salient (default 0.5; must be finite and within [0.0, 1.0]).</param>
/// <param name="ScatterRadius">The maximum pixel distance from qualifying boundaries for pigment seed scattering (default 4; must be at least 1).</param>
/// <param name="SeedProbability">The probability in [0.0, 1.0] of depositing a pigment seed at a candidate pixel (default 0.45; must be finite and within [0.0, 1.0]).</param>
/// <param name="EllipticalFilter">Optional oriented elliptical Gaussian smoothing configuration; when <see langword="null"/>, defaults to <see cref="OrientedEllipticalFilterOptions.Default"/>.</param>
/// <param name="Seed">The pseudo-random seed used for deterministic pigment seed scattering (default 1337).</param>
public sealed record WetInWetOptions(
    WetInWetMode Mode = WetInWetMode.Default,
    double ColorGradientThreshold = 0.1,
    double SalientHueThresholdDegrees = 20.0,
    double NonSalientHueThresholdDegrees = 90.0,
    double MinIntensityDifference = 0.02,
    double SalientOverlapThreshold = 0.5,
    int ScatterRadius = 4,
    double SeedProbability = 0.45,
    OrientedEllipticalFilterOptions? EllipticalFilter = null,
    int Seed = 1337)
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default bleed radii and gradient thresholds apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="WetInWetOptions"/> instance.
    /// </summary>
    public static WetInWetOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="WetInWetOptions"/> with scatter radius, elliptical filter radii, and color gradient threshold scaled for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="WetInWetOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static WetInWetOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        WetInWetOptions defaults = Default;
        if (totalPixels == 0)
        {
            return defaults with
            {
                EllipticalFilter = OrientedEllipticalFilterOptions.Default
            };
        }

        return defaults with
        {
            ScatterRadius = OptionTuning.RoundPositiveInt(defaults.ScatterRadius * s),
            EllipticalFilter = OrientedEllipticalFilterOptions.ForSize(size, referenceDimension),
            ColorGradientThreshold = Math.Clamp(defaults.ColorGradientThreshold / s, 0.005, 0.5)
        };
    }

    /// <summary>
    /// Creates <see cref="WetInWetOptions"/> tuned for both the dimensions and scene clutter of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="WetInWetOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static WetInWetOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        ImageTuningProfile profile = ImageTuningProfile.Analyze(image);
        return ForProfile(in profile, referenceDimension);
    }

    internal static WetInWetOptions ForProfile(in ImageTuningProfile profile, double referenceDimension)
    {
        WetInWetOptions scaled = ForSize(profile.ImageSize, referenceDimension);
        if (profile.IsEmpty)
        {
            return scaled;
        }

        return scaled with
        {
            ColorGradientThreshold = Math.Clamp(scaled.ColorGradientThreshold * profile.ClutterFactor, 0.005, 0.5)
        };
    }

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid range.</exception>
    public void Validate()
    {
        Guard.ThrowIfUndefinedEnum(Mode);
        Guard.ThrowIfNotFiniteNonNegative(ColorGradientThreshold);
        Guard.ThrowIfNotFiniteInRange(SalientHueThresholdDegrees, 0.0, 180.0);
        Guard.ThrowIfNotFiniteInRange(NonSalientHueThresholdDegrees, 0.0, 180.0);
        Guard.ThrowIfNotFiniteNonNegative(MinIntensityDifference);
        Guard.ThrowIfNotFiniteInRange(SalientOverlapThreshold, 0.0, 1.0);
        ArgumentOutOfRangeException.ThrowIfLessThan(ScatterRadius, 1);
        Guard.ThrowIfNotFiniteInRange(SeedProbability, 0.0, 1.0);
        EllipticalFilter?.Validate();
    }
}
