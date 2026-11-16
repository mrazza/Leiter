using Leiter.Tests.TestUtils;
using Xunit;
using Leiter.Pixels;
using Leiter.Core;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Leiter.Tests.Pixels;

/// <summary>
/// Provides unit tests for <see cref="Lab32" />.
/// </summary>
public class Lab32Tests
{
    [Fact]
    public void Properties_ShouldWork()
    {
        var p = new Lab32(1.0f, 2.0f, 3.0f);
        Assert.Equal(1.0f, p.L);
        Assert.Equal(2.0f, p.A);
        Assert.Equal(3.0f, p.B);
        Assert.Equal(0.0f, Lab32.Zero.L);
    }

    [Fact]
    public void SelfOperations_ShouldWork()
    {
        var p1 = new Lab32(10.0f, 20.0f, 30.0f);
        var p2 = new Lab32(2.0f, 2.0f, 2.0f);

        Assert.Equal(new Lab32(12.0f, 22.0f, 32.0f), p1.Add(p2));
        Assert.Equal(new Lab32(8.0f, 18.0f, 28.0f), p1.Subtract(p2));
        Assert.Equal(new Lab32(20.0f, 40.0f, 60.0f), p1.Multiply(p2));
        Assert.Equal(new Lab32(5.0f, 10.0f, 15.0f), p1.Divide(p2));
    }

    [Fact]
    public void NumericOperations_Decimal_ShouldWork()
    {
        var p = new Lab32(10.0f, 20.0f, 30.0f);

        Assert.Equal(new Lab32(12.0f, 22.0f, 32.0f), p.Add(2.0m));
        Assert.Equal(new Lab32(8.0f, 18.0f, 28.0f), p.Subtract(2.0m));
        Assert.Equal(new Lab32(20.0f, 40.0f, 60.0f), p.Multiply(2.0m));
        Assert.Equal(new Lab32(5.0f, 10.0f, 15.0f), p.Divide(2.0m));
    }

    [Fact]
    public void NumericOperations_Double_ShouldWork()
    {
        var p = new Lab32(10.0f, 20.0f, 30.0f);

        Assert.Equal(new Lab32(12.0f, 22.0f, 32.0f), p.Add(2.0));
        Assert.Equal(new Lab32(8.0f, 18.0f, 28.0f), p.Subtract(2.0));
        Assert.Equal(new Lab32(20.0f, 40.0f, 60.0f), p.Multiply(2.0));
        Assert.Equal(new Lab32(5.0f, 10.0f, 15.0f), p.Divide(2.0));
    }

    [Fact]
    public void ScalarOperations_ShouldWork()
    {
        var p = new Lab32(10.0f, 20.0f, 30.0f);
        var scalar = new DummyScalar(2.0);

        Assert.Equal(new Lab32(12.0f, 22.0f, 32.0f), p.Add(scalar));
        Assert.Equal(new Lab32(8.0f, 18.0f, 28.0f), p.Subtract(scalar));
        Assert.Equal(new Lab32(20.0f, 40.0f, 60.0f), p.Multiply(scalar));
        Assert.Equal(new Lab32(5.0f, 10.0f, 15.0f), p.Divide(scalar));
    }

    [Fact]
    public void VectorOperations_ShouldWork()
    {
        var p = new Lab32(1.0f, 2.0f, 3.0f);
        if (Vector<double>.Count >= 3)
        {
            var vec = p.ToDoubleVector();
            Assert.Equal(1.0, vec[0]);

            var pFromVec = Lab32.FromDoubleVector(vec);
            Assert.Equal(p, pFromVec);
        }
    }

    [Fact]
    public void Enumerator_ShouldYieldValues()
    {
        var p = new Lab32(1.0f, 2.0f, 3.0f);
        var list = new List<float>(p);
        Assert.Equal(new float[] { 1.0f, 2.0f, 3.0f }, list);

        var seq = (System.Collections.IEnumerable)p;
        var seqEnum = seq.GetEnumerator();
        Assert.True(seqEnum.MoveNext());
        Assert.Equal(1.0f, seqEnum.Current);
    }

    [Fact]
    public void ComponentMaps_ShouldWork()
    {
        var p = new Lab32(10.0f, 20.0f, 30.0f);
        Assert.Equal(new Lab32(10.0f, 40.0f, 60.0f), p.ColorComponentMap(b => b * 2)); // ColorComponentMap maps A and B only!
        Assert.Equal(new Lab32(20.0f, 40.0f, 60.0f), p.ComponentMap(b => b * 2));
    }

    [Fact]
    public void Distance_ShouldWork()
    {
        var p1 = new Lab32(1.0f, 2.0f, 3.0f);
        var p2 = new Lab32(4.0f, 2.0f, 3.0f);
        Assert.Equal(1.7550397986383963, p1.Distance(p2));
    }

    [Fact]
    public void TestCIEDE2000_SpecialCases()
    {
        var gray1 = new Lab32(50, 0, 0);
        var gray2 = new Lab32(50, 0, 0);
        var color1 = new Lab32(50, 10, 20);
        var color2 = new Lab32(50, -10, -20);
        var color3 = new Lab32(50, 10, -20);

        Assert.Equal(0.0, gray1.Distance(gray2), 5);
        Assert.True(gray1.Distance(color1) > 0);
        Assert.True(color1.Distance(gray2) > 0);
        Assert.True(color1.Distance(color2) > 0);
        Assert.True(color1.Distance(color3) > 0);
    }

    [Fact]
    public void EuclideanDistance_ShouldComputeCIE76Distance()
    {
        var a = new Lab32(50, 10, 20);
        var b = new Lab32(53, 14, 32);

        Assert.Equal(0.0, a.EuclideanDistance(a), 12);
        Assert.Equal(13.0, a.EuclideanDistance(b), 12);
        Assert.Equal(a.EuclideanDistance(b), b.EuclideanDistance(a), 12);
    }

    /// <summary>
    /// Verifies that <see cref="Lab32.EuclideanDistanceSquared"/> computes exact squared CIE76 Euclidean distance.
    /// </summary>
    [Fact]
    public void EuclideanDistanceSquared_ShouldComputeSquaredCIE76Distance()
    {
        var a = new Lab32(50.0f, 10.0f, 20.0f);
        var b = new Lab32(53.0f, 14.0f, 32.0f);

        Assert.Equal(0.0, a.EuclideanDistanceSquared(a), 12);
        Assert.Equal(169.0, a.EuclideanDistanceSquared(b), 12);
        Assert.Equal(a.EuclideanDistanceSquared(b), b.EuclideanDistanceSquared(a), 12);
    }

    [Fact]
    public void TestCIEDE2000_HueWraparoundBranches()
    {
        // Straddle 0°/360° with sumH' < 360 and |diffH'| > 180.
        var lowHue1 = new Lab32(50f, 30f, 2f);
        var highHue1 = new Lab32(50f, 30f, -10f);
        double d12 = lowHue1.Distance(highHue1);
        double d21 = highHue1.Distance(lowHue1);
        Assert.True(d12 > 0);
        Assert.Equal(d12, d21, 10);

        // Straddle 0°/360° with sumH' >= 360 and |diffH'| > 180.
        var lowHue2 = new Lab32(50f, 30f, 15f);
        var highHue2 = new Lab32(50f, 30f, -2f);
        double d34 = lowHue2.Distance(highHue2);
        Assert.True(d34 > 0);
        Assert.Equal(d34, highHue2.Distance(lowHue2), 10);
    }
}
