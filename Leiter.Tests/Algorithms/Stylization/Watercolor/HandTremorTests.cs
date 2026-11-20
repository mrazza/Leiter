namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using System.Threading;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="HandTremor"/>.
/// </summary>
public class HandTremorTests
{
    private static readonly Rgb8 DefaultPaperColor = HandTremorOptions.DefaultPaperColor;
    private static readonly PerlinNoiseOptions HighFreqPrimaryNoise = new(Seed: 101, Frequency: 0.25, Octaves: 3);
    private static readonly PerlinNoiseOptions HighFreqSecondaryNoise = new(Seed: 202, Frequency: 0.25, Octaves: 3);

    /// <summary>
    /// Verifies that null arguments passed to <see cref="HandTremor.Apply(IReadOnlyMatrix{Rgb8}, RegionGraph, IReadOnlyMatrix{DoublePixel}, WetInWetResult?, HandTremorOptions?, CancellationToken)"/>
    /// and <see cref="HandTremor.Apply(IReadOnlyMatrix{Rgb8}, IDisjointSet, IReadOnlyMatrix{DoublePixel}, WetInWetResult?, HandTremorOptions?, CancellationToken)"/> throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Apply_NullArguments_ShouldThrowArgumentNullException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        RegionGraph graph = TestMatrices.SingleRegionGraph(4, 4);
        var disjointSet = CreateVerticalSplitDisjointSet(4, 4);
        var dist = new SequentialMatrix<DoublePixel>(4, 4);

        Assert.Throws<ArgumentNullException>(() => HandTremor.Apply(null!, graph, dist));
        Assert.Throws<ArgumentNullException>(() => HandTremor.Apply(image, (RegionGraph)null!, dist));
        Assert.Throws<ArgumentNullException>(() => HandTremor.Apply(image, graph, null!));

