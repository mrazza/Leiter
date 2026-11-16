namespace Leiter.Algorithms.Segmentation;

/// <summary>
/// Configures parameters for <see cref="GrabCut"/> foreground/background segmentation.
/// </summary>
/// <param name="Iterations">The maximum number of alternating GMM-update and graph-cut iterations (<c>≥ 1</c>).</param>
/// <param name="MixtureComponentCount">The number of Gaussian components in each foreground/background GMM (<c>≥ 1</c>).</param>
/// <param name="SmoothnessWeight">The contrast-sensitive pairwise boundary smoothness weight γ (<c>≥ 0</c>).</param>
public sealed record GrabCutOptions(
    int Iterations = 5,
    int MixtureComponentCount = 5,
    double SmoothnessWeight = 50.0)
{
    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Iterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MixtureComponentCount, 1);
        if (double.IsNaN(SmoothnessWeight) || double.IsInfinity(SmoothnessWeight) || SmoothnessWeight < 0.0)
            throw new ArgumentOutOfRangeException(nameof(SmoothnessWeight), SmoothnessWeight, "SmoothnessWeight must be a finite non-negative number.");
    }
}
