namespace Leiter.Tests.Algorithms.Segmentation;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MeanShiftOptions"/>.
/// </summary>
public class MeanShiftOptionsTests
{
    [Fact]
    public void Defaults_ShouldHaveExpectedValues()
    {
        var options = new MeanShiftOptions();

        Assert.Equal(8, options.SpatialRadius);
        Assert.Equal(5.5, options.RangeRadius);
        Assert.Equal(50, options.MinRegionSize);
        Assert.Equal(50, options.MaxIterations);
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
    [MemberData(nameof(OptionValidationData.InvalidPositiveInts), MemberType = typeof(OptionValidationData))]
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
        Assert.Equal(refOpts, MeanShiftOptions.ForSize(new Size(1000, 640)));

        MeanShiftOptions small = MeanShiftOptions.ForSize(new Size(200, 200));
        MeanShiftOptions large = MeanShiftOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(2, small.SpatialRadius);
        Assert.True(large.SpatialRadius > refOpts.SpatialRadius);
        Assert.True(small.MinRegionSize < refOpts.MinRegionSize);
        Assert.True(large.MinRegionSize > refOpts.MinRegionSize);

        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(3, 3), new Size(4000, 80), new Size(12000, 9000) })
        {
            MeanShiftOptions.ForSize(size).Validate();
        }

        SequentialMatrix<Rgb8> flat = TestMatrices.Constant(256, 256, new Rgb8(128, 128, 128));
        SequentialMatrix<Rgb8> cluttered = TestMatrices.Checkerboard(256, 256, new Rgb8(245, 220, 30), new Rgb8(20, 30, 235), cellSize: 4);

        MeanShiftOptions flatOpts = MeanShiftOptions.ForImage(flat);
        MeanShiftOptions clutteredOpts = MeanShiftOptions.ForImage(cluttered);
        Assert.True(clutteredOpts.RangeRadius > flatOpts.RangeRadius);
        Assert.True(clutteredOpts.MinRegionSize > flatOpts.MinRegionSize);

        var labImage = new SequentialMatrix<Lab32>(64, 64);
        labImage.SetAll(new Lab32(50f, 0f, 0f));
        MeanShiftOptions.ForImage(labImage).Validate();

        Assert.Throws<ArgumentOutOfRangeException>(() => MeanShiftOptions.ForSize(new Size(10, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => MeanShiftOptions.ForSize(new Size(10, 10), -1.0));
        Assert.Throws<ArgumentNullException>(() => MeanShiftOptions.ForImage((IReadOnlyMatrix<Rgb8>)null!));
        Assert.Throws<ArgumentNullException>(() => MeanShiftOptions.ForImage((IReadOnlyMatrix<Lab32>)null!));
    }
}
