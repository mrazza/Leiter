namespace Leiter.Pixels;

using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Leiter.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct DoublePixel(double Value) : ITypedPixel<DoublePixel, double>, ITypedScalar<DoublePixel, double>
{
    private static readonly DoublePixel INTERNAL_ZERO = new();
    public static DoublePixel Zero => INTERNAL_ZERO;

    public DoublePixel ColorComponentMap(Func<double, double> func) => new() { Value = func(Value) };

    public DoublePixel ComponentMap(Func<double, double> func) => ColorComponentMap(func);

    public DoublePixel Add(DoublePixel right) =>
        new()
        {
            Value = Value + right.Value
        };

    public DoublePixel Subtract(DoublePixel right) =>
        new()
        {
            Value = Value - right.Value
        };

    public DoublePixel Multiply(DoublePixel right) =>
        new()
        {
            Value = Value * right.Value
        };

    public DoublePixel Divide(DoublePixel right) =>
        new()
        {
            Value = Value / right.Value
        };

    public DoublePixel Add(decimal right) => ComponentMap(channel => channel + (double)right);

    public DoublePixel Subtract(decimal right) => ComponentMap(channel => channel - (double)right);

    public DoublePixel Multiply(decimal right) => ComponentMap(channel => channel * (double)right);

    public DoublePixel Divide(decimal right) => ComponentMap(channel => channel / (double)right);

    public DoublePixel Add(double right) => ComponentMap(channel => channel + right);

    public DoublePixel Subtract(double right) => ComponentMap(channel => channel - right);

    public DoublePixel Multiply(double right) => ComponentMap(channel => channel * right);

    public DoublePixel Divide(double right) => ComponentMap(channel => channel / right);

    public DoublePixel Add<S>(IScalar<S> right) where S : notnull, IScalar<S> =>
        Add(right.AsDouble());

    public DoublePixel Subtract<S>(IScalar<S> right) where S : notnull, IScalar<S> =>
        Subtract(right.AsDouble());

    public DoublePixel Multiply<S>(IScalar<S> right) where S : notnull, IScalar<S> =>
        Multiply(right.AsDouble());

    public DoublePixel Divide<S>(IScalar<S> right) where S : notnull, IScalar<S> =>
        Divide(right.AsDouble());

    public double AsDouble() => Value;

    public Vector<double> ToDoubleVector()
    {
        if (Vector<double>.Count < 1)
            throw new PlatformNotSupportedException("This operation is not supported on this platform. Vector width is too small.");

        var data = new double[Vector<double>.Count];
        data[0] = Value;
        return new(data);
    }

    public static DoublePixel FromDoubleVector(Vector<double> vector) => vector[0];

    public IEnumerator<double> GetEnumerator()
    {
        yield return Value;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public double Distance(DoublePixel otherPixel) =>
        Math.Abs(Value - otherPixel.Value);

    public static implicit operator double(DoublePixel pixel) => pixel.Value;

    public static implicit operator DoublePixel(double value) => new(value);
}
