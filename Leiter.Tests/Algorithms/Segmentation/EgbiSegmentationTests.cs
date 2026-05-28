
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.DataStructures;
using System;
using System.Collections.Immutable;
using System.Linq;

namespace Leiter.Tests.Algorithms.Segmentation;

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
        // Choose kFactor = 5.0, epsilon = 1.0, minSegmentSize = 20
        var segmentation = EgbiSegmentation.Segment(img, kFactor: 5.0, epsilon: 1.0, minSegmentSize: 20);

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

    [Fact]
    public void ColorAssigners_AndColorImageBySegmentation_ShouldWork()
    {
        var img = new SequentialMatrix<Rgb8>(10, 10);
        img.SetAll(new Rgb8(255, 0, 0));

        var segmentation = EgbiSegmentation.Segment(img, kFactor: 1.0, epsilon: 10.0, minSegmentSize: 10);
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
}
