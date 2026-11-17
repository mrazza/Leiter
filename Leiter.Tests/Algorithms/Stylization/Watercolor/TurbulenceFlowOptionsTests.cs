namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization.Watercolor;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="TurbulenceFlowOptions"/>.
/// </summary>
public class TurbulenceFlowOptionsTests
{
    /// <summary>
    /// Verifies that default options match expected default values and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new TurbulenceFlowOptions();

        Assert.Equal(0.35, options.Intensity);
        Assert.Null(options.NoiseOptions);

        options.Validate();
    }

    /// <summary>
    /// Verifies that valid custom options pass validation without throwing exceptions.
    /// </summary>
    [Fact]
    public void Validate_ValidCustomOptions_ShouldNotThrow()
    {
        var noiseOpts = new PerlinNoiseOptions(Seed: 123, Frequency: 0.02, Octaves: 3, Lacunarity: 2.0, Persistence: 0.6);
        var options = new TurbulenceFlowOptions(Intensity: 0.0, NoiseOptions: noiseOpts);

        options.Validate();

        Assert.Equal(0.0, options.Intensity);
        Assert.NotNull(options.NoiseOptions);
    }

    /// <summary>
    /// Verifies that invalid Intensity values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="intensity">The invalid intensity value.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-10.0)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void Validate_InvalidIntensity_ShouldThrowArgumentOutOfRangeException(double intensity)
    {
        var options = new TurbulenceFlowOptions(Intensity: intensity);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that invalid PerlinNoiseOptions throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidNoiseOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidNoise = new PerlinNoiseOptions(Frequency: -1.0);
        var options = new TurbulenceFlowOptions(NoiseOptions: invalidNoise);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }
}
