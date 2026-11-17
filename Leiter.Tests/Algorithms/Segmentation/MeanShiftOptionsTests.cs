namespace Leiter.Tests.Algorithms.Segmentation;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
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
        Assert.Equal(15, options.MaxIterations);
        Assert.Equal(0.05, options.ConvergenceEpsilon);
        Assert.Equal(GridConnectivity.EightConnected, options.Connectivity);

        options.Validate();
    }

    [Fact]
    public void Validate_InvalidSpatialRadius_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(SpatialRadius: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(SpatialRadius: -1).Validate());
    }

    [Fact]
    public void Validate_InvalidRangeRadius_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(RangeRadius: 0.0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(RangeRadius: -1.0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(RangeRadius: double.NaN).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(RangeRadius: double.PositiveInfinity).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(RangeRadius: double.NegativeInfinity).Validate());
    }

    [Fact]
    public void Validate_InvalidMinRegionSize_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(MinRegionSize: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(MinRegionSize: -1).Validate());
    }

    [Fact]
    public void Validate_InvalidMaxIterations_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(MaxIterations: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(MaxIterations: -1).Validate());
    }

    [Fact]
    public void Validate_InvalidConvergenceEpsilon_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(ConvergenceEpsilon: 0.0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(ConvergenceEpsilon: -0.1).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(ConvergenceEpsilon: double.NaN).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(ConvergenceEpsilon: double.PositiveInfinity).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new MeanShiftOptions(ConvergenceEpsilon: double.NegativeInfinity).Validate());
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

        var flat = new SequentialMatrix<Rgb8>(256, 256);
        flat.SetAll(new Rgb8(128, 128, 128));
        var cluttered = new SequentialMatrix<Rgb8>(256, 256);
        for (int y = 0; y < 256; y++)
        {
            for (int x = 0; x < 256; x++)
            {
                cluttered[x, y] = (((x / 4) + (y / 4)) & 1) == 0 ? new Rgb8(245, 220, 30) : new Rgb8(20, 30, 235);
            }
        }

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
