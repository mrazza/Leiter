
using Xunit;
using Leiter.Pixels;
using Leiter.Core;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Leiter.Tests.Pixels;

public class Xyz32Tests
{
    [Fact]
    public void Properties_ShouldWork()
    {
        var p = new Xyz32(1.0f, 2.0f, 3.0f);
        Assert.Equal(1.0f, p.X);
        Assert.Equal(2.0f, p.Y);
        Assert.Equal(3.0f, p.Z);
        Assert.Equal(0.0f, Xyz32.Zero.X);
    }

    [Fact]
    public void SelfOperations_ShouldWork()
    {
        var p1 = new Xyz32(10.0f, 20.0f, 30.0f);
        var p2 = new Xyz32(2.0f, 2.0f, 2.0f);

        Assert.Equal(new Xyz32(12.0f, 22.0f, 32.0f), p1.Add(p2));
        Assert.Equal(new Xyz32(8.0f, 18.0f, 28.0f), p1.Subtract(p2));
        Assert.Equal(new Xyz32(20.0f, 40.0f, 60.0f), p1.Multiply(p2));
        Assert.Equal(new Xyz32(5.0f, 10.0f, 15.0f), p1.Divide(p2));
    }

    [Fact]
    public void NumericOperations_Decimal_ShouldWork()
    {
        var p = new Xyz32(10.0f, 20.0f, 30.0f);

        Assert.Equal(new Xyz32(12.0f, 22.0f, 32.0f), p.Add(2.0m));
        Assert.Equal(new Xyz32(8.0f, 18.0f, 28.0f), p.Subtract(2.0m));
        Assert.Equal(new Xyz32(20.0f, 40.0f, 60.0f), p.Multiply(2.0m));
        Assert.Equal(new Xyz32(5.0f, 10.0f, 15.0f), p.Divide(2.0m));
    }

    [Fact]
    public void NumericOperations_Double_ShouldWork()
    {
        var p = new Xyz32(10.0f, 20.0f, 30.0f);

        Assert.Equal(new Xyz32(12.0f, 22.0f, 32.0f), p.Add(2.0));
        Assert.Equal(new Xyz32(8.0f, 18.0f, 28.0f), p.Subtract(2.0));
        Assert.Equal(new Xyz32(20.0f, 40.0f, 60.0f), p.Multiply(2.0));
        Assert.Equal(new Xyz32(5.0f, 10.0f, 15.0f), p.Divide(2.0));
    }

    private class DummyScalar : IScalar<DummyScalar>
    {
        public static DummyScalar Zero => new DummyScalar(0.0);
        public double Val { get; }
        public DummyScalar(double val) => Val = val;
        public double AsDouble() => Val;
        public DummyScalar Add(DummyScalar right) => new(Val + right.Val);
        public DummyScalar Subtract(DummyScalar right) => new(Val - right.Val);
        public DummyScalar Multiply(DummyScalar right) => new(Val * right.Val);
        public DummyScalar Divide(DummyScalar right) => new(Val / right.Val);
        public DummyScalar Add(decimal right) => new(Val + (double)right);
        public DummyScalar Subtract(decimal right) => new(Val - (double)right);
        public DummyScalar Multiply(decimal right) => new(Val * (double)right);
        public DummyScalar Divide(decimal right) => new(Val / (double)right);
        public DummyScalar Add(double right) => new(Val + right);
        public DummyScalar Subtract(double right) => new(Val - right);
        public DummyScalar Multiply(double right) => new(Val * right);
        public DummyScalar Divide(double right) => new(Val / right);
        public Vector<double> ToDoubleVector() => new(Val);
        public static DummyScalar FromDoubleVector(Vector<double> vector) => new(vector[0]);
    }

    [Fact]
    public void ScalarOperations_ShouldWork()
    {
        var p = new Xyz32(10.0f, 20.0f, 30.0f);
        var scalar = new DummyScalar(2.0);

        Assert.Equal(new Xyz32(12.0f, 22.0f, 32.0f), p.Add(scalar));
        Assert.Equal(new Xyz32(5.0f, 10.0f, 15.0f), p.Subtract(scalar));
        Assert.Equal(new Xyz32(20.0f, 40.0f, 60.0f), p.Multiply(scalar));
        Assert.Equal(new Xyz32(8.0f, 18.0f, 28.0f), p.Divide(scalar));
    }

    [Fact]
    public void VectorOperations_ShouldWork()
    {
        var p = new Xyz32(1.0f, 2.0f, 3.0f);
        if (Vector<double>.Count >= 3)
        {
            var vec = p.ToDoubleVector();
            Assert.Equal(1.0, vec[0]);

            var pFromVec = Xyz32.FromDoubleVector(vec);
            Assert.Equal(p, pFromVec);
        }
    }

    [Fact]
    public void Enumerator_ShouldYieldValues()
    {
        var p = new Xyz32(1.0f, 2.0f, 3.0f);
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
        var p = new Xyz32(10.0f, 20.0f, 30.0f);
        Assert.Equal(new Xyz32(20.0f, 20.0f, 60.0f), p.ColorComponentMap(b => b * 2)); // ColorComponentMap maps X and Z only!
        Assert.Equal(new Xyz32(20.0f, 40.0f, 60.0f), p.ComponentMap(b => b * 2));
    }

    [Fact]
    public void Distance_ShouldWork()
    {
        var p1 = new Xyz32(1.0f, 2.0f, 3.0f);
        var p2 = new Xyz32(4.0f, 2.0f, 3.0f);
        Assert.Equal(3.0, p1.Distance(p2));
    }
}
