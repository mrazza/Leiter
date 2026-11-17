namespace Leiter.Tests.Algorithms.DataStructures;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="RegionGraph"/>.
/// </summary>
public class RegionGraphTests
{
    /// <summary>
    /// Verifies that <see cref="RegionGraph.FromLabels"/> remaps arbitrary negative and non-contiguous labels deterministically in row-major first-seen order.
    /// </summary>
    [Fact]
    public void FromLabels_ArbitraryNegativeAndNonContiguousLabels_ShouldRemapDeterministicallyInRowMajorOrder()
    {
        var labels = new SequentialMatrix<int>(3, 2);
        // Row 0: -100, -100, 42
        labels[0, 0] = -100;
        labels[1, 0] = -100;
        labels[2, 0] = 42;
        // Row 1: 500, 42, -100
        labels[0, 1] = 500;
        labels[1, 1] = 42;
        labels[2, 1] = -100;

        var graph = RegionGraph.FromLabels(labels);

        Assert.Equal(3, graph.RegionCount);
        Assert.Equal(3, graph.Width);
        Assert.Equal(2, graph.Height);
        Assert.Equal(new Size(3, 2), graph.Size);
        Assert.Equal(GridConnectivity.EightConnected, graph.Connectivity);

        // Remapping order: -100 -> 0, 42 -> 1, 500 -> 2
        Assert.Equal(0, graph.Labels[0, 0]);
        Assert.Equal(0, graph.Labels[1, 0]);
        Assert.Equal(1, graph.Labels[2, 0]);
        Assert.Equal(2, graph.Labels[0, 1]);
        Assert.Equal(1, graph.Labels[1, 1]);
        Assert.Equal(0, graph.Labels[2, 1]);

        // Region pixel lists
        Assert.Equal(0, graph.Regions[0].Id);
        Assert.Equal(3, graph.Regions[0].Pixels.Count);
        Assert.Contains(new Coord(0, 0), graph.Regions[0].Pixels);
        Assert.Contains(new Coord(1, 0), graph.Regions[0].Pixels);
        Assert.Contains(new Coord(2, 1), graph.Regions[0].Pixels);

        Assert.Equal(1, graph.Regions[1].Id);
        Assert.Equal(2, graph.Regions[1].Pixels.Count);
        Assert.Contains(new Coord(2, 0), graph.Regions[1].Pixels);
        Assert.Contains(new Coord(1, 1), graph.Regions[1].Pixels);

        Assert.Equal(2, graph.Regions[2].Id);
        Assert.Single(graph.Regions[2].Pixels);
        Assert.Contains(new Coord(0, 1), graph.Regions[2].Pixels);
    }

