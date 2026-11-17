namespace Leiter.Algorithms.Stylization;

using System;

/// <summary>
/// Configures parameters for a piecewise tone reproduction curve with shadow toe, linear midtone, and highlight shoulder.
/// </summary>
/// <param name="MinTone">The output minimum tone floor mapped from input 0.0, in [0.0, 1.0) (default 0.05). Eliminates pure blacks for watercolor substrate simulation.</param>
/// <param name="MaxTone">The output maximum tone ceiling mapped from input 1.0, in (MinTone, 1.0] (default 0.96). Simulates unprinted paper tint without stark pure whites.</param>
/// <param name="ShadowKnee">The input domain breakpoint in (0.0, 1.0) marking the transition from shadow toe easing to linear midtones (default 0.15).</param>
/// <param name="HighlightKnee">The input domain breakpoint in (ShadowKnee, 1.0) marking the transition from linear midtones to highlight shoulder easing (default 0.85).</param>
public sealed record PiecewiseToneCurveOptions(
    double MinTone = 0.05,
    double MaxTone = 0.96,
    double ShadowKnee = 0.15,
    double HighlightKnee = 0.85)
{
    /// <summary>
    /// Validates that all tone curve parameters are finite, satisfy output bounds 0.0 &lt;= MinTone &lt; MaxTone &lt;= 1.0, and satisfy input knots 0.0 &lt; ShadowKnee &lt; HighlightKnee &lt; 1.0.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid domain.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(MinTone) || MinTone < 0.0 || MinTone >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(MinTone), MinTone, "MinTone must be finite and within [0.0, 1.0).");
        }

        if (!double.IsFinite(MaxTone) || MaxTone <= MinTone || MaxTone > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxTone), MaxTone, "MaxTone must be finite, strictly greater than MinTone, and within (MinTone, 1.0].");
        }

        if (!double.IsFinite(ShadowKnee) || ShadowKnee <= 0.0 || ShadowKnee >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(ShadowKnee), ShadowKnee, "ShadowKnee must be finite and within (0.0, 1.0).");
        }

        if (!double.IsFinite(HighlightKnee) || HighlightKnee <= ShadowKnee || HighlightKnee >= 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(HighlightKnee), HighlightKnee, "HighlightKnee must be finite, strictly greater than ShadowKnee, and within (ShadowKnee, 1.0).");
        }
    }
}
