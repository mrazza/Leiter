
using Xunit;
using Leiter.Core;

namespace Leiter.Tests.Core;

/// <summary>
/// Provides unit tests for <see cref="NormalizedCoordTests" />.
/// </summary>
public class NormalizedCoordTests
{
    /// <summary>
    /// Verifies that the distance should calculate correctly behaves correctly.
    /// </summary>
    [Fact]
    public void Distance_ShouldCalculateCorrectly()
    {
        var c1 = new NormalizedCoord(0.0, 0.0);
        var c2 = new NormalizedCoord(3.0, 4.0);
        Assert.Equal(5.0, c1.Distance(c2));
    }

    /// <summary>
    /// Verifies that the addition operator should add behaves correctly.
    /// </summary>
    [Fact]
    public void AdditionOperator_ShouldAdd()
    {
        var c1 = new NormalizedCoord(1.0, 2.0);
        var c2 = new NormalizedCoord(0.5, 0.5);
        var result = c1 + c2;
        Assert.Equal(1.5, result.X);
        Assert.Equal(2.5, result.Y);
    }

    /// <summary>
    /// Verifies that the subtraction operator should subtract scalar behaves correctly.
    /// </summary>
    [Fact]
    public void SubtractionOperator_ShouldSubtractScalar()
    {
        var c1 = new NormalizedCoord(1.0, 2.0);
        var result = c1 - 0.5;
        Assert.Equal(0.5, result.X);
        Assert.Equal(1.5, result.Y);
    }
}
