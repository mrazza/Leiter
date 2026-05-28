
using Xunit;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;

namespace Leiter.Tests.Algorithms.DataStructures;

public class RegionExtraTests
{
    [Fact]
    public void TestRegion_SingleElementConstructor()
    {
        var region = new Region<Coord>(new Coord(1, 1));
        Assert.Single(region.Pixels);
        Assert.Contains(new Coord(1, 1), region.Pixels);
    }
}
