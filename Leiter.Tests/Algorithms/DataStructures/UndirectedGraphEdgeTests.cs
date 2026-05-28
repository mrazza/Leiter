
using Xunit;
using Leiter.Algorithms.DataStructures;

namespace Leiter.Tests.Algorithms.DataStructures;

/// <summary>
/// Provides unit tests or helpers for <see cref="UndirectedGraphEdgeTests" />.
/// </summary>
public class UndirectedGraphEdgeTests
{
    /// <summary>
    /// Verifies that the undirected graph edge should compare by weight behaves correctly.
    /// </summary>
    [Fact]
    public void UndirectedGraphEdge_ShouldCompareByWeight()
    {
        var e1 = new UndirectedGraphEdge<int> { First = 1, Second = 2, Weight = 1.5 };
        var e2 = new UndirectedGraphEdge<int> { First = 2, Second = 3, Weight = 2.5 };
        var e3 = new UndirectedGraphEdge<int> { First = 3, Second = 4, Weight = 1.5 };

        Assert.True(e1.CompareTo(e2) < 0);
        Assert.True(e2.CompareTo(e1) > 0);
        Assert.Equal(0, e1.CompareTo(e3));
    }
}
