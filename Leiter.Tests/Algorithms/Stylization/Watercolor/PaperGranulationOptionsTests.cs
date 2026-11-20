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
/// Provides unit tests for <see cref="PaperGranulationOptions"/>.
/// </summary>
public class PaperGranulationOptionsTests : SizeTunableOptionsContractTestsBase<PaperGranulationOptions>
{
    /// <inheritdoc/>
    protected override PaperGranulationOptions CreateValidCustomOptions() =>
        new(
            DensityStrength: 0.45,
            WarpAmplitude: 0.9,
            GrainNoise: new PerlinNoiseOptions(Seed: 808, Frequency: 0.12, Octaves: 3, Lacunarity: 2.1, Persistence: 0.5));

    /// <inheritdoc/>
    protected override PaperGranulationOptions CreateInvalidOptions() =>
        new(WarpAmplitude: -1.0);
    /// <summary>
    /// Verifies that default options match expected default values and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new PaperGranulationOptions();

        Assert.Equal(0.3, options.DensityStrength);
        Assert.Equal(0.6, options.WarpAmplitude);
        Assert.Equal(PaperGranulationOptions.DefaultGrainNoise, options.GrainNoise);
        Assert.Equal(PaperGranulationOptions.Default, options);

        var explicitDefaults = new PaperGranulationOptions(GrainNoise: PaperGranulationOptions.DefaultGrainNoise);
        Assert.Equal(options, explicitDefaults);

        PaperGranulationOptions resetViaWith = CreateValidCustomOptions() with { GrainNoise = null! };
        Assert.Equal(PaperGranulationOptions.DefaultGrainNoise, resetViaWith.GrainNoise);

        options.Validate();
    }

    /// <summary>
    /// Verifies that valid custom options pass validation without throwing exceptions.
    /// </summary>
    [Fact]
    public void Validate_ValidCustomOptions_ShouldNotThrow()
    {
        var grainOpts = new PerlinNoiseOptions(Seed: 707, Frequency: 0.35, Octaves: 3, Lacunarity: 2.1, Persistence: 0.55);
        var options = new PaperGranulationOptions(DensityStrength: 0.0, WarpAmplitude: 0.0, GrainNoise: grainOpts);

        options.Validate();

        Assert.Equal(0.0, options.DensityStrength);
        Assert.Equal(0.0, options.WarpAmplitude);
        Assert.NotNull(options.GrainNoise);
    }

    /// <summary>
    /// Verifies that invalid DensityStrength values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="densityStrength">The invalid density strength value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidDensityStrength_ShouldThrowArgumentOutOfRangeException(double densityStrength)
    {
        var options = new PaperGranulationOptions(DensityStrength: densityStrength);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that invalid WarpAmplitude values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="warpAmplitude">The invalid warp amplitude value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidWarpAmplitude_ShouldThrowArgumentOutOfRangeException(double warpAmplitude)
    {
        var options = new PaperGranulationOptions(WarpAmplitude: warpAmplitude);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that invalid GrainNoise throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidGrainNoise_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidNoise = new PerlinNoiseOptions(Octaves: 0);
        var options = new PaperGranulationOptions(GrainNoise: invalidNoise);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that <see cref="PaperGranulationOptions.ForSize"/> and <see cref="PaperGranulationOptions.ForImage"/> scale warp amplitude linearly and grain frequency inversely.
    /// </summary>
    [Fact]
    public void ForSize_And_ForImage_ShouldScaleWarpAndGrainFrequency()
    {
        PaperGranulationOptions refOpts = PaperGranulationOptions.ForSize(new Size(800, 800));
        Assert.Equal(0.6, refOpts.WarpAmplitude, 6);
        Assert.NotNull(refOpts.GrainNoise);
        Assert.Equal(0.18, refOpts.GrainNoise!.Frequency, 6);
        Assert.Equal(707, refOpts.GrainNoise.Seed);
        Assert.Equal(refOpts, PaperGranulationOptions.ForSize(new Size(1000, 640)));

        PaperGranulationOptions small = PaperGranulationOptions.ForSize(new Size(200, 200));
        PaperGranulationOptions large = PaperGranulationOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(0.15, small.WarpAmplitude, 6);
        Assert.Equal(0.5, small.GrainNoise!.Frequency, 6); // Clamped to Nyquist limit
        Assert.True(large.WarpAmplitude > refOpts.WarpAmplitude);
        Assert.True(large.GrainNoise!.Frequency < refOpts.GrainNoise.Frequency);

        var image = new SequentialMatrix<Rgb8>(400, 400);
        Assert.Equal(PaperGranulationOptions.ForSize(image.Size), PaperGranulationOptions.ForImage(image));

        PaperGranulationOptions customBaseline = CreateValidCustomOptions();
        PaperGranulationOptions customHalf = PaperGranulationOptions.ForSize(new Size(400, 400), customBaseline);
        Assert.Equal(0.45, customHalf.DensityStrength, 6);
        Assert.Equal(0.45, customHalf.WarpAmplitude, 6);
        Assert.Equal(0.24, customHalf.GrainNoise!.Frequency, 6);
        Assert.Equal(808, customHalf.GrainNoise.Seed);

        var scalarImage = new SequentialMatrix<DoublePixel>(400, 400);
        Assert.Equal(customHalf, PaperGranulationOptions.ForImage(scalarImage, customBaseline));

        var rgb64Image = new SequentialMatrix<Rgb64>(400, 400);
        Assert.Equal(PaperGranulationOptions.ForSize(rgb64Image.Size), PaperGranulationOptions.ForImage(rgb64Image));

        ImageAnalysis imageAnalysis = ImageAnalysis.Analyze(image);
        Assert.Equal(PaperGranulationOptions.ForSize(image.Size), PaperGranulationOptions.ForAnalysis(imageAnalysis));
        Assert.Equal(customHalf, PaperGranulationOptions.ForAnalysis(imageAnalysis, customBaseline));

        var overflowBaseline = new PaperGranulationOptions(WarpAmplitude: 1e300);
        PaperGranulationOptions overflowScaled = PaperGranulationOptions.ForSize(new Size(100_000, 100_000), overflowBaseline, referenceDimension: 1.0);
        overflowScaled.Validate();
        Assert.True(double.IsFinite(overflowScaled.WarpAmplitude));

        Assert.Throws<ArgumentNullException>(() => PaperGranulationOptions.ForImage<Rgb8>(null!));
        Assert.Throws<ArgumentNullException>(() => PaperGranulationOptions.ForImage<Rgb8>(null!, customBaseline));
        Assert.Throws<ArgumentNullException>(() => PaperGranulationOptions.ForImage(image, null!));
        Assert.Throws<ArgumentNullException>(() => PaperGranulationOptions.ForAnalysis(null!));
        Assert.Throws<ArgumentNullException>(() => PaperGranulationOptions.ForAnalysis(imageAnalysis, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaperGranulationOptions.ForAnalysis(imageAnalysis, 0.0));
    }
}
