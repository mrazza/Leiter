namespace Leiter.Tests.Pixels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using Leiter.Core;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Shared contract tests for <see cref="ITypedPixel{TPixel, TComponent}"/> implementations.
/// </summary>
/// <typeparam name="TPixel">The pixel struct under test.</typeparam>
/// <typeparam name="TComponent">The unmanaged channel/component type of <typeparamref name="TPixel"/>.</typeparam>
public abstract class PixelContractTestsBase<TPixel, TComponent>
    where TPixel : struct, ITypedPixel<TPixel, TComponent>
    where TComponent : unmanaged, IComparable, IFormattable, IConvertible, IComparable<TComponent>, IEquatable<TComponent>
{
    protected abstract TPixel ArithmeticLeft { get; }

    protected abstract TPixel ArithmeticRight { get; }

    protected abstract (TPixel Add, TPixel Subtract, TPixel Multiply, TPixel Divide) ExpectedSelfResults { get; }

    protected abstract (TPixel Add, TPixel Subtract, TPixel Multiply, TPixel Divide) ExpectedScaledByTwoResults { get; }

    protected abstract TPixel VectorSample { get; }

    protected abstract double ExpectedVectorFirstComponent { get; }

    protected abstract TPixel EnumeratorSample { get; }

    protected abstract TComponent[] ExpectedComponents { get; }

    protected abstract Func<TComponent, TComponent> ComponentDoubleFunc { get; }

    protected abstract TPixel ExpectedComponentMapped { get; }

    protected abstract TPixel ExpectedColorComponentMapped { get; }

    protected abstract TPixel DistanceFirst { get; }

    protected abstract TPixel DistanceSecond { get; }

    protected abstract double ExpectedDistance { get; }

    protected abstract TPixel ExpectedLerpMidpoint { get; }

    [Fact]
    public void SelfOperations_ShouldWork()
    {
        (TPixel add, TPixel sub, TPixel mul, TPixel div) = ExpectedSelfResults;
        Assert.Equal(add, ArithmeticLeft.Add(ArithmeticRight));
        Assert.Equal(sub, ArithmeticLeft.Subtract(ArithmeticRight));
        Assert.Equal(mul, ArithmeticLeft.Multiply(ArithmeticRight));
        Assert.Equal(div, ArithmeticLeft.Divide(ArithmeticRight));
    }

    [Fact]
    public void NumericOperations_Decimal_ShouldWork()
    {
        (TPixel add, TPixel sub, TPixel mul, TPixel div) = ExpectedScaledByTwoResults;
        Assert.Equal(add, ArithmeticLeft.Add(2.0m));
        Assert.Equal(sub, ArithmeticLeft.Subtract(2.0m));
        Assert.Equal(mul, ArithmeticLeft.Multiply(2.0m));
        Assert.Equal(div, ArithmeticLeft.Divide(2.0m));
    }

    [Fact]
    public void NumericOperations_Double_ShouldWork()
    {
        (TPixel add, TPixel sub, TPixel mul, TPixel div) = ExpectedScaledByTwoResults;
        Assert.Equal(add, ArithmeticLeft.Add(2.0));
        Assert.Equal(sub, ArithmeticLeft.Subtract(2.0));
        Assert.Equal(mul, ArithmeticLeft.Multiply(2.0));
        Assert.Equal(div, ArithmeticLeft.Divide(2.0));
    }

    [Fact]
    public void ScalarOperations_ShouldWork()
    {
        var scalar = new DummyScalar(2.0);
        (TPixel add, TPixel sub, TPixel mul, TPixel div) = ExpectedScaledByTwoResults;
        Assert.Equal(add, ArithmeticLeft.Add(scalar));
        Assert.Equal(sub, ArithmeticLeft.Subtract(scalar));
        Assert.Equal(mul, ArithmeticLeft.Multiply(scalar));
        Assert.Equal(div, ArithmeticLeft.Divide(scalar));
    }

    [Fact]
    public void VectorOperations_ShouldWork()
    {
        Vector256<double> vec = VectorSample.ToDoubleVector();
        Assert.Equal(ExpectedVectorFirstComponent, vec[0], 5);

        TPixel pFromVec = TPixel.FromDoubleVector(vec);
        Assert.Equal(VectorSample, pFromVec);
    }

    [Fact]
    public void Enumerator_ShouldYieldValues()
    {
        var actual = new List<TComponent>(EnumeratorSample);
        Assert.Equal(ExpectedComponents, actual);

        IEnumerator nonGeneric = ((IEnumerable)EnumeratorSample).GetEnumerator();
        Assert.True(nonGeneric.MoveNext());
        Assert.Equal(ExpectedComponents[0], nonGeneric.Current);
    }

    [Fact]
    public void ComponentMaps_ShouldWork()
    {
        Assert.Equal(ExpectedComponentMapped, ArithmeticLeft.ComponentMap(ComponentDoubleFunc));
        Assert.Equal(ExpectedColorComponentMapped, ArithmeticLeft.ColorComponentMap(ComponentDoubleFunc));
    }

    [Fact]
    public void Distance_ShouldWork()
    {
        Assert.Equal(ExpectedDistance, DistanceFirst.Distance(DistanceSecond), 12);
    }

    [Fact]
    public void Lerp_ShouldInterpolateEndpointsAndMidpoint()
    {
        Assert.Equal(ArithmeticLeft, TPixel.Lerp(ArithmeticLeft, ArithmeticRight, 0.0));
        Assert.Equal(ArithmeticRight, TPixel.Lerp(ArithmeticLeft, ArithmeticRight, 1.0));
        Assert.Equal(ExpectedLerpMidpoint, TPixel.Lerp(ArithmeticLeft, ArithmeticRight, 0.5));
    }
}
