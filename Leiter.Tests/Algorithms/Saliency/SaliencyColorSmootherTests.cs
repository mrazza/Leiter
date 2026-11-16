namespace Leiter.Tests.Algorithms.Saliency;

using System;
using System.Collections.Generic;
using Leiter.Algorithms.Saliency;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyColorSmoother"/>.
/// </summary>
public class SaliencyColorSmootherTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void SmoothColorSaliency_InvalidNeighborDivisor_ShouldThrowArgumentOutOfRangeException(int neighborDivisor)
    {
        var map = new Dictionary<Rgb8, double>
        {
            [new Rgb8(0, 0, 0)] = 0.5
        };
        var labLookup = new Dictionary<Rgb8, Lab32>
        {
            [new Rgb8(0, 0, 0)] = new Lab32(0f, 0f, 0f)
        };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SaliencyColorSmoother.SmoothColorSaliency(map, labLookup, neighborDivisor));
    }

    [Fact]
    public void SmoothColorSaliency_NearestColorCountLessThanOrEqualToOne_ShouldReturnWithoutModification()
    {
        var map = new Dictionary<Rgb8, double>
        {
            [new Rgb8(0, 0, 0)] = 0.2,
            [new Rgb8(255, 0, 0)] = 0.8
        };
        var labLookup = new Dictionary<Rgb8, Lab32>
        {
            [new Rgb8(0, 0, 0)] = new Lab32(0f, 0f, 0f),
            [new Rgb8(255, 0, 0)] = new Lab32(53f, 80f, 67f)
        };

        // Count = 2, neighborDivisor = 2 -> nearestColorCount = 1 (<= 1), returns early
        SaliencyColorSmoother.SmoothColorSaliency(map, labLookup, neighborDivisor: 2);

        Assert.Equal(0.2, map[new Rgb8(0, 0, 0)], 6);
        Assert.Equal(0.8, map[new Rgb8(255, 0, 0)], 6);
    }

    [Fact]
    public void SmoothColorSaliency_ZeroDistanceNeighbors_ShouldPreserveValuesWithoutNaN()
    {
        var map = new Dictionary<Rgb8, double>
        {
            [new Rgb8(0, 0, 0)] = 0.2,
            [new Rgb8(1, 0, 0)] = 0.4,
            [new Rgb8(255, 0, 0)] = 0.8,
            [new Rgb8(0, 255, 0)] = 0.6
        };
        var labLookup = new Dictionary<Rgb8, Lab32>
        {
            [new Rgb8(0, 0, 0)] = new Lab32(0f, 0f, 0f),
            [new Rgb8(1, 0, 0)] = new Lab32(0f, 0f, 0f),
            [new Rgb8(255, 0, 0)] = new Lab32(53f, 80f, 67f),
            [new Rgb8(0, 255, 0)] = new Lab32(87f, -86f, 83f)
        };

        // For neighborDivisor: 2 -> nearestColorCount = 2; the two zero-Lab keys have T == 0 and remain unchanged.
        SaliencyColorSmoother.SmoothColorSaliency(map, labLookup, neighborDivisor: 2);

        Assert.Equal(0.2, map[new Rgb8(0, 0, 0)], 6);
        Assert.Equal(0.4, map[new Rgb8(1, 0, 0)], 6);
        Assert.All(map.Values, value => Assert.False(double.IsNaN(value)));
    }

    [Fact]
    public void SmoothColorSaliency_ActiveSmoothing_ShouldInterpolateNearestNeighbors()
    {
        // 6 distinct colors positioned along the L* axis in CIELAB
        var c1 = new Rgb8(10, 0, 0);
        var c2 = new Rgb8(20, 0, 0);
        var c3 = new Rgb8(30, 0, 0);
        var c4 = new Rgb8(70, 0, 0);
        var c5 = new Rgb8(80, 0, 0);
        var c6 = new Rgb8(90, 0, 0);

        var map = new Dictionary<Rgb8, double>
        {
            [c1] = 0.1,
            [c2] = 0.2,
            [c3] = 0.3,
            [c4] = 0.7,
            [c5] = 0.8,
            [c6] = 0.9
        };

        var labLookup = new Dictionary<Rgb8, Lab32>
        {
            [c1] = new Lab32(10f, 0f, 0f),
            [c2] = new Lab32(20f, 0f, 0f),
            [c3] = new Lab32(30f, 0f, 0f),
            [c4] = new Lab32(70f, 0f, 0f),
            [c5] = new Lab32(80f, 0f, 0f),
            [c6] = new Lab32(90f, 0f, 0f)
        };

        // 6 colors / 2 = 3 nearest neighbors; distances use CIEDE2000 (Lab32.Distance).
        double d12 = labLookup[c1].Distance(labLookup[c2]);
        double d13 = labLookup[c1].Distance(labLookup[c3]);
        double t1 = d12 + d13;
        double expectedC1 = ((t1 * 0.1) + ((t1 - d12) * 0.2) + ((t1 - d13) * 0.3)) / (2.0 * t1);

        double d21 = labLookup[c2].Distance(labLookup[c1]);
        double d23 = labLookup[c2].Distance(labLookup[c3]);
        double t2 = d21 + d23;
        double expectedC2 = ((t2 * 0.2) + ((t2 - d21) * 0.1) + ((t2 - d23) * 0.3)) / (2.0 * t2);

        SaliencyColorSmoother.SmoothColorSaliency(map, labLookup, neighborDivisor: 2);

        Assert.Equal(expectedC1, map[c1], 10);
        Assert.Equal(expectedC2, map[c2], 10);
        Assert.All(map.Values, value =>
        {
            Assert.False(double.IsNaN(value));
            Assert.InRange(value, 0.0, 1.0);
        });
    }
}
