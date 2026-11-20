namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.Algorithms;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="HandTremorOptions"/>.
/// </summary>
public class HandTremorOptionsTests : SizeTunableOptionsContractTestsBase<HandTremorOptions>
{
    /// <inheritdoc/>
    protected override HandTremorOptions CreateValidCustomOptions() =>
        new(
            Amplitude: 4.5,
            SalientAmplitudeScale: 0.5,
            SimilarHueThresholdDegrees: 25.0,
            OverlapDarkening: 0.6,
            PaperColor: new Rgb8(245, 242, 236),
            PrimaryNoise: new PerlinNoiseOptions(Seed: 55, Frequency: 0.02, Octaves: 2),
            SecondaryNoise: new PerlinNoiseOptions(Seed: 66, Frequency: 0.03, Octaves: 2),
            MinContrastingChroma: 0.04,
            PaperGapBias: 0.2,
            PaperGapOpacity: 0.8);

    /// <inheritdoc/>
    protected override HandTremorOptions CreateInvalidOptions() =>
        new(Amplitude: -1.0);
    /// <summary>
    /// Verifies that default options match expected values and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new HandTremorOptions();

        Assert.Equal(3.0, options.Amplitude, 6);
        Assert.Equal(0.65, options.SalientAmplitudeScale, 6);
        Assert.Equal(30.0, options.SimilarHueThresholdDegrees, 6);
        Assert.Equal(0.5, options.OverlapDarkening, 6);
        Assert.Equal(HandTremorOptions.DefaultPaperColor, options.PaperColor);
        Assert.Equal(HandTremorOptions.DefaultPrimaryNoise, options.PrimaryNoise);
        Assert.Equal(HandTremorOptions.DefaultSecondaryNoise, options.SecondaryNoise);
        Assert.Equal(0.08, options.MinContrastingChroma, 6);
        Assert.Equal(0.0, options.PaperGapBias, 6);
        Assert.Equal(1.0, options.PaperGapOpacity, 6);
        Assert.Equal(HandTremorOptions.Default, options);

        var explicitDefaults = new HandTremorOptions(
            PaperColor: HandTremorOptions.DefaultPaperColor,
            PrimaryNoise: HandTremorOptions.DefaultPrimaryNoise,
            SecondaryNoise: HandTremorOptions.DefaultSecondaryNoise);
        Assert.Equal(options, explicitDefaults);

        HandTremorOptions resetViaWith = CreateValidCustomOptions() with
        {
            PrimaryNoise = null!,
            SecondaryNoise = null!
        };
        Assert.Equal(HandTremorOptions.DefaultPrimaryNoise, resetViaWith.PrimaryNoise);
        Assert.Equal(HandTremorOptions.DefaultSecondaryNoise, resetViaWith.SecondaryNoise);

