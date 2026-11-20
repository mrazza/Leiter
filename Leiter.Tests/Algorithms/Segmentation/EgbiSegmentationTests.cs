
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.DataStructures;
using Leiter.Tests.TestUtils;
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
        // Setup a simple 10x10 Rgb8 image with a distinct left and right side (Red vs Blue)
        var img = TestMatrices.VerticalSplit(10, 10, new Rgb8(255, 0, 0), new Rgb8(0, 0, 255));

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
            MatrixAssert.Equal(baseline, current);
        }
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
}
