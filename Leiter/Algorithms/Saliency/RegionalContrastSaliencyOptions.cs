namespace Leiter.Algorithms.Saliency;

using System;
using Leiter.Core;

/// <summary>
/// Configures region-level post-processing and spatial bias options for <see cref="RegionalContrastSaliency"/>.
/// </summary>
/// <param name="EnableSmoothing">Whether to smooth saliency values across similar colors in feature space (default <see langword="true"/>).</param>
/// <param name="ComputeBorderRegions">Whether to attenuate saliency for regions touching image borders (default <see langword="true"/>).</param>
/// <param name="CenterPriorWeight">The exponential center-prior falloff weight <c>w</c> in <c>exp(-w * d²)</c> (default <c>9.0</c>, matching Cheng et al. TPAMI 2015; set to <c>0.0</c> for unbiased CVPR 2011 behavior; must be finite and non-negative).</param>
public sealed record RegionalContrastSaliencyOptions(
    bool EnableSmoothing = true,
    bool ComputeBorderRegions = true,
    double CenterPriorWeight = 9.0) : IValidatableOptions
{
    /// <summary>
    /// Gets the default <see cref="RegionalContrastSaliencyOptions"/> instance.
    /// </summary>
    public static RegionalContrastSaliencyOptions Default { get; } = new();

    /// <summary>
    /// Validates all configuration parameters.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="CenterPriorWeight"/> is negative or non-finite.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFiniteNonNegative(CenterPriorWeight);
    }
}
