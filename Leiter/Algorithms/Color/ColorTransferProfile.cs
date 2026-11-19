namespace Leiter.Algorithms.Color;

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
    Illuminant Illuminant = Illuminant.E);
