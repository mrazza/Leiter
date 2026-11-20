namespace Leiter.Algorithms.Saliency;

using Leiter.Core;

/// <summary>
/// Configures color-space post-processing options for <see cref="GlobalContrastSaliency"/>.
/// </summary>
/// <param name="EnableColorSpaceSmoothing">Whether to smooth saliency values across similar colors in CIELAB space (default <see langword="true"/>).</param>
public sealed record GlobalContrastSaliencyOptions(
    bool EnableColorSpaceSmoothing = true) : IAlgorithmOptions<GlobalContrastSaliencyOptions>
{
    /// <summary>
    /// Gets the default <see cref="GlobalContrastSaliencyOptions"/> instance.
    /// </summary>
    public static GlobalContrastSaliencyOptions Default { get; } = new();

    /// <summary>
    /// Validates all configuration parameters.
    /// </summary>
    public void Validate()
    {
    }
}
