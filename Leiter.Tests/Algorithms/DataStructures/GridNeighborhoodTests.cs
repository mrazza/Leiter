namespace Leiter.Tests.Algorithms.DataStructures;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GridDirection"/>, <see cref="GridNeighbor"/>, and <see cref="GridNeighborhood"/>.
/// </summary>
public class GridNeighborhoodTests
{
    /// <summary>
    /// Verifies that stencil arrays expose expected counts, directions, offsets, and distances.
    /// </summary>
    [Fact]
    public void Stencils_ShouldHaveExpectedCountsDirectionsAndMetrics()
    {
        Assert.Equal(2, GridNeighborhood.Forward4Connected.Length);
        Assert.Equal(4, GridNeighborhood.Forward8Connected.Length);
        Assert.Equal(4, GridNeighborhood.All4Connected.Length);
        Assert.Equal(8, GridNeighborhood.All8Connected.Length);

        Assert.Equal(GridDirection.East, GridNeighborhood.Forward4Connected[0].Direction);
        Assert.Equal(GridDirection.South, GridNeighborhood.Forward4Connected[1].Direction);

        Assert.Equal(GridDirection.East, GridNeighborhood.Forward8Connected[0].Direction);
        Assert.Equal(GridDirection.South, GridNeighborhood.Forward8Connected[1].Direction);
        Assert.Equal(GridDirection.SouthEast, GridNeighborhood.Forward8Connected[2].Direction);
        Assert.Equal(GridDirection.SouthWest, GridNeighborhood.Forward8Connected[3].Direction);

        Assert.Equal(GridDirection.East, GridNeighborhood.All4Connected[0].Direction);
        Assert.Equal(GridDirection.South, GridNeighborhood.All4Connected[1].Direction);
        Assert.Equal(GridDirection.West, GridNeighborhood.All4Connected[2].Direction);
        Assert.Equal(GridDirection.North, GridNeighborhood.All4Connected[3].Direction);

        Assert.Equal(new Coord(1, 0), GridNeighborhood.All4Connected[0].Offset);
        Assert.Equal(new Coord(0, 1), GridNeighborhood.All4Connected[1].Offset);
        Assert.Equal(new Coord(-1, 0), GridNeighborhood.All4Connected[2].Offset);
        Assert.Equal(new Coord(0, -1), GridNeighborhood.All4Connected[3].Offset);

        for (int i = 0; i < GridNeighborhood.All4Connected.Length; i++)
        {
            Assert.Equal(1.0, GridNeighborhood.All4Connected[i].Distance, 12);
            Assert.Equal(1.0, GridNeighborhood.All4Connected[i].InverseDistance, 12);
        }

        var seenDirections = new HashSet<GridDirection>();
        var seenOffsets = new HashSet<Coord>();
        for (int i = 0; i < GridNeighborhood.All8Connected.Length; i++)
        {
            GridNeighbor neighbor = GridNeighborhood.All8Connected[i];
            Assert.Equal(i, (int)neighbor.Direction);
            Assert.True(seenDirections.Add(neighbor.Direction));
            Assert.True(seenOffsets.Add(neighbor.Offset));

            bool isDiagonal = neighbor.Offset.X != 0 && neighbor.Offset.Y != 0;
            double expectedDistance = isDiagonal ? Math.Sqrt(2.0) : 1.0;
            Assert.Equal(expectedDistance, neighbor.Distance, 12);
            Assert.Equal(1.0 / expectedDistance, neighbor.InverseDistance, 12);
        }
    }

    /// <summary>
    /// Verifies that <see cref="GridNeighborhood.GetForward"/> and <see cref="GridNeighborhood.GetAll"/> return expected stencils and validate connectivity.
    /// </summary>
    [Fact]
    public void GetForwardAndGetAll_ShouldReturnExpectedStencilsAndValidateConnectivity()
    {
        ReadOnlySpan<GridNeighbor> forward4 = GridNeighborhood.GetForward(GridConnectivity.FourConnected);
        Assert.Equal(2, forward4.Length);
        Assert.Equal(GridNeighborhood.Forward4Connected[0], forward4[0]);
        Assert.Equal(GridNeighborhood.Forward4Connected[1], forward4[1]);

        ReadOnlySpan<GridNeighbor> forward8 = GridNeighborhood.GetForward(GridConnectivity.EightConnected);
        Assert.Equal(4, forward8.Length);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(GridNeighborhood.Forward8Connected[i], forward8[i]);
        }

