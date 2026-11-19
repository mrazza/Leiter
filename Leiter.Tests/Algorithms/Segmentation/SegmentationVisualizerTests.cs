namespace Leiter.Tests.Algorithms.Segmentation;

using System;
using System.Collections.Generic;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SegmentationVisualizer"/>.
/// </summary>
public class SegmentationVisualizerTests
{
    [Fact]
    public void Assigners_ShouldProduceExpectedColors()
    {
        var img = new SequentialMatrix<Rgb8>(4, 2);
        img.SetAll(new Rgb8(10, 20, 30));
        img[0, 0] = new Rgb8(20, 40, 60);
        img[1, 0] = new Rgb8(40, 60, 80);

        var region = new Region<Coord>([new Coord(0, 0), new Coord(1, 0)]) { Id = 0 };

        // RandomColorAssigner is deterministic per seed and can produce full [0, 255] values
        var randomA = SegmentationVisualizer.RandomColorAssigner(seed: 42);
        var randomB = SegmentationVisualizer.RandomColorAssigner(seed: 42);
        Assert.Equal(randomA(img, region), randomB(img, region));

        // AverageColorAssigner computes rounded mean and throws on empty region
        var avgAssigner = SegmentationVisualizer.AverageColorAssigner();
        Assert.Equal(new Rgb8(30, 50, 70), avgAssigner(img, region));
        Assert.Throws<ArgumentException>(() => avgAssigner(img, new Region<Coord>()));

        // IntegerColorAssigner assigns sequential IDs
        var intAssigner = SegmentationVisualizer.IntegerColorAssigner();
        var dummyLong = new SequentialMatrix<LongPixel>(4, 2);
        Assert.Equal(0L, intAssigner(dummyLong, region).Value);
        Assert.Equal(1L, intAssigner(dummyLong, region).Value);
    }

    [Fact]
    public void ColorRegions_ShouldBeNonMutatingAndSupportCustomAndReadOnlyInputs()
    {
        var img = new SequentialMatrix<Rgb8>(3, 2);
        img.SetAll(new Rgb8(200, 100, 50));
        img[0, 0] = new Rgb8(10, 20, 30);
        img[1, 0] = new Rgb8(20, 30, 40);

        var region = new Region<Coord>([new Coord(0, 0), new Coord(1, 0)]) { Id = 0 };
        var regions = new[] { region };

        var view = new MatrixView<Rgb8>(img, 0, 0, 3, 2, EdgeHandling.Extend);
        Matrix<Rgb8> averaged = SegmentationVisualizer.ColorRegions(view, regions, SegmentationVisualizer.AverageColorAssigner());

        // Original image must not be mutated
        Assert.Equal(new Rgb8(10, 20, 30), img[0, 0]);
        Assert.Equal(new Rgb8(20, 30, 40), img[1, 0]);

        // Region pixels updated; uncovered pixels preserved from source
        Assert.Equal(new Rgb8(15, 25, 35), averaged[0, 0]);
        Assert.Equal(new Rgb8(15, 25, 35), averaged[1, 0]);
        Assert.Equal(new Rgb8(200, 100, 50), averaged[2, 1]);

        Matrix<Rgb8> randomized = SegmentationVisualizer.ColorRegions(img, regions, seed: 123);
        Assert.Equal(randomized[0, 0], randomized[1, 0]);
        Assert.Equal(new Rgb8(200, 100, 50), randomized[2, 1]);
    }

    [Fact]
    public void ColorRegions_NullArguments_ShouldThrowArgumentNullException()
    {
        var img = new SequentialMatrix<Rgb8>(2, 2);
        Region<Coord>[] regions = [new Region<Coord>([new Coord(0, 0)])];
        var assigner = SegmentationVisualizer.AverageColorAssigner();

        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(null!, regions, seed: 1));
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(img, null!, seed: 1));
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(null!, regions, assigner));
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(img, null!, assigner));
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(img, regions, (SegmentationVisualizer.ColorAssigner<Rgb8>)null!));
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(img, new Region<Coord>[] { null! }, assigner));
    }

    [Fact]
    public void ColorRegions_WithRegionGraphRegions_ShouldColorRegions()
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

        Matrix<Rgb8> colored = SegmentationVisualizer.ColorRegions(
            img,
            regions,
            (matrix, region) => region.Id == 0 ? new Rgb8(255, 0, 0) : new Rgb8(0, 255, 0));

        Assert.Equal(new Rgb8(255, 0, 0), colored[0, 0]);
        Assert.Equal(new Rgb8(255, 0, 0), colored[1, 3]);
        Assert.Equal(new Rgb8(0, 255, 0), colored[2, 0]);
        Assert.Equal(new Rgb8(0, 255, 0), colored[3, 3]);

        Matrix<Rgb8> randomColored = SegmentationVisualizer.ColorRegions(img, regions, seed: 7);
        Assert.Equal(randomColored[0, 0], randomColored[1, 3]);
        Assert.Equal(randomColored[2, 0], randomColored[3, 3]);
    }
}
