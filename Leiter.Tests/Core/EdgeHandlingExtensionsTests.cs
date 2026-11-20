namespace Leiter.Tests.Core;

using System;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="EdgeHandlingExtensions"/>.
/// </summary>
public class EdgeHandlingExtensionsTests
{
    [Fact]
    public void ResolveIndex_Extend_ShouldClampIndicesAcrossAxisLengths()
    {
        Assert.Equal(0, EdgeHandling.Extend.ResolveIndex(-10, 5));
        Assert.Equal(0, EdgeHandling.Extend.ResolveIndex(-1, 5));
        Assert.Equal(0, EdgeHandling.Extend.ResolveIndex(0, 5));
        Assert.Equal(2, EdgeHandling.Extend.ResolveIndex(2, 5));
        Assert.Equal(4, EdgeHandling.Extend.ResolveIndex(4, 5));
        Assert.Equal(4, EdgeHandling.Extend.ResolveIndex(5, 5));
        Assert.Equal(4, EdgeHandling.Extend.ResolveIndex(100, 5));
        Assert.Equal(0, EdgeHandling.Extend.ResolveIndex(int.MinValue, 5));
        Assert.Equal(4, EdgeHandling.Extend.ResolveIndex(int.MaxValue, 5));

        Assert.Equal(0, EdgeHandling.Extend.ResolveIndex(-5, 1));
        Assert.Equal(0, EdgeHandling.Extend.ResolveIndex(0, 1));
        Assert.Equal(0, EdgeHandling.Extend.ResolveIndex(5, 1));
    }

    [Fact]
    public void ResolveIndex_InvalidLengthOrEnum_ShouldThrowArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException zeroLenEx = Assert.Throws<ArgumentOutOfRangeException>(() => EdgeHandling.Extend.ResolveIndex(0, 0));
        Assert.Equal("length", zeroLenEx.ParamName);

        ArgumentOutOfRangeException negLenEx = Assert.Throws<ArgumentOutOfRangeException>(() => EdgeHandling.Extend.ResolveIndex(0, -3));
        Assert.Equal("length", negLenEx.ParamName);

        ArgumentOutOfRangeException enumEx = Assert.Throws<ArgumentOutOfRangeException>(() => ((EdgeHandling)999).ResolveIndex(1, 5));
        Assert.Equal("edgeHandling", enumEx.ParamName);
    }

    [Fact]
    public void ResolveCoordinate_Extend_ShouldClampFiniteAndInfiniteCoordinates()
    {
        Assert.Equal(0.0, EdgeHandling.Extend.ResolveCoordinate(-3.5, 5));
        Assert.Equal(0.0, EdgeHandling.Extend.ResolveCoordinate(0.0, 5));
        Assert.Equal(1.75, EdgeHandling.Extend.ResolveCoordinate(1.75, 5));
        Assert.Equal(4.0, EdgeHandling.Extend.ResolveCoordinate(4.0, 5));
        Assert.Equal(4.0, EdgeHandling.Extend.ResolveCoordinate(10.5, 5));
        Assert.Equal(0.0, EdgeHandling.Extend.ResolveCoordinate(double.NegativeInfinity, 5));
        Assert.Equal(4.0, EdgeHandling.Extend.ResolveCoordinate(double.PositiveInfinity, 5));

        Assert.Equal(0.0, EdgeHandling.Extend.ResolveCoordinate(-2.5, 1));
        Assert.Equal(0.0, EdgeHandling.Extend.ResolveCoordinate(0.0, 1));
        Assert.Equal(0.0, EdgeHandling.Extend.ResolveCoordinate(2.5, 1));
        Assert.Equal(0.0, EdgeHandling.Extend.ResolveCoordinate(double.NegativeInfinity, 1));
        Assert.Equal(0.0, EdgeHandling.Extend.ResolveCoordinate(double.PositiveInfinity, 1));
    }

    [Fact]
    public void ResolveCoordinate_InvalidArguments_ShouldThrowArgumentOutOfRangeException()
    {
        ArgumentOutOfRangeException zeroLenEx = Assert.Throws<ArgumentOutOfRangeException>(() => EdgeHandling.Extend.ResolveCoordinate(0.0, 0));
        Assert.Equal("length", zeroLenEx.ParamName);

        ArgumentOutOfRangeException negLenEx = Assert.Throws<ArgumentOutOfRangeException>(() => EdgeHandling.Extend.ResolveCoordinate(0.0, -2));
        Assert.Equal("length", negLenEx.ParamName);

        ArgumentOutOfRangeException nanEx = Assert.Throws<ArgumentOutOfRangeException>(() => EdgeHandling.Extend.ResolveCoordinate(double.NaN, 5));
        Assert.Equal("coord", nanEx.ParamName);

        ArgumentOutOfRangeException enumEx = Assert.Throws<ArgumentOutOfRangeException>(() => ((EdgeHandling)999).ResolveCoordinate(1.0, 5));
        Assert.Equal("edgeHandling", enumEx.ParamName);
    }
}
