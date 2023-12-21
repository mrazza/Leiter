namespace Leiter.Algorithms.DataStructures;

interface IHistogram<T> : IEnumerable<KeyValuePair<T, int>>
{
    public int this[T value] {get;}

    int GetCount(T value);

    int Increment(T value, int count = 1);

    int Total();
}