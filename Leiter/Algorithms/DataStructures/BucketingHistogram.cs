namespace Leiter.Algorithms.DataStructures;

using System.Collections;

/// <summary>
/// Represents a histogram that groups values into buckets using a mapping delegate.
/// </summary>
/// <typeparam name="T">The type of elements counted in the histogram.</typeparam>
public class BucketingHistogram<T> : IHistogram<T> where T : notnull
{
    private readonly Dictionary<T, int> histogram;

    private readonly Func<T, T> bucketer;

    private int total;

    /// <inheritdoc/>
    public int this[T value] => GetCount(value);

    /// <summary>
    /// Initializes a new instance of the <see cref="BucketingHistogram{T}"/> class using the specified bucketing function.
    /// </summary>
    /// <param name="bucketer">The function that maps elements to bucket keys.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bucketer"/> is <see langword="null"/>.</exception>
    public BucketingHistogram(Func<T, T> bucketer)
    {
        ArgumentNullException.ThrowIfNull(bucketer);
        histogram = [];
        this.bucketer = bucketer;
    }

    /// <inheritdoc/>
    public int GetCount(T value)
        => histogram.GetValueOrDefault(bucketer(value));

    /// <inheritdoc/>
    public double GetDensity(T value)
        => total == 0 ? 0.0 : GetCount(value) / (double)total;

    /// <inheritdoc/>
    public int Increment(T value, int count = 1)
    {
        var bucketedValue = bucketer(value);
        total += count;
        return histogram[bucketedValue] = histogram.GetValueOrDefault(bucketedValue) + count;
    }

    /// <inheritdoc/>
    public int Total => total;

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapFunc"/> is <see langword="null"/>.</exception>
    public IReadOnlyHistogram<N> MapBuckets<N>(Func<T, N> mapFunc)
        where N : notnull
    {
        ArgumentNullException.ThrowIfNull(mapFunc);
        var result = new DictionaryHistogram<N>();

        foreach (var element in histogram)
        {
            result.Increment(mapFunc(element.Key), element.Value);
        }

        return result;
    }

    /// <inheritdoc/>
    public IEnumerable<T> Buckets => histogram.Keys;

    /// <inheritdoc/>
    public int BucketCount => histogram.Count;

    /// <inheritdoc/>
    public override string ToString()
        => "{" + string.Join(", ", histogram.Select(kv => kv.Key + "=" + kv.Value).ToArray()) + "}";

    /// <inheritdoc/>
    public IEnumerator<KeyValuePair<T, int>> GetEnumerator()
        => histogram.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}