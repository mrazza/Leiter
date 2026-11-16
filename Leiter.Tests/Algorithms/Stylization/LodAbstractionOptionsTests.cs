namespace Leiter.Tests.Algorithms.Stylization;

using System;
using Leiter.Algorithms.Stylization;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="LodAbstractionOptions"/>.
/// </summary>
public class LodAbstractionOptionsTests
{
    /// <summary>
    /// Verifies that default options match the Wang et al. paper specifications.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new LodAbstractionOptions();

        Assert.Equal(5, options.SalientKernelSize);
        Assert.Equal(4, options.MinNonSalientKernelSize);
        Assert.Equal(9, options.MaxNonSalientKernelSize);
        Assert.Equal(10.0, options.DistanceKernelScale, 6);
        Assert.Equal(0.3, options.DistanceKernelBias, 6);
        Assert.Equal(0.3, options.CrossSegmentColorScale, 6);
        Assert.Equal(0.5, options.SalientOverlapThreshold, 6);

        options.Validate();
    }

    /// <summary>
    /// Verifies that non-positive SalientKernelSize values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="salientKernelSize">The invalid salient kernel size.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validate_InvalidSalientKernelSize_ShouldThrowArgumentOutOfRangeException(int salientKernelSize)
    {
        var options = new LodAbstractionOptions(SalientKernelSize: salientKernelSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that non-positive MinNonSalientKernelSize values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="minNonSalientKernelSize">The invalid minimum non-salient kernel size.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void Validate_InvalidMinNonSalientKernelSize_ShouldThrowArgumentOutOfRangeException(int minNonSalientKernelSize)
    {
        var options = new LodAbstractionOptions(MinNonSalientKernelSize: minNonSalientKernelSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that MaxNonSalientKernelSize less than MinNonSalientKernelSize throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="minKernelSize">The minimum kernel size.</param>
    /// <param name="maxKernelSize">The maximum kernel size less than the minimum.</param>
    [Theory]
    [InlineData(4, 3)]
    [InlineData(5, 4)]
    [InlineData(10, 9)]
    [InlineData(4, -1)]
    public void Validate_MaxNonSalientKernelSizeLessThanMin_ShouldThrowArgumentOutOfRangeException(int minKernelSize, int maxKernelSize)
    {
        var options = new LodAbstractionOptions(MinNonSalientKernelSize: minKernelSize, MaxNonSalientKernelSize: maxKernelSize);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that negative or non-finite DistanceKernelScale values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="scale">The invalid distance kernel scale.</param>
    [Theory]
    [InlineData(-0.001)]
    [InlineData(-10.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidDistanceKernelScale_ShouldThrowArgumentOutOfRangeException(double scale)
    {
        var options = new LodAbstractionOptions(DistanceKernelScale: scale);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that non-finite DistanceKernelBias values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="bias">The non-finite distance kernel bias.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidDistanceKernelBias_ShouldThrowArgumentOutOfRangeException(double bias)
    {
        var options = new LodAbstractionOptions(DistanceKernelBias: bias);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that finite DistanceKernelBias values (including negative or zero) do not throw.
    /// </summary>
    /// <param name="bias">The valid distance kernel bias.</param>
    [Theory]
    [InlineData(-0.3)]
    [InlineData(-10.0)]
    [InlineData(0.0)]
    [InlineData(1.5)]
    public void Validate_ValidDistanceKernelBias_ShouldNotThrow(double bias)
    {
        var options = new LodAbstractionOptions(DistanceKernelBias: bias);
        options.Validate();
    }

    /// <summary>
    /// Verifies that negative or non-finite CrossSegmentColorScale values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="colorScale">The invalid cross segment color scale.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(-5.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidCrossSegmentColorScale_ShouldThrowArgumentOutOfRangeException(double colorScale)
    {
        var options = new LodAbstractionOptions(CrossSegmentColorScale: colorScale);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that SalientOverlapThreshold values outside [0, 1] or non-finite throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="threshold">The invalid salient overlap threshold.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidSalientOverlapThreshold_ShouldThrowArgumentOutOfRangeException(double threshold)
    {
        var options = new LodAbstractionOptions(SalientOverlapThreshold: threshold);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that boundary values [0, 1] for SalientOverlapThreshold do not throw.
    /// </summary>
    /// <param name="threshold">The valid salient overlap threshold.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_ValidBoundarySalientOverlapThreshold_ShouldNotThrow(double threshold)
    {
        var options = new LodAbstractionOptions(SalientOverlapThreshold: threshold);
        options.Validate();
    }

    /// <summary>
    /// Verifies that fully custom valid options pass validation without throwing.
    /// </summary>
    [Fact]
    public void Validate_CustomValidOptions_ShouldNotThrow()
    {
        var options = new LodAbstractionOptions(
            SalientKernelSize: 3,
            MinNonSalientKernelSize: 2,
            MaxNonSalientKernelSize: 6,
            DistanceKernelScale: 8.0,
            DistanceKernelBias: 0.2,
            CrossSegmentColorScale: 0.4,
            SalientOverlapThreshold: 0.6);

        options.Validate();
    }
}
