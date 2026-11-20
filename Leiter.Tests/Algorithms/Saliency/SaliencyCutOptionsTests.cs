namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.Algorithms;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyCutOptions"/>.
/// </summary>
public class SaliencyCutOptionsTests : SizeTunableOptionsContractTestsBase<SaliencyCutOptions>
{
    /// <inheritdoc/>
    protected override SaliencyCutOptions CreateValidCustomOptions() =>
        new(
            MaxOuterIterations: 3,
            TrimapBandRadius: 14,
            GrabCut: new GrabCutOptions(Iterations: 3, MixtureComponentCount: 4, SmoothnessWeight: 40.0),
            Mask: new SaliencyMaskOptions(
                Method: SaliencyThresholdMethod.Otsu,
                FixedThreshold: 0.3,
                RetentionPolicy: ComponentRetentionPolicy.LargestComponentOnly,
                MinAreaFraction: 0.02,
                Connectivity: GridConnectivity.FourConnected),
            MaxWorkingDimension: 512);

    /// <inheritdoc/>
    protected override SaliencyCutOptions CreateInvalidOptions() =>
        new(MaxOuterIterations: 0);

    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new SaliencyCutOptions();

        Assert.Equal(4, options.MaxOuterIterations);
        Assert.Equal(10, options.TrimapBandRadius);
        Assert.Null(options.GrabCut);
        Assert.Null(options.Mask);
        Assert.Null(options.MaxWorkingDimension);
        Assert.Equal(SaliencyCutOptions.Default, options);
        Assert.Equal(GrabCutOptions.Default with { Iterations = 2 }, SaliencyCutOptions.DefaultGrabCut);
        Assert.Equal(SaliencyMaskOptions.Default with { Method = SaliencyThresholdMethod.Fixed }, SaliencyCutOptions.DefaultMask);
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidInitialThreshold_ShouldThrowArgumentOutOfRangeException(double initialThreshold)
    {
        var options = new SaliencyCutOptions(Mask: new SaliencyMaskOptions(FixedThreshold: initialThreshold));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveInts), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMaxOuterIterations_ShouldThrowArgumentOutOfRangeException(int maxOuterIterations)
    {
        var options = new SaliencyCutOptions(MaxOuterIterations: maxOuterIterations);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveInts), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidInnerGrabCutIterations_ShouldThrowArgumentOutOfRangeException(int innerGrabCutIterations)
    {
        var options = new SaliencyCutOptions(GrabCut: new GrabCutOptions(Iterations: innerGrabCutIterations));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveInts), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidTrimapBandRadius_ShouldThrowArgumentOutOfRangeException(int trimapBandRadius)
    {
        var options = new SaliencyCutOptions(TrimapBandRadius: trimapBandRadius);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveInts), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMixtureComponentCount_ShouldThrowArgumentOutOfRangeException(int mixtureComponentCount)
    {
        var options = new SaliencyCutOptions(GrabCut: new GrabCutOptions(MixtureComponentCount: mixtureComponentCount));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidSmoothnessWeight_ShouldThrowArgumentOutOfRangeException(double smoothnessWeight)
    {
        var options = new SaliencyCutOptions(GrabCut: new GrabCutOptions(SmoothnessWeight: smoothnessWeight));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void Validate_InvalidRetentionPolicy_ShouldThrowArgumentOutOfRangeException()
    {
        var options = new SaliencyCutOptions(Mask: new SaliencyMaskOptions(RetentionPolicy: (ComponentRetentionPolicy)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMinAreaFraction_ShouldThrowArgumentOutOfRangeException(double minAreaFraction)
    {
        var options = new SaliencyCutOptions(Mask: new SaliencyMaskOptions(MinAreaFraction: minAreaFraction));
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidMaxWorkingDimension_ShouldThrowArgumentOutOfRangeException(int maxWorkingDimension)
    {
        var options = new SaliencyCutOptions(MaxWorkingDimension: maxWorkingDimension);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(16)]
    [InlineData(256)]
    public void Validate_ValidMaxWorkingDimension_ShouldNotThrow(int maxWorkingDimension)
    {
        var options = new SaliencyCutOptions(MaxWorkingDimension: maxWorkingDimension);
        options.Validate();
    }

    [Fact]
    public void ForSize_ForImage_And_ForAnalysis_ShouldScaleBandAndAdaptComponents()
    {
        SaliencyCutOptions refOpts = SaliencyCutOptions.ForSize(new Size(800, 800));
        Assert.Equal(10, refOpts.TrimapBandRadius);
        Assert.NotNull(refOpts.GrabCut);
        Assert.Equal(50.0, refOpts.GrabCut!.SmoothnessWeight, 6);
        Assert.NotNull(refOpts.Mask);
        Assert.Null(refOpts.MaxWorkingDimension);
        Assert.Equal(refOpts, SaliencyCutOptions.ForSize(new Size(1000, 640)));

        SaliencyCutOptions small = SaliencyCutOptions.ForSize(new Size(200, 200));
        SaliencyCutOptions large = SaliencyCutOptions.ForSize(new Size(3200, 2400));
        SaliencyCutOptions strip = SaliencyCutOptions.ForSize(new Size(4000, 80));
        Assert.Equal(3, small.TrimapBandRadius);
        Assert.True(large.TrimapBandRadius > refOpts.TrimapBandRadius);
        Assert.InRange(strip.TrimapBandRadius, 1, 80 / 3);

        SequentialMatrix<Rgb8> flat = TestMatrices.Constant(64, 64, new Rgb8(110, 110, 110));
        SequentialMatrix<Rgb8> vibrant = TestMatrices.Checkerboard(64, 64, new Rgb8(255, 250, 20), new Rgb8(10, 20, 240));

        Assert.Equal(4, SaliencyCutOptions.ForImage(flat).GrabCut!.MixtureComponentCount);
        Assert.Equal(6, SaliencyCutOptions.ForImage(vibrant).GrabCut!.MixtureComponentCount);
        Assert.Equal(SaliencyCutOptions.DefaultGrabCut.Iterations, SaliencyCutOptions.ForImage(vibrant).GrabCut!.Iterations);

        ImageAnalysis vibrantAnalysis = ImageAnalysis.Analyze(vibrant);
        Assert.Equal(SaliencyCutOptions.ForImage(vibrant), SaliencyCutOptions.ForAnalysis(vibrantAnalysis));

        SaliencyCutOptions customBaseline = CreateValidCustomOptions();
        SaliencyCutOptions customHalf = SaliencyCutOptions.ForSize(new Size(400, 400), customBaseline);
        Assert.Equal(7, customHalf.TrimapBandRadius);
        Assert.Equal(3, customHalf.GrabCut!.Iterations);
        Assert.Equal(20.0, customHalf.GrabCut.SmoothnessWeight, 6);
        Assert.Equal(customBaseline.Mask, customHalf.Mask);
        Assert.Equal(512, customHalf.MaxWorkingDimension);

        SaliencyCutOptions customVibrant = SaliencyCutOptions.ForImage(vibrant, customBaseline);
        Assert.Equal(3, customVibrant.GrabCut!.Iterations);
        Assert.Equal(5, customVibrant.GrabCut.MixtureComponentCount);
        Assert.Equal(customBaseline.Mask, customVibrant.Mask);
        Assert.Equal(512, customVibrant.MaxWorkingDimension);
        Assert.Equal(customVibrant, SaliencyCutOptions.ForAnalysis(vibrantAnalysis, customBaseline));

        Assert.Throws<ArgumentNullException>(() => SaliencyCutOptions.ForImage(null!));
        Assert.Throws<ArgumentNullException>(() => SaliencyCutOptions.ForImage(flat, null!));
        Assert.Throws<ArgumentNullException>("analysis", () => SaliencyCutOptions.ForAnalysis(null!));
        Assert.Throws<ArgumentNullException>("baseline", () => SaliencyCutOptions.ForAnalysis(vibrantAnalysis, null!));
        Assert.Throws<ArgumentOutOfRangeException>("referenceDimension", () => SaliencyCutOptions.ForAnalysis(vibrantAnalysis, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCutOptions.ForAnalysis(vibrantAnalysis, CreateInvalidOptions()));
    }
}
