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
    [Fact]
    public void Stencils_ShouldHaveExpectedCountsDirectionsAndMetrics()
    {
        Assert.Equal(2, GridNeighborhood.Forward4Connected.Length);
        Assert.Equal(4, GridNeighborhood.Forward8Connected.Length);
        Assert.Equal(8, GridNeighborhood.All8Connected.Length);

        Assert.Equal(GridDirection.East, GridNeighborhood.Forward4Connected[0].Direction);
        Assert.Equal(GridDirection.South, GridNeighborhood.Forward4Connected[1].Direction);

        Assert.Equal(GridDirection.East, GridNeighborhood.Forward8Connected[0].Direction);
        Assert.Equal(GridDirection.South, GridNeighborhood.Forward8Connected[1].Direction);
        Assert.Equal(GridDirection.SouthEast, GridNeighborhood.Forward8Connected[2].Direction);
        Assert.Equal(GridDirection.SouthWest, GridNeighborhood.Forward8Connected[3].Direction);

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
