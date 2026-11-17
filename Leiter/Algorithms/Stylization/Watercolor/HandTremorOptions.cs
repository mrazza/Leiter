namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Noise;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for the hand tremor perturbation filter in watercolor stylization.
/// </summary>
/// <param name="Amplitude">The maximum displacement amplitude in pixels (default 2.5; must be finite and non-negative).</param>
/// <param name="SalientAmplitudeScale">The minimum displacement amplitude scale applied inside salient regions (default 0.35; must be finite and within [0.0, 1.0]).</param>
/// <param name="SimilarHueThresholdDegrees">The maximum hue difference in degrees for boundaries to be classified as similar hue (Group 2) rather than contrasting (Group 3) (default 30.0; must be finite and within [0.0, 180.0]).</param>
/// <param name="OverlapDarkening">The blend factor for pigment darkening in stroke overlap regions (default 0.5; must be finite and within [0.0, 1.0]).</param>
/// <param name="PaperColor">The paper background color revealed in gap regions between contrasting strokes (default null; resolves to (250, 248, 242) if null).</param>
/// <param name="PrimaryNoise">Optional configuration for the primary Perlin noise displacement field.</param>
/// <param name="SecondaryNoise">Optional configuration for the secondary Perlin noise displacement field.</param>
public sealed record HandTremorOptions(
    double Amplitude = 2.5,
    double SalientAmplitudeScale = 0.35,
    double SimilarHueThresholdDegrees = 30.0,
    double OverlapDarkening = 0.5,
    Rgb8? PaperColor = null,
    PerlinNoiseOptions? PrimaryNoise = null,
    PerlinNoiseOptions? SecondaryNoise = null)
{
    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid range.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(Amplitude) || Amplitude < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(Amplitude), Amplitude, "Amplitude must be finite and non-negative.");
        }

        if (!double.IsFinite(SalientAmplitudeScale) || SalientAmplitudeScale < 0.0 || SalientAmplitudeScale > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(SalientAmplitudeScale), SalientAmplitudeScale, "SalientAmplitudeScale must be finite and within [0, 1].");
        }

        if (!double.IsFinite(SimilarHueThresholdDegrees) || SimilarHueThresholdDegrees < 0.0 || SimilarHueThresholdDegrees > 180.0)
        {
            throw new ArgumentOutOfRangeException(nameof(SimilarHueThresholdDegrees), SimilarHueThresholdDegrees, "SimilarHueThresholdDegrees must be finite and within [0, 180].");
        }

        if (!double.IsFinite(OverlapDarkening) || OverlapDarkening < 0.0 || OverlapDarkening > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(OverlapDarkening), OverlapDarkening, "OverlapDarkening must be finite and within [0, 1].");
        }

        PrimaryNoise?.Validate();
        SecondaryNoise?.Validate();
    }
}
