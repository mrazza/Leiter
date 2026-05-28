
using Xunit;
using Leiter.Algorithms.DataStructures;

namespace Leiter.Tests.Algorithms.DataStructures;

/// <summary>
/// Provides unit tests or helpers for <see cref="RegionTests" />.
/// </summary>
public class RegionTests
{
    /// <summary>
    /// Verifies that the region constructors should work behaves correctly.
    /// </summary>
    [Fact]
    public void Region_Constructors_ShouldWork()
    {
        var r1 = new Region<int>();
        Assert.Equal(-1, r1.Id);
        Assert.Empty(r1.Pixels);

        var r2 = new Region<int>(5);
        Assert.Equal(-1, r2.Id);
        Assert.Empty(r2.Pixels);

        var r3 = new Region<int>(10) { Id = 42 };
        Assert.Equal(42, r3.Id);

        var r4 = new Region<int>(new[] { 1, 2, 3 });
        Assert.Equal(3, r4.Pixels.Count);
        Assert.Contains(1, r4.Pixels);

        var r5 = new Region<int>(7);
        Assert.Empty(r5.Pixels);
    }
}
