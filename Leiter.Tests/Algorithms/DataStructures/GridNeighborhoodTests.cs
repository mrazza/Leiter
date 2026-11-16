namespace Leiter.Tests.Algorithms.DataStructures;

using System;
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
}
