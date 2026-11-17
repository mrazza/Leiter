namespace Leiter.Algorithms.Stylization.Watercolor;

using System;

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
/// <param name="MajorRadius">The semi-major radius along the local tangent orientation for oriented elliptical Gaussian smoothing (default 6.0; must be finite and greater than 0.0).</param>
/// <param name="MinorRadius">The semi-minor radius perpendicular to the local tangent orientation for oriented elliptical Gaussian smoothing (default 1.5; must be finite and greater than 0.0).</param>
/// <param name="RandomSeed">The pseudo-random seed used for deterministic pigment seed scattering (default 1337).</param>
public sealed record WetInWetOptions(
    WetInWetMode Mode = WetInWetMode.Default,
    double ColorGradientThreshold = 0.1,
    double SalientHueThresholdDegrees = 20.0,
    double NonSalientHueThresholdDegrees = 90.0,
    double MinIntensityDifference = 0.02,
    double SalientOverlapThreshold = 0.5,
    int ScatterRadius = 4,
    double SeedProbability = 0.45,
    double MajorRadius = 6.0,
    double MinorRadius = 1.5,
    int RandomSeed = 1337)
{
    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid range.</exception>
    internal void Validate()
    {
        if (!Enum.IsDefined(Mode))
        {
            throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Unsupported wet-in-wet mode.");
        }

        if (!double.IsFinite(ColorGradientThreshold) || ColorGradientThreshold < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(ColorGradientThreshold), ColorGradientThreshold, "ColorGradientThreshold must be finite and non-negative.");
        }

        if (!double.IsFinite(SalientHueThresholdDegrees) || SalientHueThresholdDegrees < 0.0 || SalientHueThresholdDegrees > 180.0)
        {
            throw new ArgumentOutOfRangeException(nameof(SalientHueThresholdDegrees), SalientHueThresholdDegrees, "SalientHueThresholdDegrees must be finite and within [0, 180].");
        }

        if (!double.IsFinite(NonSalientHueThresholdDegrees) || NonSalientHueThresholdDegrees < 0.0 || NonSalientHueThresholdDegrees > 180.0)
        {
            throw new ArgumentOutOfRangeException(nameof(NonSalientHueThresholdDegrees), NonSalientHueThresholdDegrees, "NonSalientHueThresholdDegrees must be finite and within [0, 180].");
        }

        if (!double.IsFinite(MinIntensityDifference) || MinIntensityDifference < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinIntensityDifference), MinIntensityDifference, "MinIntensityDifference must be finite and non-negative.");
        }

        if (!double.IsFinite(SalientOverlapThreshold) || SalientOverlapThreshold < 0.0 || SalientOverlapThreshold > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(SalientOverlapThreshold), SalientOverlapThreshold, "SalientOverlapThreshold must be finite and within [0, 1].");
        }

        if (ScatterRadius < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(ScatterRadius), ScatterRadius, "ScatterRadius must be at least 1.");
        }

        if (!double.IsFinite(SeedProbability) || SeedProbability < 0.0 || SeedProbability > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(SeedProbability), SeedProbability, "SeedProbability must be finite and within [0, 1].");
        }

        if (!double.IsFinite(MajorRadius) || MajorRadius <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(MajorRadius), MajorRadius, "MajorRadius must be finite and greater than 0.0.");
        }

        if (!double.IsFinite(MinorRadius) || MinorRadius <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinorRadius), MinorRadius, "MinorRadius must be finite and greater than 0.0.");
        }
    }
}
