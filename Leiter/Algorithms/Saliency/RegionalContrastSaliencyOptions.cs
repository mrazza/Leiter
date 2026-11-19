namespace Leiter.Algorithms.Saliency;

/// <summary>
/// Configures region-level post-processing options for <see cref="RegionalContrastSaliency"/>.
/// </summary>
/// <param name="EnableSmoothing">Whether to smooth saliency values across similar colors in feature space (default <see langword="true"/>).</param>
/// <param name="ComputeBorderRegions">Whether to attenuate saliency for regions touching image borders (default <see langword="true"/>).</param>
public sealed record RegionalContrastSaliencyOptions(
    bool EnableSmoothing = true,
    bool ComputeBorderRegions = true)
{
    /// <summary>
    /// Gets the default <see cref="RegionalContrastSaliencyOptions"/> instance.
    /// </summary>
    public static RegionalContrastSaliencyOptions Default { get; } = new();

    /// <summary>
    /// Validates all configuration parameters.
    /// </summary>
    public void Validate()
    {
    }
}
