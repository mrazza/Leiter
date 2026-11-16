namespace Leiter.Tests.Algorithms.DistanceTransform;

using Leiter.Algorithms.DistanceTransform;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="NearestSeed"/>.
/// </summary>
public class NearestSeedTests
{
    [Fact]
    public void None_ShouldHaveDefaultValues()
    {
        NearestSeed none = NearestSeed.None;

        Assert.False(none.HasSeed);
        Assert.Equal(default, none.Seed);
        Assert.Equal(default(NearestSeed), none);
    }

    [Fact]
    public void FromCoord_ShouldInitializeSeedWithHasSeedTrue()
    {
        var coord = new Coord(12, 34);
        NearestSeed seed = NearestSeed.FromCoord(coord);

        Assert.True(seed.HasSeed);
        Assert.Equal(coord, seed.Seed);
    }

    [Fact]
    public void Constructor_ShouldAssignProperties()
    {
        var coord = new Coord(5, 10);

        var withSeed = new NearestSeed(coord, true);
        Assert.True(withSeed.HasSeed);
        Assert.Equal(coord, withSeed.Seed);

        var withoutSeed = new NearestSeed(coord, false);
        Assert.False(withoutSeed.HasSeed);
        Assert.Equal(coord, withoutSeed.Seed);
    }

    [Fact]
    public void Equality_RecordStructSemantics_ShouldBehaveCorrectly()
    {
        var a = NearestSeed.FromCoord(new Coord(2, 3));
        var b = NearestSeed.FromCoord(new Coord(2, 3));
        var c = NearestSeed.FromCoord(new Coord(3, 2));
        var d = new NearestSeed(new Coord(2, 3), false);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(a, d);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
