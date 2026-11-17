namespace Leiter.Tests.Algorithms.Color;

using System;
using Leiter.Algorithms.Color;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="LabColorStatistics"/>.
/// </summary>
public class LabColorStatisticsTests
{
    [Fact]
    public void UnitWeights_ShouldReturnAllOnes()
    {
        var unit = LabColorStatistics.UnitWeights;

        Assert.Equal(1.0, unit.MeanL);
        Assert.Equal(1.0, unit.MeanA);
        Assert.Equal(1.0, unit.MeanB);
        Assert.Equal(1.0, unit.StdDevL);
        Assert.Equal(1.0, unit.StdDevA);
        Assert.Equal(1.0, unit.StdDevB);
    }

    [Fact]
    public void FeatureDistance_WithDefaultAndCustomWeights_ShouldComputeEuclideanDistance()
    {
        var statA = new LabColorStatistics(10, 0, 0, 5, 0, 0);
        var statB = new LabColorStatistics(13, 4, 0, 5, 0, 0);

        // Unweighted Euclidean distance: sqrt((3)^2 + (4)^2 + 0 + 0 + 0 + 0) = 5.0
        Assert.Equal(5.0, statA.FeatureDistance(statB), 10);

        // Custom weights: sqrt(4*(3^2) + 1*(4^2) + 0 + 0 + 0 + 0) = sqrt(36 + 16) = sqrt(52)
        var customWeights = new LabColorStatistics(4.0, 1.0, 1.0, 1.0, 1.0, 1.0);
        Assert.Equal(Math.Sqrt(52.0), statA.FeatureDistance(statB, customWeights), 10);
    }

    [Fact]
    public void FeatureDistance_NegativeOrInvalidWeights_ShouldThrowArgumentOutOfRangeException()
    {
        var statA = new LabColorStatistics(10, 0, 0, 5, 0, 0);
        var statB = new LabColorStatistics(13, 4, 0, 5, 0, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => statA.FeatureDistance(statB, new LabColorStatistics(-0.5, 1, 1, 1, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => statA.FeatureDistance(statB, new LabColorStatistics(1, -0.5, 1, 1, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => statA.FeatureDistance(statB, new LabColorStatistics(1, 1, -0.5, 1, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => statA.FeatureDistance(statB, new LabColorStatistics(1, 1, 1, -0.5, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => statA.FeatureDistance(statB, new LabColorStatistics(1, 1, 1, 1, -0.5, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => statA.FeatureDistance(statB, new LabColorStatistics(1, 1, 1, 1, 1, -0.5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => statA.FeatureDistance(statB, new LabColorStatistics(double.NaN, 1, 1, 1, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => statA.FeatureDistance(statB, new LabColorStatistics(double.PositiveInfinity, 1, 1, 1, 1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => statA.FeatureDistance(statB, new LabColorStatistics(double.NegativeInfinity, 1, 1, 1, 1, 1)));
    }

    [Fact]
    public void Equality_RecordStructSemantics_ShouldBehaveCorrectly()
    {
        var a = new LabColorStatistics(10, 20, 30, 4, 5, 6);
        var b = new LabColorStatistics(10, 20, 30, 4, 5, 6);
        var c = new LabColorStatistics(10, 20, 30, 4, 5, 7);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.NotEqual(a, c);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
