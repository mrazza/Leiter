namespace Leiter.Tests.Algorithms.Color;

using Leiter.Algorithms.Color;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="WatercolorPalette"/>.
/// </summary>
public class WatercolorPaletteTests
{
    [Fact]
    public void Constructor_DefaultIlluminant_ShouldBeIlluminantE()
    {
        var stats = new LabColorStatistics(70.0, -5.0, 10.0, 15.0, 8.0, 12.0);
        var palette = new WatercolorPalette("Test Wash", stats);

        Assert.Equal("Test Wash", palette.Name);
        Assert.Equal(stats, palette.Statistics);
        Assert.Equal(Illuminant.E, palette.Illuminant);
    }

    [Fact]
    public void Constructor_ExplicitIlluminant_ShouldSetIlluminant()
    {
        var stats = new LabColorStatistics(65.0, 2.0, 8.0, 14.0, 5.0, 6.0);
        var d65Palette = new WatercolorPalette("Custom D65", stats, Illuminant.D65);

        Assert.Equal("Custom D65", d65Palette.Name);
        Assert.Equal(stats, d65Palette.Statistics);
        Assert.Equal(Illuminant.D65, d65Palette.Illuminant);
    }

    [Fact]
    public void Equality_ValueSemantics_ShouldBehaveCorrectly()
    {
        var pal1 = new WatercolorPalette("Wash", new LabColorStatistics(10, 20, 30, 4, 5, 6), Illuminant.E);
        var pal2 = new WatercolorPalette("Wash", new LabColorStatistics(10, 20, 30, 4, 5, 6), Illuminant.E);
        var pal3 = new WatercolorPalette("Other", new LabColorStatistics(10, 20, 30, 4, 5, 6), Illuminant.E);
        var pal4 = new WatercolorPalette("Wash", new LabColorStatistics(10, 20, 30, 4, 5, 6), Illuminant.D65);

        Assert.Equal(pal1, pal2);
        Assert.True(pal1 == pal2);
        Assert.False(pal1 != pal2);
        Assert.NotEqual(pal1, pal3);
        Assert.NotEqual(pal1, pal4);
        Assert.Equal(pal1.GetHashCode(), pal2.GetHashCode());
    }
}
