
using Xunit;
using Leiter.Core;

namespace Leiter.Tests.Core;

/// <summary>
/// Provides unit tests for <see cref="Size" />.
/// </summary>
public class SizeTests
{
    [Fact]
    public void Constructor_AndProperties_ShouldWork()
    {
        var size = new Size(10, 20);
        Assert.Equal(10, size.Width);
        Assert.Equal(20, size.Height);
    }

    [Fact]
    public void Contains_ShouldValidateBoundsCorrectly()
    {
        var size = new Size(4, 3);

        Assert.True(size.Contains(0, 0));
        Assert.True(size.Contains(3, 2));
        Assert.True(size.Contains(new Coord(2, 1)));

        Assert.False(size.Contains(-1, 0));
        Assert.False(size.Contains(0, -1));
        Assert.False(size.Contains(4, 0));
        Assert.False(size.Contains(0, 3));
        Assert.False(size.Contains(new Coord(4, 3)));

        Assert.False(new Size(0, 5).Contains(0, 0));
        Assert.False(new Size(5, 0).Contains(0, 0));
        Assert.False(new Size(-2, 5).Contains(0, 0));
    }

    [Fact]
    public void Count_IndexOf_And_CoordOf_ShouldConvertBetweenLinearAnd2DCoordinates()
    {
        var size = new Size(4, 3);

        Assert.Equal(12, size.Count);
        Assert.Equal(6, size.IndexOf(2, 1));
        Assert.Equal(6, size.IndexOf(new Coord(2, 1)));
        Assert.Equal(new Coord(2, 1), size.CoordOf(6));

        Assert.Throws<OverflowException>(() => new Size(65536, 65536).Count);
        Assert.Throws<OverflowException>(() => new Size(100_000, 100_000).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Size(0, 5).CoordOf(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Size(-3, 5).CoordOf(0));
    }

    [Fact]
    public void ClampWindow_ShouldClampBoundsAndValidateInputs()
    {
        var size = new Size(10, 8);

        Assert.Equal((3, 7, 2, 6), size.ClampWindow(5, 4, 2));
        Assert.Equal((0, 3, 0, 3), size.ClampWindow(0, 0, 3));
        Assert.Equal((6, 9, 4, 7), size.ClampWindow(9, 7, 3));
        Assert.Equal((0, 9, 0, 7), size.ClampWindow(1, 1, 100));
        Assert.Equal((3, 3, 2, 2), size.ClampWindow(3, 2, 0));
        Assert.Equal((9, 9, 0, 0), size.ClampWindow(int.MaxValue, int.MinValue, 5));
        Assert.Equal((0, 9, 0, 7), size.ClampWindow(5, 4, int.MaxValue));

        ArgumentOutOfRangeException negRadiusEx = Assert.Throws<ArgumentOutOfRangeException>(() => size.ClampWindow(2, 2, -1));
        Assert.Equal("radius", negRadiusEx.ParamName);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Size(0, 5).ClampWindow(0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Size(5, 0).ClampWindow(0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Size(0, 0).ClampWindow(0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Size(-2, 5).ClampWindow(0, 0, 1));
    }

    [Fact]
    public void IsEmpty_Area_And_LinearDimension_ShouldHandleNormalZeroNegativeAndLargeSizes()
    {
        Assert.False(new Size(1, 1).IsEmpty);
        Assert.False(new Size(4, 3).IsEmpty);
        Assert.True(new Size(0, 0).IsEmpty);
        Assert.True(new Size(0, 5).IsEmpty);
        Assert.True(new Size(5, 0).IsEmpty);
        Assert.True(new Size(-2, 5).IsEmpty);
        Assert.True(new Size(5, -2).IsEmpty);

        Assert.Equal(12L, new Size(4, 3).Area);
        Assert.Equal(10_000_000_000L, new Size(100_000, 100_000).Area);
        Assert.Equal(0L, new Size(0, 100).Area);

        Assert.Equal(6.0, new Size(4, 9).LinearDimension, 12);
        Assert.Equal(100.0, new Size(100, 100).LinearDimension, 12);
        Assert.Equal(0.0, new Size(0, 5).LinearDimension);
        Assert.Equal(0.0, new Size(5, 0).LinearDimension);
        Assert.Equal(0.0, new Size(-4, 9).LinearDimension);
        Assert.Equal(0.0, new Size(4, -9).LinearDimension);
    }
}
