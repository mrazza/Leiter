namespace Leiter.Pixels;

using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Leiter.Core;

/// <summary>
/// Represents a scalar pixel backed by a 64-bit floating-point value.
/// </summary>
/// <param name="Value">The scalar value of the pixel.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct DoublePixel(double Value) : ITypedPixel<DoublePixel, double>, ITypedScalar<DoublePixel, double>
{
    /// <inheritdoc/>
    public static DoublePixel Zero => default;

    /// <inheritdoc/>
    public DoublePixel ColorComponentMap(Func<double, double> func) => new() { Value = func(Value) };

    /// <inheritdoc/>
    public DoublePixel ComponentMap(Func<double, double> func) => ColorComponentMap(func);

    /// <inheritdoc/>
    public DoublePixel Add(DoublePixel right) =>
        new()
        {
            Value = Value + right.Value
        };

    /// <inheritdoc/>
    public DoublePixel Subtract(DoublePixel right) =>
        new()
        {
            Value = Value - right.Value
        };

    /// <inheritdoc/>
    public DoublePixel Multiply(DoublePixel right) =>
        new()
        {
            Value = Value * right.Value
        };

    /// <inheritdoc/>
    public DoublePixel Divide(DoublePixel right) =>
        new()
        {
            Value = Value / right.Value
        };

    /// <inheritdoc/>
    public DoublePixel Add(decimal right) => new(Value + (double)right);

    /// <inheritdoc/>
    public DoublePixel Subtract(decimal right) => new(Value - (double)right);

    /// <inheritdoc/>
    public DoublePixel Multiply(decimal right) => new(Value * (double)right);

    /// <inheritdoc/>
    public DoublePixel Divide(decimal right) => new(Value / (double)right);

    /// <inheritdoc/>
    public DoublePixel Add(double right) => new(Value + right);

    /// <inheritdoc/>
    public DoublePixel Subtract(double right) => new(Value - right);

    /// <inheritdoc/>
    public DoublePixel Multiply(double right) => new(Value * right);

    /// <inheritdoc/>
    public DoublePixel Divide(double right) => new(Value / right);

    /// <inheritdoc/>
    public DoublePixel Add<S>(IScalar<S> right) where S : IScalar<S> =>
        Add(right.AsDouble());

    /// <inheritdoc/>
    public DoublePixel Subtract<S>(IScalar<S> right) where S : IScalar<S> =>
        Subtract(right.AsDouble());

    /// <inheritdoc/>
    public DoublePixel Multiply<S>(IScalar<S> right) where S : IScalar<S> =>
        Multiply(right.AsDouble());

    /// <inheritdoc/>
    public DoublePixel Divide<S>(IScalar<S> right) where S : IScalar<S> =>
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
    public static DoublePixel FromDoubleVector(Vector<double> vector) => vector[0];

    /// <inheritdoc/>
    public IEnumerator<double> GetEnumerator()
    {
        yield return Value;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Calculates the distance between this pixel and another pixel as the absolute difference <c>|Value - otherPixel.Value|</c>.
    /// </summary>
    /// <param name="otherPixel">The other pixel to compare with.</param>
    /// <returns>The absolute difference between the pixel values.</returns>
    public double Distance(DoublePixel otherPixel) =>
        Math.Abs(Value - otherPixel.Value);

    /// <summary>
    /// Implicitly converts a <see cref="DoublePixel"/> to its underlying <see cref="double"/> value.
    /// </summary>
    /// <param name="pixel">The pixel to convert.</param>
    /// <returns>The underlying <see cref="double"/> value of <paramref name="pixel"/>.</returns>
    public static implicit operator double(DoublePixel pixel) => pixel.Value;

    /// <summary>
    /// Implicitly converts a <see cref="double"/> value to a <see cref="DoublePixel"/>.
    /// </summary>
    /// <param name="value">The double value to wrap.</param>
    /// <returns>A new <see cref="DoublePixel"/> initialized with <paramref name="value"/>.</returns>
    public static implicit operator DoublePixel(double value) => new(value);
}
