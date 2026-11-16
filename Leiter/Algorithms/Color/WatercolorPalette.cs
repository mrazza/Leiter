namespace Leiter.Algorithms.Color;

using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Describes a named reference watercolor color palette in terms of its CIELAB feature statistics
/// and reference illuminant.
/// </summary>
/// <param name="Name">The human-readable name of the watercolor palette.</param>
/// <param name="Statistics">The 6D CIELAB mean and standard-deviation feature vector.</param>
/// <param name="Illuminant">The CIE reference white illuminant in which <paramref name="Statistics"/> are expressed.</param>
public sealed record WatercolorPalette(
    string Name,
    LabColorStatistics Statistics,
    Illuminant Illuminant = Illuminant.E);
