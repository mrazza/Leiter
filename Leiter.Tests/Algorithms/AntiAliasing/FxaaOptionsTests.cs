namespace Leiter.Tests.Algorithms.AntiAliasing;

using System;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
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
    [MemberData(nameof(OptionValidationData.InvalidPositiveUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
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
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
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
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
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

    /// <summary>
    /// Verifies that <see cref="FxaaOptions.ForSize"/> scales span search steps and <see cref="FxaaOptions.ForImage"/> adapts contrast thresholds for dark scenes.
    /// </summary>
    [Fact]
    public void ForSize_And_ForImage_ShouldScaleAndAdaptForLowKeyImages()
    {
        FxaaOptions refOptions = FxaaOptions.ForSize(new Size(800, 800));
        Assert.Equal(8, refOptions.MaxSpanSearchSteps);
        Assert.Equal(refOptions, FxaaOptions.ForSize(new Size(1000, 640)));

        FxaaOptions small = FxaaOptions.ForSize(new Size(200, 200));
        FxaaOptions large = FxaaOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(4, small.MaxSpanSearchSteps);
        Assert.True(large.MaxSpanSearchSteps > refOptions.MaxSpanSearchSteps);

        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(3, 3), new Size(4000, 80), new Size(12000, 9000) })
        {
            FxaaOptions.ForSize(size).Validate();
        }

        var darkImage = new SequentialMatrix<Rgb8>(64, 64);
        darkImage.SetAll(new Rgb8(8, 8, 10));
        var brightImage = new SequentialMatrix<Rgb8>(64, 64);
        brightImage.SetAll(new Rgb8(210, 210, 210));

        FxaaOptions darkOpts = FxaaOptions.ForImage(darkImage);
        FxaaOptions brightOpts = FxaaOptions.ForImage(brightImage);
        Assert.True(darkOpts.AbsoluteContrastThreshold < brightOpts.AbsoluteContrastThreshold);
        Assert.Equal(0.0625, brightOpts.AbsoluteContrastThreshold, 6);

        Assert.Throws<ArgumentOutOfRangeException>(() => FxaaOptions.ForSize(new Size(-1, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => FxaaOptions.ForSize(new Size(10, 10), 0.0));
        Assert.Throws<ArgumentNullException>(() => FxaaOptions.ForImage(null!));
    }
}
