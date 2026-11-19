namespace Leiter.Algorithms.Stylization.Watercolor;

using Leiter.Algorithms.Color;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Built-in curated watercolor palettes expressed as <see cref="ColorTransferProfile"/> presets in CIELAB under <see cref="Illuminant.E"/>.
/// </summary>
public static class WatercolorPalettes
{
    /// <summary>
    /// A light, airy watercolor wash with warm paper highlights and neutral slate-umber midtones.
    /// </summary>
    public static ColorTransferProfile BalancedWash { get; } =
        new("Balanced Wash", new(71.01, -0.35, 2.95, 17.36, 8.17, 11.92), Illuminant.E);

    /// <summary>
    /// Soft botanical sap greens paired with sunlit warm paper and muted olive shadows.
    /// </summary>
    public static ColorTransferProfile BotanicalFoliage { get; } =
        new("Botanical Foliage", new(66.09, -12.67, 20.82, 16.83, 7.17, 9.12), Illuminant.E);

    /// <summary>
    /// Bright coastal cerulean and cobalt blues balanced with sandy neutrals.
    /// </summary>
    public static ColorTransferProfile CoastalCerulean { get; } =
        new("Coastal Cerulean", new(71.31, -1.56, -7.91, 16.80, 7.72, 16.68), Illuminant.E);

    /// <summary>
    /// Sunbaked yellow ochre, burnt sienna, and warm earth shadows.
    /// </summary>
    public static ColorTransferProfile WarmOchreEarth { get; } =
        new("Warm Ochre Earth", new(64.73, 7.64, 23.64, 19.53, 8.04, 12.56), Illuminant.E);

    /// <summary>
    /// Sunlit gamboge/ochre highlights with open foliage greens and soft sky blue washes.
    /// </summary>
    public static ColorTransferProfile PastoralSunlight { get; } =
        new("Pastoral Sunlight", new(72.99, -5.64, 16.35, 16.34, 7.23, 18.28), Illuminant.E);

    /// <summary>
    /// Atmospheric twilight lavenders, ultramarine indigo, and subtle warm peach accents.
    /// </summary>
    public static ColorTransferProfile AtmosphericDusk { get; } =
        new("Atmospheric Dusk", new(63.57, 6.54, -5.33, 18.80, 4.51, 14.65), Illuminant.E);

    /// <summary>
    /// Vivid carmine and vermilion botanical florals balanced against soft leaf greens and warm paper highlights.
    /// </summary>
    public static ColorTransferProfile CrimsonFloral { get; } =
        new("Crimson Floral", new(72.50, 9.80, 16.40, 16.20, 20.50, 14.80), Illuminant.E);

    /// <summary>
    /// Warm skin-toned terracotta and rose washes suited to sunlit portraits and figurative scenes.
    /// </summary>
    public static ColorTransferProfile SunlitPortrait { get; } =
        new("Sunlit Portrait", new(67.20, 8.90, 11.80, 18.90, 10.60, 12.40), Illuminant.E);

    /// <summary>
    /// Richly saturated multi-hue watercolor palette spanning primary reds, golds, and cobalt blues.
    /// </summary>
    public static ColorTransferProfile VibrantCarnival { get; } =
        new("Vibrant Carnival", new(66.40, 7.20, 5.80, 19.40, 19.80, 23.20), Illuminant.E);

    /// <summary>
    /// Low-chroma silver-slate and warm graphite grisaille wash for muted or near-monochrome photographs.
    /// </summary>
    public static ColorTransferProfile MistyMonochrome { get; } =
        new("Misty Monochrome", new(72.40, 0.40, 2.60, 16.80, 3.20, 5.40), Illuminant.E);

    /// <summary>
    /// Gets the default mutual-information-inspired 6D CIELAB feature weights for palette matching (Wang et al. 2014, Sec. 3.1; Guo et al. 2006),
    /// emphasizing chromatic mean and spread (<c>a*</c>, <c>b*</c>) over lightness (<c>L*</c>).
    /// </summary>
    public static LabColorStatistics DefaultFeatureWeights { get; } = new(0.6, 1.5, 1.5, 0.8, 1.4, 1.4);

    /// <summary>
    /// Gets the default collection of built-in watercolor palettes used for automatic palette matching.
    /// </summary>
    public static IReadOnlyList<ColorTransferProfile> Defaults { get; } =
    [
        BalancedWash,
        BotanicalFoliage,
        CoastalCerulean,
        WarmOchreEarth,
        PastoralSunlight,
        AtmosphericDusk,
        CrimsonFloral,
        SunlitPortrait,
        VibrantCarnival,
        MistyMonochrome
    ];
}
