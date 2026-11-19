namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyCutOptions"/>.
/// </summary>
public class SaliencyCutOptionsTests
{
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

        options.Validate();
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
    public void ForSize_And_ForImage_ShouldScaleBandAndAdaptComponents()
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

        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(3, 3), new Size(4000, 80), new Size(12000, 9000) })
        {
            SaliencyCutOptions.ForSize(size).Validate();
        }

        SequentialMatrix<Rgb8> flat = TestMatrices.Constant(64, 64, new Rgb8(110, 110, 110));
        SequentialMatrix<Rgb8> vibrant = TestMatrices.Checkerboard(64, 64, new Rgb8(255, 250, 20), new Rgb8(10, 20, 240));

        Assert.Equal(4, SaliencyCutOptions.ForImage(flat).GrabCut!.MixtureComponentCount);
        Assert.Equal(6, SaliencyCutOptions.ForImage(vibrant).GrabCut!.MixtureComponentCount);

        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCutOptions.ForSize(new Size(-1, 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCutOptions.ForSize(new Size(100, 100), double.NegativeInfinity));
        Assert.Throws<ArgumentNullException>(() => SaliencyCutOptions.ForImage(null!));
    }
}