        Assert.Throws<ArgumentNullException>(() => HandTremor.Apply(null!, disjointSet, dist));
        Assert.Throws<ArgumentNullException>(() => HandTremor.Apply(image, (IDisjointSet)null!, dist));
        Assert.Throws<ArgumentNullException>(() => HandTremor.Apply(image, disjointSet, null!));
    }

    /// <summary>
    /// Verifies that mismatched matrix dimensions throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Apply_MismatchedDimensions_ShouldThrowArgumentException()
    {
        var image = new SequentialMatrix<Rgb8>(6, 6);
        RegionGraph graphMismatched = TestMatrices.SingleRegionGraph(4, 6);
        RegionGraph graphMatching = TestMatrices.SingleRegionGraph(6, 6);
        var disjointMismatched = CreateVerticalSplitDisjointSet(4, 6);
        var disjointMatching = CreateVerticalSplitDisjointSet(6, 6);
        var distMismatched = new SequentialMatrix<DoublePixel>(6, 4);
        var distMatching = new SequentialMatrix<DoublePixel>(6, 6);

        Assert.Throws<ArgumentException>(() => HandTremor.Apply(image, graphMismatched, distMatching));
        Assert.Throws<ArgumentException>(() => HandTremor.Apply(image, graphMatching, distMismatched));
        Assert.Throws<ArgumentException>(() => HandTremor.Apply(image, disjointMismatched, distMatching));
        Assert.Throws<ArgumentException>(() => HandTremor.Apply(image, disjointMatching, distMismatched));
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

        Assert.Throws<ArgumentException>(() => HandTremor.Apply(image, graph, dist, wetInWet: invalidMaskWetInWet));

        // Mismatched qualifying boundaries count
        var invalidBoundariesWetInWet = new WetInWetResult(
            new SequentialMatrix<Rgb8>(6, 6),
            new MaskMatrix(6, 6),
            new bool[] { true, false, true }); // graph has 1 boundary, wetInWet has 3

        Assert.Throws<ArgumentException>(() => HandTremor.Apply(image, graph, dist, wetInWet: invalidBoundariesWetInWet));
    }

    /// <summary>
    /// Verifies that invalid options throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Apply_InvalidOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        RegionGraph graph = TestMatrices.SingleRegionGraph(4, 4);
        var dist = new SequentialMatrix<DoublePixel>(4, 4);
        var invalidOptions = new HandTremorOptions(Amplitude: -1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => HandTremor.Apply(image, graph, dist, options: invalidOptions));
    }

    /// <summary>
    /// Verifies that empty (0x0) images return an empty image.
    /// </summary>
    [Fact]
    public void Apply_EmptyImage_ShouldReturnEmptyMatrix()
    {
        var image = new SequentialMatrix<Rgb8>(0, 0);
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(0, 0));
        var disjointSet = CreateVerticalSplitDisjointSet(0, 0);
        var dist = new SequentialMatrix<DoublePixel>(0, 0);

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist);
        Matrix<Rgb8> resultDisjoint = HandTremor.Apply(image, disjointSet, dist);

        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
        Assert.Empty(resultDisjoint);
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

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, options: options);

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
        RegionGraph graph = TestMatrices.SingleRegionGraph(width, height);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist);

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

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, wetInWet: wetInWet);

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
        var options = new HandTremorOptions(
            Amplitude: 2.0,
            SimilarHueThresholdDegrees: 30.0,
            PaperColor: DefaultPaperColor);

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, options: options);

        // No pixel should equal paper color because Group 2 samples bilinearly without creating gaps
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                Assert.NotEqual(DefaultPaperColor, result[x, y]);
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

        var options = new HandTremorOptions(
            Amplitude: 6.0,
            SimilarHueThresholdDegrees: 30.0,
            OverlapDarkening: 0.8,
            PaperColor: DefaultPaperColor,
            PrimaryNoise: HighFreqPrimaryNoise,
            SecondaryNoise: HighFreqSecondaryNoise);

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, options: options);

        bool foundPaperGap = false;
        bool foundDarkenedOverlap = false;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgb8 pixel = result[x, y];
                if (pixel == DefaultPaperColor)
                {
                    foundPaperGap = true;
                }

                // Darkened overlap mixes Red and Cyan, producing darker channels (both R and G significantly reduced)
                if (pixel.R < 150 && pixel.G < 150 && pixel != DefaultPaperColor && (pixel.R > 0 || pixel.G > 0))
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
        const int width = 24;
        const int height = 24;
        // 3 regions: Top-left Red (0), Top-right Cyan (1), Bottom Yellow (2)
        SequentialMatrix<Rgb8> image = TestMatrices.Create(width, height, static (x, y) =>
            y < 12
                ? (x < 12 ? new Rgb8(220, 20, 20) : new Rgb8(20, 220, 220))
                : new Rgb8(230, 230, 20));
        SequentialMatrix<int> labels = TestMatrices.Create(width, height, static (x, y) =>
            y < 12 ? (x < 12 ? 0 : 1) : 2);

        var graph = RegionGraph.FromLabels(labels);
        SequentialMatrix<DoublePixel> dist = TestMatrices.Constant(width, height, new DoublePixel(1.0));

        var options = new HandTremorOptions(
            Amplitude: 6.0,
            SimilarHueThresholdDegrees: 20.0,
            OverlapDarkening: 0.5,
            PrimaryNoise: HighFreqPrimaryNoise,
            SecondaryNoise: HighFreqSecondaryNoise);

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, options: options);

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

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, options: options);

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

        Matrix<Rgb8> res1 = HandTremor.Apply(image, graph, dist, options: options);
        Matrix<Rgb8> res2 = HandTremor.Apply(image, graph, dist, options: options);

        MatrixAssert.Equal(res1, res2);
    }

    /// <summary>
    /// Verifies that pixels in a third region near a contrasting Group 3 boundary never receive spurious paper-gap fills.
    /// </summary>
    [Fact]
    public void Apply_TripleJunction_ThirdRegionNearContrastingBoundary_ShouldNotProduceSpuriousPaperGaps()
    {
        const int width = 24;
        const int height = 24;
        // Top-left Red (0), Top-right Cyan (1), Bottom Dark Red (2) similar hue to 0 and excluded from Group 3 gaps.
        SequentialMatrix<Rgb8> image = TestMatrices.Create(width, height, static (x, y) =>
            y < 12
                ? (x < 12 ? new Rgb8(220, 20, 20) : new Rgb8(20, 220, 220))
                : new Rgb8(180, 35, 20));
        SequentialMatrix<int> labels = TestMatrices.Create(width, height, static (x, y) =>
            y < 12 ? (x < 12 ? 0 : 1) : 2);

        var graph = RegionGraph.FromLabels(labels);
        SequentialMatrix<DoublePixel> dist = TestMatrices.Constant(width, height, new DoublePixel(1.0));

        // Mark boundaries involving Region 2 ((0,2) and (1,2)) as WetInWet-qualifying (Group 1) so ONLY boundary (0,1) is Group 3.
        bool[] qualifyingWet = new bool[graph.Boundaries.Count];
        for (int bi = 0; bi < graph.Boundaries.Count; bi++)
        {
            RegionBoundary b = graph.Boundaries[bi];
            qualifyingWet[bi] = b.First == 2 || b.Second == 2;
        }

        var wetInWet = new WetInWetResult((SequentialMatrix<Rgb8>)image.Clone(), new MaskMatrix(width, height), qualifyingWet);
        var options = new HandTremorOptions(
            Amplitude: 6.0,
            SimilarHueThresholdDegrees: 30.0,
            PaperColor: DefaultPaperColor,
            PrimaryNoise: HighFreqPrimaryNoise,
            SecondaryNoise: HighFreqSecondaryNoise);

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, wetInWet: wetInWet, options: options);

        for (int y = 12; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Assert.NotEqual(DefaultPaperColor, result[x, y]);
            }
        }
    }

    /// <summary>
    /// Verifies that boundaries involving an achromatic or near-neutral region do not produce stark white paper gaps.
    /// </summary>
    [Fact]
    public void Apply_AchromaticRegionBoundary_ShouldNotProduceWhitePaperGaps()
    {
        const int width = 24;
        const int height = 24;
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            width,
            height,
            new Rgb8(20, 22, 26),
            new Rgb8(215, 165, 140));
        SequentialMatrix<DoublePixel> dist = TestMatrices.Constant(width, height, new DoublePixel(1.0));

        var options = new HandTremorOptions(
            Amplitude: 6.0,
            SimilarHueThresholdDegrees: 30.0,
            OverlapDarkening: 0.8,
            PaperColor: DefaultPaperColor,
            PrimaryNoise: HighFreqPrimaryNoise,
            SecondaryNoise: HighFreqSecondaryNoise);

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, options: options);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Assert.NotEqual(DefaultPaperColor, result[x, y]);
            }
        }
    }

    /// <summary>
    /// Verifies that setting <see cref="HandTremorOptions.MinContrastingChroma"/> to 0.0 allows paper gaps on neutral boundaries.
    /// </summary>
    [Fact]
    public void Apply_ZeroMinContrastingChroma_ShouldAllowPaperGapsOnAchromaticBoundary()
    {
        const int width = 24;
        const int height = 24;
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            width,
            height,
            new Rgb8(20, 22, 26),
            new Rgb8(215, 165, 140));
        SequentialMatrix<DoublePixel> dist = TestMatrices.Constant(width, height, new DoublePixel(1.0));

        var options = new HandTremorOptions(
            Amplitude: 6.0,
            SimilarHueThresholdDegrees: 30.0,
            OverlapDarkening: 0.8,
            PaperColor: DefaultPaperColor,
            PrimaryNoise: HighFreqPrimaryNoise,
            SecondaryNoise: HighFreqSecondaryNoise,
            MinContrastingChroma: 0.0);

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, options: options);

        bool foundPaperGap = false;
        for (int y = 0; y < height && !foundPaperGap; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (result[x, y] == DefaultPaperColor)
                {
                    foundPaperGap = true;
                    break;
                }
            }
        }

        Assert.True(foundPaperGap);
    }

    /// <summary>
    /// Verifies that <see cref="HandTremorOptions.PaperGapBias"/> increases or decreases white paper-gap coverage monotonically.
    /// </summary>
    [Fact]
    public void Apply_PaperGapBias_ShouldModulatePaperGapCoverage()
    {
        const int width = 32;
        const int height = 32;
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            width,
            height,
            new Rgb8(220, 20, 20),
            new Rgb8(20, 220, 220));
        SequentialMatrix<DoublePixel> dist = TestMatrices.Constant(width, height, new DoublePixel(1.0));

        HandTremorOptions baseOptions = new(
            Amplitude: 6.0,
            SimilarHueThresholdDegrees: 30.0,
            OverlapDarkening: 0.8,
            PaperColor: DefaultPaperColor,
            PrimaryNoise: HighFreqPrimaryNoise,
            SecondaryNoise: HighFreqSecondaryNoise);

        int CountPaperGaps(double bias)
        {
            Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, options: baseOptions with { PaperGapBias = bias });
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (result[x, y] == DefaultPaperColor)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        int negativeCount = CountPaperGaps(-0.6);
        int neutralCount = CountPaperGaps(0.0);
        int positiveCount = CountPaperGaps(0.6);

        Assert.True(negativeCount < neutralCount && neutralCount < positiveCount);
    }

    /// <summary>
    /// Verifies that <see cref="HandTremorOptions.PaperGapOpacity"/> controls how strongly <see cref="HandTremorOptions.PaperColor"/> is blended into gaps.
    /// </summary>
    [Fact]
    public void Apply_PaperGapOpacity_ShouldControlPaperColorBlending()
    {
        const int width = 32;
        const int height = 32;
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            width,
            height,
            new Rgb8(220, 20, 20),
            new Rgb8(20, 220, 220));
        SequentialMatrix<DoublePixel> dist = TestMatrices.Constant(width, height, new DoublePixel(1.0));

        HandTremorOptions baseOptions = new(
            Amplitude: 6.0,
            SimilarHueThresholdDegrees: 30.0,
            OverlapDarkening: 0.8,
            PaperColor: DefaultPaperColor,
            PrimaryNoise: HighFreqPrimaryNoise,
            SecondaryNoise: HighFreqSecondaryNoise);

        Matrix<Rgb8> full = HandTremor.Apply(image, graph, dist, options: baseOptions with { PaperGapOpacity = 1.0 });
        Matrix<Rgb8> half = HandTremor.Apply(image, graph, dist, options: baseOptions with { PaperGapOpacity = 0.5 });
        Matrix<Rgb8> zero = HandTremor.Apply(image, graph, dist, options: baseOptions with { PaperGapOpacity = 0.0 });

        bool checkedGapLocation = false;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Assert.NotEqual(DefaultPaperColor, zero[x, y]);
                Assert.NotEqual(DefaultPaperColor, half[x, y]);

                if (full[x, y] == DefaultPaperColor)
                {
                    checkedGapLocation = true;
                    Assert.Equal(image[x, y], zero[x, y]);

                    int halfSum = half[x, y].R + half[x, y].G + half[x, y].B;
                    int zeroSum = zero[x, y].R + zero[x, y].G + zero[x, y].B;
                    Assert.True(halfSum > zeroSum);
                }
            }
        }

        Assert.True(checkedGapLocation);
    }

    /// <summary>
    /// Verifies that huge <see cref="HandTremorOptions.Amplitude"/> values do not overflow band radius or produce black fallback pixels.
    /// </summary>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    public void Apply_HugeAmplitude_ShouldNotOverflowOrProduceBlackFallbackPixels(double paperGapBias)
    {
        const int width = 16;
        const int height = 16;
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            width,
            height,
            new Rgb8(220, 40, 40),
            new Rgb8(40, 220, 220));
        SequentialMatrix<DoublePixel> dist = TestMatrices.Constant(width, height, new DoublePixel(1.0));
        var options = new HandTremorOptions(
            Amplitude: 1e12,
            PaperGapBias: paperGapBias,
            PaperColor: DefaultPaperColor);

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, options: options);

        Assert.Equal(image.Size, result.Size);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Assert.NotEqual(new Rgb8(0, 0, 0), result[x, y]);
            }
        }
    }

    /// <summary>
    /// Verifies that outer-rim diagonal boundary normals remain symmetric and stable when neighboring pixels lie outside the band.
    /// </summary>
    [Fact]
    public void Apply_OuterRimDiagonalBoundary_ShouldPreserveDiagonalNormalStability()
    {
        const int size = 20;
        SequentialMatrix<int> labels = TestMatrices.Create(size, size, (x, y) => x + y < size ? 0 : 1);
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            size,
            size,
            (x, y) => x + y < size
                ? new Rgb8((byte)(170 + (x + y)), 30, 30)
                : new Rgb8(30, (byte)(170 + (40 - x - y)), 220));
        var graph = RegionGraph.FromLabels(labels);
        SequentialMatrix<DoublePixel> dist = TestMatrices.Constant(size, size, new DoublePixel(1.0));

        // Tiny frequency isolates normal-driven PaperGapBias from random Perlin asymmetry across the diagonal.
        var nearZeroNoise = new PerlinNoiseOptions(Seed: 101, Frequency: 1e-18, Octaves: 1);
        var options = new HandTremorOptions(
            Amplitude: 2.0,
            PaperGapBias: 0.37,
            PrimaryNoise: nearZeroNoise,
            SecondaryNoise: nearZeroNoise);

        Matrix<Rgb8> result = HandTremor.Apply(image, graph, dist, options: options);

        // innerRadius = Ceil(2 * 1.37) = 3, bandRadius = 6
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Assert.Equal(result[y, x], result[x, y]);
                Assert.NotEqual(new Rgb8(0, 0, 0), result[x, y]);
            }
        }
    }

    /// <summary>
    /// Verifies that the <see cref="IDisjointSet"/> overload produces identical results to the <see cref="RegionGraph"/> overload.
    /// </summary>
    [Fact]
    public void Apply_DisjointSetOverload_ShouldProduceIdenticalResultToRegionGraph()
    {
        const int width = 16;
        const int height = 16;
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(width, height, new Rgb8(220, 20, 20), new Rgb8(20, 220, 220));
        DisjointSet<int> disjointSet = CreateVerticalSplitDisjointSet(width, height);
        RegionGraph graph = disjointSet.ToRegionGraph();
        SequentialMatrix<DoublePixel> dist = TestMatrices.Constant(width, height, new DoublePixel(0.8));
        var options = new HandTremorOptions(
            Amplitude: 4.0,
            OverlapDarkening: 0.6,
            PrimaryNoise: HighFreqPrimaryNoise,
            SecondaryNoise: HighFreqSecondaryNoise);

        Matrix<Rgb8> fromDisjointSet = HandTremor.Apply(image, disjointSet, dist, options: options);
        Matrix<Rgb8> fromGraph = HandTremor.Apply(image, graph, dist, options: options);

        MatrixAssert.Equal(fromGraph, fromDisjointSet);
    }

    /// <summary>
    /// Verifies that a pre-canceled <see cref="CancellationToken"/> throws <see cref="OperationCanceledException"/>
    /// across all overloads and that an active uncanceled token matches default execution bit-for-bit.
    /// </summary>
    [Fact]
    public void Apply_CanceledToken_ShouldThrowOperationCanceledException()
    {
        const int width = 16;
        const int height = 16;
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(width, height, new Rgb8(220, 20, 20), new Rgb8(20, 220, 220));
        DisjointSet<int> disjointSet = CreateVerticalSplitDisjointSet(width, height);
        RegionGraph graph = disjointSet.ToRegionGraph();
        SequentialMatrix<DoublePixel> dist = TestMatrices.Constant(width, height, new DoublePixel(0.8));
        var options = new HandTremorOptions(
            Amplitude: 4.0,
            OverlapDarkening: 0.6,
            PrimaryNoise: HighFreqPrimaryNoise,
            SecondaryNoise: HighFreqSecondaryNoise);
        var canceledToken = new CancellationToken(canceled: true);

        Assert.Throws<OperationCanceledException>(() =>
            HandTremor.Apply(image, graph, dist, options: options, cancellationToken: canceledToken));
        Assert.Throws<OperationCanceledException>(() =>
            HandTremor.Apply(image, disjointSet, dist, options: options, cancellationToken: canceledToken));
        Assert.Throws<OperationCanceledException>(() =>
            HandTremor.ApplyCore(image, graph, dist, wetInWet: null, options, canceledToken));

        using var cts = new CancellationTokenSource();
        Matrix<Rgb8> expected = HandTremor.Apply(image, graph, dist, options: options);
        Matrix<Rgb8> actual = HandTremor.Apply(image, graph, dist, options: options, cancellationToken: cts.Token);
        MatrixAssert.Equal(expected, actual);
    }

    private static DisjointSet<int> CreateVerticalSplitDisjointSet(int width, int height)
    {
        var dummy = new SequentialMatrix<int>(width, height);
        var disjointSet = new DisjointSet<int>(dummy, static i => i);
        int splitX = width / 2;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x < splitX && (x != 0 || y != 0))
                {
                    disjointSet.Union(new Coord(0, 0), new Coord(x, y), 0);
                }
                else if (x >= splitX && (x != splitX || y != 0))
                {
                    disjointSet.Union(new Coord(splitX, 0), new Coord(x, y), 1);
                }
            }
        }

        return disjointSet;
    }
}
