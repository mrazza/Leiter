namespace Leiter.Tests.Algorithms.DataStructures;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="UndirectedGraphEdge{T}" />.
/// </summary>
public class UndirectedGraphEdgeTests
{
    /// <summary>
    /// Verifies that edges initialized with object initializers compare strictly by weight.
    /// </summary>
    [Fact]
    public void UndirectedGraphEdge_ShouldCompareByWeight()
    {
        var e1 = new UndirectedGraphEdge<int> { First = 1, Second = 2, Weight = 1.5 };
        var e2 = new UndirectedGraphEdge<int> { First = 2, Second = 3, Weight = 2.5 };
        var e3 = new UndirectedGraphEdge<int> { First = 3, Second = 4, Weight = 1.5 };

        Assert.True(e1.CompareTo(e2) < 0);
        Assert.True(e2.CompareTo(e1) > 0);
        Assert.Equal(0, e1.CompareTo(e3));
    }

    /// <summary>
    /// Verifies positional construction defaults, value equality, and deconstruction.
    /// </summary>
    [Fact]
    public void PositionalConstructor_AndValueEquality_ShouldInitializeAndDeconstruct()
    {
        var defaultWeightEdge = new UndirectedGraphEdge<int>(2, 5);
        var weightedEdge = new UndirectedGraphEdge<int>(2, 5, 3.5);
        var identicalEdge = new UndirectedGraphEdge<int>(2, 5, 3.5);

        Assert.Equal(2, defaultWeightEdge.First);
        Assert.Equal(5, defaultWeightEdge.Second);
        Assert.Equal(0.0, defaultWeightEdge.Weight);

        Assert.Equal(weightedEdge, identicalEdge);
        Assert.NotEqual(defaultWeightEdge, weightedEdge);

        (int first, int second, double weight) = weightedEdge;
        Assert.Equal(2, first);
        Assert.Equal(5, second);
        Assert.Equal(3.5, weight);
    }

    /// <summary>
    /// Verifies that <see cref="UndirectedGraphEdge{T}.IsIncidentTo"/> returns true only for the two endpoints.
    /// </summary>
    [Fact]
    public void IsIncidentTo_ShouldIdentifyEndpoints()
    {
        var intEdge = new UndirectedGraphEdge<int>(10, 20, 1.0);
        Assert.True(intEdge.IsIncidentTo(10));
        Assert.True(intEdge.IsIncidentTo(20));
        Assert.False(intEdge.IsIncidentTo(30));

        var coordEdge = new UndirectedGraphEdge<Coord>(new Coord(1, 2), new Coord(3, 4), 2.0);
        Assert.True(coordEdge.IsIncidentTo(new Coord(1, 2)));
        Assert.True(coordEdge.IsIncidentTo(new Coord(3, 4)));
        Assert.False(coordEdge.IsIncidentTo(new Coord(1, 3)));
    }

    /// <summary>
    /// Verifies that <see cref="UndirectedGraphEdge{T}.GetOther"/> returns the opposite endpoint for incident vertices.
    /// </summary>
    [Fact]
    public void GetOther_IncidentVertices_ShouldReturnOpposite()
    {
        var edge = new UndirectedGraphEdge<int>(7, 11, 4.0);

        Assert.Equal(11, edge.GetOther(7));
        Assert.Equal(7, edge.GetOther(11));
    }

    /// <summary>
    /// Verifies that <see cref="UndirectedGraphEdge{T}.GetOther"/> throws <see cref="ArgumentOutOfRangeException"/> for non-incident vertices.
    /// </summary>
    [Fact]
    public void GetOther_NonIncidentVertex_ShouldThrowArgumentOutOfRangeException()
    {
        var edge = new UndirectedGraphEdge<int>(7, 11, 4.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => edge.GetOther(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => edge.GetOther(99));
    }

    /// <summary>
    /// Verifies constrained generic dispatch via <see cref="IUndirectedEdge{TVertex}"/> on multiple vertex types.
    /// </summary>
    [Fact]
    public void UndirectedEdgeInterface_GenericDispatch_ShouldWork()
    {
        var intEdge = new UndirectedGraphEdge<int>(3, 9, 2.5);
        VerifyEdgeInterface<UndirectedGraphEdge<int>, int>(intEdge, 3, 9, 12);

        var coordA = new Coord(0, 1);
        var coordB = new Coord(1, 1);
        var coordEdge = new UndirectedGraphEdge<Coord>(coordA, coordB, 5.0);
        VerifyEdgeInterface<UndirectedGraphEdge<Coord>, Coord>(coordEdge, coordA, coordB, new Coord(2, 2));
    }

    private static void VerifyEdgeInterface<TEdge, TVertex>(
        TEdge edge,
        TVertex first,
        TVertex second,
        TVertex nonIncident)
        where TEdge : IUndirectedEdge<TVertex>
    {
        Assert.Equal(first, edge.First);
        Assert.Equal(second, edge.Second);
        Assert.True(edge.IsIncidentTo(first));
        Assert.True(edge.IsIncidentTo(second));
        Assert.False(edge.IsIncidentTo(nonIncident));
        Assert.Equal(second, edge.GetOther(first));
        Assert.Equal(first, edge.GetOther(second));
        Assert.Throws<ArgumentOutOfRangeException>(() => edge.GetOther(nonIncident));
    }
}
