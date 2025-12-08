namespace Leiter.Algorithms.DataStructures;

using System;

public struct UndirectedGraphEdge<T> : IComparable<UndirectedGraphEdge<T>>
{
    public T First { get; set; }
    public T Second { get; set; }
    public double Weight { get; set; }

    public int CompareTo(UndirectedGraphEdge<T> other)
    {
        return Weight.CompareTo(other.Weight);
    }
}