        options.Validate();
    }

    /// <summary>
    /// Verifies that negative or non-finite Amplitude values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="amplitude">The invalid amplitude value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidAmplitude_ShouldThrowArgumentOutOfRangeException(double amplitude)
    {
        var options = new HandTremorOptions(Amplitude: amplitude);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid Amplitude values pass validation.
    /// </summary>
    /// <param name="amplitude">The valid amplitude value.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(2.5)]
    [InlineData(10.0)]
    public void Validate_ValidAmplitude_ShouldNotThrow(double amplitude)
    {
        var options = new HandTremorOptions(Amplitude: amplitude);
        options.Validate();
    }

    /// <summary>
    /// Verifies that SalientAmplitudeScale outside [0, 1] or non-finite throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="scale">The invalid amplitude scale.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidSalientAmplitudeScale_ShouldThrowArgumentOutOfRangeException(double scale)
    {
        var options = new HandTremorOptions(SalientAmplitudeScale: scale);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid SalientAmplitudeScale values pass validation.
    /// </summary>
    /// <param name="scale">The valid amplitude scale.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.35)]
    [InlineData(1.0)]
    public void Validate_ValidSalientAmplitudeScale_ShouldNotThrow(double scale)
    {
        var options = new HandTremorOptions(SalientAmplitudeScale: scale);
        options.Validate();
    }

    /// <summary>
    /// Verifies that SimilarHueThresholdDegrees outside [0, 180] or non-finite throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="degrees">The invalid hue threshold in degrees.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidHueDegrees), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidSimilarHueThresholdDegrees_ShouldThrowArgumentOutOfRangeException(double degrees)
    {
        var options = new HandTremorOptions(SimilarHueThresholdDegrees: degrees);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid SimilarHueThresholdDegrees values pass validation.
    /// </summary>
    /// <param name="degrees">The valid hue threshold in degrees.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(30.0)]
    [InlineData(180.0)]
    public void Validate_ValidSimilarHueThresholdDegrees_ShouldNotThrow(double degrees)
    {
        var options = new HandTremorOptions(SimilarHueThresholdDegrees: degrees);
        options.Validate();
    }

    /// <summary>
    /// Verifies that OverlapDarkening outside [0, 1] or non-finite throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="darkening">The invalid overlap darkening factor.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidOverlapDarkening_ShouldThrowArgumentOutOfRangeException(double darkening)
    {
        var options = new HandTremorOptions(OverlapDarkening: darkening);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid OverlapDarkening values pass validation.
    /// </summary>
    /// <param name="darkening">The valid overlap darkening factor.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_ValidOverlapDarkening_ShouldNotThrow(double darkening)
    {
        var options = new HandTremorOptions(OverlapDarkening: darkening);
        options.Validate();
    }

    /// <summary>
    /// Verifies that invalid primary noise options throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidPrimaryNoise_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidNoise = new PerlinNoiseOptions(Frequency: -1.0);
        var options = new HandTremorOptions(PrimaryNoise: invalidNoise);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that invalid secondary noise options throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidSecondaryNoise_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidNoise = new PerlinNoiseOptions(Octaves: 0);
        var options = new HandTremorOptions(SecondaryNoise: invalidNoise);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that MinContrastingChroma outside [0, 1] or non-finite throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="chroma">The invalid minimum contrasting chroma.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMinContrastingChroma_ShouldThrowArgumentOutOfRangeException(double chroma)
    {
        var options = new HandTremorOptions(MinContrastingChroma: chroma);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid MinContrastingChroma values pass validation.
    /// </summary>
    /// <param name="chroma">The valid minimum contrasting chroma.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.08)]
    [InlineData(1.0)]
    public void Validate_ValidMinContrastingChroma_ShouldNotThrow(double chroma)
    {
        var options = new HandTremorOptions(MinContrastingChroma: chroma);
        options.Validate();
    }

    /// <summary>
    /// Verifies that PaperGapBias outside [-1, 1] or non-finite throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="bias">The invalid paper gap bias.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidSignedUnitDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidPaperGapBias_ShouldThrowArgumentOutOfRangeException(double bias)
    {
        var options = new HandTremorOptions(PaperGapBias: bias);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid PaperGapBias values pass validation.
    /// </summary>
    /// <param name="bias">The valid paper gap bias.</param>
    [Theory]
    [InlineData(-1.0)]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_ValidPaperGapBias_ShouldNotThrow(double bias)
    {
        var options = new HandTremorOptions(PaperGapBias: bias);
        options.Validate();
    }

    /// <summary>
    /// Verifies that PaperGapOpacity outside [0, 1] or non-finite throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="opacity">The invalid paper gap opacity.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidPaperGapOpacity_ShouldThrowArgumentOutOfRangeException(double opacity)
    {
        var options = new HandTremorOptions(PaperGapOpacity: opacity);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid PaperGapOpacity values pass validation.
    /// </summary>
    /// <param name="opacity">The valid paper gap opacity.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_ValidPaperGapOpacity_ShouldNotThrow(double opacity)
    {
        var options = new HandTremorOptions(PaperGapOpacity: opacity);
        options.Validate();
    }

    /// <summary>
    /// Verifies that custom valid options pass validation without throwing.
    /// </summary>
    [Fact]
    public void Validate_CustomValidOptions_ShouldNotThrow()
    {
        var options = new HandTremorOptions(
            Amplitude: 3.0,
            SalientAmplitudeScale: 0.2,
            SimilarHueThresholdDegrees: 45.0,
            OverlapDarkening: 0.7,
            PaperColor: new Rgb8(240, 240, 235),
            PrimaryNoise: new PerlinNoiseOptions(Seed: 55, Frequency: 0.02, Octaves: 2),
            SecondaryNoise: new PerlinNoiseOptions(Seed: 66, Frequency: 0.03, Octaves: 2),
            MinContrastingChroma: 0.04,
            PaperGapBias: 0.35,
            PaperGapOpacity: 0.65);

        options.Validate();
    }

    /// <summary>
    /// Verifies record equality and hash code consistency for identical option instances.
    /// </summary>
    [Fact]
    public void RecordEquality_IdenticalValues_ShouldBeEqual()
    {
        var opt1 = new HandTremorOptions(Amplitude: 1.5, PaperColor: new Rgb8(255, 255, 255));
        var opt2 = new HandTremorOptions(Amplitude: 1.5, PaperColor: new Rgb8(255, 255, 255));
        var opt3 = new HandTremorOptions(Amplitude: 2.0);

        Assert.Equal(opt1, opt2);
        Assert.True(opt1 == opt2);
        Assert.False(opt1 == opt3);
        Assert.Equal(opt1.GetHashCode(), opt2.GetHashCode());
    }

    /// <summary>
    /// Verifies that <see cref="HandTremorOptions.ForSize"/> and <see cref="HandTremorOptions.ForImage"/> scale displacement amplitude and noise frequencies.
    /// </summary>
    [Fact]
    public void ForSize_And_ForImage_ShouldScaleAmplitudeAndDelegateNoise()
    {
        HandTremorOptions refOpts = HandTremorOptions.ForSize(new Size(800, 800));
        Assert.Equal(3.0, refOpts.Amplitude, 6);
        Assert.NotNull(refOpts.PrimaryNoise);
        Assert.NotNull(refOpts.SecondaryNoise);
        Assert.Equal(0.04, refOpts.PrimaryNoise!.Frequency, 6);
        Assert.Equal(0.04, refOpts.SecondaryNoise!.Frequency, 6);
        Assert.Equal(refOpts, HandTremorOptions.ForSize(new Size(1000, 640)));

        HandTremorOptions small = HandTremorOptions.ForSize(new Size(200, 200));
        HandTremorOptions large = HandTremorOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(0.75, small.Amplitude, 6);
        Assert.True(large.Amplitude > refOpts.Amplitude);
        Assert.True(small.PrimaryNoise!.Frequency > refOpts.PrimaryNoise.Frequency);
        Assert.True(large.PrimaryNoise!.Frequency < refOpts.PrimaryNoise.Frequency);

        var image = new SequentialMatrix<Rgb8>(400, 400);
        Assert.Equal(HandTremorOptions.ForSize(image.Size), HandTremorOptions.ForImage(image));

        HandTremorOptions customBaseline = CreateValidCustomOptions();
        HandTremorOptions customHalf = HandTremorOptions.ForSize(new Size(400, 400), customBaseline);
        Assert.Equal(2.25, customHalf.Amplitude, 6);
        Assert.Equal(0.04, customHalf.PrimaryNoise!.Frequency, 6);
        Assert.Equal(55, customHalf.PrimaryNoise.Seed);
        Assert.Equal(0.06, customHalf.SecondaryNoise!.Frequency, 6);
        Assert.Equal(66, customHalf.SecondaryNoise.Seed);
        Assert.Equal(customBaseline.PaperColor, customHalf.PaperColor);

        var scalarImage = new SequentialMatrix<DoublePixel>(400, 400);
        Assert.Equal(customHalf, HandTremorOptions.ForImage(scalarImage, customBaseline));

        var rgb64Image = new SequentialMatrix<Rgb64>(400, 400);
        Assert.Equal(HandTremorOptions.ForSize(rgb64Image.Size), HandTremorOptions.ForImage(rgb64Image));

        ImageAnalysis imageAnalysis = ImageAnalysis.Analyze(image);
        Assert.Equal(HandTremorOptions.ForSize(image.Size), HandTremorOptions.ForAnalysis(imageAnalysis));
        Assert.Equal(customHalf, HandTremorOptions.ForAnalysis(imageAnalysis, customBaseline));

        var overflowBaseline = new HandTremorOptions(Amplitude: 1e300);
        HandTremorOptions overflowScaled = HandTremorOptions.ForSize(new Size(100_000, 100_000), overflowBaseline, referenceDimension: 1.0);
        overflowScaled.Validate();
        Assert.True(double.IsFinite(overflowScaled.Amplitude));

        Assert.Throws<ArgumentNullException>(() => HandTremorOptions.ForImage<Rgb8>(null!));
        Assert.Throws<ArgumentNullException>(() => HandTremorOptions.ForImage<Rgb8>(null!, customBaseline));
        Assert.Throws<ArgumentNullException>(() => HandTremorOptions.ForImage(image, null!));
        Assert.Throws<ArgumentNullException>(() => HandTremorOptions.ForAnalysis(null!));
        Assert.Throws<ArgumentNullException>(() => HandTremorOptions.ForAnalysis(imageAnalysis, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => HandTremorOptions.ForAnalysis(imageAnalysis, 0.0));
    }
}
