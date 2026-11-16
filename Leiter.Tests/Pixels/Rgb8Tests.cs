using Leiter.Tests.TestUtils;
using Xunit;
using Leiter.Pixels;
using Leiter.Core;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Leiter.Tests.Pixels;

/// <summary>
/// Provides unit tests for <see cref="Rgb8" />.
/// </summary>
public class Rgb8Tests
{
    [Fact]
    public void Properties_ShouldWork()
    {
        var p = new Rgb8(10, 20, 30);
        Assert.Equal(10, p.R);
        Assert.Equal(20, p.G);
        Assert.Equal(30, p.B);
        Assert.Equal(0, Rgb8.Zero.R);
    }

    [Fact]
    public void SelfOperations_ShouldWork()
    {
        var p1 = new Rgb8(10, 20, 30);
        var p2 = new Rgb8(5, 10, 15);

        Assert.Equal(new Rgb8(15, 30, 45), p1.Add(p2));
        Assert.Equal(new Rgb8(5, 10, 15), p1.Subtract(p2));
        Assert.Equal(new Rgb8(50, 200, 255), p1.Multiply(p2));
        Assert.Equal(new Rgb8(2, 2, 2), p1.Divide(p2));
    }

    /// <summary>
    /// Verifies that arithmetic and vector conversions clamp out-of-range channel values to the byte range.
    /// </summary>
    [Fact]
    public void Operations_OverflowAndUnderflow_ShouldClampToByteRange()
    {
        var p = new Rgb8(10, 100, 240);

        // Rgb8 pixel operations
        Assert.Equal(new Rgb8(255, 110, 255), p.Add(new Rgb8(250, 10, 50)));
        Assert.Equal(new Rgb8(0, 50, 200), p.Subtract(new Rgb8(50, 50, 40)));
        Assert.Equal(new Rgb8(20, 255, 255), p.Multiply(new Rgb8(2, 3, 2)));
        Assert.Throws<DivideByZeroException>(() => p.Divide(new Rgb8(1, 0, 1)));

        // decimal operations
        Assert.Equal(new Rgb8(255, 255, 255), p.Add(300.0m));
        Assert.Equal(new Rgb8(0, 0, 90), p.Add(-150.0m));
        Assert.Equal(new Rgb8(0, 0, 90), p.Subtract(150.0m));
        Assert.Equal(new Rgb8(255, 255, 255), p.Subtract(-300.0m));
        Assert.Equal(new Rgb8(30, 255, 255), p.Multiply(3.0m));
        Assert.Equal(new Rgb8(0, 0, 0), p.Multiply(-1.0m));
        Assert.Equal(new Rgb8(100, 255, 255), p.Divide(0.1m));
        Assert.Equal(new Rgb8(0, 0, 0), p.Divide(-2.0m));

        // double operations
        Assert.Equal(new Rgb8(255, 255, 255), p.Add(300.0));
        Assert.Equal(new Rgb8(0, 0, 90), p.Add(-150.0));
        Assert.Equal(new Rgb8(0, 0, 90), p.Subtract(150.0));
        Assert.Equal(new Rgb8(255, 255, 255), p.Subtract(-300.0));
        Assert.Equal(new Rgb8(30, 255, 255), p.Multiply(3.0));
        Assert.Equal(new Rgb8(0, 0, 0), p.Multiply(-1.0));
        Assert.Equal(new Rgb8(100, 255, 255), p.Divide(0.1));
        Assert.Equal(new Rgb8(0, 0, 0), p.Divide(-2.0));
        Assert.Equal(new Rgb8(0, 0, 0), p.Add(double.NaN));

        // IScalar operations (negative and > 255)
        var negativeScalar = new DummyScalar(-50.0);
        var largeScalar = new DummyScalar(300.0);
        var smallScalar = new DummyScalar(0.5);

        Assert.Equal(new Rgb8(0, 50, 190), p.Add(negativeScalar));
        Assert.Equal(new Rgb8(60, 150, 255), p.Subtract(negativeScalar));
        Assert.Equal(new Rgb8(0, 0, 0), p.Multiply(negativeScalar));
        Assert.Equal(new Rgb8(0, 0, 0), p.Divide(negativeScalar));

        Assert.Equal(new Rgb8(255, 255, 255), p.Add(largeScalar));
        Assert.Equal(new Rgb8(0, 0, 0), p.Subtract(largeScalar));
        Assert.Equal(new Rgb8(255, 255, 255), p.Multiply(largeScalar));
        Assert.Equal(new Rgb8(0, 0, 0), p.Divide(largeScalar));
        Assert.Equal(new Rgb8(20, 200, 255), p.Divide(smallScalar));

        // Out-of-range FromDoubleVector
        if (Vector<double>.Count >= 3)
        {
            var vecData = new double[Vector<double>.Count];
            vecData[0] = -0.5;
            vecData[1] = 1.5;
            vecData[2] = double.NaN;
            Assert.Equal(new Rgb8(0, 255, 0), Rgb8.FromDoubleVector(new Vector<double>(vecData)));
        }
    }

    [Fact]
    public void NumericOperations_Decimal_ShouldWork()
    {
        var p = new Rgb8(10, 20, 30);

        Assert.Equal(new Rgb8(15, 25, 35), p.Add(5.0m));
        Assert.Equal(new Rgb8(5, 15, 25), p.Subtract(5.0m));
        Assert.Equal(new Rgb8(20, 40, 60), p.Multiply(2.0m));
        Assert.Equal(new Rgb8(5, 10, 15), p.Divide(2.0m));
    }

    [Fact]
    public void NumericOperations_Double_ShouldWork()
    {
        var p = new Rgb8(10, 20, 30);

        Assert.Equal(new Rgb8(15, 25, 35), p.Add(5.0));
        Assert.Equal(new Rgb8(5, 15, 25), p.Subtract(5.0));
        Assert.Equal(new Rgb8(20, 40, 60), p.Multiply(2.0));
        Assert.Equal(new Rgb8(5, 10, 15), p.Divide(2.0));
    }

    

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

    [Fact]
    public void GetHashCode_AndDistance_ShouldWork()
    {
        var p1 = new Rgb8(10, 20, 30);
        var p2 = new Rgb8(13, 24, 30);

        Assert.Equal(10 << 16 | 20 << 8 | 30, p1.GetHashCode());
        Assert.Equal(25.0, p1.DistanceSquared(p2));
        Assert.Equal(5.0, p1.Distance(p2));
    }

    [Fact]
    public void ComponentMaps_ShouldWork()
    {
        var p = new Rgb8(10, 20, 30);
        Assert.Equal(new Rgb8(20, 40, 60), p.ComponentMap(b => (byte)(b * 2)));
        Assert.Equal(new Rgb8(20, 40, 60), p.ColorComponentMap(b => (byte)(b * 2)));
    }
}
