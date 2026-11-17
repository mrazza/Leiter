
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.DataStructures;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Leiter.Tests.Algorithms.Segmentation;

/// <summary>
/// Provides unit tests for <see cref="EgbiSegmentation" />.
/// </summary>
public class EgbiSegmentationTests
{
    [Fact]
    public void Segment_ShouldPartitionImage()
    {
        // Setup a simple 10x10 Rgb8 image with a distinct left and right side
        var img = new SequentialMatrix<Rgb8>(10, 10);
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                if (x < 5)
                    img[x, y] = new Rgb8(255, 0, 0); // Red
                else
                    img[x, y] = new Rgb8(0, 0, 255); // Blue
            }
        }

        // Run EGBI segmentation
        // Choose KFactor = 5.0, Epsilon = 1.0, MinRegionSize = 20
        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 5.0, Epsilon: 1.0, MinRegionSize: 20));

        Assert.Equal(10, segmentation.Width);
        Assert.Equal(10, segmentation.Height);

        // We should have exactly two segments since the red side has 50 pixels (> 20) and blue side has 50 pixels (> 20)
        var regions = segmentation.ToRegions();
        Assert.Equal(2, regions.Count);

        // Verify colors are grouped together
        var leftRoot = segmentation[0, 0];
        var rightRoot = segmentation[9, 9];
        Assert.NotEqual(leftRoot.Value, rightRoot.Value);

        Assert.Equal(leftRoot, segmentation[4, 4]);
        Assert.Equal(rightRoot, segmentation[5, 5]);
    }

    /// <summary>
    /// Verifies that segmenting an image with many equal-weight edges produces deterministic results across runs.
    /// </summary>
    [Fact]
    public void Segment_EqualWeightGradient_ShouldBeDeterministicAcrossRuns()
    {
        const int width = 30;
        const int height = 30;
        var img = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte value = (byte)((x + y) * 4);
                img[x, y] = new Rgb8(value, value, value);
            }
        }

        var baselineOptions = new EgbiOptions(KFactor: 5.0, Epsilon: 0.0, MinRegionSize: 15);
        var baselineSegmentation = EgbiSegmentation.Segment(img, baselineOptions);
        Assert.True(baselineSegmentation.ToRegions().Count > 10);
        Matrix<LongPixel> baseline = baselineSegmentation.ToMatrix();

        for (int run = 0; run < 5; run++)
        {
            Matrix<LongPixel> current = EgbiSegmentation.Segment(img, baselineOptions).ToMatrix();
            Assert.Equal(baseline.Width, current.Width);
            Assert.Equal(baseline.Height, current.Height);
            for (int i = 0; i < baseline.Count; i++)
            {
                Assert.Equal(baseline[i].Value, current[i].Value);
            }
        }
    }

    [Fact]
    public void ColorAssigners_AndColorImageBySegmentation_ShouldWork()
    {
        var img = new SequentialMatrix<Rgb8>(10, 10);
        img.SetAll(new Rgb8(255, 0, 0));

        var segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 10.0, MinRegionSize: 10));
        var regions = segmentation.ToRegions();

        // 1. RandomColorAssigner
        var randomAssigner = EgbiSegmentation.RandomColorAssigner(randomSeed: 42);
        var randomCol = randomAssigner(img, regions.First());
        Assert.NotEqual(new Rgb8(0, 0, 0), randomCol);

        // 2. AverageColorAssigner
        var avgAssigner = EgbiSegmentation.AverageColorAssigner();
        var avgCol = avgAssigner(img, regions.First());
        Assert.Equal(new Rgb8(255, 0, 0), avgCol);

        // AverageColorAssigner empty region should throw
        Assert.Throws<ArgumentException>(() => avgAssigner(img, new Region<Coord>()));

        // 3. IntegerColorAssigner
        var intAssigner = EgbiSegmentation.IntegerColorAssigner();
        // IntegerColorAssigner takes IReadOnlyMatrix<LongPixel> as image parameter!
        var dummyLongMatrix = new SequentialMatrix<LongPixel>(10, 10);
        var intCol = intAssigner(dummyLongMatrix, new Region<Coord>(regions.First().Pixels));
        Assert.Equal(0L, intCol.Value);

        // ColorImageBySegmentation
        EgbiSegmentation.ColorImageBySegmentation(img, regions);
        // All pixels colored by segmentation
        Assert.NotEqual(new Rgb8(255, 0, 0), img[0, 0]);
    }

    [Fact]
    public void Segment_EdgeCasesAndValidation_ShouldBehaveCorrectly()
    {
        Assert.Throws<ArgumentNullException>(() => EgbiSegmentation.Segment<Rgb8>(null!));

        var emptyImg = new SequentialMatrix<Rgb8>(0, 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => EgbiSegmentation.Segment(emptyImg, new EgbiOptions(KFactor: -1.0)));

        IDisjointSet emptySeg = EgbiSegmentation.Segment(emptyImg);
        Assert.Equal(0, emptySeg.Count);
        Assert.Equal(0, emptySeg.Width);
        Assert.Equal(0, emptySeg.Height);
        Assert.Empty(emptySeg.ToRegions());

        // Single high-contrast pixel surrounded by black pixels stays separate when MinRegionSize is 0,
        // and merges into the background when MinRegionSize > 1.
        var speckled = new SequentialMatrix<Rgb8>(6, 6);
        speckled.SetAll(new Rgb8(0, 0, 0));
        speckled[3, 3] = new Rgb8(255, 255, 255);

        IDisjointSet withoutPostMerge = EgbiSegmentation.Segment(speckled, new EgbiOptions(KFactor: 10.0, Epsilon: 1.0, MinRegionSize: 0));
        Assert.Equal(2, withoutPostMerge.ToRegions().Count);
        Assert.NotEqual(withoutPostMerge[0, 0].Value, withoutPostMerge[3, 3].Value);

        IDisjointSet withPostMerge = EgbiSegmentation.Segment(speckled, new EgbiOptions(KFactor: 10.0, Epsilon: 1.0, MinRegionSize: 5));
        Assert.Single(withPostMerge.ToRegions());
    }

    /// <summary>
    /// Verifies that <see cref="EgbiOptions.Connectivity"/> switches between 4-connected and 8-connected diagonal merging.
    /// </summary>
    [Fact]
    public void Segment_FourConnectedVsEightConnected_ShouldControlDiagonalMerging()
    {
        var checkerboard = new SequentialMatrix<Rgb8>(2, 2);
        checkerboard[0, 0] = new Rgb8(255, 0, 0);
        checkerboard[1, 1] = new Rgb8(255, 0, 0);
        checkerboard[1, 0] = new Rgb8(0, 0, 255);
        checkerboard[0, 1] = new Rgb8(0, 0, 255);

        IDisjointSet fourConnected = EgbiSegmentation.Segment(
            checkerboard,
            new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 0, Connectivity: GridConnectivity.FourConnected));
        Assert.Equal(4, fourConnected.ToRegions().Count);

        IDisjointSet eightConnected = EgbiSegmentation.Segment(
            checkerboard,
            new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 0, Connectivity: GridConnectivity.EightConnected));
        Assert.Equal(2, eightConnected.ToRegions().Count);
    }

    /// <summary>
    /// Verifies that <see cref="EgbiSegmentation.ColorImageBySegmentation{T}(Matrix{T}, IEnumerable{Region{Coord}}, EgbiSegmentation.ColorAssigner{T})"/> accepts <see cref="RegionGraph.Regions"/>.
    /// </summary>
    [Fact]
    public void ColorImageBySegmentation_WithRegionGraphRegions_ShouldColorImage()
    {
        var labels = new SequentialMatrix<int>(4, 4);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                labels[x, y] = x < 2 ? 0 : 1;
            }
        }

        var graph = RegionGraph.FromLabels(labels);
        IReadOnlyList<Region<Coord>> regions = graph.Regions;

        var img = new SequentialMatrix<Rgb8>(4, 4);
        img.SetAll(new Rgb8(0, 0, 0));

        EgbiSegmentation.ColorImageBySegmentation(
            img,
            regions,
            (matrix, region) => region.Id == 0 ? new Rgb8(255, 0, 0) : new Rgb8(0, 255, 0));

        Assert.Equal(new Rgb8(255, 0, 0), img[0, 0]);
        Assert.Equal(new Rgb8(255, 0, 0), img[1, 3]);
        Assert.Equal(new Rgb8(0, 255, 0), img[2, 0]);
        Assert.Equal(new Rgb8(0, 255, 0), img[3, 3]);

        // Also test the Rgb8 overload with RandomColorAssigner default
        EgbiSegmentation.ColorImageBySegmentation(img, regions);
        Assert.NotEqual(new Rgb8(0, 0, 0), img[0, 0]);
        Assert.NotEqual(new Rgb8(0, 0, 0), img[3, 3]);
    }
}
