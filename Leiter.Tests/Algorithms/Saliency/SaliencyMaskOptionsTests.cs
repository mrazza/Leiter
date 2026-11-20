namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;
using Leiter.Tests.Algorithms;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyMaskOptions"/>.
/// </summary>
public class SaliencyMaskOptionsTests : AlgorithmOptionsContractTestsBase<SaliencyMaskOptions>
{
    /// <inheritdoc/>
    protected override SaliencyMaskOptions CreateValidCustomOptions() =>
        new(
            Method: SaliencyThresholdMethod.Fixed,
            FixedThreshold: 0.4,
            RetentionPolicy: ComponentRetentionPolicy.LargestComponentOnly,
            MinAreaFraction: 0.05,
            Connectivity: GridConnectivity.FourConnected);

    /// <inheritdoc/>
    protected override SaliencyMaskOptions CreateInvalidOptions() =>
        new(FixedThreshold: -0.1);

    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchDefaultInstance()
    {
        var options = new SaliencyMaskOptions();

        Assert.Equal(SaliencyThresholdMethod.Otsu, options.Method);
        Assert.Equal(70.0 / 255.0, options.FixedThreshold, 9);
        Assert.Equal(ComponentRetentionPolicy.MinimumAreaFraction, options.RetentionPolicy);
        Assert.Equal(0.01, options.MinAreaFraction, 9);
        Assert.Equal(GridConnectivity.EightConnected, options.Connectivity);
        Assert.Equal(SaliencyMaskOptions.Default, options);
    }

    [Fact]
    public void Validate_InvalidEnumValues_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaliencyMaskOptions(Method: (SaliencyThresholdMethod)99).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaliencyMaskOptions(RetentionPolicy: (ComponentRetentionPolicy)(-1)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SaliencyMaskOptions(Connectivity: (GridConnectivity)99).Validate());
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
