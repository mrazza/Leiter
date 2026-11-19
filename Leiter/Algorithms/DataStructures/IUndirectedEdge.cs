namespace Leiter.Algorithms.DataStructures;

/// <summary>
/// Represents an undirected topological edge connecting two vertices in a graph.
/// </summary>
/// <typeparam name="TVertex">The type used to identify vertices in the graph.</typeparam>
public interface IUndirectedEdge<TVertex>
{
    /// <summary>
    /// Gets the first vertex connected by the edge.
    /// </summary>
    TVertex First { get; }

    /// <summary>
    /// Gets the second vertex connected by the edge.
    /// </summary>
    TVertex Second { get; }

    /// <summary>
    /// Determines whether <paramref name="vertex"/> is one of the endpoints of this edge.
    /// </summary>
    /// <param name="vertex">The vertex to test.</param>
    /// <returns><see langword="true"/> if <paramref name="vertex"/> equals <see cref="First"/> or <see cref="Second"/>; otherwise <see langword="false"/>.</returns>
    bool IsIncidentTo(TVertex vertex) => IsIncidentTo(First, Second, vertex);

    /// <summary>
    /// Returns the endpoint on the opposite side of this edge from <paramref name="vertex"/>.
    /// </summary>
    /// <param name="vertex">One of <see cref="First"/> or <see cref="Second"/>.</param>
    /// <returns>The opposite vertex connected by this edge.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="vertex"/> is neither <see cref="First"/> nor <see cref="Second"/>.</exception>
    TVertex GetOther(TVertex vertex) => GetOther(First, Second, vertex, nameof(vertex));

    internal static bool IsIncidentTo(TVertex first, TVertex second, TVertex vertex) =>
        EqualityComparer<TVertex>.Default.Equals(vertex, first) ||
        EqualityComparer<TVertex>.Default.Equals(vertex, second);

    internal static TVertex GetOther(TVertex first, TVertex second, TVertex vertex, string paramName)
    {
        if (EqualityComparer<TVertex>.Default.Equals(vertex, first))
            return second;
        if (EqualityComparer<TVertex>.Default.Equals(vertex, second))
            return first;

        throw new ArgumentOutOfRangeException(paramName, vertex, $"Vertex '{vertex}' is not incident to edge ({first}, {second}).");
    }
}
