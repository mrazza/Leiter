namespace Leiter.Tests.Algorithms.Color;

using Leiter.Algorithms.Color;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="ColorTransferProfile"/>.
/// </summary>
public class ColorTransferProfileTests
{
    [Fact]
    public void Constructor_DefaultIlluminant_ShouldBeIlluminantE()
    {
        var stats = new LabColorStatistics(70.0, -5.0, 10.0, 15.0, 8.0, 12.0);
        var profile = new ColorTransferProfile("Test Wash", stats);

        Assert.Equal("Test Wash", profile.Name);
        Assert.Equal(stats, profile.Statistics);
        Assert.Equal(Illuminant.E, profile.Illuminant);
    }

    [Fact]
    public void Constructor_ExplicitIlluminant_ShouldSetIlluminant()
    {
        var stats = new LabColorStatistics(65.0, 2.0, 8.0, 14.0, 5.0, 6.0);
        var d65Profile = new ColorTransferProfile("Custom D65", stats, Illuminant.D65);

        Assert.Equal("Custom D65", d65Profile.Name);
        Assert.Equal(stats, d65Profile.Statistics);
        Assert.Equal(Illuminant.D65, d65Profile.Illuminant);
    }

    [Fact]
    public void Equality_ValueSemantics_ShouldBehaveCorrectly()
    {
        var pal1 = new ColorTransferProfile("Wash", new LabColorStatistics(10, 20, 30, 4, 5, 6), Illuminant.E);
        var pal2 = new ColorTransferProfile("Wash", new LabColorStatistics(10, 20, 30, 4, 5, 6), Illuminant.E);
        var pal3 = new ColorTransferProfile("Other", new LabColorStatistics(10, 20, 30, 4, 5, 6), Illuminant.E);
        var pal4 = new ColorTransferProfile("Wash", new LabColorStatistics(10, 20, 30, 4, 5, 6), Illuminant.D65);

        Assert.Equal(pal1, pal2);
        Assert.True(pal1 == pal2);
        Assert.False(pal1 != pal2);
        Assert.NotEqual(pal1, pal3);
        Assert.NotEqual(pal1, pal4);
        Assert.Equal(pal1.GetHashCode(), pal2.GetHashCode());
    }
}
