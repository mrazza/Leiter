namespace Leiter.Tests.Algorithms.Segmentation;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="EgbiOptions"/>.
/// </summary>
public class EgbiOptionsTests
{
    /// <summary>
    /// Verifies that default EGBI options match expected values and pass validation.
    /// </summary>
    [Fact]
    public void Defaults_ShouldHaveExpectedValuesAndPassValidation()
    {
        var options = new EgbiOptions();

        Assert.Equal(100.0, options.KFactor);
        Assert.Equal(1.0, options.Epsilon);
        Assert.Equal(100, options.MinRegionSize);
        Assert.Equal(GridConnectivity.EightConnected, options.Connectivity);

        options.Validate();
    }

    /// <summary>
    /// Verifies that invalid <see cref="EgbiOptions.KFactor"/> values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="kFactor">The invalid k-factor value.</param>
    [Theory]
    [InlineData(-0.1)]
    [InlineData(-100.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidKFactor_ShouldThrowArgumentOutOfRangeException(double kFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EgbiOptions(KFactor: kFactor).Validate());
    }

    /// <summary>
    /// Verifies that invalid <see cref="EgbiOptions.Epsilon"/> values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="epsilon">The invalid epsilon value.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidEpsilon_ShouldThrowArgumentOutOfRangeException(double epsilon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EgbiOptions(Epsilon: epsilon).Validate());
    }

    /// <summary>
    /// Verifies that negative <see cref="EgbiOptions.MinRegionSize"/> values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="minRegionSize">The invalid minimum region size.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(-50)]
    public void Validate_NegativeMinRegionSize_ShouldThrowArgumentOutOfRangeException(int minRegionSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EgbiOptions(MinRegionSize: minRegionSize).Validate());
    }

    /// <summary>
    /// Verifies that undefined <see cref="EgbiOptions.Connectivity"/> values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidConnectivity_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EgbiOptions(Connectivity: (GridConnectivity)99).Validate());
    }

    /// <summary>
    /// Verifies that boundary zero values and alternate connectivity settings pass validation.
    /// </summary>
    [Fact]
    public void Validate_ValidBoundaryAndCustomOptions_ShouldSucceed()
    {
        var zeroOptions = new EgbiOptions(KFactor: 0.0, Epsilon: 0.0, MinRegionSize: 0, Connectivity: GridConnectivity.FourConnected);
        zeroOptions.Validate();

        Assert.Equal(0.0, zeroOptions.KFactor);
        Assert.Equal(0.0, zeroOptions.Epsilon);
        Assert.Equal(0, zeroOptions.MinRegionSize);
        Assert.Equal(GridConnectivity.FourConnected, zeroOptions.Connectivity);

        var custom = new EgbiOptions(KFactor: 50.0, Epsilon: 2.5, MinRegionSize: 200, Connectivity: GridConnectivity.EightConnected);
        custom.Validate();

        Assert.Equal(50.0, custom.KFactor);
        Assert.Equal(2.5, custom.Epsilon);
        Assert.Equal(200, custom.MinRegionSize);
        Assert.Equal(GridConnectivity.EightConnected, custom.Connectivity);
    }
}
