
using Xunit;
using Leiter.Core;

namespace Leiter.Tests.Core;

/// <summary>
/// Provides unit tests for <see cref="Coord" />.
/// </summary>
public class CoordTests
{
    [Fact]
    public void Distance_ShouldCalculateCorrectly()
    {
        var c1 = new Coord(0, 0);
        var c2 = new Coord(3, 4);
        Assert.Equal(5.0, c1.Distance(c2));
    }

    [Fact]
    public void AdditionOperator_ShouldAddCoordinates()
    {
        var c1 = new Coord(1, 2);
        var c2 = new Coord(3, 4);
        var result = c1 + c2;
        Assert.Equal(4, result.X);
        Assert.Equal(6, result.Y);
    }

    [Fact]
    public void DistanceSquared_ShouldComputeExactSquaredDistanceWithoutOverflow()
    {
        var c1 = new Coord(0, 0);
        var c2 = new Coord(-3, 4);
        Assert.Equal(25L, c1.DistanceSquared(c2));

        var extremeA = new Coord(100_000, 0);
        var extremeB = new Coord(0, 0);
        Assert.Equal(10_000_000_000L, extremeA.DistanceSquared(extremeB));
    }
}
