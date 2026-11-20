namespace Leiter.Pixels;

using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Leiter.Core;

/// <summary>
/// Represents a scalar pixel backed by a 64-bit signed integer value.
/// </summary>
/// <param name="Value">The scalar integer value of the pixel.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct LongPixel(long Value) : ITypedPixel<LongPixel, long>, ITypedScalar<LongPixel, long>
{
    /// <inheritdoc/>
    public static LongPixel Zero => default;

    /// <summary>
    /// Linearly interpolates between <paramref name="from"/> and <paramref name="to"/> by <paramref name="amount"/>,
    /// rounding away from zero to the nearest 64-bit integer.
    /// </summary>
    /// <param name="from">The starting value returned when <paramref name="amount"/> is <c>0.0</c>.</param>
    /// <param name="to">The target value returned when <paramref name="amount"/> is <c>1.0</c>.</param>
    /// <param name="amount">The interpolation factor, typically in <c>[0.0, 1.0]</c>.</param>
    /// <returns>The interpolated and rounded <see cref="LongPixel"/> value.</returns>
    public static LongPixel Lerp(LongPixel from, LongPixel to, double amount) =>
        new((long)Math.Round(ScalarMath.Lerp(from.Value, to.Value, amount), MidpointRounding.AwayFromZero));

    /// <inheritdoc/>
    public LongPixel ColorComponentMap(Func<long, long> func) => new() { Value = func(Value) };

    /// <inheritdoc/>
    public LongPixel ComponentMap(Func<long, long> func) => ColorComponentMap(func);

    /// <inheritdoc/>
    public LongPixel Add(LongPixel right) =>
        new()
        {
            Value = Value + right.Value
        };

    /// <inheritdoc/>
    public LongPixel Subtract(LongPixel right) =>
        new()
        {
            Value = Value - right.Value
        };

    /// <inheritdoc/>
    public LongPixel Multiply(LongPixel right) =>
        new()
        {
            Value = Value * right.Value
        };

    /// <inheritdoc/>
    public LongPixel Divide(LongPixel right) =>
        new()
        {
            Value = Value / right.Value
        };

    /// <inheritdoc/>
    public LongPixel Add(decimal right) => new(Value + (long)right);

    /// <inheritdoc/>
    public LongPixel Subtract(decimal right) => new(Value - (long)right);

    /// <inheritdoc/>
    public LongPixel Multiply(decimal right) => new(Value * (long)right);

    /// <inheritdoc/>
    public LongPixel Divide(decimal right) => new(Value / (long)right);

    /// <inheritdoc/>
    public LongPixel Add(double right) => new(Value + (long)right);

    /// <inheritdoc/>
    public LongPixel Subtract(double right) => new(Value - (long)right);

    /// <inheritdoc/>
    public LongPixel Multiply(double right) => new(Value * (long)right);

    /// <inheritdoc/>
    public LongPixel Divide(double right) => new(Value / (long)right);

    /// <inheritdoc/>
    public LongPixel Add<S>(IScalar<S> right) where S : IScalar<S> =>
        Add(right.AsDouble());

    /// <inheritdoc/>
    public LongPixel Subtract<S>(IScalar<S> right) where S : IScalar<S> =>
        Subtract(right.AsDouble());

    /// <inheritdoc/>
    public LongPixel Multiply<S>(IScalar<S> right) where S : IScalar<S> =>
        Multiply(right.AsDouble());

    /// <inheritdoc/>
    public LongPixel Divide<S>(IScalar<S> right) where S : IScalar<S> =>
        Divide(right.AsDouble());

    /// <inheritdoc/>
    public double AsDouble() => Value;

    /// <inheritdoc/>
    public Vector<double> ToDoubleVector()
    {
        if (Vector<double>.Count < 1)
            throw new PlatformNotSupportedException("This operation is not supported on this platform. Vector width is too small.");

        Span<double> data = stackalloc double[Vector<double>.Count];
        data[0] = Value;
        return new(data);
    }

    /// <inheritdoc/>
    public static LongPixel FromDoubleVector(Vector<double> vector) => (long)vector[0];

    /// <inheritdoc/>
    public IEnumerator<long> GetEnumerator()
    {
        yield return Value;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Calculates the distance between this pixel and another pixel as the absolute difference <c>|Value - otherPixel.Value|</c>.
    /// </summary>
    /// <param name="otherPixel">The other pixel to compare with.</param>
    /// <returns>The absolute difference between the pixel values.</returns>
    public double Distance(LongPixel otherPixel) =>
        Math.Abs(Value - otherPixel.Value);

    /// <summary>
    /// Implicitly converts a <see cref="LongPixel"/> to its underlying <see cref="long"/> value.
    /// </summary>
    /// <param name="pixel">The pixel to convert.</param>
    /// <returns>The underlying <see cref="long"/> value of <paramref name="pixel"/>.</returns>
    public static implicit operator long(LongPixel pixel) => pixel.Value;

    /// <summary>
    /// Implicitly converts a <see cref="long"/> value to a <see cref="LongPixel"/>.
    /// </summary>
    /// <param name="value">The long value to wrap.</param>
    /// <returns>A new <see cref="LongPixel"/> initialized with <paramref name="value"/>.</returns>
    public static implicit operator LongPixel(long value) => new(value);
}
