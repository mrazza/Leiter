namespace Leiter.Algorithms.DataStructures;

using System.Collections;

public class BucketingHistogram<T> : IHistogram<T> where T : notnull
{
    public delegate Type Bucketer<Type>(Type value);

    private readonly Dictionary<T, int> histogram;

    private readonly Bucketer<T> bucketer;

    private int total;

    public int this[T value] => GetCount(value);

    public BucketingHistogram(Bucketer<T> bucketer)
    {
        histogram = new();
        this.bucketer = bucketer;
    }

    public int GetCount(T value)
        => histogram.GetValueOrDefault(bucketer(value));

    public double GetDensity(T value)
        => GetCount(value) / (double)total;

    public int Increment(T value, int count = 1)
    {
        var bucketedValue = bucketer(value);
        total += count;
        return histogram[bucketedValue] = histogram.GetValueOrDefault(bucketedValue) + count;
    }

    public int Total() => total;

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

    public override string ToString()
        => "{" + string.Join(", ", histogram.Select(kv => kv.Key + "=" + kv.Value).ToArray()) + "}";

    public IEnumerator<KeyValuePair<T, int>> GetEnumerator()
        => histogram.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}