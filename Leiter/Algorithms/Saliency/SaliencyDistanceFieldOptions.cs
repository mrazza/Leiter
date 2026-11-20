namespace Leiter.Algorithms.Saliency;

using Leiter.Core;

/// <summary>
/// Configures smoothing options for <see cref="SaliencyDistanceField"/>.
/// </summary>
/// <param name="SmoothingSigma">
/// Optional explicit Gaussian smoothing σ; when <see langword="null"/>, defaults to
/// <c>DefaultSigmaScale * min(Width, Height)</c>. Passing <c>0</c> disables smoothing.
/// </param>
/// <param name="DefaultSigmaScale">The fraction of <c>min(Width, Height)</c> used when <paramref name="SmoothingSigma"/> is <see langword="null"/> (default <c>0.03</c>).</param>
public sealed record SaliencyDistanceFieldOptions(
    double? SmoothingSigma = null,
    double DefaultSigmaScale = 0.03) : IAlgorithmOptions<SaliencyDistanceFieldOptions>
{
    /// <summary>
    /// Gets the default <see cref="SaliencyDistanceFieldOptions"/> instance.
    /// </summary>
    public static SaliencyDistanceFieldOptions Default { get; } = new();

    /// <summary>
    /// Validates that <see cref="DefaultSigmaScale"/> and any explicit <see cref="SmoothingSigma"/> are finite and non-negative.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is negative or non-finite.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFiniteNonNegative(DefaultSigmaScale);
        if (SmoothingSigma is double explicitSigma)
            Guard.ThrowIfNotFiniteNonNegative(explicitSigma, nameof(SmoothingSigma));
    }
}
