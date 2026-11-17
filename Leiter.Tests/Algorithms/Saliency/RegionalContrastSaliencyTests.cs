namespace Leiter.Tests.Algorithms.Saliency;

using System;
using System.Linq;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
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
        var img = new SequentialMatrix<Rgb8>(20, 20);
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                byte r = (byte)(x * 12);
                byte g = (byte)(y * 12);
                byte b = (byte)((x + y) * 6);
                img[x, y] = new Rgb8(r, g, b);
            }
        }

        // Get segmentation
        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 5));

        // Compute regional saliency
        var saliency = RegionalContrastSaliency.ComputeSaliency(img, segmentation, enableSmoothing: true, computeBorderRegions: true);

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
        var saliencyRaw = RegionalContrastSaliency.ComputeSaliency(img, segmentation, enableSmoothing: false, computeBorderRegions: false);
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
        var img = new SequentialMatrix<Rgb8>(12, 12);
        img.SetAll(new Rgb8(0, 0, 0));
        for (int y = 4; y <= 7; y++)
        {
            for (int x = 4; x <= 7; x++)
            {
                img[x, y] = new Rgb8(255, 0, 0);
            }
        }

        // Segment the image. With exact colors, the segment boundaries are completely clear.
        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 4));

        // Compute regional saliency
        var saliency = RegionalContrastSaliency.ComputeSaliency(img, segmentation, enableSmoothing: false, computeBorderRegions: false);

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
        var img = new SequentialMatrix<Rgb8>(10, 10);
        img.SetAll(new Rgb8(100, 150, 200));

        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 4));

        var withOptions = RegionalContrastSaliency.ComputeSaliency(img, segmentation, enableSmoothing: true, computeBorderRegions: true);
        var withoutOptions = RegionalContrastSaliency.ComputeSaliency(img, segmentation, enableSmoothing: false, computeBorderRegions: false);

        Assert.Equal(10, withOptions.Width);
        Assert.Equal(10, withOptions.Height);
        Assert.All(withOptions, p =>
        {
            Assert.False(double.IsNaN(p.Value));
            Assert.Equal(0.0, p.Value, 4);
        });
        Assert.All(withoutOptions, p =>
        {
            Assert.False(double.IsNaN(p.Value));
            Assert.Equal(0.0, p.Value, 4);
        });
    }

    /// <summary>
    /// Verifies that regional contrast saliency on a 2-color image with smoothing enabled does not produce NaN.
    /// </summary>
    [Fact]
    public void ComputeSaliency_ContrastingObjectWithSmoothing_ShouldNotProduceNaN()
    {
        var img = new SequentialMatrix<Rgb8>(12, 12);
        img.SetAll(new Rgb8(0, 0, 0));
        for (int y = 4; y <= 7; y++)
        {
            for (int x = 4; x <= 7; x++)
            {
                img[x, y] = new Rgb8(255, 0, 0);
            }
        }

        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 4));

        var withoutBorderSuppression = RegionalContrastSaliency.ComputeSaliency(img, segmentation, enableSmoothing: true, computeBorderRegions: false);
        var withBorderSuppression = RegionalContrastSaliency.ComputeSaliency(img, segmentation, enableSmoothing: true, computeBorderRegions: true);

        foreach (var result in new[] { withoutBorderSuppression, withBorderSuppression })
        {
            Assert.All(result, p => Assert.False(double.IsNaN(p.Value)));
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
        var img = new SequentialMatrix<Rgb8>(40, 40);
        img.SetAll(new Rgb8(0, 0, 0));

        // High-contrast white border strip (90 pixels, 10 on x=0 -> 11.1% > 10% border threshold).
        for (int y = 15; y <= 24; y++)
        {
            for (int x = 0; x <= 8; x++)
            {
                img[x, y] = new Rgb8(255, 255, 255);
            }
        }

        // Interior dark-gray salient square (100 pixels, 0 border pixels, distinct 12-level quantization bucket).
        for (int y = 15; y <= 24; y++)
        {
            for (int x = 28; x <= 37; x++)
            {
                img[x, y] = new Rgb8(45, 45, 45);
            }
        }

        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 5));

        // Confirm the white border strip has higher raw saliency than the interior gray region before border suppression.
        var unsuppressed = RegionalContrastSaliency.ComputeSaliency(img, segmentation, enableSmoothing: false, computeBorderRegions: false);
        Assert.Equal(1.0, unsuppressed[4, 20].Value, 4);
        Assert.True(unsuppressed[32, 20].Value < 1.0);

        foreach (bool enableSmoothing in new[] { false, true })
        {
            var saliency = RegionalContrastSaliency.ComputeSaliency(img, segmentation, enableSmoothing: enableSmoothing, computeBorderRegions: true);

            Assert.All(saliency, p =>
            {
                Assert.False(double.IsNaN(p.Value));
                Assert.InRange(p.Value, 0.0, 1.0);
            });

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
        var img = new SequentialMatrix<Rgb8>(40, 40);
        for (int by = 0; by < 5; by++)
        {
            for (int bx = 0; bx < 5; bx++)
            {
                Rgb8 blockColor = (bx == 0 && by == 2)
                    ? new Rgb8(255, 255, 255)
                    : new Rgb8((byte)(bx * 44 + 10), (byte)(by * 44 + 10), 60);

                for (int y = by * 8; y < (by + 1) * 8; y++)
                {
                    for (int x = bx * 8; x < (bx + 1) * 8; x++)
                    {
                        img[x, y] = blockColor;
                    }
                }
            }
        }

        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 10));
        var saliency = RegionalContrastSaliency.ComputeSaliency(img, segmentation, enableSmoothing: true, computeBorderRegions: true);

        Assert.All(saliency, p =>
        {
            Assert.False(double.IsNaN(p.Value));
            Assert.InRange(p.Value, 0.0, 1.0);
        });

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
    /// Verifies that regional contrast saliency truncates rare (&lt;5%) noise colors cleanly without NaN.
    /// </summary>
    [Fact]
    public void ComputeSaliency_RareNoisePixels_ShouldTruncatePaletteCleanly()
    {
        // 20x20 image (400 pixels): 300 black (75%), 90 red (22.5%), and 10 rare noise colors (2.5% < 5%).
        var img = new SequentialMatrix<Rgb8>(20, 20);
        img.SetAll(new Rgb8(0, 0, 0));

        for (int y = 5; y <= 13; y++)
        {
            for (int x = 5; x <= 14; x++)
            {
                img[x, y] = new Rgb8(255, 0, 0);
            }
        }

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
            var saliency = RegionalContrastSaliency.ComputeSaliency(img, segmentation, enableSmoothing: enableSmoothing, computeBorderRegions: enableSmoothing);

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
        var dummySeg = EgbiSegmentation.Segment(new SequentialMatrix<Rgb8>(1, 1));
        Matrix<DoublePixel> regionalEmpty = RegionalContrastSaliency.ComputeSaliency(emptyImg, dummySeg);
        Assert.Empty(regionalEmpty);
        Assert.Equal(0, regionalEmpty.Width);
        Assert.Equal(0, regionalEmpty.Height);
    }

    /// <summary>
    /// Verifies argument validation for null arguments and dimension mismatches.
    /// </summary>
    [Fact]
    public void ComputeSaliency_Validation_ShouldThrowOnNullOrDimensionMismatch()
    {
        var img = new SequentialMatrix<Rgb8>(10, 10);
        var seg = EgbiSegmentation.Segment(img);

        // Null checks
        Assert.Throws<ArgumentNullException>(() => RegionalContrastSaliency.ComputeSaliency(null!, seg));
        Assert.Throws<ArgumentNullException>(() => RegionalContrastSaliency.ComputeSaliency(img, null!));

        // Dimension mismatch with non-empty image
        var mismatchedSeg = EgbiSegmentation.Segment(new SequentialMatrix<Rgb8>(5, 5));
        Assert.Throws<ArgumentException>(() => RegionalContrastSaliency.ComputeSaliency(img, mismatchedSeg));

        // Non-Matrix IReadOnlyMatrix (MatrixView) input
        var view = new MatrixView<Rgb8>(img, 0, 0, 10, 10, EdgeHandling.EXTEND);
        Matrix<DoublePixel> viewSaliency = RegionalContrastSaliency.ComputeSaliency(view, seg);
        Assert.Equal(img.Size, viewSaliency.Size);
    }
}
