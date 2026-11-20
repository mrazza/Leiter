
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
    }
}
