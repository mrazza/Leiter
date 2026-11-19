namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms.Saliency;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyDistanceFieldOptions"/>.
/// </summary>
public class SaliencyDistanceFieldOptionsTests
{
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchDefaultInstanceAndPassValidation()
    {
        var options = new SaliencyDistanceFieldOptions();

        Assert.Null(options.SmoothingSigma);
        Assert.Equal(0.03, options.DefaultSigmaScale, 9);
        Assert.Equal(SaliencyDistanceFieldOptions.Default, options);

        options.Validate();
        SaliencyDistanceFieldOptions.Default.Validate();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    public void Validate_ValidExplicitSmoothingSigma_ShouldNotThrow(double smoothingSigma)
    {
        var options = new SaliencyDistanceFieldOptions(SmoothingSigma: smoothingSigma);
        options.Validate();
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidSmoothingSigma_ShouldThrowArgumentOutOfRangeException(double smoothingSigma)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaliencyDistanceFieldOptions(SmoothingSigma: smoothingSigma).Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidDefaultSigmaScale_ShouldThrowArgumentOutOfRangeException(double defaultSigmaScale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaliencyDistanceFieldOptions(DefaultSigmaScale: defaultSigmaScale).Validate());
    }
}
