namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Stylization.Watercolor;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="EdgeDarkeningOptions"/>.
/// </summary>
public class EdgeDarkeningOptionsTests
{
    /// <summary>
    /// Verifies that default options match expected default values and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new EdgeDarkeningOptions();

        Assert.Equal(0.65, options.Intensity);
        Assert.Equal(1.0, options.BlurSigma);
        Assert.Equal(GradientOperator.Sobel, options.GradientOperator);

        options.Validate();
    }

    /// <summary>
    /// Verifies that valid custom options pass validation without throwing exceptions.
    /// </summary>
    [Fact]
    public void Validate_ValidCustomOptions_ShouldNotThrow()
    {
        var options = new EdgeDarkeningOptions(
            Intensity: 0.0,
            BlurSigma: 0.0,
            GradientOperator: GradientOperator.Scharr);

        options.Validate();

        Assert.Equal(0.0, options.Intensity);
        Assert.Equal(0.0, options.BlurSigma);
        Assert.Equal(GradientOperator.Scharr, options.GradientOperator);
    }

    /// <summary>
    /// Verifies that invalid Intensity values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="intensity">The invalid intensity value.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void Validate_InvalidIntensity_ShouldThrowArgumentOutOfRangeException(double intensity)
    {
        var options = new EdgeDarkeningOptions(Intensity: intensity);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that invalid BlurSigma values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="blurSigma">The invalid blur sigma value.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-2.5)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void Validate_InvalidBlurSigma_ShouldThrowArgumentOutOfRangeException(double blurSigma)
    {
        var options = new EdgeDarkeningOptions(BlurSigma: blurSigma);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that an undefined GradientOperator enum value throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidGradientOperator_ShouldThrowArgumentOutOfRangeException()
    {
        var options = new EdgeDarkeningOptions(GradientOperator: (GradientOperator)999);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }
}
