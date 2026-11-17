namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Noise;

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
