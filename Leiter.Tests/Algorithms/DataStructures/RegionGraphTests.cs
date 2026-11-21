namespace Leiter.Tests.Algorithms.DataStructures;

using System;
using System.Collections.Generic;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
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
        var img = TestMatrices.VerticalSplit(4, 4, new Rgb8(255, 0, 0), new Rgb8(0, 255, 0), splitX: 2);

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
        // [0, 1, 1]
        // [1, 2, 1]
        // [1, 1, 1]
        SequentialMatrix<int> labels = TestMatrices.Constant(3, 3, 1);
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
        // Row 0: 0, 0, 1, 1
        // Row 1: 0, 0, 1, 1
        var labels = TestMatrices.Create(4, 2, static (x, _) => x < 2 ? 0 : 1);

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
        SequentialMatrix<int> labels = TestMatrices.Create(3, 3, static (x, y) => (y * 3) + x);

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
        SequentialMatrix<int> labels = TestMatrices.Create(3, 3, static (x, y) => (y * 3) + x);

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
        var labels = TestMatrices.Create(4, 2, static (x, _) => x < 2 ? 0 : 1);

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
        SequentialMatrix<int> labels = TestMatrices.Constant(5, 5, 10);

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
        Assert.Throws<ArgumentNullException>(() => RegionGraph.FromLabels<int>(null!));

        Assert.Throws<ArgumentOutOfRangeException>(() => RegionGraph.FromDisjointSet(validSeg, (GridConnectivity)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => RegionGraph.FromLabels(validLabels, (GridConnectivity)99));

        RegionGraph graph = TestMatrices.SingleRegionGraph(2, 2);

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.GetNeighborIds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.GetNeighborIds(graph.RegionCount));

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.GetBoundaryIndices(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.GetBoundaryIndices(graph.RegionCount));

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AreNeighbors(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AreNeighbors(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AreNeighbors(graph.RegionCount, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.AreNeighbors(0, graph.RegionCount));
    }

    /// <summary>
    /// Verifies that generic <see cref="RegionGraph.FromLabels{TLabel}"/> handles non-integer value-type labels.
    /// </summary>
    [Fact]
    public void FromLabels_ByteLabels_ShouldRemapInRowMajorOrder()
    {
        var byteLabels = new SequentialMatrix<byte>(3, 2);
        byteLabels[0, 0] = 200;
        byteLabels[1, 0] = 200;
        byteLabels[2, 0] = 50;
        byteLabels[0, 1] = 200;
        byteLabels[1, 1] = 50;
        byteLabels[2, 1] = 50;

        var graph = RegionGraph.FromLabels(byteLabels);

        Assert.Equal(2, graph.RegionCount);
        Assert.Equal(0, graph.Labels[0, 0]);
        Assert.Equal(1, graph.Labels[2, 0]);
        Assert.Single(graph.Boundaries);
        Assert.Equal(0, graph.Regions[0].Id);
        Assert.Equal(1, graph.Regions[1].Id);
    }

    /// <summary>
    /// Verifies <see cref="RegionGraph.ComputeRegionFractions"/> overlap ratios, empty graphs, and argument validation.
    /// </summary>
    [Fact]
    public void ComputeRegionFractions_ShouldComputePerRegionOverlapAndValidate()
    {
        SequentialMatrix<int> labels = TestMatrices.Create(4, 3, static (_, y) => y);

        var graph = RegionGraph.FromLabels(labels);
        var mask = new MaskMatrix(4, 3);
        mask[0, 0] = true; // 1/4 = 0.25 in region 0
        mask[0, 1] = true;
        mask[1, 1] = true; // 2/4 = 0.50 in region 1
        for (int x = 0; x < 4; x++)
            mask[x, 2] = true; // 4/4 = 1.00 in region 2

        double[] fractions = graph.ComputeRegionFractions(mask);
        Assert.Equal(3, fractions.Length);
        Assert.Equal(0.25, fractions[0], 10);
        Assert.Equal(0.50, fractions[1], 10);
        Assert.Equal(1.0, fractions[2], 10);

        RegionGraph emptyGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(0, 0));
        Assert.Empty(emptyGraph.ComputeRegionFractions(new MaskMatrix(0, 0)));

        var nullEx = Assert.Throws<ArgumentNullException>(() => graph.ComputeRegionFractions(null!));
        Assert.Equal("mask", nullEx.ParamName);

        var mismatchEx = Assert.Throws<ArgumentException>(() => graph.ComputeRegionFractions(new MaskMatrix(2, 2)));
        Assert.Equal("mask", mismatchEx.ParamName);
    }

    /// <summary>
    /// Verifies <see cref="RegionGraph.ComputeBoundaryNormal"/> directionality and validation.
    /// </summary>
    [Fact]
    public void ComputeBoundaryNormal_ShouldPointTowardTargetRegionAndValidate()
    {
        SequentialMatrix<int> labels = TestMatrices.Create(4, 3, static (x, _) => x < 2 ? 0 : 1);

        var graph = RegionGraph.FromLabels(labels);

        // Border pixel in region 0 at x=1; neighbors in region 1 are at x=2 -> normal points toward x<2 (negative X) when towards=0, from=1
        Vector2D towardZero = graph.ComputeBoundaryNormal(new Coord(1, 1), towardsRegionId: 0, fromRegionId: 1);
        Assert.True(towardZero.X < -0.99);
        Assert.Equal(0.0, towardZero.Y, 6);

        Vector2D towardOne = graph.ComputeBoundaryNormal(new Coord(1, 1), towardsRegionId: 1, fromRegionId: 0);
        Assert.True(towardOne.X > 0.99);
        Assert.Equal(0.0, towardOne.Y, 6);

        // Interior pixel and equal region IDs produce Vector2D.Zero
        Assert.Equal(Vector2D.Zero, graph.ComputeBoundaryNormal(new Coord(0, 1), 0, 1));
        Assert.Equal(Vector2D.Zero, graph.ComputeBoundaryNormal(new Coord(1, 1), 0, 0));

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.ComputeBoundaryNormal(new Coord(-1, 0), 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.ComputeBoundaryNormal(new Coord(1, 1), -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.ComputeBoundaryNormal(new Coord(1, 1), 0, 2));
    }

    /// <summary>
    /// Verifies <see cref="RegionGraph.ComputeBoundaryBand"/> distance propagation, boundary predicate filtering,
    /// incident-region restriction, empty graphs, and argument validation.
    /// </summary>
    [Fact]
    public void ComputeBoundaryBand_ShouldSupportPredicateRestrictionAndValidation()
    {
        // Three vertical strips: region 0 (x=0..1), region 1 (x=2..4), region 2 (x=5..6)
        var labels = new SequentialMatrix<int>(7, 1);
        labels[0, 0] = 0;
        labels[1, 0] = 0;
        labels[2, 0] = 1;
        labels[3, 0] = 1;
        labels[4, 0] = 1;
        labels[5, 0] = 2;
        labels[6, 0] = 2;

        var graph = RegionGraph.FromLabels(labels);
        Assert.Equal(2, graph.Boundaries.Count); // boundary 0: (0,1), boundary 1: (1,2)

        Assert.Throws<ArgumentOutOfRangeException>(() => graph.ComputeBoundaryBand(-1));

        // Select only boundary 0 (between regions 0 and 1, seeds at x=1 and x=2)
        RegionBoundaryBand unrestricted = graph.ComputeBoundaryBand(maxRadius: 4, boundaryPredicate: bi => bi == 0, restrictToIncidentRegions: false);
        Assert.Equal(0, unrestricted.Distances[1, 0]);
        Assert.Equal(0, unrestricted.Distances[2, 0]);
        Assert.Equal(3, unrestricted.Distances[5, 0]); // crosses into region 2
        Assert.Equal(0, unrestricted.NearestBoundaryIndices[5, 0]);
        Assert.Equal(2, unrestricted.NearestSeedIndices[5, 0]);
        Assert.Equal(2, unrestricted.SeedPixels.Length);

        RegionBoundaryBand restricted = graph.ComputeBoundaryBand(maxRadius: 4, boundaryPredicate: bi => bi == 0, restrictToIncidentRegions: true);
        Assert.Equal(2, restricted.Distances[4, 0]);
        Assert.Equal(int.MaxValue, restricted.Distances[5, 0]); // stopped before entering region 2
        Assert.Equal(-1, restricted.NearestBoundaryIndices[5, 0]);
        Assert.Equal(-1, restricted.NearestSeedIndices[5, 0]);

        RegionGraph emptyGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(0, 0));
        RegionBoundaryBand emptyBand = emptyGraph.ComputeBoundaryBand(2);
        Assert.Equal(0, emptyBand.SeedPixels.Length);
        Assert.Equal(0, emptyBand.Distances.Count);
    }

    [Fact]
    public void ComputeRegionFlags_ShouldThresholdOverlapAndValidateInputs()
    {
        SequentialMatrix<int> labels = TestMatrices.Create(4, 2, static (_, y) => y);

        var graph = RegionGraph.FromLabels(labels);
        var mask = new MaskMatrix(4, 2);
        mask[0, 0] = true; // 1/4 = 0.25 in region 0
        mask[0, 1] = true;
        mask[1, 1] = true; // 2/4 = 0.50 in region 1

        Assert.Equal([false, true], graph.ComputeRegionFlags(mask, 0.5));
        Assert.Equal([true, true], graph.ComputeRegionFlags(mask, 0.25));
        Assert.Equal([false, false], graph.ComputeRegionFlags(mask, 0.75));

        RegionGraph emptyGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(0, 0));
        Assert.Empty(emptyGraph.ComputeRegionFlags(new MaskMatrix(0, 0), 0.5));

        Assert.Throws<ArgumentNullException>(() => graph.ComputeRegionFlags(null!, 0.5));
        Assert.Throws<ArgumentException>(() => graph.ComputeRegionFlags(new MaskMatrix(2, 2), 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.ComputeRegionFlags(mask, -0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.ComputeRegionFlags(mask, 1.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => graph.ComputeRegionFlags(mask, double.NaN));
    }

    /// <summary>
    /// Verifies the internal span-based <see cref="RegionGraph.ComputeRegionFlags(ReadOnlySpan{double}, double)"/> helper.
    /// </summary>
    [Fact]
    public void ComputeRegionFlags_SpanOverload_ShouldClassifyInclusiveThresholdAndValidate()
    {
        double[] values = [0.0, 0.2499, 0.25, 0.75, 1.0];
        Assert.Equal([false, false, true, true, true], RegionGraph.ComputeRegionFlags(values, 0.25));
        Assert.Empty(RegionGraph.ComputeRegionFlags(ReadOnlySpan<double>.Empty, 0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => RegionGraph.ComputeRegionFlags(values, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => RegionGraph.ComputeRegionFlags(values, double.PositiveInfinity));
    }

    /// <summary>
    /// Verifies that <see cref="RegionGraph.ComputeBoundaryNormal"/> respects <see cref="GridConnectivity.FourConnected"/>
    /// by ignoring diagonal neighbors that contribute under <see cref="GridConnectivity.EightConnected"/>.
    /// </summary>
    [Fact]
    public void ComputeBoundaryNormal_FourConnected_ShouldIgnoreDiagonalNeighbors()
    {
        // [0, 0, 1]
        // [0, 1, 1]
        var labels = new SequentialMatrix<int>(3, 2);
        labels[0, 0] = 0;
        labels[1, 0] = 0;
        labels[2, 0] = 1;
        labels[0, 1] = 0;
        labels[1, 1] = 1;
        labels[2, 1] = 1;

        var graph4 = RegionGraph.FromLabels(labels, GridConnectivity.FourConnected);
        var graph8 = RegionGraph.FromLabels(labels, GridConnectivity.EightConnected);

        // At (0, 0), only diagonal neighbor (1, 1) belongs to region 1:
        // 4-connected sees no opposing cardinal neighbors -> Vector2D.Zero.
        // 8-connected sees diagonal (1, 1) -> normalized (1/sqrt(2), 1/sqrt(2)).
        Assert.Equal(Vector2D.Zero, graph4.ComputeBoundaryNormal(new Coord(0, 0), towardsRegionId: 1, fromRegionId: 0));
        Vector2D normal8Corner = graph8.ComputeBoundaryNormal(new Coord(0, 0), towardsRegionId: 1, fromRegionId: 0);
        Assert.Equal(ScalarMath.InvSqrt2, normal8Corner.X, 10);
        Assert.Equal(ScalarMath.InvSqrt2, normal8Corner.Y, 10);

        // Check L-shape where (1, 1) has cardinal neighbor (2, 1)=1 and diagonal neighbor (2, 2)=1:
        // [0, 0, 0]
        // [0, 0, 1]
        // [0, 0, 1]
        var lShape = new SequentialMatrix<int>(3, 3);
        lShape[2, 1] = 1;
        lShape[2, 2] = 1;

        var lGraph4 = RegionGraph.FromLabels(lShape, GridConnectivity.FourConnected);
        var lGraph8 = RegionGraph.FromLabels(lShape, GridConnectivity.EightConnected);

        // At center (1, 1), only East (2, 1) is cardinal region 1, while SouthEast (2, 2) is diagonal region 1.
        Vector2D lNormal4 = lGraph4.ComputeBoundaryNormal(new Coord(1, 1), towardsRegionId: 1, fromRegionId: 0);
        Assert.Equal(1.0, lNormal4.X, 10);
        Assert.Equal(0.0, lNormal4.Y, 10);

        Vector2D lNormal8 = lGraph8.ComputeBoundaryNormal(new Coord(1, 1), towardsRegionId: 1, fromRegionId: 0);
        Assert.True(lNormal8.X > 0.0);
        Assert.True(lNormal8.Y > 0.4); // (+2, +1) normalized -> Y = 1/sqrt(5) ≈ 0.4472
    }

    /// <summary>
    /// Verifies that <see cref="RegionGraph.ComputeBoundaryBand"/> respects <see cref="GridConnectivity.FourConnected"/>
    /// geodesic distances and does not step diagonally across corners.
    /// </summary>
    [Fact]
    public void ComputeBoundaryBand_FourConnected_ShouldUseManhattanStepsAndBlockDiagonalShortcuts()
    {
        // 3x3 grid: region 0 at (0,0), region 1 everywhere else
        SequentialMatrix<int> labels = TestMatrices.Constant(3, 3, 1);
        labels[0, 0] = 0;

        var graph4 = RegionGraph.FromLabels(labels, GridConnectivity.FourConnected);
        var graph8 = RegionGraph.FromLabels(labels, GridConnectivity.EightConnected);

        // Under 4-connected, border seeds for boundary (0,1) are (0,0), (1,0), (0,1).
        // Seed (1,0) expands diagonally to (2,1) in 1 step under 8-connected, vs 2 steps under 4-connected.
        RegionBoundaryBand band4Radius1 = graph4.ComputeBoundaryBand(maxRadius: 1);
        RegionBoundaryBand band8Radius1 = graph8.ComputeBoundaryBand(maxRadius: 1);

        Assert.Equal(1, band4Radius1.Distances[2, 0]);
        Assert.Equal(1, band4Radius1.Distances[1, 1]);
        Assert.Equal(int.MaxValue, band4Radius1.Distances[2, 1]);
        Assert.Equal(1, band8Radius1.Distances[2, 1]);

        RegionBoundaryBand band4Radius2 = graph4.ComputeBoundaryBand(maxRadius: 2);
        Assert.Equal(2, band4Radius2.Distances[2, 1]);

        // Test restrictToIncidentRegions across a diagonal-only bridge of region 1:
        // [0, 1, 2]
        // [2, 2, 1]
        // Boundary 0 is between region 0 (0,0) and region 1 (1,0).
        // Under 8-connected, (1,0) expands diagonally to (2,1) [also region 1] at distance 1.
        // Under 4-connected, (1,0) is blocked by region 2 at (2,0) and (1,1), so (2,1) stays unreachable.
        var bridge = new SequentialMatrix<int>(3, 2);
        bridge[0, 0] = 0;
        bridge[1, 0] = 1;
        bridge[2, 0] = 2;
        bridge[0, 1] = 2;
        bridge[1, 1] = 2;
        bridge[2, 1] = 1;

        var bridge4 = RegionGraph.FromLabels(bridge, GridConnectivity.FourConnected);
        var bridge8 = RegionGraph.FromLabels(bridge, GridConnectivity.EightConnected);

        RegionBoundaryBand bridgeBand4 = bridge4.ComputeBoundaryBand(
            maxRadius: 3,
            boundaryPredicate: bi => bridge4.Boundaries[bi].First == 0 && bridge4.Boundaries[bi].Second == 1,
            restrictToIncidentRegions: true);
        RegionBoundaryBand bridgeBand8 = bridge8.ComputeBoundaryBand(
            maxRadius: 3,
            boundaryPredicate: bi => bridge8.Boundaries[bi].First == 0 && bridge8.Boundaries[bi].Second == 1,
            restrictToIncidentRegions: true);

        Assert.Equal(int.MaxValue, bridgeBand4.Distances[2, 1]);
        Assert.Equal(1, bridgeBand8.Distances[2, 1]);
    }

    [Fact]
    public void FromLabels_LargeGridAboveParallelThreshold_ShouldPreserveRowMajorIdsAndBoundaryBand()
    {
        const int width = 320;
        const int height = 240; // 76,800 pixels (> ParallelRunner threshold)
        var rawLabels = new SequentialMatrix<int>(width, height);
        var expectedDense = new SequentialMatrix<int>(width, height);
        var rowMajorMap = new Dictionary<int, int>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int raw = ((x / 64) + ((y / 48) * 5)) switch
                {
                    0 => -500,
                    1 => 42,
                    2 => -500, // reappears after 42
                    3 => 999,
                    int k => 1000 + (k % 17),
                };
                rawLabels[x, y] = raw;

                if (!rowMajorMap.TryGetValue(raw, out int dense))
                {
                    dense = rowMajorMap.Count;
                    rowMajorMap.Add(raw, dense);
                }

                expectedDense[x, y] = dense;
            }
        }

        RegionGraph graph = RegionGraph.FromLabels(rawLabels, GridConnectivity.FourConnected);

        Assert.Equal(rowMajorMap.Count, graph.RegionCount);
        MatrixAssert.Equal(expectedDense, graph.Labels);

        int summedPixels = 0;
        for (int r = 0; r < graph.RegionCount; r++)
        {
            Assert.Equal(r, graph.Regions[r].Id);
            summedPixels += graph.Regions[r].Pixels.Count;
        }
        Assert.Equal(width * height, summedPixels);

        for (int i = 1; i < graph.Boundaries.Count; i++)
        {
            RegionBoundary prev = graph.Boundaries[i - 1];
            RegionBoundary curr = graph.Boundaries[i];
            Assert.True(prev.First < curr.First || (prev.First == curr.First && prev.Second < curr.Second));
        }

        RegionBoundaryBand band = graph.ComputeBoundaryBand(maxRadius: 3);
        Assert.Equal(0, band.Distances[63, 10]); // border pixel between col block 0 and col block 1
        Assert.Equal(1, band.Distances[62, 10]);
        Assert.Equal(3, band.Distances[60, 10]);
        Assert.Equal(int.MaxValue, band.Distances[59, 10]);
    }
}
