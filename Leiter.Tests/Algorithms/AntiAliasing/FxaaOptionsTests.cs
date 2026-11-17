namespace Leiter.Tests.Algorithms.AntiAliasing;

using System;
using Leiter.Algorithms.AntiAliasing;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="FxaaOptions"/>.
/// </summary>
public class FxaaOptionsTests
{
    /// <summary>
    /// Verifies that default options match expected default values and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new FxaaOptions();

        Assert.Equal(0.125, options.RelativeContrastThreshold, 6);
        Assert.Equal(0.0625, options.AbsoluteContrastThreshold, 6);
        Assert.Equal(0.75, options.SubpixelBlendStrength, 6);
        Assert.Equal(8, options.MaxSpanSearchSteps);

        options.Validate();
    }

    /// <summary>
    /// Verifies that non-positive, greater than 1.0, or non-finite RelativeContrastThreshold values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="threshold">The invalid threshold value.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.001)]
    [InlineData(2.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidRelativeContrastThreshold_ShouldThrowArgumentOutOfRangeException(double threshold)
    {
        var options = new FxaaOptions(RelativeContrastThreshold: threshold);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid upper boundary 1.0 for RelativeContrastThreshold does not throw.
    /// </summary>
    /// <param name="threshold">The boundary threshold value.</param>
    [Theory]
    [InlineData(1.0)]
    public void Validate_ValidBoundaryRelativeContrastThreshold_ShouldNotThrow(double threshold)
    {
        var options = new FxaaOptions(RelativeContrastThreshold: threshold);
        options.Validate();
    }

    /// <summary>
    /// Verifies that negative or non-finite AbsoluteContrastThreshold values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="threshold">The invalid threshold value.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidAbsoluteContrastThreshold_ShouldThrowArgumentOutOfRangeException(double threshold)
    {
        var options = new FxaaOptions(AbsoluteContrastThreshold: threshold);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that non-negative boundary 0.0 for AbsoluteContrastThreshold does not throw.
    /// </summary>
    /// <param name="threshold">The boundary threshold value.</param>
    [Theory]
    [InlineData(0.0)]
    public void Validate_ValidBoundaryAbsoluteContrastThreshold_ShouldNotThrow(double threshold)
    {
        var options = new FxaaOptions(AbsoluteContrastThreshold: threshold);
        options.Validate();
    }

    /// <summary>
    /// Verifies that SubpixelBlendStrength outside [0, 1] or non-finite throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="strength">The invalid subpixel blend strength value.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidSubpixelBlendStrength_ShouldThrowArgumentOutOfRangeException(double strength)
    {
        var options = new FxaaOptions(SubpixelBlendStrength: strength);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that boundary values [0, 1] for SubpixelBlendStrength do not throw.
    /// </summary>
    /// <param name="strength">The boundary subpixel blend strength value.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_ValidBoundarySubpixelBlendStrength_ShouldNotThrow(double strength)
    {
        var options = new FxaaOptions(SubpixelBlendStrength: strength);
        options.Validate();
    }

    /// <summary>
    /// Verifies that MaxSpanSearchSteps less than 1 throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="steps">The invalid maximum span search steps.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validate_InvalidMaxSpanSearchSteps_ShouldThrowArgumentOutOfRangeException(int steps)
    {
        var options = new FxaaOptions(MaxSpanSearchSteps: steps);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that boundary value 1 for MaxSpanSearchSteps does not throw.
    /// </summary>
    /// <param name="steps">The boundary maximum span search steps.</param>
    [Theory]
    [InlineData(1)]
    public void Validate_ValidBoundaryMaxSpanSearchSteps_ShouldNotThrow(int steps)
    {
        var options = new FxaaOptions(MaxSpanSearchSteps: steps);
        options.Validate();
    }

    /// <summary>
    /// Verifies that custom valid options pass validation without throwing.
    /// </summary>
    [Fact]
    public void Validate_CustomValidOptions_ShouldNotThrow()
    {
        var options = new FxaaOptions(
            RelativeContrastThreshold: 0.2,
            AbsoluteContrastThreshold: 0.05,
            SubpixelBlendStrength: 0.5,
            MaxSpanSearchSteps: 12);

        options.Validate();
    }
}
