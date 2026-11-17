namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="EdgeDarkeningOptions"/>.
/// </summary>
public class EdgeDarkeningOptionsTests
{
    /// <summary>
    /// Verifies that default options match expected default values and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new EdgeDarkeningOptions();

        Assert.Equal(0.65, options.Intensity);
        Assert.Equal(1.0, options.BlurSigma);
        Assert.Equal(GradientOperator.Sobel, options.GradientOperator);

        options.Validate();
    }

    /// <summary>
    /// Verifies that valid custom options pass validation without throwing exceptions.
    /// </summary>
    [Fact]
    public void Validate_ValidCustomOptions_ShouldNotThrow()
    {
        var options = new EdgeDarkeningOptions(
            Intensity: 0.0,
            BlurSigma: 0.0,
            GradientOperator: GradientOperator.Scharr);

        options.Validate();

        Assert.Equal(0.0, options.Intensity);
        Assert.Equal(0.0, options.BlurSigma);
        Assert.Equal(GradientOperator.Scharr, options.GradientOperator);
    }

    /// <summary>
    /// Verifies that invalid Intensity values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="intensity">The invalid intensity value.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void Validate_InvalidIntensity_ShouldThrowArgumentOutOfRangeException(double intensity)
    {
        var options = new EdgeDarkeningOptions(Intensity: intensity);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that invalid BlurSigma values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="blurSigma">The invalid blur sigma value.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-2.5)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void Validate_InvalidBlurSigma_ShouldThrowArgumentOutOfRangeException(double blurSigma)
    {
        var options = new EdgeDarkeningOptions(BlurSigma: blurSigma);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that an undefined GradientOperator enum value throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidGradientOperator_ShouldThrowArgumentOutOfRangeException()
    {
        var options = new EdgeDarkeningOptions(GradientOperator: (GradientOperator)999);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that <see cref="EdgeDarkeningOptions.ForSize"/> scales blur sigma and compensates edge intensity, and <see cref="EdgeDarkeningOptions.ForImage"/> adapts to color dispersion.
    /// </summary>
    [Fact]
    public void ForSize_And_ForImage_ShouldCompensateIntensityAndAdaptToSpread()
    {
        EdgeDarkeningOptions refOpts = EdgeDarkeningOptions.ForSize(new Size(800, 800));
        Assert.Equal(0.65, refOpts.Intensity, 6);
        Assert.Equal(1.0, refOpts.BlurSigma, 6);
        Assert.Equal(refOpts, EdgeDarkeningOptions.ForSize(new Size(1000, 640)));

        EdgeDarkeningOptions small = EdgeDarkeningOptions.ForSize(new Size(200, 200));
        EdgeDarkeningOptions large = EdgeDarkeningOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(0.25, small.BlurSigma, 6);
        Assert.True(small.Intensity < refOpts.Intensity);
        Assert.True(large.BlurSigma > refOpts.BlurSigma);
        Assert.True(large.Intensity > refOpts.Intensity);

        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(3, 3), new Size(4000, 80), new Size(12000, 9000) })
        {
            EdgeDarkeningOptions.ForSize(size).Validate();
        }

        var flat = new SequentialMatrix<Rgb8>(64, 64);
        flat.SetAll(new Rgb8(120, 120, 120));
        var vibrant = new SequentialMatrix<Rgb8>(64, 64);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                vibrant[x, y] = (x + y) % 2 == 0 ? new Rgb8(255, 240, 20) : new Rgb8(10, 20, 235);
            }
        }

        Assert.True(EdgeDarkeningOptions.ForImage(flat).Intensity > EdgeDarkeningOptions.ForImage(vibrant).Intensity);

        Assert.Throws<ArgumentOutOfRangeException>(() => EdgeDarkeningOptions.ForSize(new Size(10, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => EdgeDarkeningOptions.ForSize(new Size(10, 10), double.PositiveInfinity));
        Assert.Throws<ArgumentNullException>(() => EdgeDarkeningOptions.ForImage(null!));
    }
}
