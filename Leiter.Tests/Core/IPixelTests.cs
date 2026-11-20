namespace Leiter.Tests.Core;

using System;
using System.Runtime.Intrinsics;
using Leiter.Core;
using Xunit;

/// <summary>
/// Verifies default interface members on <see cref="IPixel{P}"/>.
/// </summary>
public class IPixelTests
{
    [Fact]
    public void DefaultLerp_ShouldUseVectorInterpolation()
    {
        var from = new MinimalPixel(10.0);
        var to = new MinimalPixel(20.0);

        Assert.Equal(from, Interpolate(from, to, 0.0));
        Assert.Equal(to, Interpolate(from, to, 1.0));
        Assert.Equal(12.5, Interpolate(from, to, 0.25).Value, 10);
        Assert.Equal(5.0, Interpolate(from, to, -0.5).Value, 10);
    }

    private static T Interpolate<T>(T from, T to, double amount)
        where T : struct, IPixel<T> =>
        T.Lerp(from, to, amount);

    private readonly record struct MinimalPixel(double Value) : IPixel<MinimalPixel>
    {
        public static MinimalPixel Zero => default;
        public MinimalPixel Add(MinimalPixel right) => new(Value + right.Value);
        public MinimalPixel Subtract(MinimalPixel right) => new(Value - right.Value);
        public MinimalPixel Multiply(MinimalPixel right) => new(Value * right.Value);
        public MinimalPixel Divide(MinimalPixel right) => new(Value / right.Value);
        public MinimalPixel Add(decimal right) => new(Value + (double)right);
        public MinimalPixel Subtract(decimal right) => new(Value - (double)right);
        public MinimalPixel Multiply(decimal right) => new(Value * (double)right);
        public MinimalPixel Divide(decimal right) => new(Value / (double)right);
        public MinimalPixel Add(double right) => new(Value + right);
        public MinimalPixel Subtract(double right) => new(Value - right);
        public MinimalPixel Multiply(double right) => new(Value * right);
        public MinimalPixel Divide(double right) => new(Value / right);
        public double Distance(MinimalPixel otherPixel) => Math.Abs(Value - otherPixel.Value);
        public Vector256<double> ToDoubleVector() => Vector256.CreateScalar(Value);
        public static MinimalPixel FromDoubleVector(Vector256<double> vector) => new(vector[0]);
    }
}
