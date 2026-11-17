namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;

/// <summary>
/// Configures parameters for watercolor edge darkening (Bousseau et al. 2006).
/// </summary>
/// <param name="Intensity">The darkening intensity multiplier scaling edge pigment density (default 0.65; must be finite and non-negative).</param>
/// <param name="BlurSigma">The standard deviation for Gaussian smoothing of the gradient edge map (default 1.0; must be finite and non-negative).</param>
/// <param name="GradientOperator">The spatial gradient operator kernel used to compute edge magnitude (default <see cref="GradientOperator.Sobel"/>).</param>
public sealed record EdgeDarkeningOptions(
    double Intensity = 0.65,
    double BlurSigma = 1.0,
    GradientOperator GradientOperator = GradientOperator.Sobel)
{
    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite, negative, or not a defined enum value.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(Intensity) || Intensity < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(Intensity), Intensity, "Intensity must be finite and non-negative.");
        }

        if (!double.IsFinite(BlurSigma) || BlurSigma < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(BlurSigma), BlurSigma, "BlurSigma must be finite and non-negative.");
        }

        if (!Enum.IsDefined(GradientOperator))
        {
            throw new ArgumentOutOfRangeException(nameof(GradientOperator), GradientOperator, "GradientOperator must be a defined enum value.");
        }
    }
}
