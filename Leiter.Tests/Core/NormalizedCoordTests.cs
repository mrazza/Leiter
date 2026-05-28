
using Xunit;
using Leiter.Core;

namespace Leiter.Tests.Core;

public class NormalizedCoordTests
{
    [Fact]
    public void Distance_ShouldCalculateCorrectly()
    {
        var c1 = new NormalizedCoord(0.0, 0.0);
        var c2 = new NormalizedCoord(3.0, 4.0);
        Assert.Equal(5.0, c1.Distance(c2));
    }

    [Fact]
    public void AdditionOperator_ShouldAdd()
    {
        var c1 = new NormalizedCoord(1.0, 2.0);
        var c2 = new NormalizedCoord(0.5, 0.5);
        var result = c1 + c2;
        Assert.Equal(1.5, result.X);
        Assert.Equal(2.5, result.Y);
    }

    [Fact]
    public void SubtractionOperator_ShouldSubtractScalar()
    {
        var c1 = new NormalizedCoord(1.0, 2.0);
        var result = c1 - 0.5;
        Assert.Equal(0.5, result.X);
        Assert.Equal(1.5, result.Y);
    }
}
