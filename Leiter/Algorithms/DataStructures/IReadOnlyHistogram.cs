namespace Leiter.Algorithms.DataStructures;

public interface IReadOnlyHistogram<T> : IEnumerable<KeyValuePair<T, int>>
{
    public int this[T value] { get; }

    int GetCount(T value);

    double GetDensity(T value);

    int Total();

    IReadOnlyHistogram<N> MapBuckets<N>(Func<T, N> mapFunc) where N : notnull;
}