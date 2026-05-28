
using Xunit;
using Leiter.Core;

namespace Leiter.Tests.Core;

/// <summary>
/// Provides unit tests or helpers for <see cref="CoordTests" />.
/// </summary>
public class CoordTests
{
    /// <summary>
    /// Verifies that the distance should calculate correctly behaves correctly.
    /// </summary>
    [Fact]
    public void Distance_ShouldCalculateCorrectly()
    {
        var c1 = new Coord(0, 0);
        var c2 = new Coord(3, 4);
        Assert.Equal(5.0, c1.Distance(c2));
    }

    /// <summary>
    /// Verifies that the addition operator should add coordinates behaves correctly.
    /// </summary>
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
