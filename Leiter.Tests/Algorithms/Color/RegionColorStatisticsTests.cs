namespace Leiter.Tests.Algorithms.Color;

using System;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="RegionColorStatistics{TColor}"/> and <see cref="RegionColorStatistics"/>.
/// </summary>
public class RegionColorStatisticsTests
{
    /// <summary>
    /// Verifies that RGB and CIELAB per-region statistics match expected calculations.
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
        RegionColorStatistics<Rgb64>[] stats = RegionColorStatistics.Compute(graph, img);
        RegionColorStatistics<Lab32>[] labStats = RegionColorStatistics.ComputeLab(graph, img);

        Assert.Equal(2, stats.Length);
        Assert.Equal(2, labStats.Length);

        // Region 1 is pure blue
        Assert.Equal(new Rgb64(0.0, 0.0, 1.0), stats[1].MeanColor);
        Assert.Equal(1.0 / 3.0, stats[1].MeanIntensity, 5);
        Assert.Equal(240.0, stats[1].MeanHueDegrees, 5);

        Lab32 expectedBlueLab = new Rgb64(0.0, 0.0, 1.0).ToLab32(RgbColorSpace.SRgb, Illuminant.D65);
        Assert.Equal(expectedBlueLab.L, labStats[1].MeanColor.L, 5);
        Assert.Equal(expectedBlueLab.A, labStats[1].MeanColor.A, 5);
        Assert.Equal(expectedBlueLab.B, labStats[1].MeanColor.B, 5);
        Assert.Equal(expectedBlueLab.GetNormalizedLightness(), labStats[1].MeanIntensity, 5);
        Assert.Equal(expectedBlueLab.GetHueDegrees(), labStats[1].MeanHueDegrees, 5);

        // Region 0 is mixture of red and green
        Assert.Equal(new Rgb64(0.5, 0.5, 0.0), stats[0].MeanColor);
        Assert.Equal(1.0 / 3.0, stats[0].MeanIntensity, 5);
        Assert.Equal(60.0, stats[0].MeanHueDegrees, 5);

