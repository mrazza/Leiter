namespace Leiter.Algorithms.DataStructures;

using System;
using System.Collections.Generic;

/// <summary>
/// Represents a weighted undirected edge connecting two vertices in a graph.
/// </summary>
/// <typeparam name="T">The type used to represent vertices in the graph.</typeparam>
/// <param name="First">The first vertex connected by the edge.</param>
/// <param name="Second">The second vertex connected by the edge.</param>
/// <param name="Weight">The scalar weight associated with the edge.</param>
public readonly record struct UndirectedGraphEdge<T>(
    T First,
    T Second,
    double Weight = 0.0) : IUndirectedEdge<T>, IComparable<UndirectedGraphEdge<T>>
{
    /// <inheritdoc/>
    public bool IsIncidentTo(T vertex) =>
        EqualityComparer<T>.Default.Equals(vertex, First) ||
        EqualityComparer<T>.Default.Equals(vertex, Second);

    /// <inheritdoc/>
    public T GetOther(T vertex)
    {
        if (EqualityComparer<T>.Default.Equals(vertex, First))
            return Second;
        if (EqualityComparer<T>.Default.Equals(vertex, Second))
            return First;

        throw new ArgumentOutOfRangeException(nameof(vertex), vertex, $"Vertex '{vertex}' is not incident to edge ({First}, {Second}).");
    }

    /// <summary>
    /// Compares this edge with another edge based on their weights.
    /// </summary>
    /// <param name="other">The other edge to compare against.</param>
    /// <returns>A signed value indicating the relative order of the weights.</returns>
    public int CompareTo(UndirectedGraphEdge<T> other) => Weight.CompareTo(other.Weight);
}