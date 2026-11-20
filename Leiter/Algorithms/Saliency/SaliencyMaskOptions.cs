namespace Leiter.Algorithms.Saliency;

using Leiter.Algorithms.Morphology;
using Leiter.Core;

/// <summary>
/// Configures parameters for binarizing a continuous saliency map into a filtered foreground <see cref="MaskMatrix"/>.
/// </summary>
/// <param name="Method">The thresholding strategy used to separate salient foreground from background (default <see cref="SaliencyThresholdMethod.Otsu"/>).</param>
/// <param name="FixedThreshold">The fixed threshold in <c>[0, 1]</c> used when <paramref name="Method"/> is <see cref="SaliencyThresholdMethod.Fixed"/> (default <c>70 / 255</c>).</param>
/// <param name="RetentionPolicy">The connected-component retention policy applied after thresholding (default <see cref="ComponentRetentionPolicy.MinimumAreaFraction"/>).</param>
/// <param name="MinAreaFraction">The minimum component area fraction in <c>[0, 1]</c> used by <see cref="ComponentRetentionPolicy.MinimumAreaFraction"/> (default <c>0.01</c>).</param>
public sealed record SaliencyMaskOptions(
    SaliencyThresholdMethod Method = SaliencyThresholdMethod.Otsu,
    double FixedThreshold = SaliencyMaskOptions.DefaultFixedThreshold,
    ComponentRetentionPolicy RetentionPolicy = ComponentRetentionPolicy.MinimumAreaFraction,
    double MinAreaFraction = SaliencyMaskOptions.DefaultMinAreaFraction) : IAlgorithmOptions<SaliencyMaskOptions>
{
    /// <summary>
    /// The default fixed saliency threshold (<c>70 / 255</c>).
    /// </summary>
    public const double DefaultFixedThreshold = 70.0 / 255.0;

    /// <summary>
    /// The default minimum connected-component area fraction (<c>0.01</c>).
    /// </summary>
    public const double DefaultMinAreaFraction = BinaryMorphology.DefaultMinAreaFraction;

    /// <summary>
    /// Gets the default <see cref="SaliencyMaskOptions"/> instance.
    /// </summary>
    public static SaliencyMaskOptions Default { get; } = new();

    /// <summary>
    /// Validates that all saliency mask parameters are finite and within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any enum or numeric option is invalid.</exception>
    public void Validate()
    {
        Guard.ThrowIfUndefinedEnum(Method);
        Guard.ThrowIfNotFiniteInRange(FixedThreshold, 0.0, 1.0);
        Guard.ThrowIfUndefinedEnum(RetentionPolicy);
        Guard.ThrowIfNotFiniteInRange(MinAreaFraction, 0.0, 1.0);
    }
}
