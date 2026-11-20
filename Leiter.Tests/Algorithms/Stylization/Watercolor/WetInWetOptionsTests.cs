namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.Algorithms;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="WetInWetOptions"/>.
/// </summary>
public class WetInWetOptionsTests : SizeTunableOptionsContractTestsBase<WetInWetOptions>
{
    /// <inheritdoc/>
    protected override WetInWetOptions CreateValidCustomOptions() =>
        new(
            Mode: WetInWetMode.ExcludeSalientBoundaries,
            ColorGradientThreshold: 0.15,
            SalientHueThresholdDegrees: 25.0,
            NonSalientHueThresholdDegrees: 75.0,
            MinIntensityDifference: 0.04,
            SalientOverlapThreshold: 0.6,
            ScatterRadius: 6,
            SeedProbability: 0.6,
            EllipticalFilter: new OrientedEllipticalFilterOptions(MajorRadius: 8.0, MinorRadius: 3.0, SigmaScale: 0.65),
            Seed: 99);

    /// <inheritdoc/>
    protected override WetInWetOptions CreateInvalidOptions() =>
        new(ScatterRadius: 0);
    /// <summary>
    /// Verifies that default options match expected values and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new WetInWetOptions();

        Assert.Equal(WetInWetMode.Default, options.Mode);
        Assert.Equal(0.1, options.ColorGradientThreshold, 6);
        Assert.Equal(20.0, options.SalientHueThresholdDegrees, 6);
        Assert.Equal(90.0, options.NonSalientHueThresholdDegrees, 6);
        Assert.Equal(0.02, options.MinIntensityDifference, 6);
        Assert.Equal(0.5, options.SalientOverlapThreshold, 6);
        Assert.Equal(4, options.ScatterRadius);
        Assert.Equal(0.45, options.SeedProbability, 6);
        Assert.Equal(WetInWetOptions.DefaultEllipticalFilter, options.EllipticalFilter);
        Assert.Equal(1337, options.Seed);
        Assert.Equal(WetInWetOptions.Default, options);

        var explicitDefaults = new WetInWetOptions(EllipticalFilter: WetInWetOptions.DefaultEllipticalFilter);
        Assert.Equal(options, explicitDefaults);

        WetInWetOptions resetViaWith = CreateValidCustomOptions() with { EllipticalFilter = null! };
        Assert.Equal(WetInWetOptions.DefaultEllipticalFilter, resetViaWith.EllipticalFilter);

