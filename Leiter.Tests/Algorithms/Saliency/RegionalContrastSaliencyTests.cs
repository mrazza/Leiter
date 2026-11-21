namespace Leiter.Tests.Algorithms.Saliency;

using System;
using System.Linq;
using System.Threading;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="RegionalContrastSaliency"/>.
/// </summary>
public class RegionalContrastSaliencyTests
{
    [Fact]
    public void ComputeSaliency_MultiColorImage_ShouldComputeNormalizedRegionalSaliency()
    {
        // 20x20 image with 25+ unique quantized colors to satisfy the smoothing logic
        SequentialMatrix<Rgb8> img = TestMatrices.Create(
            20,
            20,
            (x, y) => new Rgb8((byte)(x * 12), (byte)(y * 12), (byte)((x + y) * 6)));

        // Get segmentation
        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 5));

        // Compute regional saliency
        var saliency = RegionalContrastSaliency.ComputeSaliency(img, segmentation, new RegionalContrastSaliencyOptions(EnableSmoothing: true, ComputeBorderRegions: true));

        Assert.Equal(20, saliency.Width);
        Assert.Equal(20, saliency.Height);

        // Ensure normalized
        var minS = saliency.Min(p => p.Value);
        var maxS = saliency.Max(p => p.Value);
        Assert.Equal(0.0, minS, 4);
        Assert.Equal(1.0, maxS, 4);

        // Verify regional saliency contains various levels (i.e. not all flat zeros/ones)
        Assert.Contains(saliency, p => p.Value > 0.0 && p.Value < 1.0);

        // Turn off smoothing and border calculation to test the alternate branches
        var saliencyRaw = RegionalContrastSaliency.ComputeSaliency(img, segmentation, new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: false));
        Assert.Equal(20, saliencyRaw.Width);
        Assert.True(saliencyRaw.Min(p => p.Value) >= 0.0);
    }

    /// <summary>
    /// Verifies that regional contrast saliency of a contrasting object is correctly computed.
    /// </summary>
    [Fact]
    public void ComputeSaliency_ContrastingObject_ShouldBeMostSalient()
    {
        // 12x12 image with a 4x4 red square in the center (16 pixels) and black background
        SequentialMatrix<Rgb8> img = TestMatrices.CenteredBox(12, 12, 4, 4, 7, 7, new Rgb8(255, 0, 0), new Rgb8(0, 0, 0));

        // Segment the image. With exact colors, the segment boundaries are completely clear.
        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 4));

        // Compute regional saliency
        var saliency = RegionalContrastSaliency.ComputeSaliency(img, segmentation, new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: false));

        // The contrasting center red region should have highest saliency (1.0)
        Assert.Equal(1.0, saliency[5, 5].Value, 4);

        // The background black region should have the lowest saliency (0.0)
        Assert.Equal(0.0, saliency[0, 0].Value, 4);
    }

    /// <summary>
    /// Verifies that regional contrast saliency of a solid-color image returns all zeros without NaN.
    /// </summary>
    [Fact]
    public void ComputeSaliency_SolidColorImage_ShouldReturnZerosWithoutNaN()
    {
        SequentialMatrix<Rgb8> img = TestMatrices.Constant(10, 10, new Rgb8(100, 150, 200));

        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 4));

        var withOptions = RegionalContrastSaliency.ComputeSaliency(img, segmentation, new RegionalContrastSaliencyOptions(EnableSmoothing: true, ComputeBorderRegions: true));
        var withoutOptions = RegionalContrastSaliency.ComputeSaliency(img, segmentation, new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: false));

        SequentialMatrix<DoublePixel> expectedZeros = TestMatrices.Constant<DoublePixel>(10, 10, 0.0);
        MatrixAssert.ApproxEqual(expectedZeros, withOptions, 1e-4);
        MatrixAssert.ApproxEqual(expectedZeros, withoutOptions, 1e-4);
    }

    /// <summary>
    /// Verifies that regional contrast saliency on a 2-color image with smoothing enabled does not produce NaN.
    /// </summary>
    [Fact]
    public void ComputeSaliency_ContrastingObjectWithSmoothing_ShouldNotProduceNaN()
    {
        SequentialMatrix<Rgb8> img = TestMatrices.CenteredBox(12, 12, 4, 4, 7, 7, new Rgb8(255, 0, 0), new Rgb8(0, 0, 0));

        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 4));

        var withoutBorderSuppression = RegionalContrastSaliency.ComputeSaliency(img, segmentation, new RegionalContrastSaliencyOptions(EnableSmoothing: true, ComputeBorderRegions: false));
        var withBorderSuppression = RegionalContrastSaliency.ComputeSaliency(img, segmentation, new RegionalContrastSaliencyOptions(EnableSmoothing: true, ComputeBorderRegions: true));

        foreach (var result in new[] { withoutBorderSuppression, withBorderSuppression })
        {
            MatrixAssert.AllInRange(result, 0.0, 1.0);
            Assert.Equal(1.0, result[5, 5].Value, 4);
            Assert.Equal(0.0, result[0, 0].Value, 4);
        }
    }

    /// <summary>
    /// Verifies that suppressing a high-contrast border region zeros border pixels and still normalizes
    /// the surviving interior salient region to 1.0.
    /// </summary>
    [Fact]
    public void ComputeSaliency_BorderSuppression_ShouldZeroBorderAndNormalizeInteriorToOne()
    {
        // 40x40 image so 2.5% border width/height covers the 1-pixel outer ring (x=0, x=39, y=0, y=39).
        SequentialMatrix<Rgb8> img = TestMatrices.Create(
            40,
            40,
            (x, y) =>
                x <= 8 && y >= 15 && y <= 24 ? new Rgb8(255, 255, 255) :
                x >= 28 && x <= 37 && y >= 15 && y <= 24 ? new Rgb8(45, 45, 45) :
                new Rgb8(0, 0, 0));

        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 5));

        // Confirm the white border strip has higher raw saliency than the interior gray region before border suppression.
        var unsuppressed = RegionalContrastSaliency.ComputeSaliency(img, segmentation, new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: false));
        Assert.Equal(1.0, unsuppressed[4, 20].Value, 4);
        Assert.True(unsuppressed[32, 20].Value < 1.0);

        foreach (bool enableSmoothing in new[] { false, true })
        {
            var saliency = RegionalContrastSaliency.ComputeSaliency(img, segmentation, new RegionalContrastSaliencyOptions(EnableSmoothing: enableSmoothing, ComputeBorderRegions: true));

            MatrixAssert.AllInRange(saliency, 0.0, 1.0);

            // Border regions (white strip and background ring) must be suppressed to 0.0.
            Assert.Equal(0.0, saliency[0, 20].Value, 4);
            Assert.Equal(0.0, saliency[4, 20].Value, 4);
            Assert.Equal(0.0, saliency[0, 0].Value, 4);
            Assert.Equal(0.0, saliency[20, 0].Value, 4);

            // Surviving interior salient region must normalize to 1.0.
            Assert.Equal(1.0, saliency[32, 20].Value, 4);
        }
    }

    /// <summary>
    /// Verifies border suppression and normalization when active color-space smoothing runs (20+ palette colors).
    /// </summary>
    [Fact]
    public void ComputeSaliency_BorderSuppressionWithActiveSmoothing_ShouldZeroBorderAndNormalizeInteriorToOne()
    {
        // 40x40 image split into a 5x5 grid of 8x8 blocks (25 distinct colors, 64 pixels each).
        SequentialMatrix<Rgb8> img = TestMatrices.Create(
            40,
            40,
            (x, y) =>
            {
                int bx = x / 8;
                int by = y / 8;
                return (bx == 0 && by == 2)
                    ? new Rgb8(255, 255, 255)
                    : new Rgb8((byte)((bx * 44) + 10), (byte)((by * 44) + 10), 60);
            });

        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 10));
        var saliency = RegionalContrastSaliency.ComputeSaliency(img, segmentation, new RegionalContrastSaliencyOptions(EnableSmoothing: true, ComputeBorderRegions: true));

        MatrixAssert.AllInRange(saliency, 0.0, 1.0);

        // All perimeter blocks touch the 1-pixel border along at least 8 of 64 pixels (12.5% > 10%) and must be 0.0.
        Assert.Equal(0.0, saliency[0, 0].Value, 4);
        Assert.Equal(0.0, saliency[4, 20].Value, 4);
        Assert.Equal(0.0, saliency[20, 0].Value, 4);
        Assert.Equal(0.0, saliency[20, 39].Value, 4);
        Assert.Equal(0.0, saliency[39, 39].Value, 4);

        // Surviving interior blocks must normalize up to 1.0 and include intermediate values.
        double interiorMax = 0.0;
        for (int y = 8; y < 32; y++)
        {
            for (int x = 8; x < 32; x++)
            {
                interiorMax = Math.Max(interiorMax, saliency[x, y].Value);
            }
        }
        Assert.Equal(1.0, interiorMax, 4);
        Assert.Contains(saliency, p => p.Value > 0.0 && p.Value < 1.0);
    }

    /// <summary>
    /// Verifies that border suppression applied after color-space smoothing does not darken interior regions sharing a color with a border region.
    /// </summary>
    [Fact]
    public void ComputeSaliency_BorderSuppressionWithSmoothing_ShouldNotDarkenInteriorRegionsSharingColor()
    {
        // 40x40 image split into a 5x5 grid of 8x8 blocks where border block (0, 2) shares the exact color of interior block (2, 2),
        // and all other blocks have distinct quantized colors so active smoothing runs (24 colors >= 20).
        var sharedColor = new Rgb8(240, 20, 20);
        SequentialMatrix<Rgb8> img = TestMatrices.Create(
            40,
            40,
            (x, y) =>
            {
                int bx = x / 8;
                int by = y / 8;
                return (bx == 0 && by == 2) || (bx == 2 && by == 2)
                    ? sharedColor
                    : new Rgb8((byte)((bx * 44) + 10), (byte)((by * 44) + 10), 120);
            });

        SequentialMatrix<int> blockLabels = TestMatrices.Create(40, 40, (x, y) => ((y / 8) * 5) + (x / 8));
        RegionGraph graph = RegionGraph.FromLabels(blockLabels);

        Matrix<DoublePixel> unsuppressed = RegionalContrastSaliency.ComputeSaliency(
            img,
            graph,
            new RegionalContrastSaliencyOptions(EnableSmoothing: true, ComputeBorderRegions: false));
        Matrix<DoublePixel> suppressed = RegionalContrastSaliency.ComputeSaliency(
            img,
            graph,
            new RegionalContrastSaliencyOptions(EnableSmoothing: true, ComputeBorderRegions: true));

        // Border block (0, 2) must be suppressed to 0.0.
        Assert.Equal(0.0, suppressed[4, 20].Value, 9);

        // Interior regions (1,1), (2,2), (3,3) must preserve exact affine ordering/ratios relative to each other
        // because border suppression happens after color-space smoothing.
        double u11 = unsuppressed[12, 12].Value;
        double u22 = unsuppressed[20, 20].Value;
        double u33 = unsuppressed[28, 28].Value;

        double s11 = suppressed[12, 12].Value;
        double s22 = suppressed[20, 20].Value;
        double s33 = suppressed[28, 28].Value;

        double expectedRatio = (u22 - u11) / (u33 - u11);
        double actualRatio = (s22 - s11) / (s33 - s11);
        Assert.Equal(expectedRatio, actualRatio, 9);
    }

    /// <summary>
    /// Verifies that regional contrast saliency truncates rare (&lt;5%) noise colors cleanly without NaN.
    /// </summary>
    [Fact]
    public void ComputeSaliency_RareNoisePixels_ShouldTruncatePaletteCleanly()
    {
        // 20x20 image (400 pixels): 300 black (75%), 90 red (22.5%), and 10 rare noise colors (2.5% < 5%).
        SequentialMatrix<Rgb8> img = TestMatrices.CenteredBox(20, 20, 5, 5, 14, 13, new Rgb8(255, 0, 0), new Rgb8(0, 0, 0));

        Rgb8[] rareColors =
        [
            new(0, 255, 0),
            new(0, 0, 255),
            new(255, 255, 0),
            new(0, 255, 255),
            new(255, 0, 255),
            new(128, 255, 0),
            new(0, 128, 255),
            new(255, 128, 0),
            new(128, 0, 255),
            new(255, 255, 255)
        ];
        (int X, int Y)[] noiseCoords =
        [
            (1, 1), (3, 1), (5, 1), (7, 1), (9, 1),
            (11, 1), (13, 1), (15, 1), (17, 1), (1, 3)
        ];
        for (int i = 0; i < rareColors.Length; i++)
        {
            img[noiseCoords[i].X, noiseCoords[i].Y] = rareColors[i];
        }

        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 4));

        foreach (bool enableSmoothing in new[] { false, true })
        {
            var saliency = RegionalContrastSaliency.ComputeSaliency(img, segmentation, new RegionalContrastSaliencyOptions(EnableSmoothing: enableSmoothing, ComputeBorderRegions: enableSmoothing));

            Assert.All(saliency, p =>
            {
                Assert.False(double.IsNaN(p.Value));
                Assert.InRange(p.Value, 0.0, 1.0);
            });
            Assert.Equal(1.0, saliency[10, 9].Value, 4);
            Assert.Equal(0.0, saliency[0, 19].Value, 4);
        }
    }

    [Fact]
    public void ComputeSaliency_EmptyImage_ShouldReturnEmptyMatrix()
    {
        var emptyImg = new SequentialMatrix<Rgb8>(0, 0);
        var emptySeg = EgbiSegmentation.Segment(emptyImg);
        Matrix<DoublePixel> regionalEmpty = RegionalContrastSaliency.ComputeSaliency(emptyImg, emptySeg);
        Assert.Empty(regionalEmpty);
        Assert.Equal(0, regionalEmpty.Width);
        Assert.Equal(0, regionalEmpty.Height);

        var emptyGraph = RegionGraph.FromDisjointSet(emptySeg);
        Matrix<DoublePixel> graphEmpty = RegionalContrastSaliency.ComputeSaliency(emptyImg, emptyGraph);
        Assert.Empty(graphEmpty);

        var mismatchedSeg = EgbiSegmentation.Segment(new SequentialMatrix<Rgb8>(1, 1));
        Assert.Throws<ArgumentException>(() => RegionalContrastSaliency.ComputeSaliency(emptyImg, mismatchedSeg));

        var mismatchedGraph = RegionGraph.FromDisjointSet(mismatchedSeg);
        Assert.Throws<ArgumentException>(() => RegionalContrastSaliency.ComputeSaliency(emptyImg, mismatchedGraph));
    }

    /// <summary>
    /// Verifies argument validation for null arguments, dimension mismatches, and invalid options.
    /// </summary>
    [Fact]
    public void ComputeSaliency_Validation_ShouldThrowOnNullOrDimensionMismatch()
    {
        var img = new SequentialMatrix<Rgb8>(10, 10);
        var seg = EgbiSegmentation.Segment(img);
        var graph = RegionGraph.FromDisjointSet(seg);

        // Null checks
        Assert.Throws<ArgumentNullException>(() => RegionalContrastSaliency.ComputeSaliency(null!, seg));
        Assert.Throws<ArgumentNullException>(() => RegionalContrastSaliency.ComputeSaliency(img, (IDisjointSet)null!));
        Assert.Throws<ArgumentNullException>(() => RegionalContrastSaliency.ComputeSaliency(null!, graph));
        Assert.Throws<ArgumentNullException>(() => RegionalContrastSaliency.ComputeSaliency(img, (RegionGraph)null!));

        // Dimension mismatch with non-empty image
        var mismatchedSeg = EgbiSegmentation.Segment(new SequentialMatrix<Rgb8>(5, 5));
        var mismatchedGraph = RegionGraph.FromDisjointSet(mismatchedSeg);
        Assert.Throws<ArgumentException>(() => RegionalContrastSaliency.ComputeSaliency(img, mismatchedSeg));
        Assert.Throws<ArgumentException>(() => RegionalContrastSaliency.ComputeSaliency(img, mismatchedGraph));

        // Invalid options on both overloads
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RegionalContrastSaliency.ComputeSaliency(img, graph, new RegionalContrastSaliencyOptions(CenterPriorWeight: -0.1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RegionalContrastSaliency.ComputeSaliency(img, graph, new RegionalContrastSaliencyOptions(CenterPriorWeight: double.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RegionalContrastSaliency.ComputeSaliency(img, graph, new RegionalContrastSaliencyOptions(CenterPriorWeight: double.PositiveInfinity)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RegionalContrastSaliency.ComputeSaliency(img, seg, new RegionalContrastSaliencyOptions(CenterPriorWeight: -0.1)));

        // Non-Matrix IReadOnlyMatrix (MatrixView) input and RegionGraph overload parity
        var view = new MatrixView<Rgb8>(img, 0, 0, 10, 10, EdgeHandling.Extend);
        Matrix<DoublePixel> viewSaliency = RegionalContrastSaliency.ComputeSaliency(view, seg);
        Matrix<DoublePixel> graphSaliency = RegionalContrastSaliency.ComputeSaliency(view, graph);
        Assert.Equal(img.Size, viewSaliency.Size);
        MatrixAssert.Equal(viewSaliency, graphSaliency);

        // Offset MatrixView over multi-region image should match materialized SequentialMatrix
        SequentialMatrix<Rgb8> padded = TestMatrices.Create(
            14,
            14,
            (x, y) => new Rgb8((byte)(x * 18), (byte)(y * 18), 40));

        var croppedView = new MatrixView<Rgb8>(padded, 2, 2, 10, 10, EdgeHandling.Extend);
        SequentialMatrix<Rgb8> croppedSeq = croppedView.ToSequentialMatrix();
        var croppedSeg = EgbiSegmentation.Segment(croppedSeq, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 4));
        MatrixAssert.Equal(
            RegionalContrastSaliency.ComputeSaliency(croppedSeq, croppedSeg),
            RegionalContrastSaliency.ComputeSaliency(croppedView, croppedSeg));
    }

    [Fact]
    public void ComputeSaliency_MultiRegionNonTrivialImage_DisjointSetAndRegionGraphOverloadsShouldMatchAcrossOptions()
    {
        SequentialMatrix<Rgb8> img = TestMatrices.Create(
            24,
            24,
            (x, y) =>
            {
                int bx = x / 6;
                int by = y / 6;
                return new Rgb8((byte)((bx * 70) + 15), (byte)((by * 70) + 15), (byte)(((bx + by) % 4) * 70));
            });

        IDisjointSet segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 5));
        RegionGraph graph = RegionGraph.FromDisjointSet(segmentation);
        Assert.True(graph.RegionCount >= 4);

        RegionalContrastSaliencyOptions[] optionVariants =
        [
            new(EnableSmoothing: false, ComputeBorderRegions: false, CenterPriorWeight: 0.0),
            new(EnableSmoothing: true, ComputeBorderRegions: false, CenterPriorWeight: 2.5),
            new(EnableSmoothing: false, ComputeBorderRegions: true, CenterPriorWeight: 4.0),
        ];

        foreach (RegionalContrastSaliencyOptions options in optionVariants)
        {
            Matrix<DoublePixel> fromDisjointSet = RegionalContrastSaliency.ComputeSaliency(img, segmentation, options);
            Matrix<DoublePixel> fromGraph = RegionalContrastSaliency.ComputeSaliency(img, graph, options);

            MatrixAssert.Equal(fromDisjointSet, fromGraph);
            Assert.Equal(0.0, fromGraph.Min(p => p.Value), 4);
            Assert.Equal(1.0, fromGraph.Max(p => p.Value), 4);
            Assert.Contains(fromGraph, p => p.Value > 0.0 && p.Value < 1.0);
        }
    }

    [Fact]
    public void ComputeSaliency_ZeroCenterPriorWeight_ShouldRemoveCenterPriorSuppression()
    {
        // Place two identical contrasting red squares on black: one centered (13..17) and one near the left edge (2..6).
        SequentialMatrix<Rgb8> img = TestMatrices.Create(
            31,
            31,
            (x, y) =>
                y >= 13 && y <= 17 && ((x >= 13 && x <= 17) || (x >= 2 && x <= 6))
                    ? new Rgb8(255, 0, 0)
                    : new Rgb8(0, 0, 0));

        var seg = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 5));

        Matrix<DoublePixel> biased = RegionalContrastSaliency.ComputeSaliency(
            img,
            seg,
            new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: false, CenterPriorWeight: 9.0));
        Matrix<DoublePixel> unbiased = RegionalContrastSaliency.ComputeSaliency(
            img,
            seg,
            new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: false, CenterPriorWeight: 0.0));

        // Center patch normalizes to 1.0 in both; under CenterPriorWeight=9.0 the edge patch suffers additional
        // exp(-9 * d^2) suppression (~0.12 normalized vs ~0.39 from pairwise spatial distance alone).
        Assert.Equal(1.0, biased[15, 15].Value, 4);
        Assert.Equal(1.0, unbiased[15, 15].Value, 4);
        Assert.True(biased[4, 15].Value < 0.20, $"Expected heavy center-prior suppression on edge patch, got {biased[4, 15].Value}");
        Assert.True(unbiased[4, 15].Value > 0.35, $"Expected higher unbiased saliency on edge patch, got {unbiased[4, 15].Value}");
        Assert.True(unbiased[4, 15].Value > biased[4, 15].Value * 2.5);
    }

    [Fact]
    public void ComputeSaliency_CancellationToken_ShouldHonorCancellationAndMatchUncanceledResults()
    {
        SequentialMatrix<Rgb8> img = TestMatrices.Create(
            20,
            20,
            (x, y) => new Rgb8((byte)(x * 12), (byte)(y * 12), (byte)((x + y) * 6)));
        IDisjointSet seg = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 5));
        RegionGraph graph = RegionGraph.FromDisjointSet(seg);

        var emptyImg = new SequentialMatrix<Rgb8>(0, 0);
        IDisjointSet emptySeg = EgbiSegmentation.Segment(emptyImg);
        RegionGraph emptyGraph = RegionGraph.FromDisjointSet(emptySeg);

        using var canceledCts = new CancellationTokenSource();
        canceledCts.Cancel();

        Assert.Throws<OperationCanceledException>(() => RegionalContrastSaliency.ComputeSaliency(img, seg, cancellationToken: canceledCts.Token));
        Assert.Throws<OperationCanceledException>(() => RegionalContrastSaliency.ComputeSaliency(img, graph, cancellationToken: canceledCts.Token));
        Assert.Throws<OperationCanceledException>(() => RegionalContrastSaliency.ComputeSaliency(emptyImg, emptySeg, cancellationToken: canceledCts.Token));
        Assert.Throws<OperationCanceledException>(() => RegionalContrastSaliency.ComputeSaliency(emptyImg, emptyGraph, cancellationToken: canceledCts.Token));

        using var activeCts = new CancellationTokenSource();
        foreach (bool enableSmoothing in new[] { false, true })
        {
            var opts = new RegionalContrastSaliencyOptions(EnableSmoothing: enableSmoothing, ComputeBorderRegions: true);
            Matrix<DoublePixel> baseline = RegionalContrastSaliency.ComputeSaliency(img, graph, opts);
            Matrix<DoublePixel> withToken = RegionalContrastSaliency.ComputeSaliency(img, graph, opts, activeCts.Token);
            MatrixAssert.Equal(baseline, withToken);
        }
    }

    [Fact]
    public void ComputeSaliency_LargeGridWithSmoothingAndBorderSuppression_ShouldBeDeterministicAndFinite()
    {
        const int width = 280;
        const int height = 260;

        SequentialMatrix<Rgb8> img = TestMatrices.Create(
            width,
            height,
            static (x, y) =>
            {
                int bx = x / 28;
                int by = y / 26;
                return new Rgb8(
                    (byte)((bx * 25) + 10),
                    (byte)((by * 25) + 10),
                    (byte)(((bx * 7) + (by * 13)) % 256));
            });

        SequentialMatrix<int> labels = TestMatrices.Create(
            width,
            height,
            static (x, y) => ((y / 26) * 10) + (x / 28));

        RegionGraph graph = RegionGraph.FromLabels(labels);
        var opts = new RegionalContrastSaliencyOptions(EnableSmoothing: true, ComputeBorderRegions: true);

        Matrix<DoublePixel> first = RegionalContrastSaliency.ComputeSaliency(img, graph, opts);
        Matrix<DoublePixel> second = RegionalContrastSaliency.ComputeSaliency(img, graph, opts);

        Assert.Equal(img.Size, first.Size);
        MatrixAssert.AllInRange(first, 0.0, 1.0);
        Assert.Contains(first, static p => p.Value > 0.0 && p.Value < 1.0);
        MatrixAssert.Equal(first, second);
    }
}
