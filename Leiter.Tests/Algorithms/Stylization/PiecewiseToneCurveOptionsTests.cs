namespace Leiter.Tests.Algorithms.Stylization;

using System;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;
using static Leiter.Tests.TestUtils.TestConstants;

/// <summary>
/// Provides unit tests for <see cref="PiecewiseToneCurveOptions"/>.
/// </summary>
public class PiecewiseToneCurveOptionsTests
{

    /// <summary>
    /// Verifies that default options match the paper specifications and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new PiecewiseToneCurveOptions();

        Assert.Equal(0.05, options.MinTone, PrecisionTolerance);
        Assert.Equal(0.96, options.MaxTone, PrecisionTolerance);
        Assert.Equal(0.15, options.ShadowKnee, PrecisionTolerance);
        Assert.Equal(0.85, options.HighlightKnee, PrecisionTolerance);

        options.Validate();
    }

    /// <summary>
    /// Verifies that non-finite or out-of-range MinTone values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="minTone">The invalid minimum tone floor.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-0.01)]
    [InlineData(-1.0)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void Validate_InvalidMinTone_ShouldThrowArgumentOutOfRangeException(double minTone)
    {
        var options = new PiecewiseToneCurveOptions(MinTone: minTone);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that non-finite or out-of-range MaxTone values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="maxTone">The invalid maximum tone ceiling.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0.05)] // Equal to default MinTone (0.05)
    [InlineData(0.01)] // Less than default MinTone
    [InlineData(-0.5)]
    [InlineData(1.01)]
    [InlineData(2.0)]
    public void Validate_InvalidMaxTone_ShouldThrowArgumentOutOfRangeException(double maxTone)
    {
        var options = new PiecewiseToneCurveOptions(MaxTone: maxTone);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that non-finite or out-of-domain ShadowKnee values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="shadowKnee">The invalid shadow knee breakpoint.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.0)]
    [InlineData(1.2)]
    public void Validate_InvalidShadowKnee_ShouldThrowArgumentOutOfRangeException(double shadowKnee)
    {
        var options = new PiecewiseToneCurveOptions(ShadowKnee: shadowKnee);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that non-finite or out-of-domain HighlightKnee values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="highlightKnee">The invalid highlight knee breakpoint.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0.15)] // Equal to default ShadowKnee (0.15)
    [InlineData(0.10)] // Less than default ShadowKnee
    [InlineData(-0.1)]
    [InlineData(1.0)]
    [InlineData(1.1)]
    public void Validate_InvalidHighlightKnee_ShouldThrowArgumentOutOfRangeException(double highlightKnee)
    {
        var options = new PiecewiseToneCurveOptions(HighlightKnee: highlightKnee);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid custom parameter combinations pass validation.
    /// </summary>
    /// <param name="minTone">The valid minimum tone.</param>
    /// <param name="maxTone">The valid maximum tone.</param>
    /// <param name="shadowKnee">The valid shadow knee breakpoint.</param>
    /// <param name="highlightKnee">The valid highlight knee breakpoint.</param>
    [Theory]
    [InlineData(0.0, 1.0, 0.001, 0.999)]
    [InlineData(0.1, 0.9, 0.2, 0.8)]
    [InlineData(0.25, 0.75, 0.1, 0.9)]
    public void Validate_ValidParameters_ShouldSucceed(double minTone, double maxTone, double shadowKnee, double highlightKnee)
    {
        var options = new PiecewiseToneCurveOptions(minTone, maxTone, shadowKnee, highlightKnee);
        options.Validate();

        Assert.Equal(minTone, options.MinTone, PrecisionTolerance);
        Assert.Equal(maxTone, options.MaxTone, PrecisionTolerance);
        Assert.Equal(shadowKnee, options.ShadowKnee, PrecisionTolerance);
        Assert.Equal(highlightKnee, options.HighlightKnee, PrecisionTolerance);
    }

    /// <summary>
    /// Verifies that <see cref="PiecewiseToneCurveOptions.ForSize"/> preserves defaults and <see cref="PiecewiseToneCurveOptions.ForImage"/> adapts knee breakpoints safely.
    /// </summary>
    [Fact]
    public void ForSize_And_ForImage_ShouldPreserveBoundsAndAdaptKnees()
    {
        PiecewiseToneCurveOptions refOpts = PiecewiseToneCurveOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(new PiecewiseToneCurveOptions(), refOpts);

        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(4000, 80), new Size(12000, 9000) })
        {
            PiecewiseToneCurveOptions.ForSize(size).Validate();
        }

        var flat = new SequentialMatrix<Rgb8>(64, 64);
        flat.SetAll(new Rgb8(128, 128, 128));
        var highContrast = new SequentialMatrix<Rgb8>(64, 64);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                highContrast[x, y] = x < 32 ? new Rgb8(5, 5, 5) : new Rgb8(250, 250, 250);
            }
        }

        PiecewiseToneCurveOptions flatOpts = PiecewiseToneCurveOptions.ForImage(flat);
        PiecewiseToneCurveOptions hcOpts = PiecewiseToneCurveOptions.ForImage(highContrast);
        Assert.True(hcOpts.ShadowKnee > flatOpts.ShadowKnee);
        Assert.True(hcOpts.HighlightKnee < flatOpts.HighlightKnee);
        Assert.InRange(hcOpts.ShadowKnee, 0.12, 0.22);
        Assert.InRange(hcOpts.HighlightKnee, 0.78, 0.88);
        Assert.Equal(0.05, hcOpts.MinTone, PrecisionTolerance);
        Assert.Equal(0.96, hcOpts.MaxTone, PrecisionTolerance);

        Assert.Throws<ArgumentOutOfRangeException>(() => PiecewiseToneCurveOptions.ForSize(new Size(-1, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PiecewiseToneCurveOptions.ForSize(new Size(10, 10), 0.0));
        Assert.Throws<ArgumentNullException>(() => PiecewiseToneCurveOptions.ForImage(null!));
    }
}
