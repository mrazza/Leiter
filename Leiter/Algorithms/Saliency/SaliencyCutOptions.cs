namespace Leiter.Algorithms.Saliency;

/// <summary>
/// Configures parameters for Cheng et al.'s iterative <see cref="SaliencyCut"/> salient object segmentation.
/// </summary>
/// <param name="InitialThreshold">Initial fixed threshold in [0, 1] used on the saliency map (default <c>70 / 255</c> per Cheng et al. Sec. 5.1).</param>
/// <param name="MaxOuterIterations">Maximum number of trimap dilation/erosion refinement iterations (<c>≥ 1</c>).</param>
/// <param name="InnerGrabCutIterations">Number of GMM/min-cut iterations executed per outer trimap step (<c>≥ 1</c>).</param>
/// <param name="TrimapBandRadius">Morphological dilation/erosion radius in pixels used to construct the unknown boundary band (<c>≥ 1</c>).</param>
/// <param name="MixtureComponentCount">Number of Gaussian components per foreground/background GMM (<c>≥ 1</c>).</param>
/// <param name="SmoothnessWeight">Contrast-sensitive pairwise boundary smoothness weight γ (<c>≥ 0</c>).</param>
/// <param name="RetentionPolicy">Connected-component cleanup policy applied to initial and refined masks.</param>
/// <param name="MinAreaFraction">Minimum component area fraction in [0, 1] when <paramref name="RetentionPolicy"/> is <see cref="ComponentRetentionPolicy.MinimumAreaFraction"/>.</param>
/// <param name="MaxWorkingDimension">Optional maximum width or height for working resolution; <see langword="null"/> runs at full native resolution.</param>
public sealed record SaliencyCutOptions(
    double InitialThreshold = 70.0 / 255.0,
    int MaxOuterIterations = 4,
    int InnerGrabCutIterations = 2,
    int TrimapBandRadius = 10,
    int MixtureComponentCount = 5,
    double SmoothnessWeight = 50.0,
    ComponentRetentionPolicy RetentionPolicy = ComponentRetentionPolicy.MinimumAreaFraction,
    double MinAreaFraction = 0.01,
    int? MaxWorkingDimension = null)
{
    internal void Validate()
    {
        if (!double.IsFinite(InitialThreshold) || InitialThreshold < 0.0 || InitialThreshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(InitialThreshold), InitialThreshold, "InitialThreshold must be finite and within [0, 1].");
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxOuterIterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(InnerGrabCutIterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(TrimapBandRadius, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MixtureComponentCount, 1);
        if (!double.IsFinite(SmoothnessWeight) || SmoothnessWeight < 0.0)
            throw new ArgumentOutOfRangeException(nameof(SmoothnessWeight), SmoothnessWeight, "SmoothnessWeight must be finite and non-negative.");
        if (!Enum.IsDefined(RetentionPolicy))
            throw new ArgumentOutOfRangeException(nameof(RetentionPolicy), RetentionPolicy, "Unsupported component retention policy.");
        if (!double.IsFinite(MinAreaFraction) || MinAreaFraction < 0.0 || MinAreaFraction > 1.0)
            throw new ArgumentOutOfRangeException(nameof(MinAreaFraction), MinAreaFraction, "MinAreaFraction must be finite and within [0, 1].");
        if (MaxWorkingDimension is int maxDim)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDim, 2, nameof(MaxWorkingDimension));
    }
}
