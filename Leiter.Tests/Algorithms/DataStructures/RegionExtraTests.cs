
using Xunit;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;

namespace Leiter.Tests.Algorithms.DataStructures;

/// <summary>
/// Provides unit tests or helpers for <see cref="RegionExtraTests" />.
/// </summary>
public class RegionExtraTests
{
    /// <summary>
    /// Executes the test region single element constructor operation.
    /// </summary>
    [Fact]
    public void TestRegion_SingleElementConstructor()
    {
        var region = new Region<Coord>(new Coord(1, 1));
        Assert.Single(region.Pixels);
        Assert.Contains(new Coord(1, 1), region.Pixels);
    }
}
