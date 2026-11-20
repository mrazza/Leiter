namespace Leiter.Tests.Algorithms.Segmentation;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.Algorithms;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MeanShiftOptions"/>.
/// </summary>
public class MeanShiftOptionsTests : SizeTunableOptionsContractTestsBase<MeanShiftOptions>
{
    /// <inheritdoc/>
    protected override MeanShiftOptions CreateValidCustomOptions() =>
        new(
            SpatialRadius: 12,
            RangeRadius: 8.0,
            MinRegionSize: 80,
            MaxIterations: 15,
            ConvergenceEpsilon: 0.08,
            Connectivity: GridConnectivity.FourConnected);

    /// <inheritdoc/>
    protected override MeanShiftOptions CreateInvalidOptions() =>
        new(SpatialRadius: 0);
    [Fact]
    public void Defaults_ShouldHaveExpectedValues()
    {
        var options = new MeanShiftOptions();

        Assert.Equal(8, options.SpatialRadius);
        Assert.Equal(5.5, options.RangeRadius);
        Assert.Equal(50, options.MinRegionSize);
        Assert.Equal(20, options.MaxIterations);
        Assert.Equal(0.05, options.ConvergenceEpsilon);
        Assert.Equal(GridConnectivity.EightConnected, options.Connectivity);

        options.Validate();
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveInts), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidSpatialRadius_ShouldThrowArgumentOutOfRangeException(int spatialRadius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(SpatialRadius: spatialRadius).Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidRangeRadius_ShouldThrowArgumentOutOfRangeException(double rangeRadius)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(RangeRadius: rangeRadius).Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeInts), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMinRegionSize_ShouldThrowArgumentOutOfRangeException(int minRegionSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(MinRegionSize: minRegionSize).Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveInts), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMaxIterations_ShouldThrowArgumentOutOfRangeException(int maxIterations)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(MaxIterations: maxIterations).Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidConvergenceEpsilon_ShouldThrowArgumentOutOfRangeException(double epsilon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(ConvergenceEpsilon: epsilon).Validate());
    }

    [Fact]
    public void Validate_InvalidConnectivity_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(Connectivity: (GridConnectivity)99).Validate());
    }

    [Fact]
    public void Validate_ValidCustomOptions_ShouldSucceed()
    {
        var options = new MeanShiftOptions(
            SpatialRadius: 16,
            RangeRadius: 10.0,
            MinRegionSize: 100,
            MaxIterations: 20,
            ConvergenceEpsilon: 0.01,
            Connectivity: GridConnectivity.FourConnected);

        options.Validate();

        Assert.Equal(16, options.SpatialRadius);
        Assert.Equal(10.0, options.RangeRadius);
        Assert.Equal(100, options.MinRegionSize);
        Assert.Equal(20, options.MaxIterations);
        Assert.Equal(0.01, options.ConvergenceEpsilon);
        Assert.Equal(GridConnectivity.FourConnected, options.Connectivity);
    }

    [Fact]
    public void ForSize_And_ForImage_ShouldScaleAndAdaptAcrossRgbAndLab()
    {
        MeanShiftOptions refOpts = MeanShiftOptions.ForSize(new Size(800, 800));
        Assert.Equal(8, refOpts.SpatialRadius);
        Assert.Equal(5.5, refOpts.RangeRadius, 6);
        Assert.Equal(50, refOpts.MinRegionSize);
        Assert.Equal(0.05, refOpts.ConvergenceEpsilon, 6);
        Assert.Equal(refOpts, MeanShiftOptions.ForSize(new Size(1000, 640)));

        MeanShiftOptions small = MeanShiftOptions.ForSize(new Size(200, 200));
        MeanShiftOptions large = MeanShiftOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(2, small.SpatialRadius);
        Assert.Equal(0.0125, small.ConvergenceEpsilon, 6);
        Assert.True(small.ConvergenceEpsilon < refOpts.ConvergenceEpsilon);
        Assert.True(large.SpatialRadius > refOpts.SpatialRadius);
        Assert.True(small.MinRegionSize < refOpts.MinRegionSize);
        Assert.True(large.MinRegionSize > refOpts.MinRegionSize);
        Assert.True(large.ConvergenceEpsilon > refOpts.ConvergenceEpsilon && large.ConvergenceEpsilon <= 0.5);

        // MinRegionSize = 0 disables pruning and stays 0 across ForSize/ForImage/ForAnalysis.
        var disabledPruning = new MeanShiftOptions(MinRegionSize: 0);
        disabledPruning.Validate();
        Assert.Equal(0, MeanShiftOptions.ForSize(new Size(1600, 1600), disabledPruning).MinRegionSize);

        // Scale-1.0 identity for ConvergenceEpsilon below floor (0.01) and above ceiling (0.5).
        var subFloorEpsilon = new MeanShiftOptions(ConvergenceEpsilon: 0.002);
        Assert.Equal(subFloorEpsilon, MeanShiftOptions.ForSize(new Size(800, 800), subFloorEpsilon));
        var aboveCeilingEpsilon = new MeanShiftOptions(ConvergenceEpsilon: 0.8);
        Assert.Equal(aboveCeilingEpsilon, MeanShiftOptions.ForSize(new Size(800, 800), aboveCeilingEpsilon));
        Assert.Equal(0.2, MeanShiftOptions.ForSize(new Size(200, 200), aboveCeilingEpsilon).ConvergenceEpsilon, 6);

        // Extreme scaling must stay finite and valid.
        var hugeBaseline = new MeanShiftOptions(RangeRadius: double.MaxValue / 2.0, ConvergenceEpsilon: double.MaxValue / 2.0);
        MeanShiftOptions hugeScaled = MeanShiftOptions.ForSize(new Size(3200, 3200), hugeBaseline, referenceDimension: 1e-200);
        hugeScaled.Validate();
        Assert.True(double.IsFinite(hugeScaled.RangeRadius));
        Assert.True(double.IsFinite(hugeScaled.ConvergenceEpsilon));

        SequentialMatrix<Rgb8> flat = TestMatrices.Constant(256, 256, new Rgb8(128, 128, 128));
        SequentialMatrix<Rgb8> cluttered = TestMatrices.Checkerboard(256, 256, new Rgb8(245, 220, 30), new Rgb8(20, 30, 235), cellSize: 4);

        MeanShiftOptions flatOpts = MeanShiftOptions.ForImage(flat);
        MeanShiftOptions clutteredOpts = MeanShiftOptions.ForImage(cluttered);
        Assert.True(clutteredOpts.RangeRadius > flatOpts.RangeRadius);
        Assert.True(clutteredOpts.MinRegionSize > flatOpts.MinRegionSize);
        Assert.Equal(0, MeanShiftOptions.ForImage(cluttered, disabledPruning).MinRegionSize);

        ImageAnalysis clutteredAnalysis = ImageAnalysis.Analyze(cluttered);
        Assert.Equal(clutteredOpts, MeanShiftOptions.ForAnalysis(clutteredAnalysis));

        // Single-rounding check for MinRegionSize: 256x256 flat image has areaScale=0.1024, clutterFactor=0.75.
        // 45 * 0.1024 * 0.75 = 3.456 -> rounds to 3 (whereas double-rounding Round(4.608)=5 -> Round(3.75)=4).
        MeanShiftOptions singleRounded = MeanShiftOptions.ForImage(flat, new MeanShiftOptions(MinRegionSize: 45));
        Assert.Equal(3, singleRounded.MinRegionSize);

        SequentialMatrix<Lab32> labImage = TestMatrices.Constant(64, 64, new Lab32(50f, 0f, 0f));
        MeanShiftOptions.ForImage(labImage).Validate();

        MeanShiftOptions customBaseline = CreateValidCustomOptions();
        MeanShiftOptions customScaled = MeanShiftOptions.ForSize(new Size(1600, 1600), customBaseline);
        Assert.Equal(24, customScaled.SpatialRadius);
        Assert.Equal(8.0, customScaled.RangeRadius, 6);
        Assert.Equal(15, customScaled.MaxIterations);
        Assert.Equal(GridConnectivity.FourConnected, customScaled.Connectivity);
        Assert.Equal(0.16, customScaled.ConvergenceEpsilon, 6);

        MeanShiftOptions customLab = MeanShiftOptions.ForImage(labImage, customBaseline);
        Assert.Equal(15, customLab.MaxIterations);
        Assert.Equal(GridConnectivity.FourConnected, customLab.Connectivity);
        Assert.True(MeanShiftOptions.ForImage(cluttered, customBaseline).RangeRadius > MeanShiftOptions.ForImage(flat, customBaseline).RangeRadius);
        Assert.Equal(customLab, MeanShiftOptions.ForAnalysis(ImageAnalysis.Analyze(labImage), customBaseline));

        Assert.Throws<ArgumentNullException>(() => MeanShiftOptions.ForImage((IReadOnlyMatrix<Rgb8>)null!));
        Assert.Throws<ArgumentNullException>(() => MeanShiftOptions.ForImage((IReadOnlyMatrix<Lab32>)null!));
        Assert.Throws<ArgumentNullException>(() => MeanShiftOptions.ForImage(flat, null!));
        Assert.Throws<ArgumentNullException>(() => MeanShiftOptions.ForImage(labImage, null!));
        Assert.Throws<ArgumentNullException>("analysis", () => MeanShiftOptions.ForAnalysis(null!));
        Assert.Throws<ArgumentNullException>("baseline", () => MeanShiftOptions.ForAnalysis(clutteredAnalysis, null!));
        Assert.Throws<ArgumentOutOfRangeException>("referenceDimension", () => MeanShiftOptions.ForAnalysis(clutteredAnalysis, -1.0));
    }
}
