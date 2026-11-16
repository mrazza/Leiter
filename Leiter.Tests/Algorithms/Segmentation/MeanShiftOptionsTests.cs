namespace Leiter.Tests.Algorithms.Segmentation;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MeanShiftOptions"/>.
/// </summary>
public class MeanShiftOptionsTests
{
    [Fact]
    public void Defaults_ShouldHaveExpectedValues()
    {
        var options = new MeanShiftOptions();

        Assert.Equal(8, options.SpatialRadius);
        Assert.Equal(5.5, options.RangeRadius);
        Assert.Equal(50, options.MinRegionSize);
        Assert.Equal(15, options.MaxIterations);
        Assert.Equal(0.05, options.ConvergenceEpsilon);
        Assert.Equal(GridConnectivity.EightConnected, options.Connectivity);

        options.Validate();
    }

    [Fact]
    public void Validate_InvalidSpatialRadius_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(SpatialRadius: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(SpatialRadius: -1).Validate());
    }

    [Fact]
    public void Validate_InvalidRangeRadius_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(RangeRadius: 0.0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(RangeRadius: -1.0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(RangeRadius: double.NaN).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(RangeRadius: double.PositiveInfinity).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(RangeRadius: double.NegativeInfinity).Validate());
    }

    [Fact]
    public void Validate_InvalidMinRegionSize_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(MinRegionSize: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(MinRegionSize: -1).Validate());
    }

    [Fact]
    public void Validate_InvalidMaxIterations_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(MaxIterations: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(MaxIterations: -1).Validate());
    }

    [Fact]
    public void Validate_InvalidConvergenceEpsilon_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(ConvergenceEpsilon: 0.0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(ConvergenceEpsilon: -0.1).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(ConvergenceEpsilon: double.NaN).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(ConvergenceEpsilon: double.PositiveInfinity).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(ConvergenceEpsilon: double.NegativeInfinity).Validate());
    }

    [Fact]
    public void Validate_InvalidConnectivity_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(Connectivity: (GridConnectivity)99).Validate());
    }

    [Fact]
    public void Validate_ValidCustomOptions_ShouldSucceed()
    {
        var options = new MeanShiftOptions(
            SpatialRadius: 16,
            RangeRadius: 10.0,
            MinRegionSize: 100,
            MaxIterations: 20,
            ConvergenceEpsilon: 0.01,
            Connectivity: GridConnectivity.FourConnected);

        options.Validate();

        Assert.Equal(16, options.SpatialRadius);
        Assert.Equal(10.0, options.RangeRadius);
        Assert.Equal(100, options.MinRegionSize);
        Assert.Equal(20, options.MaxIterations);
        Assert.Equal(0.01, options.ConvergenceEpsilon);
        Assert.Equal(GridConnectivity.FourConnected, options.Connectivity);
    }
}
