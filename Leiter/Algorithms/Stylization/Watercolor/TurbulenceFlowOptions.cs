namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Noise;

/// <summary>
/// Configures parameters for watercolor turbulence flow and pigment dispersion (Bousseau et al. 2006).
/// </summary>
/// <param name="Intensity">The turbulence flow intensity multiplier scaling noise density offsets (default 0.35; must be finite and non-negative).</param>
/// <param name="NoiseOptions">Optional Perlin noise options for generating the fractal turbulence field. If <see langword="null"/>, default flow noise parameters are used.</param>
public sealed record TurbulenceFlowOptions(
    double Intensity = 0.35,
    PerlinNoiseOptions? NoiseOptions = null)
{
    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite, negative, or contains invalid noise options.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(Intensity) || Intensity < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(Intensity), Intensity, "Intensity must be finite and non-negative.");
        }

        NoiseOptions?.Validate();
    }
}
