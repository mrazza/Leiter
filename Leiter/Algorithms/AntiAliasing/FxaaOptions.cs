namespace Leiter.Algorithms.AntiAliasing;

using System;

/// <summary>
/// Configures parameters for Fast Approximate Anti-Aliasing (FXAA 3.11).
/// </summary>
/// <param name="RelativeContrastThreshold">The relative contrast threshold determining edge sensitivity (default 0.125; must be finite and within (0, 1]).</param>
/// <param name="AbsoluteContrastThreshold">The minimum contrast threshold preventing dark region noise processing (default 0.0625; must be finite and non-negative).</param>
/// <param name="SubpixelBlendStrength">The strength of subpixel anti-aliasing blending (default 0.75; must be finite and within [0, 1]).</param>
/// <param name="MaxSpanSearchSteps">The maximum number of tangent search steps along an edge span (default 8; must be at least 1).</param>
public sealed record FxaaOptions(
    double RelativeContrastThreshold = 0.125,
    double AbsoluteContrastThreshold = 0.0625,
    double SubpixelBlendStrength = 0.75,
    int MaxSpanSearchSteps = 8)
{
    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid range.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(RelativeContrastThreshold) || RelativeContrastThreshold <= 0.0 || RelativeContrastThreshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(RelativeContrastThreshold), RelativeContrastThreshold, "RelativeContrastThreshold must be finite and within (0, 1].");

        if (!double.IsFinite(AbsoluteContrastThreshold) || AbsoluteContrastThreshold < 0.0)
            throw new ArgumentOutOfRangeException(nameof(AbsoluteContrastThreshold), AbsoluteContrastThreshold, "AbsoluteContrastThreshold must be finite and non-negative.");

        if (!double.IsFinite(SubpixelBlendStrength) || SubpixelBlendStrength < 0.0 || SubpixelBlendStrength > 1.0)
            throw new ArgumentOutOfRangeException(nameof(SubpixelBlendStrength), SubpixelBlendStrength, "SubpixelBlendStrength must be finite and within [0, 1].");

        if (MaxSpanSearchSteps < 1)
            throw new ArgumentOutOfRangeException(nameof(MaxSpanSearchSteps), MaxSpanSearchSteps, "MaxSpanSearchSteps must be at least 1.");
    }
}
