namespace Leiter.Algorithms.DataStructures;

public interface IHistogram<T> : IReadOnlyHistogram<T>
{
    int Increment(T value, int count = 1);
}