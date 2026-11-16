namespace Leiter.Algorithms.DataStructures;

/// <summary>
/// Defines a read-only histogram of discrete values and their frequency counts.
/// </summary>
/// <typeparam name="T">The type of elements counted in the histogram.</typeparam>
public interface IReadOnlyHistogram<T> : IEnumerable<KeyValuePair<T, int>>
{
    /// <summary>
    /// Gets the count of occurrences for the specified value.
    /// </summary>
    /// <param name="value">The element value to look up.</param>
    /// <returns>The number of occurrences of <paramref name="value"/>.</returns>
    public int this[T value] { get; }

    /// <summary>
    /// Gets the count of occurrences for the specified value.
    /// </summary>
    /// <param name="value">The element value to look up.</param>
    /// <returns>The number of occurrences of <paramref name="value"/>, or zero if not present.</returns>
    int GetCount(T value);

    /// <summary>
    /// Computes the relative frequency (density) of the specified value.
    /// </summary>
    /// <param name="value">The element value to compute density for.</param>
    /// <returns>The proportion of occurrences of <paramref name="value"/> relative to the total count.</returns>
    double GetDensity(T value);

    /// <summary>
    /// Gets the total number of all element occurrences recorded in the histogram.
    /// </summary>
    /// <returns>The sum of counts across all buckets.</returns>
    int Total();

    /// <summary>
    /// Gets the number of distinct buckets in the histogram.
    /// </summary>
    int BucketCount { get; }

    /// <summary>
    /// Gets an enumerable collection of all bucket keys present in the histogram.
    /// </summary>
    IEnumerable<T> Buckets { get; }

    /// <summary>
    /// Projects each bucket key to a new key type, aggregating counts into a new histogram.
    /// </summary>
    /// <typeparam name="N">The target key type.</typeparam>
    /// <param name="mapFunc">A transform function to apply to each bucket key.</param>
    /// <returns>A new read-only histogram with projected bucket keys.</returns>
    IReadOnlyHistogram<N> MapBuckets<N>(Func<T, N> mapFunc) where N : notnull;
}