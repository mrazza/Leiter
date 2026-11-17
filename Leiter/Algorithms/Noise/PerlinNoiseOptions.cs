namespace Leiter.Algorithms.Noise;

using System;

/// <summary>
/// Configures parameters for Perlin gradient noise and fractal field synthesis.
/// </summary>
/// <param name="Seed">The pseudo-random seed used to initialize the permutation table (default 1337).</param>
/// <param name="Frequency">The base spatial frequency multiplier for coordinate scaling (default 0.05; must be finite and greater than 0.0).</param>
/// <param name="Octaves">The number of fractal noise octaves to blend (default 4; must be between 1 and 16 inclusive).</param>
/// <param name="Lacunarity">The frequency multiplier per octave step (default 2.0; must be finite and greater than 0.0).</param>
/// <param name="Persistence">The amplitude decay multiplier per octave step (default 0.5; must be finite and non-negative).</param>
public sealed record PerlinNoiseOptions(
    int Seed = 1337,
    double Frequency = 0.05,
    int Octaves = 4,
    double Lacunarity = 2.0,
    double Persistence = 0.5)
{
    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or outside its valid range.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(Frequency) || Frequency <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(Frequency), Frequency, "Frequency must be finite and greater than 0.0.");

        if (Octaves is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(Octaves), Octaves, "Octaves must be between 1 and 16 inclusive.");

        if (!double.IsFinite(Lacunarity) || Lacunarity <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(Lacunarity), Lacunarity, "Lacunarity must be finite and greater than 0.0.");

        if (!double.IsFinite(Persistence) || Persistence < 0.0)
            throw new ArgumentOutOfRangeException(nameof(Persistence), Persistence, "Persistence must be finite and non-negative.");
    }
}
