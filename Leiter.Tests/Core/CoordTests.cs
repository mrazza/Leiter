
using Xunit;
using Leiter.Core;

namespace Leiter.Tests.Core;

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
}
