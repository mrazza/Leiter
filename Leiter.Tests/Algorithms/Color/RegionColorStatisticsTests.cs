namespace Leiter.Tests.Algorithms.Color;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="RegionColorStatistics"/>.
/// </summary>
public class RegionColorStatisticsTests
{
    /// <summary>
    /// Verifies that MeanRgb, MeanLab, MeanHueDegrees, and MeanIntensity match expected per-region color statistics.
    /// </summary>
    [Fact]
    public void Compute_ColorMetrics_ShouldMatchExpectedCalculations()
    {
        var labels = new SequentialMatrix<int>(2, 2);
        // Region 0: top row
        labels[0, 0] = 0;
        labels[1, 0] = 0;
        // Region 1: bottom row
        labels[0, 1] = 1;
        labels[1, 1] = 1;

        var img = new SequentialMatrix<Rgb8>(2, 2);
        img[0, 0] = new Rgb8(255, 0, 0); // Red
        img[1, 0] = new Rgb8(0, 255, 0); // Green
        img[0, 1] = new Rgb8(0, 0, 255); // Blue
        img[1, 1] = new Rgb8(0, 0, 255); // Blue

        var graph = RegionGraph.FromLabels(labels);
        RegionColorStatistics[] stats = RegionColorStatistics.Compute(graph, img);

        Assert.Equal(2, stats.Length);

        // Region 1 is pure blue
        Assert.Equal(new Rgb64(0.0, 0.0, 1.0), stats[1].MeanRgb);
        Assert.Equal(1.0 / 3.0, stats[1].MeanIntensity, 5);
        Assert.Equal(240.0, stats[1].MeanHueDegrees, 5);

        Lab32 expectedBlueLab = new Rgb64(0.0, 0.0, 1.0).ToLab32(RgbColorSpace.sRGB, Illuminant.D65);
        Assert.Equal(expectedBlueLab.L, stats[1].MeanLab.L, 5);
        Assert.Equal(expectedBlueLab.A, stats[1].MeanLab.A, 5);
        Assert.Equal(expectedBlueLab.B, stats[1].MeanLab.B, 5);

        // Region 0 is mixture of red and green
        Assert.Equal(new Rgb64(0.5, 0.5, 0.0), stats[0].MeanRgb);
        Assert.Equal(1.0 / 3.0, stats[0].MeanIntensity, 5);
        Assert.Equal(60.0, stats[0].MeanHueDegrees, 5);

        Lab32 redLab = new Rgb64(1.0, 0.0, 0.0).ToLab32(RgbColorSpace.sRGB, Illuminant.D65);
        Lab32 greenLab = new Rgb64(0.0, 1.0, 0.0).ToLab32(RgbColorSpace.sRGB, Illuminant.D65);
        Assert.Equal((redLab.L + greenLab.L) * 0.5f, stats[0].MeanLab.L, 5);
        Assert.Equal((redLab.A + greenLab.A) * 0.5f, stats[0].MeanLab.A, 5);
        Assert.Equal((redLab.B + greenLab.B) * 0.5f, stats[0].MeanLab.B, 5);
    }

    /// <summary>
    /// Verifies that SalientFraction computes correct overlap for null, partial, and full salient masks.
    /// </summary>
    [Fact]
    public void Compute_SalientFraction_ShouldComputeCorrectOverlap()
    {
        var labels = new SequentialMatrix<int>(2, 2);
        labels[0, 0] = 0;
        labels[1, 0] = 0;
        labels[0, 1] = 1;
        labels[1, 1] = 1;

        var img = new SequentialMatrix<Rgb8>(2, 2);
        img.SetAll(new Rgb8(128, 128, 128));

        var graph = RegionGraph.FromLabels(labels);

        // When salientMask is null, fraction must be 0
        RegionColorStatistics[] statsWithoutMask = RegionColorStatistics.Compute(graph, img, salientMask: null);
        Assert.Equal(0.0, statsWithoutMask[0].SalientFraction);
        Assert.Equal(0.0, statsWithoutMask[1].SalientFraction);

        // Partial mask: region 0 has 1 of 2 salient; region 1 has 2 of 2 salient
        var mask = new MaskMatrix(2, 2);
        mask[0, 0] = true;
        mask[1, 0] = false;
        mask[0, 1] = true;
        mask[1, 1] = true;

        RegionColorStatistics[] statsWithMask = RegionColorStatistics.Compute(graph, img, mask);
        Assert.Equal(0.5, statsWithMask[0].SalientFraction, 5);
        Assert.Equal(1.0, statsWithMask[1].SalientFraction, 5);
    }

    /// <summary>
    /// Verifies that changing color space or illuminant changes the computed MeanLab.
    /// </summary>
    [Fact]
    public void Compute_ColorSpaceAndIlluminant_ShouldAffectMeanLab()
    {
        var labels = new SequentialMatrix<int>(2, 2);
        var img = new SequentialMatrix<Rgb8>(2, 2);
        img.SetAll(new Rgb8(200, 100, 50));

        var graph = RegionGraph.FromLabels(labels);

        RegionColorStatistics[] statsSrgb = RegionColorStatistics.Compute(graph, img, colorSpace: RgbColorSpace.sRGB);
        RegionColorStatistics[] statsLinear = RegionColorStatistics.Compute(graph, img, colorSpace: RgbColorSpace.LinearRGB);
        Assert.NotEqual(statsSrgb[0].MeanLab.L, statsLinear[0].MeanLab.L);

        RegionColorStatistics[] statsD65 = RegionColorStatistics.Compute(graph, img, illuminant: Illuminant.D65);
        RegionColorStatistics[] statsE = RegionColorStatistics.Compute(graph, img, illuminant: Illuminant.E);
        Assert.NotEqual(statsD65[0].MeanLab.B, statsE[0].MeanLab.B);
    }

    /// <summary>
    /// Verifies that empty 0x0 inputs return an empty statistics array.
    /// </summary>
    [Fact]
    public void Compute_EmptyGraph_ShouldReturnEmptyArray()
    {
        var emptyLabels = new SequentialMatrix<int>(0, 0);
        var emptyGraph = RegionGraph.FromLabels(emptyLabels);
        var emptyImg = new SequentialMatrix<Rgb8>(0, 0);
        var emptyMask = new MaskMatrix(0, 0);

        RegionColorStatistics[] stats = RegionColorStatistics.Compute(emptyGraph, emptyImg, emptyMask);
        Assert.Empty(stats);
    }

    /// <summary>
    /// Verifies argument validation for null arguments, dimension mismatches, and invalid enums.
    /// </summary>
    [Fact]
    public void Compute_Validation_ShouldThrowOnInvalidArguments()
    {
        var labels = new SequentialMatrix<int>(2, 2);
        var graph = RegionGraph.FromLabels(labels);
        var img = new SequentialMatrix<Rgb8>(2, 2);

        // Null checks
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.Compute(null!, img));
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.Compute(graph, null!));

        // Dimension mismatches
        var mismatchedImg = new SequentialMatrix<Rgb8>(3, 3);
        Assert.Throws<ArgumentException>(() => RegionColorStatistics.Compute(graph, mismatchedImg));

        var mismatchedMask = new MaskMatrix(3, 3);
        Assert.Throws<ArgumentException>(() => RegionColorStatistics.Compute(graph, img, mismatchedMask));

        // Invalid enums
        Assert.Throws<ArgumentOutOfRangeException>(() => RegionColorStatistics.Compute(graph, img, colorSpace: (RgbColorSpace)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => RegionColorStatistics.Compute(graph, img, illuminant: (Illuminant)99));
    }
}
