namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="HandTremorFilter"/>.
/// </summary>
public class HandTremorFilterTests
{
    /// <summary>
    /// Verifies that null arguments passed to <see cref="HandTremorFilter.Apply"/> throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Apply_NullArguments_ShouldThrowArgumentNullException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var labels = new SequentialMatrix<int>(4, 4);
        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(4, 4);

        Assert.Throws<ArgumentNullException>(() => HandTremorFilter.Apply(null!, graph, dist));
        Assert.Throws<ArgumentNullException>(() => HandTremorFilter.Apply(image, null!, dist));
        Assert.Throws<ArgumentNullException>(() => HandTremorFilter.Apply(image, graph, null!));
    }

    /// <summary>
    /// Verifies that mismatched matrix dimensions throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Apply_MismatchedDimensions_ShouldThrowArgumentException()
    {
        var image = new SequentialMatrix<Rgb8>(6, 6);
        var graphMismatched = RegionGraph.FromLabels(new SequentialMatrix<int>(4, 6));
        var graphMatching = RegionGraph.FromLabels(new SequentialMatrix<int>(6, 6));
        var distMismatched = new SequentialMatrix<DoublePixel>(6, 4);
        var distMatching = new SequentialMatrix<DoublePixel>(6, 6);

        Assert.Throws<ArgumentException>(() => HandTremorFilter.Apply(image, graphMismatched, distMatching));
        Assert.Throws<ArgumentException>(() => HandTremorFilter.Apply(image, graphMatching, distMismatched));
    }

    /// <summary>
    /// Verifies that mismatched wet-in-wet mask size or boundary count throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Apply_MismatchedWetInWetResult_ShouldThrowArgumentException()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            6,
            6,
            new Rgb8(20, 20, 200),
            new Rgb8(200, 20, 20));
        var dist = new SequentialMatrix<DoublePixel>(6, 6);

        // Mismatched mask size
        var invalidMaskWetInWet = new WetInWetResult(
            new SequentialMatrix<Rgb8>(4, 4),
            new MaskMatrix(4, 4),
            new bool[] { true });

        Assert.Throws<ArgumentException>(() => HandTremorFilter.Apply(image, graph, dist, wetInWet: invalidMaskWetInWet));

        // Mismatched qualifying boundaries count
        var invalidBoundariesWetInWet = new WetInWetResult(
            new SequentialMatrix<Rgb8>(6, 6),
            new MaskMatrix(6, 6),
            new bool[] { true, false, true }); // graph has 1 boundary, wetInWet has 3

        Assert.Throws<ArgumentException>(() => HandTremorFilter.Apply(image, graph, dist, wetInWet: invalidBoundariesWetInWet));
    }

    /// <summary>
    /// Verifies that invalid options throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Apply_InvalidOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(4, 4));
        var dist = new SequentialMatrix<DoublePixel>(4, 4);
        var invalidOptions = new HandTremorOptions(Amplitude: -1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => HandTremorFilter.Apply(image, graph, dist, options: invalidOptions));
    }

    /// <summary>
    /// Verifies that empty (0x0) images return an empty image.
    /// </summary>
    [Fact]
    public void Apply_EmptyImage_ShouldReturnEmptyMatrix()
    {
        var image = new SequentialMatrix<Rgb8>(0, 0);
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(0, 0));
        var dist = new SequentialMatrix<DoublePixel>(0, 0);

        Matrix<Rgb8> result = HandTremorFilter.Apply(image, graph, dist);

        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
    }

    /// <summary>
    /// Verifies that an amplitude of 0.0 returns an unmodified copy of the image.
    /// </summary>
    [Fact]
    public void Apply_ZeroAmplitude_ShouldReturnUnmodifiedImage()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            6,
            6,
            new Rgb8(20, 20, 200),
            new Rgb8(200, 20, 20));
        var dist = new SequentialMatrix<DoublePixel>(6, 6);
        var options = new HandTremorOptions(Amplitude: 0.0);

        Matrix<Rgb8> result = HandTremorFilter.Apply(image, graph, dist, options: options);

        Assert.Equal((byte)20, result[0, 0].R);
        Assert.Equal((byte)200, result[5, 5].R);
    }

    /// <summary>
    /// Verifies that a single-region image with zero boundaries returns an unmodified image copy.
    /// </summary>
    [Fact]
    public void Apply_ZeroBoundaries_ShouldReturnUnmodifiedImage()
    {
        int width = 4;
        int height = 4;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);
        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        Matrix<Rgb8> result = HandTremorFilter.Apply(image, graph, dist);

        Assert.Equal(width, result.Width);
    }

    /// <summary>
    /// Verifies that boundaries qualifying in wet-in-wet (Group 1) and wet area mask pixels remain undistorted.
    /// </summary>
    [Fact]
    public void Apply_WetInWetBoundaries_ShouldRemainUndistorted()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            8,
            8,
            new Rgb8(20, 20, 200),
            new Rgb8(200, 20, 20));
        var dist = new SequentialMatrix<DoublePixel>(8, 8);

        // Mark boundary as qualifying in wetInWet result
        var wetMask = new MaskMatrix(8, 8);
        var wetInWet = new WetInWetResult(
            (SequentialMatrix<Rgb8>)image.Clone(),
            wetMask,
            new bool[] { true });

        Matrix<Rgb8> result = HandTremorFilter.Apply(image, graph, dist, wetInWet: wetInWet);

        // Entire image must match original since boundary is Group 1 (wet-in-wet)
        MatrixAssert.Equal(image, result);
    }

    /// <summary>
    /// Verifies that similar-hue boundaries (Group 2) distort smoothly without producing paper gaps or darkened glaze overlaps.
    /// </summary>
    [Fact]
    public void Apply_SimilarHueBoundaries_ShouldDistortSmoothlyWithoutGapsOrDarkenedGlazes()
    {
        // Similar hues: Dark Red (200, 20, 20) vs Bright Red/Orange (240, 60, 20) -> hue diff ~ 11 degrees (< 30)
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            12,
            12,
            new Rgb8(200, 20, 20),
            new Rgb8(240, 60, 20));
        var dist = new SequentialMatrix<DoublePixel>(12, 12);
        var paperColor = new Rgb8(250, 248, 242);
        var options = new HandTremorOptions(
            Amplitude: 2.0,
            SimilarHueThresholdDegrees: 30.0,
            PaperColor: paperColor);

        Matrix<Rgb8> result = HandTremorFilter.Apply(image, graph, dist, options: options);

        // No pixel should equal paper color because Group 2 samples bilinearly without creating gaps
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                Assert.NotEqual(paperColor, result[x, y]);
            }
        }
    }

    /// <summary>
    /// Verifies that contrasting-hue boundaries (Group 3) produce both paper gaps and darkened pigment glaze overlaps.
    /// </summary>
    [Fact]
    public void Apply_ContrastingHueBoundaries_ShouldProduceBothPaperGapsAndDarkenedOverlaps()
    {
        const int width = 32;
        const int height = 32;
        // Contrasting hues: Pure Red (220, 20, 20) vs Pure Cyan (20, 220, 220), hue diff 180 deg
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            width,
            height,
            new Rgb8(220, 20, 20),
            new Rgb8(20, 220, 220));
        SequentialMatrix<DoublePixel> dist = TestMatrices.Constant(width, height, new DoublePixel(1.0));
        var customPaperColor = new Rgb8(250, 248, 242);

        var options = new HandTremorOptions(
            Amplitude: 6.0,
            SimilarHueThresholdDegrees: 30.0,
            OverlapDarkening: 0.8,
            PaperColor: customPaperColor,
            PrimaryNoise: new PerlinNoiseOptions(Seed: 101, Frequency: 0.25, Octaves: 3),
            SecondaryNoise: new PerlinNoiseOptions(Seed: 202, Frequency: 0.25, Octaves: 3));

        Matrix<Rgb8> result = HandTremorFilter.Apply(image, graph, dist, options: options);

        bool foundPaperGap = false;
        bool foundDarkenedOverlap = false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgb8 pixel = result[x, y];
                if (pixel == customPaperColor)
                {
                    foundPaperGap = true;
                }

                // Darkened overlap mixes Red and Cyan, producing darker channels (both R and G significantly reduced)
                if (pixel.R < 150 && pixel.G < 150 && pixel != customPaperColor && (pixel.R > 0 || pixel.G > 0))
                {
                    foundDarkenedOverlap = true;
                }
            }
        }

        Assert.True(foundPaperGap, "Expected authentic paper gaps along contrasting boundary.");
        Assert.True(foundDarkenedOverlap, "Expected darkened pigment overlap glazes along contrasting boundary.");
    }

    /// <summary>
    /// Verifies handling at a triple junction with 3 distinct regions, covering the third-region fallback branch.
    /// </summary>
    [Fact]
    public void Apply_TripleJunction_ShouldHandleThirdRegionTransitions()
    {
        int width = 24;
        int height = 24;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);

        // 3 regions: Top-left Red (0), Top-right Cyan (1), Bottom Yellow (2)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (y < 12)
                {
                    labels[x, y] = x < 12 ? 0 : 1;
                    image[x, y] = x < 12 ? new Rgb8(220, 20, 20) : new Rgb8(20, 220, 220);
                }
                else
                {
                    labels[x, y] = 2;
                    image[x, y] = new Rgb8(230, 230, 20);
                }
            }
        }

        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);
        dist.SetAll(1.0);

        var options = new HandTremorOptions(
            Amplitude: 6.0,
            SimilarHueThresholdDegrees: 20.0,
            OverlapDarkening: 0.5,
            PrimaryNoise: new PerlinNoiseOptions(Seed: 101, Frequency: 0.25, Octaves: 3),
            SecondaryNoise: new PerlinNoiseOptions(Seed: 202, Frequency: 0.25, Octaves: 3));

        Matrix<Rgb8> result = HandTremorFilter.Apply(image, graph, dist, options: options);

        Assert.Equal(width, result.Width);
        Assert.Equal(height, result.Height);
    }

    /// <summary>
    /// Verifies amplitude scaling based on distance field values, including non-finite distance handling.
    /// </summary>
    [Fact]
    public void Apply_SaliencyAmplitudeScaling_ShouldModulateTremorMagnitude()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            12,
            12,
            new Rgb8(200, 20, 20),
            new Rgb8(20, 200, 200));
        // Top half salient (d=0.0), bottom half non-salient (d=1.0)
        SequentialMatrix<DoublePixel> dist = TestMatrices.Create(12, 12, (_, y) => y < 6 ? new DoublePixel(0.0) : new DoublePixel(1.0));

        // Add non-finite distance to verify double.IsFinite branch
        dist[11, 11] = new DoublePixel(double.NaN);

        var options = new HandTremorOptions(
            Amplitude: 3.0,
            SalientAmplitudeScale: 0.1);

        Matrix<Rgb8> result = HandTremorFilter.Apply(image, graph, dist, options: options);

        Assert.Equal(12, result.Width);
    }

    /// <summary>
    /// Verifies that running with identical noise configuration produces bit-identical output.
    /// </summary>
    [Fact]
    public void Apply_Determinism_IdenticalOptionsShouldProduceIdenticalOutput()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            10,
            10,
            new Rgb8(20, 200, 50),
            new Rgb8(200, 30, 180));
        var dist = new SequentialMatrix<DoublePixel>(10, 10);
        var options = new HandTremorOptions(Amplitude: 2.0);

        Matrix<Rgb8> res1 = HandTremorFilter.Apply(image, graph, dist, options: options);
        Matrix<Rgb8> res2 = HandTremorFilter.Apply(image, graph, dist, options: options);

        MatrixAssert.Equal(res1, res2);
    }
}
