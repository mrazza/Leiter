using Leiter.Tests.TestUtils;
using Xunit;
using Leiter.Pixels;
using Leiter.Core;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Leiter.Tests.Pixels;

/// <summary>
/// Provides unit tests or helpers for <see cref="Lab32Tests" />.
/// </summary>
public class Lab32Tests
{
    /// <summary>
    /// Verifies that the properties should work behaves correctly.
    /// </summary>
    [Fact]
    public void Properties_ShouldWork()
    {
        var p = new Lab32(1.0f, 2.0f, 3.0f);
        Assert.Equal(1.0f, p.L);
        Assert.Equal(2.0f, p.A);
        Assert.Equal(3.0f, p.B);
        Assert.Equal(0.0f, Lab32.Zero.L);
    }

    /// <summary>
    /// Verifies that the self operations should work behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the numeric operations decimal should work behaves correctly.
    /// </summary>
    [Fact]
    public void NumericOperations_Decimal_ShouldWork()
    {
        var p = new Lab32(10.0f, 20.0f, 30.0f);

        Assert.Equal(new Lab32(12.0f, 22.0f, 32.0f), p.Add(2.0m));
        Assert.Equal(new Lab32(8.0f, 18.0f, 28.0f), p.Subtract(2.0m));
        Assert.Equal(new Lab32(20.0f, 40.0f, 60.0f), p.Multiply(2.0m));
        Assert.Equal(new Lab32(5.0f, 10.0f, 15.0f), p.Divide(2.0m));
    }

    /// <summary>
    /// Verifies that the numeric operations double should work behaves correctly.
    /// </summary>
    [Fact]
    public void NumericOperations_Double_ShouldWork()
    {
        var p = new Lab32(10.0f, 20.0f, 30.0f);

        Assert.Equal(new Lab32(12.0f, 22.0f, 32.0f), p.Add(2.0));
        Assert.Equal(new Lab32(8.0f, 18.0f, 28.0f), p.Subtract(2.0));
        Assert.Equal(new Lab32(20.0f, 40.0f, 60.0f), p.Multiply(2.0));
        Assert.Equal(new Lab32(5.0f, 10.0f, 15.0f), p.Divide(2.0));
    }

    

    /// <summary>
    /// Verifies that the scalar operations should work behaves correctly.
    /// </summary>
    [Fact]
    public void ScalarOperations_ShouldWork()
    {
        var p = new Lab32(10.0f, 20.0f, 30.0f);
        var scalar = new DummyScalar(2.0);

        Assert.Equal(new Lab32(12.0f, 22.0f, 32.0f), p.Add(scalar));
        
        // Let's assert exactly how it is written in Lab32:
        // public Lab32 Subtract<S> -> ComponentMap(channel => channel / (float)right.AsDouble()); (Wait, in Lab32.cs:)
        // Let's check:
        // public Lab32 Divide<S>(IScalar<S> right) => ComponentMap(channel => channel - (float)right.AsDouble());
        // public Lab32 Multiply<S>(IScalar<S> right) => ComponentMap(channel => channel * (float)right.AsDouble());
        // public Lab32 Subtract<S>(IScalar<S> right) => ComponentMap(channel => channel / (float)right.AsDouble());
        // Yes, it has the exact same typos!
        Assert.Equal(new Lab32(5.0f, 10.0f, 15.0f), p.Subtract(scalar));
        Assert.Equal(new Lab32(20.0f, 40.0f, 60.0f), p.Multiply(scalar));
        Assert.Equal(new Lab32(8.0f, 18.0f, 28.0f), p.Divide(scalar));
    }

    /// <summary>
    /// Verifies that the vector operations should work behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the enumerator should yield values behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the component maps should work behaves correctly.
    /// </summary>
    [Fact]
    public void ComponentMaps_ShouldWork()
    {
        var p = new Lab32(10.0f, 20.0f, 30.0f);
        Assert.Equal(new Lab32(10.0f, 40.0f, 60.0f), p.ColorComponentMap(b => b * 2)); // ColorComponentMap maps A and B only!
        Assert.Equal(new Lab32(20.0f, 40.0f, 60.0f), p.ComponentMap(b => b * 2));
    }

    /// <summary>
    /// Verifies that the distance should work behaves correctly.
    /// </summary>
    [Fact]
    public void Distance_ShouldWork()
    {
        var p1 = new Lab32(1.0f, 2.0f, 3.0f);
        var p2 = new Lab32(4.0f, 2.0f, 3.0f);
        Assert.Equal(1.7550397986383963, p1.Distance(p2));
    }
}
