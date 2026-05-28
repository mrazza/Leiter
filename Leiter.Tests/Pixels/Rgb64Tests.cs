using Leiter.Tests.TestUtils;
using Xunit;
using Leiter.Pixels;
using Leiter.Core;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Leiter.Tests.Pixels;

public class Rgb64Tests
{
    [Fact]
    public void Properties_ShouldWork()
    {
        var p = new Rgb64(0.1, 0.2, 0.3);
        Assert.Equal(0.1, p.R);
        Assert.Equal(0.2, p.G);
        Assert.Equal(0.3, p.B);
        Assert.Equal(0.0, Rgb64.Zero.R);
    }

    [Fact]
    public void SelfOperations_ShouldWork()
    {
        var p1 = new Rgb64(10.0, 20.0, 30.0);
        var p2 = new Rgb64(2.0, 2.0, 2.0);

        Assert.Equal(new Rgb64(12.0, 22.0, 32.0), p1.Add(p2));
        Assert.Equal(new Rgb64(8.0, 18.0, 28.0), p1.Subtract(p2));
        Assert.Equal(new Rgb64(20.0, 40.0, 60.0), p1.Multiply(p2));
        Assert.Equal(new Rgb64(5.0, 10.0, 15.0), p1.Divide(p2));
    }

    [Fact]
    public void NumericOperations_Decimal_ShouldWork()
    {
        var p = new Rgb64(10.0, 20.0, 30.0);

        Assert.Equal(new Rgb64(12.0, 22.0, 32.0), p.Add(2.0m));
        Assert.Equal(new Rgb64(8.0, 18.0, 28.0), p.Subtract(2.0m));
        Assert.Equal(new Rgb64(20.0, 40.0, 60.0), p.Multiply(2.0m));
        Assert.Equal(new Rgb64(5.0, 10.0, 15.0), p.Divide(2.0m));
    }

    [Fact]
    public void NumericOperations_Double_ShouldWork()
    {
        var p = new Rgb64(10.0, 20.0, 30.0);

        Assert.Equal(new Rgb64(12.0, 22.0, 32.0), p.Add(2.0));
        Assert.Equal(new Rgb64(8.0, 18.0, 28.0), p.Subtract(2.0));
        Assert.Equal(new Rgb64(20.0, 40.0, 60.0), p.Multiply(2.0));
        Assert.Equal(new Rgb64(5.0, 10.0, 15.0), p.Divide(2.0));
    }

    

    [Fact]
    public void ScalarOperations_ShouldWork()
    {
        var p = new Rgb64(10.0, 20.0, 30.0);
        var scalar = new DummyScalar(2.0);

        Assert.Equal(new Rgb64(12.0, 22.0, 32.0), p.Add(scalar));
        Assert.Equal(new Rgb64(8.0, 18.0, 28.0), p.Divide(scalar));
        Assert.Equal(new Rgb64(5.0, 10.0, 15.0), p.Subtract(scalar));
    }

    [Fact]
    public void VectorOperations_ShouldWork()
    {
        var p = new Rgb64(1.0, 2.0, 3.0);
        if (Vector<double>.Count >= 3)
        {
            var vec = p.ToDoubleVector();
            Assert.Equal(1.0, vec[0]);

            var pFromVec = Rgb64.FromDoubleVector(vec);
            Assert.Equal(p, pFromVec);
        }
    }

    [Fact]
    public void Enumerator_ShouldYieldValues()
    {
        var p = new Rgb64(1.0, 2.0, 3.0);
        var list = new List<double>(p);
        Assert.Equal(new double[] { 1.0, 2.0, 3.0 }, list);

        var seq = (System.Collections.IEnumerable)p;
        var seqEnum = seq.GetEnumerator();
        Assert.True(seqEnum.MoveNext());
        Assert.Equal(1.0, seqEnum.Current);
    }

    [Fact]
    public void ComponentMaps_ShouldWork()
    {
        var p = new Rgb64(10.0, 20.0, 30.0);
        Assert.Equal(new Rgb64(20.0, 40.0, 60.0), p.ComponentMap(b => b * 2));
        Assert.Equal(new Rgb64(20.0, 40.0, 60.0), p.ColorComponentMap(b => b * 2));
    }

    [Fact]
    public void Distance_ShouldWork()
    {
        var p1 = new Rgb64(1.0, 2.0, 3.0);
        var p2 = new Rgb64(4.0, 2.0, 3.0);
        Assert.Equal(3.0, p1.Distance(p2));
    }
}
