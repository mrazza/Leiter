using Leiter.Tests.TestUtils;
using Xunit;
using Leiter.Pixels;
using Leiter.Core;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Leiter.Tests.Pixels;

public class LongPixelTests
{
    [Fact]
    public void Properties_AndImplicitOperators_ShouldWork()
    {
        var p = new LongPixel(123L);
        Assert.Equal(123L, p.Value);
        Assert.Equal(123.0, p.AsDouble());
        Assert.Equal(LongPixel.Zero.Value, 0L);

        long l = p;
        Assert.Equal(123L, l);

        LongPixel p2 = 456L;
        Assert.Equal(456L, p2.Value);
    }

    [Fact]
    public void SelfOperations_ShouldWork()
    {
        var p1 = new LongPixel(10L);
        var p2 = new LongPixel(2L);

        Assert.Equal(12L, p1.Add(p2).Value);
        Assert.Equal(8L, p1.Subtract(p2).Value);
        Assert.Equal(20L, p1.Multiply(p2).Value);
        Assert.Equal(5L, p1.Divide(p2).Value);
    }

    [Fact]
    public void NumericOperations_Decimal_ShouldWork()
    {
        var p = new LongPixel(10L);

        Assert.Equal(12L, p.Add(2.0m).Value);
        Assert.Equal(8L, p.Subtract(2.0m).Value);
        Assert.Equal(20L, p.Multiply(2.0m).Value);
        Assert.Equal(5L, p.Divide(2.0m).Value);
    }

    [Fact]
    public void NumericOperations_Double_ShouldWork()
    {
        var p = new LongPixel(10L);

        Assert.Equal(12L, p.Add(2.0).Value);
        Assert.Equal(8L, p.Subtract(2.0).Value);
        Assert.Equal(20L, p.Multiply(2.0).Value);
        Assert.Equal(5L, p.Divide(2.0).Value);
    }

    

    [Fact]
    public void ScalarOperations_ShouldWork()
    {
        var p = new LongPixel(10L);
        var scalar = new DummyScalar(2.0);

        Assert.Equal(12L, p.Add(scalar).Value);
        Assert.Equal(8L, p.Subtract(scalar).Value);
        Assert.Equal(20L, p.Multiply(scalar).Value);
        Assert.Equal(5L, p.Divide(scalar).Value);
    }

    [Fact]
    public void VectorOperations_ShouldWork()
    {
        var p = new LongPixel(123L);
        if (Vector<double>.Count >= 1)
        {
            var vec = p.ToDoubleVector();
            Assert.Equal(123.0, vec[0]);

            var pFromVec = LongPixel.FromDoubleVector(vec);
            Assert.Equal(123L, pFromVec.Value);
        }
    }

    [Fact]
    public void Enumerator_ShouldYieldValue()
    {
        var p = new LongPixel(123L);
        var enumerator = p.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(123L, enumerator.Current);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void Distance_ShouldComputeCorrectly()
    {
        var p1 = new LongPixel(100L);
        var p2 = new LongPixel(150L);
        Assert.Equal(50.0, p1.Distance(p2));
    }

    [Fact]
    public void Maps_ShouldWork()
    {
        var p = new LongPixel(5L);
        var r1 = p.ColorComponentMap(v => v * 2);
        Assert.Equal(10L, r1.Value);

        var r2 = p.ComponentMap(v => v + 3);
        Assert.Equal(8L, r2.Value);
    }
}
