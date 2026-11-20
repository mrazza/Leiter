namespace Leiter.Tests.Algorithms.Noise;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Noise;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.Algorithms;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="PerlinNoiseOptions"/>.
/// </summary>
public class PerlinNoiseOptionsTests : SizeTunableOptionsContractTestsBase<PerlinNoiseOptions>
{
    /// <inheritdoc/>
    protected override PerlinNoiseOptions CreateValidCustomOptions() =>
        new(Seed: 42, Frequency: 0.1, Octaves: 6, Lacunarity: 2.2, Persistence: 0.4);

    /// <inheritdoc/>
    protected override PerlinNoiseOptions CreateInvalidOptions() =>
        new(Frequency: 0.0);
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
    [MemberData(nameof(OptionValidationData.InvalidPositiveDoubles), MemberType = typeof(OptionValidationData))]
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
    [MemberData(nameof(OptionValidationData.InvalidPositiveDoubles), MemberType = typeof(OptionValidationData))]
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
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
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

    /// <summary>
    /// Verifies that <see cref="PerlinNoiseOptions.ForSize(Size, double)"/> matches defaults at reference dimensions and scales frequency inversely across sizes.
    /// </summary>
    [Fact]
    public void ForSize_And_ForImage_ShouldScaleFrequencyInverselyAndPassValidation()
    {
        PerlinNoiseOptions refOptions = PerlinNoiseOptions.ForSize(new Size(800, 800));
        PerlinNoiseOptions rectRefOptions = PerlinNoiseOptions.ForSize(new Size(1000, 640));
        Assert.Equal(0.05, refOptions.Frequency, 6);
        Assert.Equal(refOptions, rectRefOptions);

        PerlinNoiseOptions small = PerlinNoiseOptions.ForSize(new Size(200, 200));
        PerlinNoiseOptions large = PerlinNoiseOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(0.20, small.Frequency, 6);
        Assert.True(large.Frequency < refOptions.Frequency);

        var baseline = new PerlinNoiseOptions(Seed: 404, Frequency: 0.45, Octaves: 3);
        PerlinNoiseOptions tiny = PerlinNoiseOptions.ForSize(new Size(16, 16), baseline);
        Assert.Equal(0.5, tiny.Frequency, 6); // Clamped to Nyquist limit

        // Extreme scale factors must stay finite and bounded within [MinFrequency, MaxNyquistFrequency].
        PerlinNoiseOptions overflowFreq = PerlinNoiseOptions.ForSize(new Size(16, 16), baseline, referenceDimension: 1e300);
        overflowFreq.Validate();
        Assert.Equal(OptionTuning.MaxNyquistFrequency, overflowFreq.Frequency, 12);

        PerlinNoiseOptions underflowFreq = PerlinNoiseOptions.ForSize(new Size(16, 16), baseline, referenceDimension: 1e-300);
        underflowFreq.Validate();
        Assert.Equal(OptionTuning.MinFrequency, underflowFreq.Frequency, 12);

        var image = new SequentialMatrix<Rgb8>(400, 400);
        PerlinNoiseOptions fromImage = PerlinNoiseOptions.ForImage(image);
        Assert.Equal(PerlinNoiseOptions.ForSize(image.Size), fromImage);
        Assert.Equal(PerlinNoiseOptions.ForSize(image.Size, baseline), PerlinNoiseOptions.ForImage(image, baseline));

        var scalarImage = new SequentialMatrix<DoublePixel>(200, 200);
        Assert.Equal(PerlinNoiseOptions.ForSize(scalarImage.Size, baseline), PerlinNoiseOptions.ForImage(scalarImage, baseline));
    }

    /// <summary>
    /// Verifies that <see cref="PerlinNoiseOptions.ForImage{T}"/> validates null arguments.
    /// </summary>
    [Fact]
    public void ForImage_InvalidArguments_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => PerlinNoiseOptions.ForImage<Rgb8>(null!));
        Assert.Throws<ArgumentNullException>(() => PerlinNoiseOptions.ForImage<Rgb8>(null!, new PerlinNoiseOptions()));
        Assert.Throws<ArgumentNullException>(() => PerlinNoiseOptions.ForImage(new SequentialMatrix<Rgb8>(8, 8), null!));
    }
}
