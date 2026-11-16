namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms.Saliency;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyCutOptions"/>.
/// </summary>
public class SaliencyCutOptionsTests
{
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new SaliencyCutOptions();

        Assert.Equal(70.0 / 255.0, options.InitialThreshold, 6);
        Assert.Equal(4, options.MaxOuterIterations);
        Assert.Equal(2, options.InnerGrabCutIterations);
        Assert.Equal(10, options.TrimapBandRadius);
        Assert.Equal(5, options.MixtureComponentCount);
        Assert.Equal(50.0, options.SmoothnessWeight, 6);
        Assert.Equal(ComponentRetentionPolicy.MinimumAreaFraction, options.RetentionPolicy);
        Assert.Equal(0.01, options.MinAreaFraction, 6);
        Assert.Null(options.MaxWorkingDimension);

        options.Validate();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidInitialThreshold_ShouldThrowArgumentOutOfRangeException(double initialThreshold)
    {
        var options = new SaliencyCutOptions(InitialThreshold: initialThreshold);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validate_InvalidMaxOuterIterations_ShouldThrowArgumentOutOfRangeException(int maxOuterIterations)
    {
        var options = new SaliencyCutOptions(MaxOuterIterations: maxOuterIterations);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validate_InvalidInnerGrabCutIterations_ShouldThrowArgumentOutOfRangeException(int innerGrabCutIterations)
    {
        var options = new SaliencyCutOptions(InnerGrabCutIterations: innerGrabCutIterations);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validate_InvalidTrimapBandRadius_ShouldThrowArgumentOutOfRangeException(int trimapBandRadius)
    {
        var options = new SaliencyCutOptions(TrimapBandRadius: trimapBandRadius);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validate_InvalidMixtureComponentCount_ShouldThrowArgumentOutOfRangeException(int mixtureComponentCount)
    {
        var options = new SaliencyCutOptions(MixtureComponentCount: mixtureComponentCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-50.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidSmoothnessWeight_ShouldThrowArgumentOutOfRangeException(double smoothnessWeight)
    {
        var options = new SaliencyCutOptions(SmoothnessWeight: smoothnessWeight);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void Validate_InvalidRetentionPolicy_ShouldThrowArgumentOutOfRangeException()
    {
        var options = new SaliencyCutOptions(RetentionPolicy: (ComponentRetentionPolicy)99);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidMinAreaFraction_ShouldThrowArgumentOutOfRangeException(double minAreaFraction)
    {
        var options = new SaliencyCutOptions(MinAreaFraction: minAreaFraction);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidMaxWorkingDimension_ShouldThrowArgumentOutOfRangeException(int maxWorkingDimension)
    {
        var options = new SaliencyCutOptions(MaxWorkingDimension: maxWorkingDimension);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(16)]
    [InlineData(256)]
    public void Validate_ValidMaxWorkingDimension_ShouldNotThrow(int maxWorkingDimension)
    {
        var options = new SaliencyCutOptions(MaxWorkingDimension: maxWorkingDimension);
        options.Validate();
    }
}
