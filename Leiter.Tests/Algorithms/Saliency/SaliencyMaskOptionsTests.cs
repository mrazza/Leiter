namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyMaskOptions"/>.
/// </summary>
public class SaliencyMaskOptionsTests
{
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchDefaultInstanceAndPassValidation()
    {
        var options = new SaliencyMaskOptions();

        Assert.Equal(SaliencyThresholdMethod.Otsu, options.Method);
        Assert.Equal(70.0 / 255.0, options.FixedThreshold, 9);
        Assert.Equal(ComponentRetentionPolicy.MinimumAreaFraction, options.RetentionPolicy);
        Assert.Equal(0.01, options.MinAreaFraction, 9);
        Assert.Equal(SaliencyMaskOptions.Default, options);

        options.Validate();
        SaliencyMaskOptions.Default.Validate();
    }

    [Fact]
    public void Validate_InvalidEnumValues_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaliencyMaskOptions(Method: (SaliencyThresholdMethod)99).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaliencyMaskOptions(RetentionPolicy: (ComponentRetentionPolicy)(-1)).Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidFixedThreshold_ShouldThrowArgumentOutOfRangeException(double fixedThreshold)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaliencyMaskOptions(FixedThreshold: fixedThreshold).Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMinAreaFraction_ShouldThrowArgumentOutOfRangeException(double minAreaFraction)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaliencyMaskOptions(MinAreaFraction: minAreaFraction).Validate());
    }
}
