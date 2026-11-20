namespace Leiter.Tests.Algorithms.Segmentation;

using System;
using System.Collections.Generic;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SegmentationVisualizer"/>.
/// </summary>
public class SegmentationVisualizerTests
{
    [Fact]
    public void Assigners_ShouldProduceExpectedColors()
    {
        SequentialMatrix<Rgb8> img = TestMatrices.Constant(4, 2, new Rgb8(10, 20, 30));
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
        ArgumentException emptyEx = Assert.Throws<ArgumentException>(() => avgAssigner(img, new Region<Coord>()));
        Assert.Equal("region", emptyEx.ParamName);

        // IntegerColorAssigner assigns sequential IDs
        var intAssigner = SegmentationVisualizer.IntegerColorAssigner();
        var dummyLong = new SequentialMatrix<LongPixel>(4, 2);
        Assert.Equal(0L, intAssigner(dummyLong, region).Value);
        Assert.Equal(1L, intAssigner(dummyLong, region).Value);
    }

    [Fact]
    public void ColorRegions_ShouldBeNonMutatingAndSupportCustomAndReadOnlyInputs()
    {
        SequentialMatrix<Rgb8> img = TestMatrices.Constant(3, 2, new Rgb8(200, 100, 50));
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
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(img, (IEnumerable<Region<Coord>>)null!, seed: 1));
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(null!, regions, assigner));
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(img, (IEnumerable<Region<Coord>>)null!, assigner));
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(img, regions, (SegmentationVisualizer.ColorAssigner<Rgb8>)null!));
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(img, new Region<Coord>[] { null! }, assigner));
    }

    [Fact]
    public void ColorRegions_WithRegionGraphRegions_ShouldColorRegions()
    {
        (SequentialMatrix<Rgb8> img, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            4,
            4,
            new Rgb8(0, 0, 0),
            new Rgb8(0, 0, 0),
            splitX: 2);
        IReadOnlyList<Region<Coord>> regions = graph.Regions;

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

        // RegionGraph and IDisjointSet overloads
        Matrix<Rgb8> fromGraphCustom = SegmentationVisualizer.ColorRegions(
            img,
            graph,
            (matrix, region) => region.Id == 0 ? new Rgb8(10, 20, 30) : new Rgb8(40, 50, 60));
        Assert.Equal(new Rgb8(10, 20, 30), fromGraphCustom[0, 0]);
        Assert.Equal(new Rgb8(40, 50, 60), fromGraphCustom[3, 3]);

        Matrix<Rgb8> fromGraphSeed = SegmentationVisualizer.ColorRegions(img, graph, seed: 7);
        MatrixAssert.Equal(randomColored, fromGraphSeed);

        DisjointSet<byte> dsu = DisjointSet.CreateSingletons(img);
        dsu.Union(0, 1, 0);
        Matrix<Rgb8> fromDsuSeed = SegmentationVisualizer.ColorRegions(img, dsu, seed: 11);
        Assert.Equal(fromDsuSeed[0, 0], fromDsuSeed[1, 0]);

        Matrix<Rgb8> fromDsuCustom = SegmentationVisualizer.ColorRegions(
            img,
            dsu,
            (_, region) => new Rgb8((byte)region.Pixels.Count, 0, 0));
        Assert.Equal(new Rgb8(2, 0, 0), fromDsuCustom[0, 0]);
        Assert.Equal(new Rgb8(1, 0, 0), fromDsuCustom[3, 3]);

        var wrongSize = new SequentialMatrix<Rgb8>(2, 2);
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(null!, graph));
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(img, (RegionGraph)null!));
        ArgumentException graphSizeEx = Assert.Throws<ArgumentException>(() => SegmentationVisualizer.ColorRegions(wrongSize, graph));
        Assert.Equal("regionGraph", graphSizeEx.ParamName);
        ArgumentException graphCustomSizeEx = Assert.Throws<ArgumentException>(() =>
            SegmentationVisualizer.ColorRegions(wrongSize, graph, SegmentationVisualizer.AverageColorAssigner()));
        Assert.Equal("regionGraph", graphCustomSizeEx.ParamName);
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(null!, dsu));
        Assert.Throws<ArgumentNullException>(() => SegmentationVisualizer.ColorRegions(img, (IDisjointSet)null!));
        Assert.Throws<ArgumentException>(() => SegmentationVisualizer.ColorRegions(wrongSize, dsu));
    }

    [Fact]
    public void ColorRegions_DisjointSetWithMultipleRegions_ShouldBeDeterministicAcrossCalls()
    {
        SequentialMatrix<Rgb8> img = TestMatrices.Constant(4, 4, new Rgb8(100, 100, 100));
        DisjointSet<byte> dsu = DisjointSet.CreateSingletons(img);

        // Merge into 4 distinct multi-pixel regions with non-trivial union order
        dsu.Union(new Coord(0, 0), new Coord(1, 0), 0);
        dsu.Union(new Coord(0, 0), new Coord(0, 1), 0);
        dsu.Union(new Coord(0, 0), new Coord(1, 1), 0);

        dsu.Union(new Coord(2, 0), new Coord(3, 0), 0);
        dsu.Union(new Coord(2, 0), new Coord(2, 1), 0);
        dsu.Union(new Coord(2, 0), new Coord(3, 1), 0);

        dsu.Union(new Coord(1, 3), new Coord(0, 3), 0);
        dsu.Union(new Coord(1, 3), new Coord(1, 2), 0);
        dsu.Union(new Coord(1, 3), new Coord(0, 2), 0);

        dsu.Union(new Coord(3, 3), new Coord(2, 3), 0);
        dsu.Union(new Coord(3, 3), new Coord(3, 2), 0);
        dsu.Union(new Coord(3, 3), new Coord(2, 2), 0);

        Matrix<Rgb8> baseline = SegmentationVisualizer.ColorRegions(img, dsu, seed: 11);
        for (int i = 0; i < 5; i++)
        {
            Matrix<Rgb8> repeated = SegmentationVisualizer.ColorRegions(img, dsu, seed: 11);
            MatrixAssert.Equal(baseline, repeated);
        }

        var longImg = new SequentialMatrix<LongPixel>(img.Size);
        Matrix<LongPixel> firstIds = SegmentationVisualizer.ColorRegions(longImg, dsu, SegmentationVisualizer.IntegerColorAssigner());
        Matrix<LongPixel> secondIds = SegmentationVisualizer.ColorRegions(longImg, dsu, SegmentationVisualizer.IntegerColorAssigner());
        MatrixAssert.Equal(firstIds, secondIds);

        Assert.Equal(0L, firstIds[0, 0].Value);
        Assert.Equal(1L, firstIds[2, 0].Value);
        Assert.Equal(2L, firstIds[0, 2].Value);
        Assert.Equal(3L, firstIds[2, 2].Value);
    }

    [Fact]
    public void ColorRegions_RegionGraphAndDisjointSetOverloads_NullAssignerAndEmptyMatrices_ShouldBehaveCorrectly()
    {
        var img = new SequentialMatrix<Rgb8>(2, 2);
        DisjointSet<byte> dsu = DisjointSet.CreateSingletons(img);
        RegionGraph graph = RegionGraph.FromDisjointSet(dsu);

        ArgumentNullException graphNullAssigner = Assert.Throws<ArgumentNullException>(() =>
            SegmentationVisualizer.ColorRegions(img, graph, (SegmentationVisualizer.ColorAssigner<Rgb8>)null!));
        Assert.Equal("colorAssigner", graphNullAssigner.ParamName);

        ArgumentNullException dsuNullAssigner = Assert.Throws<ArgumentNullException>(() =>
            SegmentationVisualizer.ColorRegions(img, dsu, (SegmentationVisualizer.ColorAssigner<Rgb8>)null!));
        Assert.Equal("colorAssigner", dsuNullAssigner.ParamName);

        Assert.Throws<ArgumentNullException>(() =>
            SegmentationVisualizer.ColorRegions(null!, graph, SegmentationVisualizer.AverageColorAssigner()));
        Assert.Throws<ArgumentNullException>(() =>
            SegmentationVisualizer.ColorRegions(img, (RegionGraph)null!, SegmentationVisualizer.AverageColorAssigner()));
        ArgumentNullException dsuNullImgEx = Assert.Throws<ArgumentNullException>(() =>
            SegmentationVisualizer.ColorRegions(null!, dsu, SegmentationVisualizer.AverageColorAssigner()));
        Assert.Equal("image", dsuNullImgEx.ParamName);
        ArgumentNullException dsuNullSegEx = Assert.Throws<ArgumentNullException>(() =>
            SegmentationVisualizer.ColorRegions(img, (IDisjointSet)null!, SegmentationVisualizer.AverageColorAssigner()));
        Assert.Equal("segmentation", dsuNullSegEx.ParamName);

        // Complete partition overloads must not clone/read image pixels unless the assigner inspects them.
        var throwingMatrix = new ThrowingReadOnlyMatrix(2, 2);
        Matrix<Rgb8> graphNoClone = SegmentationVisualizer.ColorRegions(throwingMatrix, graph, seed: 7);
        Matrix<Rgb8> dsuNoClone = SegmentationVisualizer.ColorRegions(throwingMatrix, dsu, seed: 7);
        MatrixAssert.Equal(graphNoClone, dsuNoClone);

        var emptyImg = new SequentialMatrix<Rgb8>(0, 0);
        DisjointSet<byte> emptyDsu = DisjointSet.CreateSingletons(emptyImg);
        RegionGraph emptyGraph = RegionGraph.FromDisjointSet(emptyDsu);
        bool assignerCalled = false;
        SegmentationVisualizer.ColorAssigner<Rgb8> trackingAssigner = (_, _) =>
        {
            assignerCalled = true;
            return new Rgb8(1, 2, 3);
        };

        Matrix<Rgb8> emptyFromGraphSeed = SegmentationVisualizer.ColorRegions(emptyImg, emptyGraph, seed: 11);
        Matrix<Rgb8> emptyFromGraphAssigner = SegmentationVisualizer.ColorRegions(emptyImg, emptyGraph, trackingAssigner);
        Matrix<Rgb8> emptyFromDsuSeed = SegmentationVisualizer.ColorRegions(emptyImg, emptyDsu, seed: 11);
        Matrix<Rgb8> emptyFromDsuAssigner = SegmentationVisualizer.ColorRegions(emptyImg, emptyDsu, trackingAssigner);

        Assert.Empty(emptyFromGraphSeed);
        Assert.Equal(new Size(0, 0), emptyFromGraphSeed.Size);
        Assert.Empty(emptyFromGraphAssigner);
        Assert.Equal(new Size(0, 0), emptyFromGraphAssigner.Size);
        Assert.Empty(emptyFromDsuSeed);
        Assert.Equal(new Size(0, 0), emptyFromDsuSeed.Size);
        Assert.Empty(emptyFromDsuAssigner);
        Assert.Equal(new Size(0, 0), emptyFromDsuAssigner.Size);
        Assert.False(assignerCalled);
    }

    [Fact]
    public void AverageColorAssigner_Generic_ShouldSupportRgb8Rgb64Lab32AndThrowOnEmptyRegions()
    {
        // Rgb8 generic assigner: accurate over >10 pixels (sum > 255) and throws ArgumentException on empty region
        SequentialMatrix<Rgb8> rgb8Img = TestMatrices.Create(
            4,
            4,
            (x, y) => ((x + y) % 2 == 0) ? new Rgb8(180, 200, 220) : new Rgb8(220, 240, 240));
        var allCoords = new List<Coord>();
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
                allCoords.Add(new Coord(x, y));
        }

        var fullRegion = new Region<Coord>(allCoords) { Id = 0 };
        var emptyRegion = new Region<Coord>() { Id = 1 };

        SegmentationVisualizer.ColorAssigner<Rgb8> rgb8Assigner = SegmentationVisualizer.AverageColorAssigner<Rgb8>();
        Assert.Equal(new Rgb8(200, 220, 230), rgb8Assigner(rgb8Img, fullRegion));
        ArgumentException rgb8EmptyEx = Assert.Throws<ArgumentException>(() => rgb8Assigner(rgb8Img, emptyRegion));
        Assert.Equal("region", rgb8EmptyEx.ParamName);
        Assert.Throws<ArgumentNullException>(() => rgb8Assigner(null!, fullRegion));
        Assert.Throws<ArgumentNullException>(() => rgb8Assigner(rgb8Img, null!));

        // Exact Rgb8 rounding parity: (1, 32) -> (1 + 32) / 2 = 16.5 -> 17 (no /255.0 * 255.0 drift)
        var pairImg = new SequentialMatrix<Rgb8>(2, 1);
        pairImg[0, 0] = new Rgb8(1, 1, 1);
        pairImg[1, 0] = new Rgb8(32, 32, 32);
        var pairRegion = new Region<Coord>([new Coord(0, 0), new Coord(1, 0)]) { Id = 0 };
        Assert.Equal(new Rgb8(17, 17, 17), SegmentationVisualizer.AverageColorAssigner()(pairImg, pairRegion));
        Assert.Equal(new Rgb8(17, 17, 17), rgb8Assigner(pairImg, pairRegion));

        Matrix<Rgb8> rgb8Colored = SegmentationVisualizer.ColorRegions(rgb8Img, [fullRegion], rgb8Assigner);
        Assert.Equal(new Rgb8(200, 220, 230), rgb8Colored[0, 0]);
        Assert.Equal(new Rgb8(200, 220, 230), rgb8Colored[3, 3]);

        // Rgb64 generic assigner
        var rgb64Img = new SequentialMatrix<Rgb64>(2, 1);
        rgb64Img[0, 0] = new Rgb64(0.2, 0.4, 0.6);
        rgb64Img[1, 0] = new Rgb64(0.6, 0.8, 1.0);
        var twoPixelRegion = new Region<Coord>([new Coord(0, 0), new Coord(1, 0)]) { Id = 0 };

        SegmentationVisualizer.ColorAssigner<Rgb64> rgb64Assigner = SegmentationVisualizer.AverageColorAssigner<Rgb64>();
        Rgb64 meanRgb64 = rgb64Assigner(rgb64Img, twoPixelRegion);
        Assert.Equal(0.4, meanRgb64.R, 10);
        Assert.Equal(0.6, meanRgb64.G, 10);
        Assert.Equal(0.8, meanRgb64.B, 10);
        ArgumentException rgb64EmptyEx = Assert.Throws<ArgumentException>(() => rgb64Assigner(rgb64Img, emptyRegion));
        Assert.Equal("region", rgb64EmptyEx.ParamName);

        Matrix<Rgb64> rgb64Colored = SegmentationVisualizer.ColorRegions(rgb64Img, [twoPixelRegion], rgb64Assigner);
        Assert.Equal(0.4, rgb64Colored[1, 0].R, 10);

        // Lab32 generic assigner across >10 pixels including negative chromaticity channels
        SequentialMatrix<Lab32> labImg = TestMatrices.Create(
            4,
            4,
            (x, y) => ((x + y) % 2 == 0)
                ? new Lab32(30.0f, -30.0f, 15.0f)
                : new Lab32(70.0f, 10.0f, -5.0f));

        SegmentationVisualizer.ColorAssigner<Lab32> labAssigner = SegmentationVisualizer.AverageColorAssigner<Lab32>();
        Lab32 meanLab = labAssigner(labImg, fullRegion);
        Assert.Equal(50.0f, meanLab.L, 5);
        Assert.Equal(-10.0f, meanLab.A, 5);
        Assert.Equal(5.0f, meanLab.B, 5);
        ArgumentException labEmptyEx = Assert.Throws<ArgumentException>(() => labAssigner(labImg, emptyRegion));
        Assert.Equal("region", labEmptyEx.ParamName);

        Matrix<Lab32> labColored = SegmentationVisualizer.ColorRegions(labImg, [fullRegion], labAssigner);
        Assert.Equal(50.0f, labColored[2, 3].L, 5);
        Assert.Equal(-10.0f, labColored[2, 3].A, 5);
        Assert.Equal(5.0f, labColored[2, 3].B, 5);
    }
}
