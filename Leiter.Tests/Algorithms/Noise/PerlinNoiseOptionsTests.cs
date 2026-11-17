namespace Leiter.Tests.Algorithms.Noise;

using System;
using Leiter.Algorithms.Noise;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="PerlinNoiseOptions"/>.
/// </summary>
public class PerlinNoiseOptionsTests
{
    /// <summary>
    /// Verifies that default options match expected default values and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new PerlinNoiseOptions();

        Assert.Equal(1337, options.Seed);
        Assert.Equal(0.05, options.Frequency, 6);
        Assert.Equal(4, options.Octaves);
        Assert.Equal(2.0, options.Lacunarity, 6);
        Assert.Equal(0.5, options.Persistence, 6);

        options.Validate();
    }

    /// <summary>
    /// Verifies that non-positive or non-finite Frequency values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="frequency">The invalid frequency value.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.05)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidFrequency_ShouldThrowArgumentOutOfRangeException(double frequency)
    {
        var options = new PerlinNoiseOptions(Frequency: frequency);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that Octaves outside [1, 16] throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="octaves">The invalid octaves count.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(17)]
    [InlineData(100)]
    public void Validate_InvalidOctaves_ShouldThrowArgumentOutOfRangeException(int octaves)
    {
        var options = new PerlinNoiseOptions(Octaves: octaves);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that boundary values for Octaves (1 and 16) pass validation.
    /// </summary>
    /// <param name="octaves">The boundary octaves value.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(16)]
    public void Validate_ValidBoundaryOctaves_ShouldNotThrow(int octaves)
    {
        var options = new PerlinNoiseOptions(Octaves: octaves);
        options.Validate();
    }

    /// <summary>
    /// Verifies that non-positive or non-finite Lacunarity values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="lacunarity">The invalid lacunarity value.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(-2.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidLacunarity_ShouldThrowArgumentOutOfRangeException(double lacunarity)
    {
        var options = new PerlinNoiseOptions(Lacunarity: lacunarity);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that negative or non-finite Persistence values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="persistence">The invalid persistence value.</param>
    [Theory]
    [InlineData(-0.001)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_InvalidPersistence_ShouldThrowArgumentOutOfRangeException(double persistence)
    {
        var options = new PerlinNoiseOptions(Persistence: persistence);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that non-negative boundary value 0.0 for Persistence passes validation.
    /// </summary>
    /// <param name="persistence">The boundary persistence value.</param>
    [Theory]
    [InlineData(0.0)]
    public void Validate_ValidBoundaryPersistence_ShouldNotThrow(double persistence)
    {
        var options = new PerlinNoiseOptions(Persistence: persistence);
        options.Validate();
    }

    /// <summary>
    /// Verifies that custom valid options pass validation without throwing.
    /// </summary>
    [Fact]
    public void Validate_CustomValidOptions_ShouldNotThrow()
    {
        var options = new PerlinNoiseOptions(
            Seed: 42,
            Frequency: 0.1,
            Octaves: 8,
            Lacunarity: 1.5,
            Persistence: 0.7);

        options.Validate();
    }
}
