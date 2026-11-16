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

/// <summary>
/// Built-in curated watercolor palettes expressed in CIELAB under <see cref="Illuminant.E"/>.
/// </summary>
public static class WatercolorPalettes
{
    /// <summary>
    /// A light, airy watercolor wash with warm paper highlights and neutral slate-umber midtones.
    /// </summary>
    public static WatercolorPalette BalancedWash { get; } =
        new("Balanced Wash", new(71.00, -6.77, -1.30, 17.36, 8.17, 11.92), Illuminant.E);

    /// <summary>
    /// Soft botanical sap greens paired with sunlit warm paper and muted olive shadows.
    /// </summary>
    public static WatercolorPalette BotanicalFoliage { get; } =
        new("Botanical Foliage", new(66.10, -19.68, 17.39, 16.83, 7.17, 9.12), Illuminant.E);

    /// <summary>
    /// Bright coastal cerulean and cobalt blues balanced with sandy neutrals.
    /// </summary>
    public static WatercolorPalette CoastalCerulean { get; } =
        new("Coastal Cerulean", new(71.37, -7.59, -12.37, 16.80, 7.72, 16.68), Illuminant.E);

    /// <summary>
    /// Sunbaked yellow ochre, burnt sienna, and warm earth shadows.
    /// </summary>
    public static WatercolorPalette WarmOchreEarth { get; } =
        new("Warm Ochre Earth", new(64.55, 1.28, 20.01, 19.53, 8.04, 12.56), Illuminant.E);

    /// <summary>
    /// Sunlit gamboge/ochre highlights with open foliage greens and soft sky blue washes.
    /// </summary>
    public static WatercolorPalette PastoralSunlight { get; } =
        new("Pastoral Sunlight", new(72.96, -12.80, 12.37, 16.34, 7.23, 18.28), Illuminant.E);

    /// <summary>
    /// Atmospheric twilight lavenders, ultramarine indigo, and subtle warm peach accents.
    /// </summary>
    public static WatercolorPalette AtmosphericDusk { get; } =
        new("Atmospheric Dusk", new(63.54, 1.14, -9.49, 18.80, 4.51, 14.65), Illuminant.E);

    /// <summary>
    /// Gets the default collection of built-in watercolor palettes used for automatic palette matching.
    /// </summary>
    public static IReadOnlyList<WatercolorPalette> Defaults { get; } =
    [
        BalancedWash,
        BotanicalFoliage,
        CoastalCerulean,
        WarmOchreEarth,
        PastoralSunlight,
        AtmosphericDusk
    ];
}
