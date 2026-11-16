namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MeanShiftMode"/>.
/// </summary>
public class MeanShiftModeTests
{
    [Fact]
    public void ConstructorAndProperties_ShouldInitializeCorrectly()
    {
        var color = new Lab32(50f, 10f, -20f);
        var mode = new MeanShiftMode(3.5, 7.25, color);

        Assert.Equal(3.5, mode.X);
        Assert.Equal(7.25, mode.Y);
        Assert.Equal(color, mode.Color);
    }

    [Fact]
    public void SpatialDistanceSquared_ShouldBeSymmetricAndExact()
    {
        var color = new Lab32(0f, 0f, 0f);
        var origin = new MeanShiftMode(0.0, 0.0, color);
        var point = new MeanShiftMode(3.0, 4.0, color);

        Assert.Equal(0.0, origin.SpatialDistanceSquared(origin));
        Assert.Equal(25.0, origin.SpatialDistanceSquared(point));
        Assert.Equal(25.0, point.SpatialDistanceSquared(origin));

        var m1 = new MeanShiftMode(-1.5, 2.5, color);
        var m2 = new MeanShiftMode(1.5, -1.5, color);
        Assert.Equal(25.0, m1.SpatialDistanceSquared(m2));
        Assert.Equal(25.0, m2.SpatialDistanceSquared(m1));
    }

    [Fact]
    public void RecordEquality_SameValues_ShouldBeEqual()
    {
        var color = new Lab32(40f, -12f, 35f);
        var m1 = new MeanShiftMode(10.5, 20.25, color);
        var m2 = new MeanShiftMode(10.5, 20.25, color);
        var m3 = new MeanShiftMode(10.5, 20.26, color);

        Assert.Equal(m1, m2);
        Assert.NotEqual(m1, m3);
    }
}