    /// <summary>
    /// Verifies that <see cref="RegionGraph.FromDisjointSet"/> produces expected dense labels and region counts from a segmented image.
    /// </summary>
    [Fact]
    public void FromDisjointSet_ShouldConstructGraphWithDenseLabels()
    {
        var img = new SequentialMatrix<Rgb8>(4, 4);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                img[x, y] = x < 2 ? new Rgb8(255, 0, 0) : new Rgb8(0, 255, 0);
            }
        }

        IDisjointSet segmentation = EgbiSegmentation.Segment(img, new EgbiOptions(KFactor: 1.0, Epsilon: 1.0, MinRegionSize: 4));
        var graph = RegionGraph.FromDisjointSet(segmentation);

        Assert.Equal(2, graph.RegionCount);
        Assert.Equal(4, graph.Width);
        Assert.Equal(4, graph.Height);
        Assert.Equal(8, graph.Regions[0].Pixels.Count);
        Assert.Equal(8, graph.Regions[1].Pixels.Count);
    }

    /// <summary>
    /// Verifies that diagonal neighbor adjacency differentiates <see cref="GridConnectivity.FourConnected"/> from <see cref="GridConnectivity.EightConnected"/>.
    /// </summary>
    [Fact]
    public void Connectivity_FourConnectedVsEightConnected_DiagonalAdjacencyDifferences()
    {
        var labels = new SequentialMatrix<int>(3, 3);
        // [0, 1, 1]
        // [1, 2, 1]
        // [1, 1, 1]
        labels.SetAll(1);
        labels[0, 0] = 0;
        labels[1, 1] = 2;

        var graph4 = RegionGraph.FromLabels(labels, GridConnectivity.FourConnected);
        var graph8 = RegionGraph.FromLabels(labels, GridConnectivity.EightConnected);

        Assert.Equal(GridConnectivity.FourConnected, graph4.Connectivity);
        Assert.Equal(GridConnectivity.EightConnected, graph8.Connectivity);

        // Under 4-connectivity, (0,0) [id 0] and (1,1) [id 2] do not share an edge
        Assert.False(graph4.AreNeighbors(0, 2));
        Assert.False(graph4.AreNeighbors(2, 0));
        Assert.True(graph4.AreNeighbors(0, 1));
        Assert.True(graph4.AreNeighbors(2, 1));

        // Under 8-connectivity, (0,0) [id 0] and (1,1) [id 2] are diagonal neighbors
        Assert.True(graph8.AreNeighbors(0, 2));
        Assert.True(graph8.AreNeighbors(2, 0));
        Assert.True(graph8.AreNeighbors(0, 1));
        Assert.True(graph8.AreNeighbors(2, 1));
    }

    /// <summary>
    /// Verifies that shared boundary border pixels are deduplicated and boundaries are sorted deterministically.
    /// </summary>
    [Fact]
    public void Boundaries_DeduplicationAndRowMajorSorting()
    {
        var labels = new SequentialMatrix<int>(4, 2);
        // Row 0: 0, 0, 1, 1
        // Row 1: 0, 0, 1, 1
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                labels[x, y] = x < 2 ? 0 : 1;
            }
        }

        var graph = RegionGraph.FromLabels(labels);

        Assert.Single(graph.Boundaries);
        RegionBoundary boundary = graph.Boundaries[0];
        Assert.Equal(0, boundary.First);
        Assert.Equal(1, boundary.Second);

        // Border pixels must be unique and in row-major order
        Assert.Equal(2, boundary.FirstBorderPixels.Length);
        Assert.Equal(new Coord(1, 0), boundary.FirstBorderPixels.Span[0]);
        Assert.Equal(new Coord(1, 1), boundary.FirstBorderPixels.Span[1]);

        Assert.Equal(2, boundary.SecondBorderPixels.Length);
        Assert.Equal(new Coord(2, 0), boundary.SecondBorderPixels.Span[0]);
        Assert.Equal(new Coord(2, 1), boundary.SecondBorderPixels.Span[1]);

        // Multi-boundary sorting verification
        var multiLabels = new SequentialMatrix<int>(3, 1);
        multiLabels[0, 0] = 0;
        multiLabels[1, 0] = 1;
        multiLabels[2, 0] = 2;
        var multiGraph = RegionGraph.FromLabels(multiLabels);

        Assert.Equal(2, multiGraph.Boundaries.Count);
        Assert.Equal(0, multiGraph.Boundaries[0].First);
        Assert.Equal(1, multiGraph.Boundaries[0].Second);
        Assert.Equal(1, multiGraph.Boundaries[1].First);
        Assert.Equal(2, multiGraph.Boundaries[1].Second);
    }

    /// <summary>
    /// Verifies that <see cref="RegionGraph.GetNeighborIds"/> returns ascending sorted IDs and positionally aligns with <see cref="RegionGraph.GetBoundaryIndices"/>.
    /// </summary>
    [Fact]
    public void Adjacency_GetNeighborIds_And_GetBoundaryIndices_Alignment()
    {
        var labels = new SequentialMatrix<int>(3, 3);
        int id = 0;
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                labels[x, y] = id++;
            }
        }

        var graph = RegionGraph.FromLabels(labels);

        for (int r = 0; r < graph.RegionCount; r++)
        {
            ReadOnlySpan<int> neighbors = graph.GetNeighborIds(r);
            ReadOnlySpan<int> bIndices = graph.GetBoundaryIndices(r);

            Assert.Equal(neighbors.Length, bIndices.Length);

            // Neighbors must be strictly ascending
            for (int i = 1; i < neighbors.Length; i++)
            {
                Assert.True(neighbors[i] > neighbors[i - 1]);
            }

            // Boundary indices must point to valid boundaries linking r and neighbor
            for (int i = 0; i < neighbors.Length; i++)
            {
                int neighborId = neighbors[i];
                int bIndex = bIndices[i];
                RegionBoundary boundary = graph.Boundaries[bIndex];

                Assert.True(boundary.IsIncidentTo(r));
                Assert.Equal(neighborId, boundary.GetOther(r));
            }
        }
    }

    /// <summary>
    /// Verifies neighbor query behaviors including self-comparison, true neighbors, non-neighbors, and degree asymmetry.
    /// </summary>
    [Fact]
    public void AreNeighbors_SelfNonNeighborsAndDegreeAsymmetry()
    {
        var labels = new SequentialMatrix<int>(3, 3);
        int id = 0;
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                labels[x, y] = id++;
            }
        }

        var graph = RegionGraph.FromLabels(labels);

        // Self adjacency is always false
        Assert.False(graph.AreNeighbors(0, 0));
        Assert.False(graph.AreNeighbors(4, 4));

        // True neighbors regardless of argument order
        Assert.True(graph.AreNeighbors(0, 1));
        Assert.True(graph.AreNeighbors(1, 0));

        // Non-neighbors (opposite corners in 3x3)
        Assert.False(graph.AreNeighbors(0, 8));
        Assert.False(graph.AreNeighbors(8, 0));

        // Degree asymmetry: corner region 0 has 3 neighbors, center region 4 has 8 neighbors
        ReadOnlySpan<int> cornerNeighbors = graph.GetNeighborIds(0);
        ReadOnlySpan<int> centerNeighbors = graph.GetNeighborIds(4);
        Assert.Equal(3, cornerNeighbors.Length);
        Assert.Equal(8, centerNeighbors.Length);

        // Tests both length comparison branches (corner <= center and center > corner)
        Assert.True(graph.AreNeighbors(0, 4));
        Assert.True(graph.AreNeighbors(4, 0));
    }

    /// <summary>
    /// Verifies that <see cref="RegionGraph.BuildWeightedEdges"/> converts boundaries to graph edges correctly.
    /// </summary>
    [Fact]
    public void BuildWeightedEdges_ShouldProjectBoundariesIntoEdges()
    {
        var labels = new SequentialMatrix<int>(3, 1);
        labels[0, 0] = 0;
        labels[1, 0] = 1;
        labels[2, 0] = 2;
        var graph = RegionGraph.FromLabels(labels);

        UndirectedGraphEdge<int>[] edges = graph.BuildWeightedEdges(b => (b.First * 10.0) + b.Second);

        Assert.Equal(2, edges.Length);
        Assert.Equal(0, edges[0].First);
        Assert.Equal(1, edges[0].Second);
        Assert.Equal(1.0, edges[0].Weight);

        Assert.Equal(1, edges[1].First);
        Assert.Equal(2, edges[1].Second);
        Assert.Equal(12.0, edges[1].Weight);

        Assert.Throws<ArgumentNullException>(() => graph.BuildWeightedEdges(null!));
    }

    /// <summary>
    /// Verifies that <see cref="RegionGraph.ComputeRegionStatistics{TStat}"/> computes per-region values in parallel.
    /// </summary>
    [Fact]
    public void ComputeRegionStatistics_ShouldComputePerRegionValues()
    {
        var labels = new SequentialMatrix<int>(4, 2);
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                labels[x, y] = x < 2 ? 0 : 1;
            }
        }

        var graph = RegionGraph.FromLabels(labels);

        int[] pixelCounts = graph.ComputeRegionStatistics(r => r.Pixels.Count);

        Assert.Equal(2, pixelCounts.Length);
        Assert.Equal(4, pixelCounts[0]);
        Assert.Equal(4, pixelCounts[1]);

        Assert.Throws<ArgumentNullException>(() => graph.ComputeRegionStatistics<int>(null!));
    }

    /// <summary>
    /// Verifies that a single-region graph has zero boundaries and empty adjacency spans.
    /// </summary>
    [Fact]
    public void SingleRegion_ShouldHaveNoBoundaries()
    {
        var labels = new SequentialMatrix<int>(5, 5);
        labels.SetAll(10);

        var graph = RegionGraph.FromLabels(labels);

        Assert.Equal(1, graph.RegionCount);
        Assert.Equal(25, graph.Regions[0].Pixels.Count);
        Assert.Empty(graph.Boundaries);
        Assert.Equal(0, graph.GetNeighborIds(0).Length);
        Assert.Equal(0, graph.GetBoundaryIndices(0).Length);
        Assert.False(graph.AreNeighbors(0, 0));
    }

    /// <summary>
    /// Verifies that 0x0 empty inputs produce clean empty graphs without throwing exceptions.
    /// </summary>
    [Fact]
    public void EmptyGraph_ShouldBehaveCorrectly()
    {
        var emptyLabels = new SequentialMatrix<int>(0, 0);
        var graphFromLabels = RegionGraph.FromLabels(emptyLabels);

        Assert.Equal(0, graphFromLabels.RegionCount);
        Assert.Equal(0, graphFromLabels.Width);
        Assert.Equal(0, graphFromLabels.Height);
        Assert.Equal(new Size(0, 0), graphFromLabels.Size);
        Assert.Empty(graphFromLabels.Regions);
        Assert.Empty(graphFromLabels.Boundaries);

        int[] stats = graphFromLabels.ComputeRegionStatistics(r => 1);
        Assert.Empty(stats);

        var emptyImage = new SequentialMatrix<Rgb8>(0, 0);
        IDisjointSet emptySeg = EgbiSegmentation.Segment(emptyImage);
        var graphFromSeg = RegionGraph.FromDisjointSet(emptySeg);

        Assert.Equal(0, graphFromSeg.RegionCount);
        Assert.Empty(graphFromSeg.Regions);
        Assert.Empty(graphFromSeg.Boundaries);
    }

    /// <summary>
    /// Verifies argument validation for <see cref="RegionGraph"/> methods.
    /// </summary>
    [Fact]
    public void Validation_ShouldThrowOnInvalidArguments()
    {
        var validLabels = new SequentialMatrix<int>(2, 2);
        var validSeg = EgbiSegmentation.Segment(new SequentialMatrix<Rgb8>(2, 2));

        Assert.Throws<ArgumentNullException>(() => RegionGraph.FromDisjointSet(null!));
        Assert.Throws<ArgumentNullException>(() => RegionGraph.FromLabels(null!));

        Assert.Throws<ArgumentOutOfRangeException>(() => RegionGraph.FromDisjointSet(validSeg, (GridConnectivity)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => RegionGraph.FromLabels(validLabels, (GridConnectivity)99));

        var graph = RegionGraph.FromLabels(validLabels);

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.GetNeighborIds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.GetNeighborIds(graph.RegionCount));

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.GetBoundaryIndices(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.GetBoundaryIndices(graph.RegionCount));

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AreNeighbors(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AreNeighbors(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AreNeighbors(graph.RegionCount, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AreNeighbors(0, graph.RegionCount));
    }
}
