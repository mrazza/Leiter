namespace Leiter.Algorithms;

using System;

/// <summary>
/// Configures parameters for the oriented elliptical Gaussian smoothing filter.
/// </summary>
/// <param name="MajorRadius">The semi-major radius along the local tangent orientation (default 6.0; must be finite and greater than 0.0).</param>
/// <param name="MinorRadius">The semi-minor radius perpendicular to the local tangent orientation (default 1.5; must be finite and greater than 0.0).</param>
/// <param name="SigmaScale">The Gaussian standard deviation scaling factor relative to the radii (default 0.5; must be finite and greater than 0.0).</param>
public sealed record OrientedEllipticalFilterOptions(
    double MajorRadius = 6.0,
    double MinorRadius = 1.5,
    double SigmaScale = 0.5)
{
    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or less than or equal to 0.0.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(MajorRadius) || MajorRadius <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(MajorRadius), MajorRadius, "MajorRadius must be finite and greater than 0.0.");

        if (!double.IsFinite(MinorRadius) || MinorRadius <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(MinorRadius), MinorRadius, "MinorRadius must be finite and greater than 0.0.");

        if (!double.IsFinite(SigmaScale) || SigmaScale <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(SigmaScale), SigmaScale, "SigmaScale must be finite and greater than 0.0.");
    }
}
