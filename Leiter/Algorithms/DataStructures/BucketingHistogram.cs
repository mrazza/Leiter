namespace Leiter.Algorithms.DataStructures;

using System.Collections;

/// <summary>
/// Represents a histogram that groups values into buckets using a mapping delegate.
/// </summary>
/// <typeparam name="T">The type of elements counted in the histogram.</typeparam>
public class BucketingHistogram<T> : IHistogram<T> where T : notnull
{
    /// <summary>
    /// Represents a method that maps an input value to its corresponding bucket key.
    /// </summary>
    /// <typeparam name="Type">The type of the value to bucket.</typeparam>
    /// <param name="value">The value to determine the bucket for.</param>
    /// <returns>The bucket key corresponding to <paramref name="value"/>.</returns>
    public delegate Type Bucketer<Type>(Type value);

    private readonly Dictionary<T, int> histogram;

    private readonly Bucketer<T> bucketer;

    private int total;

    /// <inheritdoc/>
    public int this[T value] => GetCount(value);

    /// <summary>
    /// Initializes a new instance of the <see cref="BucketingHistogram{T}"/> class using the specified bucketing delegate.
    /// </summary>
    /// <param name="bucketer">The delegate that maps elements to bucket keys.</param>
    public BucketingHistogram(Bucketer<T> bucketer)
    {
        histogram = [];
        this.bucketer = bucketer;
    }

    /// <inheritdoc/>
    public int GetCount(T value)
        => histogram.GetValueOrDefault(bucketer(value));

    /// <inheritdoc/>
    public double GetDensity(T value)
        => GetCount(value) / (double)total;

    /// <inheritdoc/>
    public int Increment(T value, int count = 1)
    {
        var bucketedValue = bucketer(value);
        total += count;
        return histogram[bucketedValue] = histogram.GetValueOrDefault(bucketedValue) + count;
    }

    /// <inheritdoc/>
    public int Total() => total;

    /// <inheritdoc/>
    public IReadOnlyHistogram<N> MapBuckets<N>(Func<T, N> mapFunc)
        where N : notnull
    {
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