        Lab32 redLab = new Rgb64(1.0, 0.0, 0.0).ToLab32(RgbColorSpace.SRgb, Illuminant.D65);
        Lab32 greenLab = new Rgb64(0.0, 1.0, 0.0).ToLab32(RgbColorSpace.SRgb, Illuminant.D65);
        Lab32 expectedMixedLab = new(
            (redLab.L + greenLab.L) * 0.5f,
            (redLab.A + greenLab.A) * 0.5f,
            (redLab.B + greenLab.B) * 0.5f);
        Assert.Equal(expectedMixedLab.L, labStats[0].MeanColor.L, 5);
        Assert.Equal(expectedMixedLab.A, labStats[0].MeanColor.A, 5);
        Assert.Equal(expectedMixedLab.B, labStats[0].MeanColor.B, 5);
        Assert.Equal(expectedMixedLab.GetNormalizedLightness(), labStats[0].MeanIntensity, 5);
        Assert.Equal(expectedMixedLab.GetHueDegrees(), labStats[0].MeanHueDegrees, 5);
    }

    /// <summary>
    /// Verifies direct <see cref="Rgb64"/>, direct <see cref="Lab32"/>, and generic delegate overloads.
    /// </summary>
    [Fact]
    public void Compute_TypedAndCustomOverloads_ShouldMatchExpectedCalculations()
    {
        var labels = new SequentialMatrix<int>(2, 2);
        labels[0, 0] = 0;
        labels[1, 0] = 0;
        labels[0, 1] = 1;
        labels[1, 1] = 1;

        var graph = RegionGraph.FromLabels(labels);

        var rgb64Img = new SequentialMatrix<Rgb64>(2, 2);
        rgb64Img[0, 0] = new Rgb64(0.2, 0.4, 0.6);
        rgb64Img[1, 0] = new Rgb64(0.4, 0.6, 0.8);
        rgb64Img[0, 1] = new Rgb64(0.0, 1.0, 0.0);
        rgb64Img[1, 1] = new Rgb64(0.0, 1.0, 0.0);

        RegionColorStatistics<Rgb64>[] rgb64Stats = RegionColorStatistics.Compute(graph, rgb64Img);
        Assert.Equal(0.3, rgb64Stats[0].MeanColor.R, 6);
        Assert.Equal(0.5, rgb64Stats[0].MeanColor.G, 6);
        Assert.Equal(0.7, rgb64Stats[0].MeanColor.B, 6);
        Assert.Equal(0.5, rgb64Stats[0].MeanIntensity, 6);
        Assert.Equal(120.0, rgb64Stats[1].MeanHueDegrees, 6);

        var labImg = new SequentialMatrix<Lab32>(2, 2);
        labImg[0, 0] = new Lab32(40f, 10f, -10f);
        labImg[1, 0] = new Lab32(60f, 30f, 10f);
        labImg[0, 1] = new Lab32(80f, 0f, 20f);
        labImg[1, 1] = new Lab32(80f, 0f, 20f);

        RegionColorStatistics<Lab32>[] directLabStats = RegionColorStatistics.Compute(graph, labImg);
        Assert.Equal(50f, directLabStats[0].MeanColor.L, 5);
        Assert.Equal(20f, directLabStats[0].MeanColor.A, 5);
        Assert.Equal(0f, directLabStats[0].MeanColor.B, 5);
        Assert.Equal(0.5, directLabStats[0].MeanIntensity, 5);
        Assert.Equal(0.0, directLabStats[0].MeanHueDegrees, 5);
        Assert.Equal(90.0, directLabStats[1].MeanHueDegrees, 5);

        RegionColorStatistics<DoublePixel>[] customStats = RegionColorStatistics.Compute(
            graph,
            rgb64Img,
            static p => new DoublePixel(p.GetLuma()),
            static c => c.Value * 180.0,
            static c => c.Value);

        double expectedLuma0 = ((0.299 * 0.2 + 0.587 * 0.4 + 0.114 * 0.6) + (0.299 * 0.4 + 0.587 * 0.6 + 0.114 * 0.8)) * 0.5;
        Assert.Equal(expectedLuma0, customStats[0].MeanColor.Value, 6);
        Assert.Equal(expectedLuma0 * 180.0, customStats[0].MeanHueDegrees, 6);
        Assert.Equal(expectedLuma0, customStats[0].MeanIntensity, 6);
    }

    /// <summary>
    /// Verifies that changing color space or illuminant changes the computed <see cref="Lab32"/> means.
    /// </summary>
    [Fact]
    public void ComputeLab_ColorSpaceAndIlluminant_ShouldAffectMeanLab()
    {
        var labels = new SequentialMatrix<int>(2, 2);
        var img = new SequentialMatrix<Rgb8>(2, 2);
        img.SetAll(new Rgb8(200, 100, 50));

        var graph = RegionGraph.FromLabels(labels);

        RegionColorStatistics<Lab32>[] statsSrgb = RegionColorStatistics.ComputeLab(graph, img, colorSpace: RgbColorSpace.SRgb);
        RegionColorStatistics<Lab32>[] statsLinear = RegionColorStatistics.ComputeLab(graph, img, colorSpace: RgbColorSpace.LinearRgb);
        Assert.NotEqual(statsSrgb[0].MeanColor.L, statsLinear[0].MeanColor.L);

        RegionColorStatistics<Lab32>[] statsD65 = RegionColorStatistics.ComputeLab(graph, img, illuminant: Illuminant.D65);
        RegionColorStatistics<Lab32>[] statsE = RegionColorStatistics.ComputeLab(graph, img, illuminant: Illuminant.E);
        Assert.NotEqual(statsD65[0].MeanColor.B, statsE[0].MeanColor.B);
    }

    /// <summary>
    /// Verifies that empty 0x0 inputs return an empty statistics array across overloads.
    /// </summary>
    [Fact]
    public void Compute_EmptyGraph_ShouldReturnEmptyArray()
    {
        var emptyLabels = new SequentialMatrix<int>(0, 0);
        var emptyGraph = RegionGraph.FromLabels(emptyLabels);
        var emptyRgb8 = new SequentialMatrix<Rgb8>(0, 0);
        var emptyRgb64 = new SequentialMatrix<Rgb64>(0, 0);
        var emptyLab = new SequentialMatrix<Lab32>(0, 0);

        Assert.Empty(RegionColorStatistics.Compute(emptyGraph, emptyRgb8));
        Assert.Empty(RegionColorStatistics.Compute(emptyGraph, emptyRgb64));
        Assert.Empty(RegionColorStatistics.Compute(emptyGraph, emptyLab));
        Assert.Empty(RegionColorStatistics.ComputeLab(emptyGraph, emptyRgb8));
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
        var rgb64Img = new SequentialMatrix<Rgb64>(2, 2);
        var labImg = new SequentialMatrix<Lab32>(2, 2);

        // Null checks
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.Compute(null!, img));
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.Compute(null!, rgb64Img));
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.Compute(null!, labImg));
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.Compute(graph, (IReadOnlyMatrix<Rgb8>)null!));
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.Compute(graph, (IReadOnlyMatrix<Rgb64>)null!));
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.Compute(graph, (IReadOnlyMatrix<Lab32>)null!));
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.ComputeLab(null!, img));
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.ComputeLab(graph, null!));

        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.Compute<Rgb8, Rgb64>(graph, img, null!, static c => c.GetHueDegrees(), static c => c.GetIntensity()));
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.Compute<Rgb8, Rgb64>(graph, img, static p => p.ToRgb64(), null!, static c => c.GetIntensity()));
        Assert.Throws<ArgumentNullException>(() => RegionColorStatistics.Compute<Rgb8, Rgb64>(graph, img, static p => p.ToRgb64(), static c => c.GetHueDegrees(), null!));

        // Dimension mismatches
        var mismatchedImg = new SequentialMatrix<Rgb8>(3, 3);
        Assert.Throws<ArgumentException>(() => RegionColorStatistics.Compute(graph, mismatchedImg));
        Assert.Throws<ArgumentException>(() => RegionColorStatistics.Compute(graph, new SequentialMatrix<Rgb64>(3, 3)));
        Assert.Throws<ArgumentException>(() => RegionColorStatistics.Compute(graph, new SequentialMatrix<Lab32>(3, 3)));
        Assert.Throws<ArgumentException>(() => RegionColorStatistics.ComputeLab(graph, mismatchedImg));

        // Invalid enums
        Assert.Throws<ArgumentOutOfRangeException>(() => RegionColorStatistics.ComputeLab(graph, img, colorSpace: (RgbColorSpace)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => RegionColorStatistics.ComputeLab(graph, img, illuminant: (Illuminant)99));
    }

    /// <summary>
    /// Verifies that generic <see cref="RegionColorStatistics.Compute{TSource, TColor}"/> accumulates in double precision
    /// without <see cref="Rgb8"/> byte saturation or 32-bit float precision loss on large multi-pixel regions.
    /// </summary>
    [Fact]
    public void Compute_GenericLargeMultiPixelRegions_ShouldNotSaturateRgb8OrLoseLab32Precision()
    {
        // 20-pixel single region where each channel sum exceeds 255 (4000, 4400, 4600)
        const int width = 5;
        const int height = 4;
        var labels = new SequentialMatrix<int>(width, height);
        var graph = RegionGraph.FromLabels(labels);

        var rgb8Img = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                rgb8Img[x, y] = ((x + y) % 2 == 0)
                    ? new Rgb8(180, 200, 220)
                    : new Rgb8(220, 240, 240);
            }
        }

        RegionColorStatistics<Rgb8>[] rgb8Stats = RegionColorStatistics.Compute<Rgb8, Rgb8>(
            graph,
            rgb8Img,
            static p => p,
            static c => c.R,
            static c => c.G / 255.0);

        Assert.Single(rgb8Stats);
        Assert.Equal(new Rgb8(200, 220, 230), rgb8Stats[0].MeanColor);
        Assert.Equal(200.0, rgb8Stats[0].MeanHueDegrees, 6);
        Assert.Equal(220.0 / 255.0, rgb8Stats[0].MeanIntensity, 6);

        // 20-pixel Lab32 region with standard values (sumL = 1010)
        var labImg = new SequentialMatrix<Lab32>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labImg[x, y] = ((x + y) % 2 == 0)
                    ? new Lab32(40.25f, 15.5f, -20.5f)
                    : new Lab32(60.75f, 25.5f, 40.5f);
            }
        }

        RegionColorStatistics<Lab32>[] labStats = RegionColorStatistics.Compute<Lab32, Lab32>(
            graph,
            labImg,
            static p => p,
            static c => c.GetHueDegrees(),
            static c => c.GetNormalizedLightness());

        Assert.Single(labStats);
        Assert.Equal(50.5f, labStats[0].MeanColor.L, 5);
        Assert.Equal(20.5f, labStats[0].MeanColor.A, 5);
        Assert.Equal(10.0f, labStats[0].MeanColor.B, 5);

        // 16-pixel precision regression: in float32, 16_777_216f + 1f drops the +1f terms completely;
        // double-precision Vector256<double> accumulation preserves all 15 smaller terms.
        var precisionLabels = new SequentialMatrix<int>(16, 1);
        var precisionGraph = RegionGraph.FromLabels(precisionLabels);
        var precisionLab = new SequentialMatrix<Lab32>(16, 1);
        precisionLab[0, 0] = new Lab32(16_777_216f, 0f, 0f);
        for (int x = 1; x < 16; x++)
            precisionLab[x, 0] = new Lab32(1.0f, 0f, 0f);

        RegionColorStatistics<Lab32>[] precisionStats = RegionColorStatistics.Compute<Lab32, Lab32>(
            precisionGraph,
            precisionLab,
            static p => p,
            static c => c.GetHueDegrees(),
            static c => c.GetNormalizedLightness());

        float expectedMeanL = (float)((16_777_216.0 + 15.0) / 16.0);
        Assert.Equal(expectedMeanL, precisionStats[0].MeanColor.L);
        Assert.NotEqual(1_048_576.0f, precisionStats[0].MeanColor.L);
    }
}
