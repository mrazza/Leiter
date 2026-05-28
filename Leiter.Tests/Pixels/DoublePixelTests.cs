using Leiter.Tests.TestUtils;
using Xunit;
using Leiter.Pixels;
using Leiter.Core;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Leiter.Tests.Pixels;

/// <summary>
/// Provides unit tests or helpers for <see cref="DoublePixelTests" />.
/// </summary>
public class DoublePixelTests
{
    /// <summary>
    /// Verifies that the properties and implicit operators should work behaves correctly.
    /// </summary>
    [Fact]
    public void Properties_AndImplicitOperators_ShouldWork()
    {
        var p = new DoublePixel(2.5);
        Assert.Equal(2.5, p.Value);
        Assert.Equal(2.5, p.AsDouble());
        Assert.Equal(DoublePixel.Zero.Value, 0.0);

        // Implicit conversions
        double d = p;
        Assert.Equal(2.5, d);

        DoublePixel p2 = 3.5;
        Assert.Equal(3.5, p2.Value);
    }

    /// <summary>
    /// Verifies that the self operations should work behaves correctly.
    /// </summary>
    [Fact]
    public void SelfOperations_ShouldWork()
    {
        var p1 = new DoublePixel(10.0);
        var p2 = new DoublePixel(2.0);

        Assert.Equal(12.0, p1.Add(p2).Value);
        Assert.Equal(8.0, p1.Subtract(p2).Value);
        Assert.Equal(20.0, p1.Multiply(p2).Value);
        Assert.Equal(5.0, p1.Divide(p2).Value);
    }

    /// <summary>
    /// Verifies that the numeric operations decimal should work behaves correctly.
    /// </summary>
    [Fact]
    public void NumericOperations_Decimal_ShouldWork()
    {
        var p = new DoublePixel(10.0);

        Assert.Equal(12.0, p.Add(2.0m).Value);
        Assert.Equal(8.0, p.Subtract(2.0m).Value);
        Assert.Equal(20.0, p.Multiply(2.0m).Value);
        Assert.Equal(5.0, p.Divide(2.0m).Value);
    }

    /// <summary>
    /// Verifies that the numeric operations double should work behaves correctly.
    /// </summary>
    [Fact]
    public void NumericOperations_Double_ShouldWork()
    {
        var p = new DoublePixel(10.0);

        Assert.Equal(12.0, p.Add(2.0).Value);
        Assert.Equal(8.0, p.Subtract(2.0).Value);
        Assert.Equal(20.0, p.Multiply(2.0).Value);
        Assert.Equal(5.0, p.Divide(2.0).Value);
    }

    

    /// <summary>
    /// Verifies that the scalar operations should work behaves correctly.
    /// </summary>
    [Fact]
    public void ScalarOperations_ShouldWork()
    {
        var p = new DoublePixel(10.0);
        var scalar = new DummyScalar(2.0);

        Assert.Equal(12.0, p.Add(scalar).Value);
        Assert.Equal(8.0, p.Subtract(scalar).Value);
        Assert.Equal(20.0, p.Multiply(scalar).Value);
        Assert.Equal(5.0, p.Divide(scalar).Value);
    }

    /// <summary>
    /// Verifies that the vector operations should work behaves correctly.
    /// </summary>
    [Fact]
    public void VectorOperations_ShouldWork()
    {
        var p = new DoublePixel(5.5);
        if (Vector<double>.Count >= 1)
        {
            var vec = p.ToDoubleVector();
            Assert.Equal(5.5, vec[0]);

            var pFromVec = DoublePixel.FromDoubleVector(vec);
            Assert.Equal(5.5, pFromVec.Value);
        }
    }

    /// <summary>
    /// Verifies that the enumerator should yield value behaves correctly.
    /// </summary>
    [Fact]
    public void Enumerator_ShouldYieldValue()
    {
        var p = new DoublePixel(5.5);
        var enumerator = p.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(5.5, enumerator.Current);
        Assert.False(enumerator.MoveNext());

        // Test non-generic enumerator
        var seq = (System.Collections.IEnumerable)p;
        var seqEnum = seq.GetEnumerator();
        Assert.True(seqEnum.MoveNext());
        Assert.Equal(5.5, seqEnum.Current);
    }

    /// <summary>
    /// Verifies that the distance should compute correctly behaves correctly.
    /// </summary>
    [Fact]
    public void Distance_ShouldComputeCorrectly()
    {
        var p1 = new DoublePixel(1.5);
        var p2 = new DoublePixel(4.0);
        Assert.Equal(2.5, p1.Distance(p2));
    }

    /// <summary>
    /// Verifies that the maps should work behaves correctly.
    /// </summary>
    [Fact]
    public void Maps_ShouldWork()
    {
        var p = new DoublePixel(5.0);
        var r1 = p.ColorComponentMap(v => v * 2);
        Assert.Equal(10.0, r1.Value);

        var r2 = p.ComponentMap(v => v + 3);
        Assert.Equal(8.0, r2.Value);
    }
}
