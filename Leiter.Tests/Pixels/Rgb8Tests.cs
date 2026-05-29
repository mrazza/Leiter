using Leiter.Tests.TestUtils;
using Xunit;
using Leiter.Pixels;
using Leiter.Core;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Leiter.Tests.Pixels;

/// <summary>
/// Provides unit tests for <see cref="Rgb8Tests" />.
/// </summary>
public class Rgb8Tests
{
    /// <summary>
    /// Verifies that the properties should work behaves correctly.
    /// </summary>
    [Fact]
    public void Properties_ShouldWork()
    {
        var p = new Rgb8(10, 20, 30);
        Assert.Equal(10, p.R);
        Assert.Equal(20, p.G);
        Assert.Equal(30, p.B);
        Assert.Equal(0, Rgb8.Zero.R);
    }

    /// <summary>
    /// Verifies that the self operations should work behaves correctly.
    /// </summary>
    [Fact]
    public void SelfOperations_ShouldWork()
    {
        var p1 = new Rgb8(10, 20, 30);
        var p2 = new Rgb8(5, 10, 15);

        Assert.Equal(new Rgb8(15, 30, 45), p1.Add(p2));
        Assert.Equal(new Rgb8(5, 10, 15), p1.Subtract(p2));
        Assert.Equal(new Rgb8(50, 200, 194), p1.Multiply(p2)); // 30*15 = 450 = 194 (byte overflow)
        Assert.Equal(new Rgb8(2, 2, 2), p1.Divide(p2));
    }

    /// <summary>
    /// Verifies that the numeric operations decimal should work behaves correctly.
    /// </summary>
    [Fact]
    public void NumericOperations_Decimal_ShouldWork()
    {
        var p = new Rgb8(10, 20, 30);

        Assert.Equal(new Rgb8(15, 25, 35), p.Add(5.0m));
        Assert.Equal(new Rgb8(5, 15, 25), p.Subtract(5.0m));
        Assert.Equal(new Rgb8(20, 40, 60), p.Multiply(2.0m));
        Assert.Equal(new Rgb8(5, 10, 15), p.Divide(2.0m));
    }

    /// <summary>
    /// Verifies that the numeric operations double should work behaves correctly.
    /// </summary>
    [Fact]
    public void NumericOperations_Double_ShouldWork()
    {
        var p = new Rgb8(10, 20, 30);

        Assert.Equal(new Rgb8(15, 25, 35), p.Add(5.0));
        Assert.Equal(new Rgb8(5, 15, 25), p.Subtract(5.0));
        Assert.Equal(new Rgb8(20, 40, 60), p.Multiply(2.0));
        Assert.Equal(new Rgb8(5, 10, 15), p.Divide(2.0));
    }

    

    /// <summary>
    /// Verifies that the scalar operations should work behaves correctly.
    /// </summary>
    [Fact]
    public void ScalarOperations_ShouldWork()
    {
        var p = new Rgb8(10, 20, 30);
        var scalar = new DummyScalar(5.0);

        Assert.Equal(new Rgb8(15, 25, 35), p.Add(scalar));
        Assert.Equal(new Rgb8(5, 15, 25), p.Subtract(scalar));
        Assert.Equal(new Rgb8(50, 100, 150), p.Multiply(scalar));
        Assert.Equal(new Rgb8(2, 4, 6), p.Divide(scalar));
    }

    /// <summary>
    /// Verifies that the vector operations should work behaves correctly.
    /// </summary>
    [Fact]
    public void VectorOperations_ShouldWork()
    {
        var p = new Rgb8(255, 255, 255);
        if (Vector<double>.Count >= 3)
        {
            var vec = p.ToDoubleVector();
            Assert.Equal(1.0, vec[0], 5);

            var pFromVec = Rgb8.FromDoubleVector(vec);
            Assert.Equal(p, pFromVec);
        }
    }

    /// <summary>
    /// Verifies that the enumerator should yield values behaves correctly.
    /// </summary>
    [Fact]
    public void Enumerator_ShouldYieldValues()
    {
        var p = new Rgb8(10, 20, 30);
        var list = new List<byte>(p);
        Assert.Equal(new byte[] { 10, 20, 30 }, list);

        var seq = (System.Collections.IEnumerable)p;
        var seqEnum = seq.GetEnumerator();
        Assert.True(seqEnum.MoveNext());
        Assert.Equal((byte)10, seqEnum.Current);
    }

    /// <summary>
    /// Verifies that the get hash code and distance should work behaves correctly.
    /// </summary>
    [Fact]
    public void GetHashCode_AndDistance_ShouldWork()
    {
        var p1 = new Rgb8(10, 20, 30);
        var p2 = new Rgb8(13, 24, 30);

        Assert.Equal(10 << 16 | 20 << 8 | 30, p1.GetHashCode());
        Assert.Equal(5.0, p1.Distance(p2));
    }

    /// <summary>
    /// Verifies that the component maps should work behaves correctly.
    /// </summary>
    [Fact]
    public void ComponentMaps_ShouldWork()
    {
        var p = new Rgb8(10, 20, 30);
        Assert.Equal(new Rgb8(20, 40, 60), p.ComponentMap(b => (byte)(b * 2)));
        Assert.Equal(new Rgb8(20, 40, 60), p.ColorComponentMap(b => (byte)(b * 2)));
    }
}
