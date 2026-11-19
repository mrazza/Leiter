namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="WatercolorPalettes"/>.
/// </summary>
public class WatercolorPalettesTests
{
    [Fact]
    public void Presets_ShouldHaveExpectedNamesAndValidProperties()
    {
        Assert.Equal("Balanced Wash", WatercolorPalettes.BalancedWash.Name);
        Assert.Equal("Botanical Foliage", WatercolorPalettes.BotanicalFoliage.Name);
        Assert.Equal("Coastal Cerulean", WatercolorPalettes.CoastalCerulean.Name);
        Assert.Equal("Warm Ochre Earth", WatercolorPalettes.WarmOchreEarth.Name);
        Assert.Equal("Pastoral Sunlight", WatercolorPalettes.PastoralSunlight.Name);
        Assert.Equal("Atmospheric Dusk", WatercolorPalettes.AtmosphericDusk.Name);
        Assert.Equal("Crimson Floral", WatercolorPalettes.CrimsonFloral.Name);
        Assert.Equal("Sunlit Portrait", WatercolorPalettes.SunlitPortrait.Name);
        Assert.Equal("Vibrant Carnival", WatercolorPalettes.VibrantCarnival.Name);
        Assert.Equal("Misty Monochrome", WatercolorPalettes.MistyMonochrome.Name);

        Assert.Equal(Illuminant.E, WatercolorPalettes.BalancedWash.Illuminant);
        Assert.Equal(Illuminant.E, WatercolorPalettes.BotanicalFoliage.Illuminant);
        Assert.Equal(Illuminant.E, WatercolorPalettes.CoastalCerulean.Illuminant);
        Assert.Equal(Illuminant.E, WatercolorPalettes.WarmOchreEarth.Illuminant);
        Assert.Equal(Illuminant.E, WatercolorPalettes.PastoralSunlight.Illuminant);
        Assert.Equal(Illuminant.E, WatercolorPalettes.AtmosphericDusk.Illuminant);
        Assert.Equal(Illuminant.E, WatercolorPalettes.CrimsonFloral.Illuminant);
        Assert.Equal(Illuminant.E, WatercolorPalettes.SunlitPortrait.Illuminant);
        Assert.Equal(Illuminant.E, WatercolorPalettes.VibrantCarnival.Illuminant);
        Assert.Equal(Illuminant.E, WatercolorPalettes.MistyMonochrome.Illuminant);

        var weights = WatercolorPalettes.DefaultFeatureWeights;
        Assert.True(double.IsFinite(weights.MeanL) && weights.MeanL > 0.0);
        Assert.True(double.IsFinite(weights.MeanA) && weights.MeanA > 0.0);
        Assert.True(double.IsFinite(weights.MeanB) && weights.MeanB > 0.0);
        Assert.True(double.IsFinite(weights.StdDevL) && weights.StdDevL > 0.0);
        Assert.True(double.IsFinite(weights.StdDevA) && weights.StdDevA > 0.0);
        Assert.True(double.IsFinite(weights.StdDevB) && weights.StdDevB > 0.0);
    }

    [Fact]
    public void Defaults_ShouldContainAllPresetsInOrder()
    {
        Assert.Equal(10, WatercolorPalettes.Defaults.Count);

        Assert.Same(WatercolorPalettes.BalancedWash, WatercolorPalettes.Defaults[0]);
        Assert.Same(WatercolorPalettes.BotanicalFoliage, WatercolorPalettes.Defaults[1]);
        Assert.Same(WatercolorPalettes.CoastalCerulean, WatercolorPalettes.Defaults[2]);
        Assert.Same(WatercolorPalettes.WarmOchreEarth, WatercolorPalettes.Defaults[3]);
        Assert.Same(WatercolorPalettes.PastoralSunlight, WatercolorPalettes.Defaults[4]);
        Assert.Same(WatercolorPalettes.AtmosphericDusk, WatercolorPalettes.Defaults[5]);
        Assert.Same(WatercolorPalettes.CrimsonFloral, WatercolorPalettes.Defaults[6]);
        Assert.Same(WatercolorPalettes.SunlitPortrait, WatercolorPalettes.Defaults[7]);
        Assert.Same(WatercolorPalettes.VibrantCarnival, WatercolorPalettes.Defaults[8]);
        Assert.Same(WatercolorPalettes.MistyMonochrome, WatercolorPalettes.Defaults[9]);

        Assert.All(WatercolorPalettes.Defaults, palette =>
        {
            Assert.False(string.IsNullOrWhiteSpace(palette.Name));
            Assert.Equal(Illuminant.E, palette.Illuminant);
            Assert.True(double.IsFinite(palette.Statistics.MeanL));
            Assert.True(double.IsFinite(palette.Statistics.MeanA));
            Assert.True(double.IsFinite(palette.Statistics.MeanB));
            Assert.True(palette.Statistics.StdDevL > 0.0);
            Assert.True(palette.Statistics.StdDevA > 0.0);
            Assert.True(palette.Statistics.StdDevB > 0.0);
        });
    }
}
