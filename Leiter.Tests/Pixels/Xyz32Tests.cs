using Leiter.Tests.TestUtils;
using Xunit;
using Leiter.Pixels;
using Leiter.Core;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Leiter.Tests.Pixels;

/// <summary>
/// Provides unit tests or helpers for <see cref="Xyz32Tests" />.
/// </summary>
public class Xyz32Tests
{
    /// <summary>
    /// Verifies that the properties should work behaves correctly.
    /// </summary>
    [Fact]
    public void Properties_ShouldWork()
    {
        var p = new Xyz32(1.0f, 2.0f, 3.0f);
        Assert.Equal(1.0f, p.X);
        Assert.Equal(2.0f, p.Y);
        Assert.Equal(3.0f, p.Z);
        Assert.Equal(0.0f, Xyz32.Zero.X);
    }

    /// <summary>
    /// Verifies that the self operations should work behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the numeric operations decimal should work behaves correctly.
    /// </summary>
    [Fact]
    public void NumericOperations_Decimal_ShouldWork()
    {
        var p = new Xyz32(10.0f, 20.0f, 30.0f);

        Assert.Equal(new Xyz32(12.0f, 22.0f, 32.0f), p.Add(2.0m));
        Assert.Equal(new Xyz32(8.0f, 18.0f, 28.0f), p.Subtract(2.0m));
        Assert.Equal(new Xyz32(20.0f, 40.0f, 60.0f), p.Multiply(2.0m));
        Assert.Equal(new Xyz32(5.0f, 10.0f, 15.0f), p.Divide(2.0m));
    }

    /// <summary>
    /// Verifies that the numeric operations double should work behaves correctly.
    /// </summary>
    [Fact]
    public void NumericOperations_Double_ShouldWork()
    {
        var p = new Xyz32(10.0f, 20.0f, 30.0f);

        Assert.Equal(new Xyz32(12.0f, 22.0f, 32.0f), p.Add(2.0));
        Assert.Equal(new Xyz32(8.0f, 18.0f, 28.0f), p.Subtract(2.0));
        Assert.Equal(new Xyz32(20.0f, 40.0f, 60.0f), p.Multiply(2.0));
        Assert.Equal(new Xyz32(5.0f, 10.0f, 15.0f), p.Divide(2.0));
    }

    

    /// <summary>
    /// Verifies that the scalar operations should work behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the vector operations should work behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the enumerator should yield values behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the component maps should work behaves correctly.
    /// </summary>
    [Fact]
    public void ComponentMaps_ShouldWork()
    {
        var p = new Xyz32(10.0f, 20.0f, 30.0f);
        Assert.Equal(new Xyz32(20.0f, 20.0f, 60.0f), p.ColorComponentMap(b => b * 2)); // ColorComponentMap maps X and Z only!
        Assert.Equal(new Xyz32(20.0f, 40.0f, 60.0f), p.ComponentMap(b => b * 2));
    }

    /// <summary>
    /// Verifies that the distance should work behaves correctly.
    /// </summary>
    [Fact]
    public void Distance_ShouldWork()
    {
        var p1 = new Xyz32(1.0f, 2.0f, 3.0f);
        var p2 = new Xyz32(4.0f, 2.0f, 3.0f);
        Assert.Equal(3.0, p1.Distance(p2));
    }
}
