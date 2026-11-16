namespace Leiter.Tests.Algorithms.DataStructures;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="RegionBoundary"/>.
/// </summary>
public class RegionBoundaryTests
{
    /// <summary>
    /// Verifies that record properties correctly expose initialized values.
    /// </summary>
    [Fact]
    public void Properties_ShouldInitializeCorrectly()
    {
        Coord[] firstPixels = [new Coord(0, 1), new Coord(0, 2)];
        Coord[] secondPixels = [new Coord(1, 1), new Coord(1, 2)];
        var boundary = new RegionBoundary(0, 1, firstPixels, secondPixels);

        Assert.Equal(0, boundary.FirstRegionId);
        Assert.Equal(1, boundary.SecondRegionId);
        Assert.Equal(2, boundary.FirstBorderPixels.Length);
        Assert.Equal(2, boundary.SecondBorderPixels.Length);
        Assert.Equal(new Coord(0, 1), boundary.FirstBorderPixels.Span[0]);
        Assert.Equal(new Coord(1, 2), boundary.SecondBorderPixels.Span[1]);
    }

    /// <summary>
    /// Verifies that <see cref="RegionBoundary.GetOtherRegionId"/> returns the opposite region for incident IDs.
    /// </summary>
    [Fact]
    public void GetOtherRegionId_IncidentRegions_ShouldReturnOpposite()
    {
        var boundary = new RegionBoundary(2, 5, Array.Empty<Coord>(), Array.Empty<Coord>());

        Assert.Equal(5, boundary.GetOtherRegionId(2));
        Assert.Equal(2, boundary.GetOtherRegionId(5));
    }

    /// <summary>
    /// Verifies that <see cref="RegionBoundary.GetOtherRegionId"/> throws <see cref="ArgumentOutOfRangeException"/> for non-incident region IDs.
    /// </summary>
    [Fact]
    public void GetOtherRegionId_NonIncidentRegion_ShouldThrowArgumentOutOfRangeException()
    {
        var boundary = new RegionBoundary(2, 5, Array.Empty<Coord>(), Array.Empty<Coord>());

        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetOtherRegionId(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetOtherRegionId(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetOtherRegionId(-1));
    }

    /// <summary>
    /// Verifies that <see cref="RegionBoundary.GetBorderPixelsFor"/> returns the correct coordinate slice for incident IDs.
    /// </summary>
    [Fact]
    public void GetBorderPixelsFor_IncidentRegions_ShouldReturnCorrectPixels()
    {
        Coord[] firstPixels = [new Coord(3, 4)];
        Coord[] secondPixels = [new Coord(4, 4), new Coord(5, 4)];
        var boundary = new RegionBoundary(10, 20, firstPixels, secondPixels);

        ReadOnlyMemory<Coord> pixels10 = boundary.GetBorderPixelsFor(10);
        ReadOnlyMemory<Coord> pixels20 = boundary.GetBorderPixelsFor(20);

        Assert.Equal(1, pixels10.Length);
        Assert.Equal(2, pixels20.Length);
        Assert.Equal(new Coord(3, 4), pixels10.Span[0]);
        Assert.Equal(new Coord(4, 4), pixels20.Span[0]);
        Assert.Equal(new Coord(5, 4), pixels20.Span[1]);
    }

    /// <summary>
    /// Verifies that <see cref="RegionBoundary.GetBorderPixelsFor"/> throws <see cref="ArgumentOutOfRangeException"/> for non-incident region IDs.
    /// </summary>
    [Fact]
    public void GetBorderPixelsFor_NonIncidentRegion_ShouldThrowArgumentOutOfRangeException()
    {
        var boundary = new RegionBoundary(10, 20, Array.Empty<Coord>(), Array.Empty<Coord>());

        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetBorderPixelsFor(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetBorderPixelsFor(99));
        Assert.Throws<ArgumentOutOfRangeException>(() => boundary.GetBorderPixelsFor(-1));
    }
}