        ReadOnlySpan<GridNeighbor> all4 = GridNeighborhood.GetAll(GridConnectivity.FourConnected);
        Assert.Equal(4, all4.Length);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(GridNeighborhood.All4Connected[i], all4[i]);
        }

        ReadOnlySpan<GridNeighbor> all8 = GridNeighborhood.GetAll(GridConnectivity.EightConnected);
        Assert.Equal(8, all8.Length);
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(GridNeighborhood.All8Connected[i], all8[i]);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.GetForward((GridConnectivity)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.GetAll((GridConnectivity)99));
    }

    /// <summary>
    /// Verifies direction inversion and offset conversion.
    /// </summary>
    [Fact]
    public void OppositeAndToOffset_ShouldRoundTripAndValidateRange()
    {
        foreach (GridNeighbor neighbor in GridNeighborhood.All8Connected)
        {
            GridDirection opposite = neighbor.Direction.Opposite();
            Assert.Equal(neighbor.Direction, opposite.Opposite());
            Assert.Equal(neighbor.Offset, neighbor.Direction.ToOffset());

            Coord oppositeOffset = opposite.ToOffset();
            Assert.Equal(new Coord(0, 0), neighbor.Offset + oppositeOffset);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => ((GridDirection)8).Opposite());
        Assert.Throws<ArgumentOutOfRangeException>(() => ((GridDirection)255).ToOffset());
    }

    /// <summary>
    /// Verifies <see cref="GridNeighborhood.PackUnorderedPairKey"/> order independence, bit layout, and uniqueness.
    /// </summary>
    [Fact]
    public void PackUnorderedPairKey_ShouldBeOrderIndependentAndUnique()
    {
        Assert.Equal(GridNeighborhood.PackUnorderedPairKey(3, 17), GridNeighborhood.PackUnorderedPairKey(17, 3));
        Assert.Equal(GridNeighborhood.PackUnorderedPairKey(-5, 2), GridNeighborhood.PackUnorderedPairKey(2, -5));
        Assert.Equal(GridNeighborhood.PackUnorderedPairKey(42, 42), GridNeighborhood.PackUnorderedPairKey(42, 42));
        Assert.NotEqual(GridNeighborhood.PackUnorderedPairKey(1, 2), GridNeighborhood.PackUnorderedPairKey(1, 3));
        Assert.NotEqual(GridNeighborhood.PackUnorderedPairKey(1, 100), GridNeighborhood.PackUnorderedPairKey(2, 100));

        ulong key = GridNeighborhood.PackUnorderedPairKey(9, 4);
        Assert.Equal(4, (int)(key >> 32));
        Assert.Equal(9, (int)(uint)key);
    }

    /// <summary>
    /// Verifies sequential, per-row, and parallel <see cref="GridNeighborhood.ForEachForwardPair"/> overloads across
    /// 4- and 8-connected grids, edge shapes, and argument validation.
    /// </summary>
    [Fact]
    public void ForEachForwardPair_AllOverloads_ShouldVisitExpectedPairsWithoutDuplicates()
    {
        Size[] sizes =
        [
            new(0, 0),
            new(0, 3),
            new(3, 0),
            new(1, 1),
            new(4, 1),
            new(1, 4),
            new(4, 3),
        ];

        foreach (Size size in sizes)
        {
            foreach (GridConnectivity connectivity in new[] { GridConnectivity.FourConnected, GridConnectivity.EightConnected })
            {
                var expectedCoordPairs = BuildExpectedForwardPairs(size, connectivity);
                var expectedIndexPairs = new List<(int First, int Second)>(expectedCoordPairs.Count);
                foreach ((Coord first, Coord second, _) in expectedCoordPairs)
                    expectedIndexPairs.Add((size.IndexOf(first), size.IndexOf(second)));

                // 1. Sequential index-pair overloads (Action<int, int> and stateful)
                var actualIndices = new List<(int First, int Second)>();
                GridNeighborhood.ForEachForwardPair(size, connectivity, (a, b) => actualIndices.Add((a, b)));
                Assert.Equal(expectedIndexPairs, actualIndices);

                var actualStatefulIndices = new List<(int First, int Second)>();
                GridNeighborhood.ForEachForwardPair(size, connectivity, actualStatefulIndices, static (a, b, list) => list.Add((a, b)));
                Assert.Equal(expectedIndexPairs, actualStatefulIndices);

                // 2. Sequential coord-pair overloads (Action<Coord, Coord, GridNeighbor> and stateful)
                var actualCoords = new List<(Coord First, Coord Second, GridDirection Direction)>();
                GridNeighborhood.ForEachForwardPair(size, connectivity, (a, b, n) => actualCoords.Add((a, b, n.Direction)));
                Assert.Equal(expectedCoordPairs, actualCoords);

                var actualStatefulCoords = new List<(Coord First, Coord Second, GridDirection Direction)>();
                GridNeighborhood.ForEachForwardPair(size, connectivity, actualStatefulCoords, static (a, b, n, list) => list.Add((a, b, n.Direction)));
                Assert.Equal(expectedCoordPairs, actualStatefulCoords);

                // 3. Per-row sweeps
                var rowIndices = new List<(int First, int Second)>();
                var rowCoords = new List<(Coord First, Coord Second, GridDirection Direction)>();
                var rowStatefulIndices = new List<(int First, int Second)>();
                var rowStatefulCoords = new List<(Coord First, Coord Second, GridDirection Direction)>();
                for (int y = 0; y < size.Height; y++)
                {
                    GridNeighborhood.ForEachForwardPairInRow(size, y, connectivity, (a, b) => rowIndices.Add((a, b)));
                    GridNeighborhood.ForEachForwardPairInRow(size, y, connectivity, (a, b, n) => rowCoords.Add((a, b, n.Direction)));
                    GridNeighborhood.ForEachForwardPairInRow(size, y, connectivity, rowStatefulIndices, static (a, b, list) => list.Add((a, b)));
                    GridNeighborhood.ForEachForwardPairInRow(size, y, connectivity, rowStatefulCoords, static (a, b, n, list) => list.Add((a, b, n.Direction)));
                }
                Assert.Equal(expectedIndexPairs, rowIndices);
                Assert.Equal(expectedCoordPairs, rowCoords);
                Assert.Equal(expectedIndexPairs, rowStatefulIndices);
                Assert.Equal(expectedCoordPairs, rowStatefulCoords);

                // 4. Parallel sweeps (index & coord, stateless & stateful)
                var parallelIndices = new ConcurrentBag<(int First, int Second)>();
                GridNeighborhood.ForEachForwardPairParallel(size, connectivity, (a, b) => parallelIndices.Add((a, b)));
                Assert.Equal(expectedIndexPairs.OrderBy(p => p), parallelIndices.OrderBy(p => p));

                var parallelStatefulIndices = new ConcurrentBag<(int First, int Second)>();
                GridNeighborhood.ForEachForwardPairParallel(size, connectivity, parallelStatefulIndices, static (a, b, bag) => bag.Add((a, b)));
                Assert.Equal(expectedIndexPairs.OrderBy(p => p), parallelStatefulIndices.OrderBy(p => p));

                var parallelCoords = new ConcurrentBag<(Coord First, Coord Second, GridDirection Direction)>();
                GridNeighborhood.ForEachForwardPairParallel(size, connectivity, (a, b, n) => parallelCoords.Add((a, b, n.Direction)));
                Assert.Equal(
                    expectedCoordPairs.OrderBy(p => (p.First.Y, p.First.X, p.Direction)),
                    parallelCoords.OrderBy(p => (p.First.Y, p.First.X, p.Direction)));

                var parallelStatefulCoords = new ConcurrentBag<(Coord First, Coord Second, GridDirection Direction)>();
                GridNeighborhood.ForEachForwardPairParallel(size, connectivity, parallelStatefulCoords, static (a, b, n, bag) => bag.Add((a, b, n.Direction)));
                Assert.Equal(
                    expectedCoordPairs.OrderBy(p => (p.First.Y, p.First.X, p.Direction)),
                    parallelStatefulCoords.OrderBy(p => (p.First.Y, p.First.X, p.Direction)));
            }
        }

        // Exact undirected edge count verification on 4x3 grid:
        // 4-connected: (4-1)*3 + 4*(3-1) = 9 + 8 = 17
        // 8-connected: 17 + 2*(4-1)*(3-1) = 17 + 12 = 29
        int count4 = 0;
        int count8 = 0;
        GridNeighborhood.ForEachForwardPair(new Size(4, 3), GridConnectivity.FourConnected, (_, _) => count4++);
        GridNeighborhood.ForEachForwardPair(new Size(4, 3), GridConnectivity.EightConnected, (_, _) => count8++);
        Assert.Equal(17, count4);
        Assert.Equal(29, count8);

        // Argument validation across overloads
        var validSize = new Size(3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.ForEachForwardPair(new Size(-1, 3), GridConnectivity.FourConnected, (_, _) => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.ForEachForwardPair(new Size(3, -1), GridConnectivity.FourConnected, (_, _, _) => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.ForEachForwardPair(validSize, (GridConnectivity)99, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => GridNeighborhood.ForEachForwardPair(validSize, GridConnectivity.FourConnected, (Action<int, int>)null!));
        Assert.Throws<ArgumentNullException>(() => GridNeighborhood.ForEachForwardPair(validSize, GridConnectivity.FourConnected, (Action<Coord, Coord, GridNeighbor>)null!));
        Assert.Throws<ArgumentNullException>(() => GridNeighborhood.ForEachForwardPair(validSize, GridConnectivity.FourConnected, 0, (Action<int, int, int>)null!));
        Assert.Throws<ArgumentNullException>(() => GridNeighborhood.ForEachForwardPair(validSize, GridConnectivity.FourConnected, 0, (Action<Coord, Coord, GridNeighbor, int>)null!));

        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.ForEachForwardPairInRow(validSize, -1, GridConnectivity.FourConnected, (_, _) => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.ForEachForwardPairInRow(validSize, 3, GridConnectivity.FourConnected, (_, _, _) => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.ForEachForwardPairInRow(new Size(3, 0), 0, GridConnectivity.FourConnected, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => GridNeighborhood.ForEachForwardPairInRow(validSize, 0, GridConnectivity.FourConnected, (Action<int, int>)null!));
        Assert.Throws<ArgumentNullException>(() => GridNeighborhood.ForEachForwardPairInRow(validSize, 0, GridConnectivity.FourConnected, (Action<Coord, Coord, GridNeighbor>)null!));

        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.ForEachForwardPairParallel(new Size(-1, 2), GridConnectivity.EightConnected, (_, _, _) => { }));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.ForEachForwardPairParallel(validSize, (GridConnectivity)99, (_, _) => { }));
        Assert.Throws<ArgumentNullException>(() => GridNeighborhood.ForEachForwardPairParallel(validSize, GridConnectivity.EightConnected, (Action<int, int>)null!));
        Assert.Throws<ArgumentNullException>(() => GridNeighborhood.ForEachForwardPairParallel(validSize, GridConnectivity.EightConnected, (Action<Coord, Coord, GridNeighbor>)null!));
    }

    private static List<(Coord First, Coord Second, GridDirection Direction)> BuildExpectedForwardPairs(Size size, GridConnectivity connectivity)
    {
        var pairs = new List<(Coord First, Coord Second, GridDirection Direction)>();
        ReadOnlySpan<GridNeighbor> forward = GridNeighborhood.GetForward(connectivity);

        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                var first = new Coord(x, y);
                for (int i = 0; i < forward.Length; i++)
                {
                    Coord second = first + forward[i].Offset;
                    if (size.Contains(second))
                        pairs.Add((first, second, forward[i].Direction));
                }
            }
        }

        return pairs;
    }

    [Fact]
    public void BuildValidNeighborMasks_AndGetLinearOffsets_ShouldMatchGridBounds()
    {
        Size[] sizes =
        [
            new(0, 0),
            new(1, 1),
            new(1, 4),
            new(4, 1),
            new(5, 4),
        ];

        GridConnectivity[] connectivities = [GridConnectivity.FourConnected, GridConnectivity.EightConnected];

        foreach (GridConnectivity connectivity in connectivities)
        {
            ReadOnlySpan<GridNeighbor> neighbors = GridNeighborhood.GetAll(connectivity);
            foreach (Size size in sizes)
            {
                byte[] masks = GridNeighborhood.BuildValidNeighborMasks(size, connectivity);
                int[] offsets = GridNeighborhood.GetLinearOffsets(size.Width, connectivity);

                Assert.Equal(size.Count, masks.Length);
                Assert.Equal(neighbors.Length, offsets.Length);

                for (int y = 0; y < size.Height; y++)
                {
                    for (int x = 0; x < size.Width; x++)
                    {
                        var coord = new Coord(x, y);
                        int index = size.IndexOf(coord);
                        byte mask = masks[index];

                        for (int s = 0; s < neighbors.Length; s++)
                        {
                            Coord neighborCoord = coord + neighbors[s].Offset;
                            bool expectedValid = size.Contains(neighborCoord);
                            bool actualValid = (mask & (1 << s)) != 0;
                            Assert.Equal(expectedValid, actualValid);

                            if (expectedValid)
                                Assert.Equal(size.IndexOf(neighborCoord), index + offsets[s]);
                        }
                    }
                }
            }
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.BuildValidNeighborMasks(new Size(-1, 2), GridConnectivity.FourConnected));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.BuildValidNeighborMasks(new Size(2, 2), (GridConnectivity)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridNeighborhood.GetLinearOffsets(4, (GridConnectivity)99));
    }
}
