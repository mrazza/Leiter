namespace Leiter.Tests.Algorithms.AntiAliasing;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.Algorithms;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="FxaaOptions"/>.
/// </summary>
public class FxaaOptionsTests : SizeTunableOptionsContractTestsBase<FxaaOptions>
{
    /// <inheritdoc/>
    protected override FxaaOptions CreateValidCustomOptions() =>
        new(RelativeContrastThreshold: 0.2, AbsoluteContrastThreshold: 0.08, SubpixelBlendStrength: 0.5, MaxSpanSearchSteps: 12);

    /// <inheritdoc/>
    protected override FxaaOptions CreateInvalidOptions() =>
        new(MaxSpanSearchSteps: 0);
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
    [MemberData(nameof(OptionValidationData.InvalidPositiveInts), MemberType = typeof(OptionValidationData))]
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

        SequentialMatrix<Rgb8> darkImage = TestMatrices.Constant(64, 64, new Rgb8(8, 8, 10));
        SequentialMatrix<Rgb8> brightImage = TestMatrices.Constant(64, 64, new Rgb8(210, 210, 210));

        FxaaOptions darkOpts = FxaaOptions.ForImage(darkImage);
        FxaaOptions brightOpts = FxaaOptions.ForImage(brightImage);
        Assert.True(darkOpts.AbsoluteContrastThreshold < brightOpts.AbsoluteContrastThreshold);
        Assert.Equal(0.0625, brightOpts.AbsoluteContrastThreshold, 6);

        var customBaseline = new FxaaOptions(
            RelativeContrastThreshold: 0.25,
            AbsoluteContrastThreshold: 0.08,
            SubpixelBlendStrength: 0.4,
            MaxSpanSearchSteps: 16);
        FxaaOptions scaledCustom = FxaaOptions.ForSize(new Size(400, 400), customBaseline);
        Assert.Equal(8, scaledCustom.MaxSpanSearchSteps);
        Assert.Equal(0.25, scaledCustom.RelativeContrastThreshold, 6);
        Assert.Equal(0.4, scaledCustom.SubpixelBlendStrength, 6);

        FxaaOptions darkCustom = FxaaOptions.ForImage(darkImage, customBaseline);
        Assert.True(darkCustom.AbsoluteContrastThreshold < customBaseline.AbsoluteContrastThreshold);
        Assert.Equal(0.25, darkCustom.RelativeContrastThreshold, 6);

        var zeroAbsBaseline = new FxaaOptions(AbsoluteContrastThreshold: 0.0);
        Assert.Equal(0.0, FxaaOptions.ForImage(darkImage, zeroAbsBaseline).AbsoluteContrastThreshold, 6);

        // Low MaxSpanSearchSteps (< 4) must remain unchanged at scale 1.0 instead of clamping upward to 4.
        var lowStepBaseline = new FxaaOptions(MaxSpanSearchSteps: 2);
        Assert.Equal(lowStepBaseline, FxaaOptions.ForSize(new Size(800, 800), lowStepBaseline));
        Assert.Equal(lowStepBaseline, FxaaOptions.ForSize(new Size(1000, 640), lowStepBaseline));
        Assert.Equal(2, FxaaOptions.ForSize(new Size(200, 200), lowStepBaseline).MaxSpanSearchSteps);
        Assert.Equal(4, FxaaOptions.ForSize(new Size(1600, 1600), lowStepBaseline).MaxSpanSearchSteps);

        var minStepBaseline = new FxaaOptions(MaxSpanSearchSteps: 1);
        Assert.Equal(minStepBaseline, FxaaOptions.ForSize(new Size(800, 800), minStepBaseline));

        // ForAnalysis must match ForImage and validate inputs.
        ImageAnalysis darkAnalysis = ImageAnalysis.Analyze(darkImage);
        Assert.Equal(darkOpts, FxaaOptions.ForAnalysis(darkAnalysis));
        Assert.Equal(darkCustom, FxaaOptions.ForAnalysis(darkAnalysis, customBaseline));

        Assert.Throws<ArgumentNullException>(() => FxaaOptions.ForImage(null!));
        Assert.Throws<ArgumentNullException>(() => FxaaOptions.ForImage(darkImage, null!));
        Assert.Throws<ArgumentNullException>("analysis", () => FxaaOptions.ForAnalysis(null!));
        Assert.Throws<ArgumentNullException>("analysis", () => FxaaOptions.ForAnalysis(null!, customBaseline));
        Assert.Throws<ArgumentNullException>("baseline", () => FxaaOptions.ForAnalysis(darkAnalysis, null!));
        Assert.Throws<ArgumentOutOfRangeException>("referenceDimension", () => FxaaOptions.ForAnalysis(darkAnalysis, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => FxaaOptions.ForAnalysis(darkAnalysis, new FxaaOptions(MaxSpanSearchSteps: 0)));
    }
}
