namespace Leiter.Algorithms.DataStructures;

using System.Text;

public sealed class UndirectedGraphEdge<T> : IComparable<UndirectedGraphEdge<T>>, IComparable
        where T : notnull
{
    public required T First
    {
        get;
        init;
    }

    public required T Second
    {
        get;
        init;
    }

    public double Weight
    {
        get;
        init;
    }

    public override int GetHashCode()
    {
        return ((First.GetHashCode() * Second.GetHashCode()) << 5) ^ Weight.GetHashCode();
    }

    public bool Equals(UndirectedGraphEdge<T>? other)
    {
        if (other == null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Weight == other.Weight &&
                (First.Equals(other.First) && Second.Equals(other.Second) ||
                (First.Equals(other.Second) && Second.Equals(other.First)));
    }

    public override bool Equals(object? obj)
    {
        if (obj == null) return false;
        if (obj.GetType() != GetType()) return false;
        return Equals(obj as UndirectedGraphEdge<T>);
    }

    public int CompareTo(object? obj)
    {
        return CompareTo(obj as UndirectedGraphEdge<T>);
    }

    public int CompareTo(UndirectedGraphEdge<T>? other)
    {
        if (Equals(other)) return 0;
        if (other == null) return 1; // All instances are greater than null
        if (Weight == other.Weight) return 1;
        return Weight.CompareTo(other.Weight);
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.Append(GetType());
        builder.AppendLine(" {");

        builder.Append("First => ");
        builder.AppendLine(First.ToString());

        builder.Append("Second => ");
        builder.AppendLine(Second.ToString());

        builder.AppendLine($"Weight {Weight}");

        builder.Append('}');
        return builder.ToString();
    }
}