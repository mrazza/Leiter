namespace Leiter.Algorithms.Color;

using Leiter.Core;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Describes a named reference CIELAB color transfer profile and its reference illuminant.
/// </summary>
/// <param name="Name">The human-readable name of the color profile.</param>
/// <param name="Statistics">The 6D CIELAB mean and standard-deviation feature vector.</param>
/// <param name="Illuminant">The CIE reference white illuminant in which <paramref name="Statistics"/> are expressed.</param>
public sealed record ColorTransferProfile(
    string Name,
    LabColorStatistics Statistics,
    Illuminant Illuminant = Illuminant.E) : IValidatableOptions
{
    /// <summary>
    /// Validates that <see cref="Name"/>, <see cref="Illuminant"/>, and <see cref="Statistics"/> are valid and finite.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <see cref="Name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="Name"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Illuminant"/> is undefined, any mean is non-finite, or any standard deviation is negative or non-finite.</exception>
    public void Validate() => Validate(paramName: null);

    internal void Validate(string? paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name, paramName ?? nameof(Name));
        Guard.ThrowIfUndefinedEnum(Illuminant, paramName ?? nameof(Illuminant));
        Guard.ThrowIfNotFinite(Statistics.MeanL, paramName ?? nameof(Statistics));
        Guard.ThrowIfNotFinite(Statistics.MeanA, paramName ?? nameof(Statistics));
        Guard.ThrowIfNotFinite(Statistics.MeanB, paramName ?? nameof(Statistics));
        Guard.ThrowIfNotFiniteNonNegative(Statistics.StdDevL, paramName ?? nameof(Statistics));
        Guard.ThrowIfNotFiniteNonNegative(Statistics.StdDevA, paramName ?? nameof(Statistics));
        Guard.ThrowIfNotFiniteNonNegative(Statistics.StdDevB, paramName ?? nameof(Statistics));
    }
}
