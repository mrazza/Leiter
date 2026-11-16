namespace Leiter.Algorithms.DataStructures;

using System;

/// <summary>
/// Represents a weighted undirected edge connecting two vertices in a graph.
/// </summary>
/// <typeparam name="T">The type used to represent vertices in the graph.</typeparam>
public struct UndirectedGraphEdge<T> : IComparable<UndirectedGraphEdge<T>>
{
    /// <summary>
    /// Gets or sets the first vertex connected by the edge.
    /// </summary>
    public T First { get; set; }

    /// <summary>
    /// Gets or sets the second vertex connected by the edge.
    /// </summary>
    public T Second { get; set; }

    /// <summary>
    /// Gets or sets the scalar weight associated with the edge.
    /// </summary>
    public double Weight { get; set; }

    /// <summary>
    /// Compares this edge with another edge based on their weights.
    /// </summary>
    /// <param name="other">The other edge to compare against.</param>
    /// <returns>A signed value indicating the relative order of the weights.</returns>
    public readonly int CompareTo(UndirectedGraphEdge<T> other)
    {
        return Weight.CompareTo(other.Weight);
    }
}