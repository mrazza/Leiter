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
/// Provides unit tests for <see cref="TurbulenceFlowOptions"/>.
/// </summary>
public class TurbulenceFlowOptionsTests : SizeTunableOptionsContractTestsBase<TurbulenceFlowOptions>
{
    /// <inheritdoc/>
    protected override TurbulenceFlowOptions CreateValidCustomOptions() =>
        new(
            Intensity: 0.5,
            Noise: new PerlinNoiseOptions(Seed: 909, Frequency: 0.025, Octaves: 3, Lacunarity: 2.2, Persistence: 0.45));

    /// <inheritdoc/>
    protected override TurbulenceFlowOptions CreateInvalidOptions() =>
        new(Intensity: -1.0);
    /// <summary>
    /// Verifies that default options match expected default values and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new TurbulenceFlowOptions();

        Assert.Equal(0.35, options.Intensity);
        Assert.Equal(TurbulenceFlowOptions.DefaultNoise, options.Noise);
        Assert.Equal(TurbulenceFlowOptions.Default, options);

        var explicitDefaults = new TurbulenceFlowOptions(Noise: TurbulenceFlowOptions.DefaultNoise);
        Assert.Equal(options, explicitDefaults);

        TurbulenceFlowOptions resetViaWith = CreateValidCustomOptions() with { Noise = null! };
        Assert.Equal(TurbulenceFlowOptions.DefaultNoise, resetViaWith.Noise);

        options.Validate();
    }

    /// <summary>
    /// Verifies that valid custom options pass validation without throwing exceptions.
    /// </summary>
    [Fact]
    public void Validate_ValidCustomOptions_ShouldNotThrow()
    {
        var noiseOpts = new PerlinNoiseOptions(Seed: 123, Frequency: 0.02, Octaves: 3, Lacunarity: 2.0, Persistence: 0.6);
        var options = new TurbulenceFlowOptions(Intensity: 0.0, Noise: noiseOpts);

        options.Validate();

        Assert.Equal(0.0, options.Intensity);
        Assert.NotNull(options.Noise);
    }

    /// <summary>
    /// Verifies that invalid Intensity values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="intensity">The invalid intensity value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidIntensity_ShouldThrowArgumentOutOfRangeException(double intensity)
    {
        var options = new TurbulenceFlowOptions(Intensity: intensity);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that invalid PerlinNoiseOptions throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidNoise_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidNoise = new PerlinNoiseOptions(Frequency: -1.0);
        var options = new TurbulenceFlowOptions(Noise: invalidNoise);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that <see cref="TurbulenceFlowOptions.ForSize"/> and <see cref="TurbulenceFlowOptions.ForImage"/> scale turbulence frequency cleanly.
    /// </summary>
    [Fact]
    public void ForSize_And_ForImage_ShouldScaleNoiseFrequency()
    {
        TurbulenceFlowOptions refOpts = TurbulenceFlowOptions.ForSize(new Size(800, 800));
        Assert.NotNull(refOpts.Noise);
        Assert.Equal(0.015, refOpts.Noise!.Frequency, 6);
        Assert.Equal(404, refOpts.Noise.Seed);
        Assert.Equal(refOpts, TurbulenceFlowOptions.ForSize(new Size(1000, 640)));

        TurbulenceFlowOptions small = TurbulenceFlowOptions.ForSize(new Size(200, 200));
        TurbulenceFlowOptions large = TurbulenceFlowOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(0.06, small.Noise!.Frequency, 6);
        Assert.True(large.Noise!.Frequency < refOpts.Noise.Frequency);

        var image = new SequentialMatrix<Rgb8>(400, 400);
        Assert.Equal(TurbulenceFlowOptions.ForSize(image.Size), TurbulenceFlowOptions.ForImage(image));

        TurbulenceFlowOptions customBaseline = CreateValidCustomOptions();
        TurbulenceFlowOptions customHalf = TurbulenceFlowOptions.ForSize(new Size(400, 400), customBaseline);
        Assert.Equal(0.5, customHalf.Intensity, 6);
        Assert.Equal(0.05, customHalf.Noise!.Frequency, 6);
        Assert.Equal(909, customHalf.Noise.Seed);

        var scalarImage = new SequentialMatrix<DoublePixel>(400, 400);
        Assert.Equal(customHalf, TurbulenceFlowOptions.ForImage(scalarImage, customBaseline));

        var rgb64Image = new SequentialMatrix<Rgb64>(400, 400);
        Assert.Equal(TurbulenceFlowOptions.ForSize(rgb64Image.Size), TurbulenceFlowOptions.ForImage(rgb64Image));

        ImageAnalysis imageAnalysis = ImageAnalysis.Analyze(image);
        Assert.Equal(TurbulenceFlowOptions.ForSize(image.Size), TurbulenceFlowOptions.ForAnalysis(imageAnalysis));
        Assert.Equal(customHalf, TurbulenceFlowOptions.ForAnalysis(imageAnalysis, customBaseline));

        Assert.Throws<ArgumentNullException>(() => TurbulenceFlowOptions.ForImage<Rgb8>(null!));
        Assert.Throws<ArgumentNullException>(() => TurbulenceFlowOptions.ForImage<Rgb8>(null!, customBaseline));
        Assert.Throws<ArgumentNullException>(() => TurbulenceFlowOptions.ForImage(image, null!));
        Assert.Throws<ArgumentNullException>(() => TurbulenceFlowOptions.ForAnalysis(null!));
        Assert.Throws<ArgumentNullException>(() => TurbulenceFlowOptions.ForAnalysis(imageAnalysis, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => TurbulenceFlowOptions.ForAnalysis(imageAnalysis, 0.0));
    }
}
