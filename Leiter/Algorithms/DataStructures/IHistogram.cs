namespace Leiter.Algorithms.DataStructures;

/// <summary>
/// Defines a mutable histogram that tallies occurrences of discrete values.
/// </summary>
/// <typeparam name="T">The type of elements counted in the histogram.</typeparam>
public interface IHistogram<T> : IReadOnlyHistogram<T>
{
    /// <summary>
    /// Increments the count for the specified value by a given amount.
    /// </summary>
    /// <param name="value">The element value whose bucket count to increment.</param>
    /// <param name="count">The amount to add to the element's count. Defaults to 1.</param>
    /// <returns>The updated count for <paramref name="value"/>.</returns>
    int Increment(T value, int count = 1);
}