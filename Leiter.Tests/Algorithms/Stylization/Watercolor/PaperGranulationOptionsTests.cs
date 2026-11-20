namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
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
            GrainNoiseOptions: new PerlinNoiseOptions(Seed: 808, Frequency: 0.12, Octaves: 3, Lacunarity: 2.1, Persistence: 0.5));

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
        Assert.Null(options.GrainNoiseOptions);

        options.Validate();
    }

    /// <summary>
    /// Verifies that valid custom options pass validation without throwing exceptions.
    /// </summary>
    [Fact]
    public void Validate_ValidCustomOptions_ShouldNotThrow()
    {
        var grainOpts = new PerlinNoiseOptions(Seed: 707, Frequency: 0.35, Octaves: 3, Lacunarity: 2.1, Persistence: 0.55);
        var options = new PaperGranulationOptions(DensityStrength: 0.0, WarpAmplitude: 0.0, GrainNoiseOptions: grainOpts);

        options.Validate();

        Assert.Equal(0.0, options.DensityStrength);
        Assert.Equal(0.0, options.WarpAmplitude);
        Assert.NotNull(options.GrainNoiseOptions);
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
    /// Verifies that invalid GrainNoiseOptions throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidGrainNoiseOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidNoise = new PerlinNoiseOptions(Octaves: 0);
        var options = new PaperGranulationOptions(GrainNoiseOptions: invalidNoise);
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
        Assert.NotNull(refOpts.GrainNoiseOptions);
        Assert.Equal(0.18, refOpts.GrainNoiseOptions!.Frequency, 6);
        Assert.Equal(707, refOpts.GrainNoiseOptions.Seed);
        Assert.Equal(refOpts, PaperGranulationOptions.ForSize(new Size(1000, 640)));

        PaperGranulationOptions small = PaperGranulationOptions.ForSize(new Size(200, 200));
        PaperGranulationOptions large = PaperGranulationOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(0.15, small.WarpAmplitude, 6);
        Assert.Equal(0.5, small.GrainNoiseOptions!.Frequency, 6); // Clamped to Nyquist limit
        Assert.True(large.WarpAmplitude > refOpts.WarpAmplitude);
        Assert.True(large.GrainNoiseOptions!.Frequency < refOpts.GrainNoiseOptions.Frequency);

        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(3, 3), new Size(4000, 80), new Size(12000, 9000) })
        {
            PaperGranulationOptions.ForSize(size).Validate();
        }

        var image = new SequentialMatrix<Rgb8>(400, 400);
        Assert.Equal(PaperGranulationOptions.ForSize(image.Size), PaperGranulationOptions.ForImage(image));

        PaperGranulationOptions customBaseline = CreateValidCustomOptions();
        PaperGranulationOptions customHalf = PaperGranulationOptions.ForSize(new Size(400, 400), customBaseline);
        Assert.Equal(0.45, customHalf.DensityStrength, 6);
        Assert.Equal(0.45, customHalf.WarpAmplitude, 6);
        Assert.Equal(0.24, customHalf.GrainNoiseOptions!.Frequency, 6);
        Assert.Equal(808, customHalf.GrainNoiseOptions.Seed);

        var scalarImage = new SequentialMatrix<DoublePixel>(400, 400);
        Assert.Equal(customHalf, PaperGranulationOptions.ForImage(scalarImage, customBaseline));

        var rgb64Image = new SequentialMatrix<Rgb64>(400, 400);
        Assert.Equal(PaperGranulationOptions.ForSize(rgb64Image.Size), PaperGranulationOptions.ForImage(rgb64Image));

        Assert.Throws<ArgumentOutOfRangeException>(() => PaperGranulationOptions.ForSize(new Size(-1, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaperGranulationOptions.ForSize(new Size(10, 10), double.NaN));
        Assert.Throws<ArgumentNullException>(() => PaperGranulationOptions.ForImage<Rgb8>(null!));
        Assert.Throws<ArgumentNullException>(() => PaperGranulationOptions.ForImage<Rgb8>(null!, customBaseline));
        Assert.Throws<ArgumentNullException>(() => PaperGranulationOptions.ForImage(image, null!));
    }
}
