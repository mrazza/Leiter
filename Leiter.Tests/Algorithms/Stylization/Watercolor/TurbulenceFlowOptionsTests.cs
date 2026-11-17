namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
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

    /// <summary>
    /// Verifies that <see cref="TurbulenceFlowOptions.ForSize"/> and <see cref="TurbulenceFlowOptions.ForImage"/> scale turbulence frequency cleanly.
    /// </summary>
    [Fact]
    public void ForSize_And_ForImage_ShouldScaleNoiseFrequency()
    {
        TurbulenceFlowOptions refOpts = TurbulenceFlowOptions.ForSize(new Size(800, 800));
        Assert.NotNull(refOpts.NoiseOptions);
        Assert.Equal(0.015, refOpts.NoiseOptions!.Frequency, 6);
        Assert.Equal(404, refOpts.NoiseOptions.Seed);
        Assert.Equal(refOpts, TurbulenceFlowOptions.ForSize(new Size(1000, 640)));

        TurbulenceFlowOptions small = TurbulenceFlowOptions.ForSize(new Size(200, 200));
        TurbulenceFlowOptions large = TurbulenceFlowOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(0.06, small.NoiseOptions!.Frequency, 6);
        Assert.True(large.NoiseOptions!.Frequency < refOpts.NoiseOptions.Frequency);

        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(3, 3), new Size(4000, 80), new Size(12000, 9000) })
        {
            TurbulenceFlowOptions.ForSize(size).Validate();
        }

        var image = new SequentialMatrix<Rgb8>(400, 400);
        Assert.Equal(TurbulenceFlowOptions.ForSize(image.Size), TurbulenceFlowOptions.ForImage(image));

        Assert.Throws<ArgumentOutOfRangeException>(() => TurbulenceFlowOptions.ForSize(new Size(-1, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TurbulenceFlowOptions.ForSize(new Size(10, 10), -2.0));
        Assert.Throws<ArgumentNullException>(() => TurbulenceFlowOptions.ForImage(null!));
    }
}
