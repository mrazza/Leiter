namespace Leiter.Tests.Algorithms.DataStructures;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="RegionBoundary"/>.
/// </summary>
public class RegionBoundaryTests
{
    /// <summary>
    /// Verifies that record properties correctly expose initialized values.
    /// </summary>
    [Fact]
    public void Properties_ShouldInitializeCorrectly()
    {
        Coord[] firstPixels = [new Coord(0, 1), new Coord(0, 2)];
        Coord[] secondPixels = [new Coord(1, 1), new Coord(1, 2)];
        var boundary = new RegionBoundary(0, 1, firstPixels, secondPixels);

        Assert.Equal(0, boundary.First);
        Assert.Equal(1, boundary.Second);
        Assert.Equal(2, boundary.FirstBorderPixels.Length);
        Assert.Equal(2, boundary.SecondBorderPixels.Length);
        Assert.Equal(new Coord(0, 1), boundary.FirstBorderPixels.Span[0]);
        Assert.Equal(new Coord(1, 2), boundary.SecondBorderPixels.Span[1]);
    }

    /// <summary>
    /// Verifies that <see cref="RegionBoundary.IsIncidentTo"/> identifies incident regions accurately.
    /// </summary>
    [Fact]
    public void IsIncidentTo_ShouldIdentifyIncidentRegions()
    {
        var boundary = new RegionBoundary(2, 5, Array.Empty<Coord>(), Array.Empty<Coord>());

        Assert.True(boundary.IsIncidentTo(2));
        Assert.True(boundary.IsIncidentTo(5));
        Assert.False(boundary.IsIncidentTo(0));
        Assert.False(boundary.IsIncidentTo(3));
        Assert.False(boundary.IsIncidentTo(-1));
    }

    /// <summary>
    /// Verifies that <see cref="RegionBoundary.GetOther"/> returns the opposite region for incident IDs.
    /// </summary>
    [Fact]
    public void GetOther_IncidentRegions_ShouldReturnOpposite()
    {
        var boundary = new RegionBoundary(2, 5, Array.Empty<Coord>(), Array.Empty<Coord>());

        Assert.Equal(5, boundary.GetOther(2));
        Assert.Equal(2, boundary.GetOther(5));
    }

    /// <summary>
    /// Verifies that <see cref="RegionBoundary.GetOther"/> throws <see cref="ArgumentOutOfRangeException"/> for non-incident region IDs.
    /// </summary>
    [Fact]
    public void GetOther_NonIncidentRegion_ShouldThrowArgumentOutOfRangeException()
    {
        var boundary = new RegionBoundary(2, 5, Array.Empty<Coord>(), Array.Empty<Coord>());

        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetOther(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetOther(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetOther(-1));
    }

    /// <summary>
    /// Verifies that <see cref="RegionBoundary.ToEdge"/> creates a matching <see cref="UndirectedGraphEdge{T}"/>.
    /// </summary>
    [Fact]
    public void ToEdge_ShouldProjectToUndirectedGraphEdge()
    {
        var boundary = new RegionBoundary(2, 5, Array.Empty<Coord>(), Array.Empty<Coord>());

        UndirectedGraphEdge<int> defaultEdge = boundary.ToEdge();
        Assert.Equal(2, defaultEdge.First);
        Assert.Equal(5, defaultEdge.Second);
        Assert.Equal(0.0, defaultEdge.Weight);

        UndirectedGraphEdge<int> weightedEdge = boundary.ToEdge(3.5);
        Assert.Equal(2, weightedEdge.First);
        Assert.Equal(5, weightedEdge.Second);
        Assert.Equal(3.5, weightedEdge.Weight);
    }

    /// <summary>
    /// Verifies constrained generic dispatch via <see cref="IUndirectedEdge{TVertex}"/> on <see cref="RegionBoundary"/>.
    /// </summary>
    [Fact]
    public void UndirectedEdgeInterface_GenericDispatch_ShouldMatchBoundaryMembers()
    {
        var boundary = new RegionBoundary(4, 9, Array.Empty<Coord>(), Array.Empty<Coord>());

        Assert.Equal(9, GetOppositeViaInterface(boundary, 4));
        Assert.Equal(4, GetOppositeViaInterface(boundary, 9));
        Assert.Throws<ArgumentOutOfRangeException>(() => GetOppositeViaInterface(boundary, 7));
    }

    private static int GetOppositeViaInterface<TEdge>(TEdge edge, int regionId)
        where TEdge : IUndirectedEdge<int>
    {
        Assert.True(edge.First < edge.Second);
        if (!edge.IsIncidentTo(regionId))
            return edge.GetOther(regionId);

        return edge.GetOther(regionId);
    }

    /// <summary>
    /// Verifies that <see cref="RegionBoundary.GetBorderPixelsFor"/> returns the correct coordinate slice for incident IDs.
    /// </summary>
    [Fact]
    public void GetBorderPixelsFor_IncidentRegions_ShouldReturnCorrectPixels()
    {
        Coord[] firstPixels = [new Coord(3, 4)];
        Coord[] secondPixels = [new Coord(4, 4), new Coord(5, 4)];
        var boundary = new RegionBoundary(10, 20, firstPixels, secondPixels);

        ReadOnlyMemory<Coord> pixels10 = boundary.GetBorderPixelsFor(10);
        ReadOnlyMemory<Coord> pixels20 = boundary.GetBorderPixelsFor(20);

        Assert.Equal(1, pixels10.Length);
        Assert.Equal(2, pixels20.Length);
        Assert.Equal(new Coord(3, 4), pixels10.Span[0]);
        Assert.Equal(new Coord(4, 4), pixels20.Span[0]);
        Assert.Equal(new Coord(5, 4), pixels20.Span[1]);
    }

    /// <summary>
    /// Verifies that <see cref="RegionBoundary.GetBorderPixelsFor"/> throws <see cref="ArgumentOutOfRangeException"/> for non-incident region IDs.
    /// </summary>
    [Fact]
    public void GetBorderPixelsFor_NonIncidentRegion_ShouldThrowArgumentOutOfRangeException()
    {
        var boundary = new RegionBoundary(10, 20, Array.Empty<Coord>(), Array.Empty<Coord>());

        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetBorderPixelsFor(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetBorderPixelsFor(99));
        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetBorderPixelsFor(-1));
    }
}
