namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization.Watercolor;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="PaperGranulationOptions"/>.
/// </summary>
public class PaperGranulationOptionsTests
{
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
    [InlineData(-0.01)]
    [InlineData(-5.0)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
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
    [InlineData(-0.01)]
    [InlineData(-2.0)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
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
}