        options.Validate();
    }

    /// <summary>
    /// Verifies that undefined mode enum values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="mode">The invalid mode value.</param>
    [Theory]
    [InlineData((WetInWetMode)(-1))]
    [InlineData((WetInWetMode)99)]
    public void Validate_InvalidMode_ShouldThrowArgumentOutOfRangeException(WetInWetMode mode)
    {
        var options = new WetInWetOptions(Mode: mode);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that negative or non-finite ColorGradientThreshold values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="threshold">The invalid threshold value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidColorGradientThreshold_ShouldThrowArgumentOutOfRangeException(double threshold)
    {
        var options = new WetInWetOptions(ColorGradientThreshold: threshold);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid ColorGradientThreshold values pass validation.
    /// </summary>
    /// <param name="threshold">The valid threshold value.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.1)]
    [InlineData(5.0)]
    public void Validate_ValidColorGradientThreshold_ShouldNotThrow(double threshold)
    {
        var options = new WetInWetOptions(ColorGradientThreshold: threshold);
        options.Validate();
    }

    /// <summary>
    /// Verifies that SalientHueThresholdDegrees outside [0, 180] or non-finite throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="degrees">The invalid hue threshold in degrees.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidHueDegrees), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidSalientHueThresholdDegrees_ShouldThrowArgumentOutOfRangeException(double degrees)
    {
        var options = new WetInWetOptions(SalientHueThresholdDegrees: degrees);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid SalientHueThresholdDegrees values pass validation.
    /// </summary>
    /// <param name="degrees">The valid hue threshold in degrees.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(20.0)]
    [InlineData(180.0)]
    public void Validate_ValidSalientHueThresholdDegrees_ShouldNotThrow(double degrees)
    {
        var options = new WetInWetOptions(SalientHueThresholdDegrees: degrees);
        options.Validate();
    }

    /// <summary>
    /// Verifies that NonSalientHueThresholdDegrees outside [0, 180] or non-finite throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="degrees">The invalid hue threshold in degrees.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidHueDegrees), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidNonSalientHueThresholdDegrees_ShouldThrowArgumentOutOfRangeException(double degrees)
    {
        var options = new WetInWetOptions(NonSalientHueThresholdDegrees: degrees);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid NonSalientHueThresholdDegrees values pass validation.
    /// </summary>
    /// <param name="degrees">The valid hue threshold in degrees.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(90.0)]
    [InlineData(180.0)]
    public void Validate_ValidNonSalientHueThresholdDegrees_ShouldNotThrow(double degrees)
    {
        var options = new WetInWetOptions(NonSalientHueThresholdDegrees: degrees);
        options.Validate();
    }

    /// <summary>
    /// Verifies that negative or non-finite MinIntensityDifference values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="diff">The invalid intensity difference.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMinIntensityDifference_ShouldThrowArgumentOutOfRangeException(double diff)
    {
        var options = new WetInWetOptions(MinIntensityDifference: diff);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid MinIntensityDifference values pass validation.
    /// </summary>
    /// <param name="diff">The valid intensity difference.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.02)]
    [InlineData(1.0)]
    public void Validate_ValidMinIntensityDifference_ShouldNotThrow(double diff)
    {
        var options = new WetInWetOptions(MinIntensityDifference: diff);
        options.Validate();
    }

    /// <summary>
    /// Verifies that SalientOverlapThreshold outside [0, 1] or non-finite throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="threshold">The invalid salient overlap threshold.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidSalientOverlapThreshold_ShouldThrowArgumentOutOfRangeException(double threshold)
    {
        var options = new WetInWetOptions(SalientOverlapThreshold: threshold);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid SalientOverlapThreshold values pass validation.
    /// </summary>
    /// <param name="threshold">The valid salient overlap threshold.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_ValidSalientOverlapThreshold_ShouldNotThrow(double threshold)
    {
        var options = new WetInWetOptions(SalientOverlapThreshold: threshold);
        options.Validate();
    }

    /// <summary>
    /// Verifies that ScatterRadius less than 1 throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="radius">The invalid scatter radius.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveInts), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidScatterRadius_ShouldThrowArgumentOutOfRangeException(int radius)
    {
        var options = new WetInWetOptions(ScatterRadius: radius);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid ScatterRadius values pass validation.
    /// </summary>
    /// <param name="radius">The valid scatter radius.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(10)]
    public void Validate_ValidScatterRadius_ShouldNotThrow(int radius)
    {
        var options = new WetInWetOptions(ScatterRadius: radius);
        options.Validate();
    }

    /// <summary>
    /// Verifies that SeedProbability outside [0, 1] or non-finite throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="prob">The invalid seed probability.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidSeedProbability_ShouldThrowArgumentOutOfRangeException(double prob)
    {
        var options = new WetInWetOptions(SeedProbability: prob);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid SeedProbability values pass validation.
    /// </summary>
    /// <param name="prob">The valid seed probability.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.45)]
    [InlineData(1.0)]
    public void Validate_ValidSeedProbability_ShouldNotThrow(double prob)
    {
        var options = new WetInWetOptions(SeedProbability: prob);
        options.Validate();
    }

    /// <summary>
    /// Verifies that non-positive or non-finite MajorRadius values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="radius">The invalid major radius.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMajorRadius_ShouldThrowArgumentOutOfRangeException(double radius)
    {
        var options = new WetInWetOptions(EllipticalFilter: new OrientedEllipticalFilterOptions(MajorRadius: radius));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid MajorRadius values pass validation.
    /// </summary>
    /// <param name="radius">The valid major radius.</param>
    [Theory]
    [InlineData(0.01)]
    [InlineData(6.0)]
    [InlineData(20.0)]
    public void Validate_ValidMajorRadius_ShouldNotThrow(double radius)
    {
        var options = new WetInWetOptions(EllipticalFilter: new OrientedEllipticalFilterOptions(MajorRadius: radius));
        options.Validate();
    }

    /// <summary>
    /// Verifies that non-positive or non-finite MinorRadius values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="radius">The invalid minor radius.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMinorRadius_ShouldThrowArgumentOutOfRangeException(double radius)
    {
        var options = new WetInWetOptions(EllipticalFilter: new OrientedEllipticalFilterOptions(MinorRadius: radius));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid MinorRadius values pass validation.
    /// </summary>
    /// <param name="radius">The valid minor radius.</param>
    [Theory]
    [InlineData(0.01)]
    [InlineData(1.5)]
    [InlineData(10.0)]
    public void Validate_ValidMinorRadius_ShouldNotThrow(double radius)
    {
        var options = new WetInWetOptions(EllipticalFilter: new OrientedEllipticalFilterOptions(MinorRadius: radius));
        options.Validate();
    }

    /// <summary>
    /// Verifies that custom valid options pass validation without throwing.
    /// </summary>
    [Fact]
    public void Validate_CustomValidOptions_ShouldNotThrow()
    {
        var options = new WetInWetOptions(
            Mode: WetInWetMode.ExcludeSalientBoundaries,
            ColorGradientThreshold: 0.2,
            SalientHueThresholdDegrees: 15.0,
            NonSalientHueThresholdDegrees: 60.0,
            MinIntensityDifference: 0.05,
            SalientOverlapThreshold: 0.6,
            ScatterRadius: 3,
            SeedProbability: 0.3,
            EllipticalFilter: new OrientedEllipticalFilterOptions(MajorRadius: 5.0, MinorRadius: 2.0),
            Seed: 42);

        options.Validate();
    }

    /// <summary>
    /// Verifies record equality and hash code consistency for identical option instances.
    /// </summary>
    [Fact]
    public void RecordEquality_IdenticalValues_ShouldBeEqual()
    {
        var opt1 = new WetInWetOptions(Mode: WetInWetMode.ExcludeSalientRegions, Seed: 999);
        var opt2 = new WetInWetOptions(Mode: WetInWetMode.ExcludeSalientRegions, Seed: 999);
        var opt3 = new WetInWetOptions(Mode: WetInWetMode.Disabled, Seed: 999);

        Assert.Equal(opt1, opt2);
        Assert.True(opt1 == opt2);
        Assert.False(opt1 == opt3);
        Assert.Equal(opt1.GetHashCode(), opt2.GetHashCode());
    }

    /// <summary>
    /// Verifies that <see cref="WetInWetOptions.ForSize"/> delegates elliptical radii and scales scatter/gradient thresholds, and <see cref="WetInWetOptions.ForImage"/> adapts to clutter.
    /// </summary>
    [Fact]
    public void ForSize_And_ForImage_ShouldScaleAndAdaptGradientThreshold()
    {
        WetInWetOptions refOpts = WetInWetOptions.ForSize(new Size(800, 800));
        Assert.Equal(4, refOpts.ScatterRadius);
        Assert.NotNull(refOpts.EllipticalFilter);
        Assert.Equal(6.0, refOpts.EllipticalFilter!.MajorRadius, 6);
        Assert.Equal(2.5, refOpts.EllipticalFilter!.MinorRadius, 6);
        Assert.Equal(0.55, refOpts.EllipticalFilter!.SigmaScale, 6);
        Assert.Equal(0.1, refOpts.ColorGradientThreshold, 6);
        Assert.Equal(refOpts, WetInWetOptions.ForSize(new Size(1000, 640)));

        Size largeSize = new(3200, 2400);
        WetInWetOptions large = WetInWetOptions.ForSize(largeSize);
        double expectedScale = Math.Sqrt((3200.0 * 2400.0) / (800.0 * 800.0));
        Assert.Equal(6.0 * expectedScale, large.EllipticalFilter!.MajorRadius, 6);
        Assert.Equal(2.5 * expectedScale, large.EllipticalFilter!.MinorRadius, 6);
        Assert.Equal(0.55, large.EllipticalFilter!.SigmaScale, 6);
        Assert.True(large.ScatterRadius > refOpts.ScatterRadius);
        Assert.True(large.ColorGradientThreshold < refOpts.ColorGradientThreshold);

        SequentialMatrix<Rgb8> flat = TestMatrices.Constant(256, 256, new Rgb8(120, 120, 120));
        SequentialMatrix<Rgb8> busy = TestMatrices.Checkerboard(256, 256, new Rgb8(255, 255, 255), Rgb8.Zero, cellSize: 4);

        Assert.True(WetInWetOptions.ForImage(busy).ColorGradientThreshold > WetInWetOptions.ForImage(flat).ColorGradientThreshold);

        WetInWetOptions customBaseline = CreateValidCustomOptions();
        WetInWetOptions customHalf = WetInWetOptions.ForSize(new Size(400, 400), customBaseline);
        Assert.Equal(3, customHalf.ScatterRadius);
        Assert.Equal(4.0, customHalf.EllipticalFilter!.MajorRadius, 6);
        Assert.Equal(1.5, customHalf.EllipticalFilter!.MinorRadius, 6);
        Assert.Equal(0.65, customHalf.EllipticalFilter!.SigmaScale, 6);
        Assert.Equal(0.3, customHalf.ColorGradientThreshold, 6);
        Assert.Equal(WetInWetMode.ExcludeSalientBoundaries, customHalf.Mode);
        Assert.Equal(99, customHalf.Seed);

        WetInWetOptions customBusy = WetInWetOptions.ForImage(busy, customBaseline);
        WetInWetOptions customFlat = WetInWetOptions.ForImage(flat, customBaseline);
        Assert.True(customBusy.ColorGradientThreshold > customFlat.ColorGradientThreshold);
        Assert.Equal(99, customBusy.Seed);

        var zeroThresholdBaseline = new WetInWetOptions(ColorGradientThreshold: 0.0);
        Assert.Equal(0.0, WetInWetOptions.ForImage(busy, zeroThresholdBaseline).ColorGradientThreshold, 6);

        ImageAnalysis busyAnalysis = ImageAnalysis.Analyze(busy);
        Assert.Equal(WetInWetOptions.ForImage(busy), WetInWetOptions.ForAnalysis(busyAnalysis));
        Assert.Equal(customBusy, WetInWetOptions.ForAnalysis(busyAnalysis, customBaseline));

        var overflowBaseline = new WetInWetOptions(ColorGradientThreshold: 1e300);
        WetInWetOptions overflowScaled = WetInWetOptions.ForSize(new Size(1, 1), overflowBaseline, referenceDimension: 100_000.0);
        overflowScaled.Validate();
        Assert.True(double.IsFinite(overflowScaled.ColorGradientThreshold));

        Assert.Throws<ArgumentNullException>(() => WetInWetOptions.ForImage(null!));
        Assert.Throws<ArgumentNullException>(() => WetInWetOptions.ForImage(busy, null!));
        Assert.Throws<ArgumentNullException>(() => WetInWetOptions.ForAnalysis(null!));
        Assert.Throws<ArgumentNullException>(() => WetInWetOptions.ForAnalysis(busyAnalysis, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => WetInWetOptions.ForAnalysis(busyAnalysis, 0.0));
    }
}
