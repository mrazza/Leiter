namespace Leiter.Pixels;

using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Leiter.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct LongPixel(long Value) : ITypedPixel<LongPixel, long>, ITypedScalar<LongPixel, long>
{
    private static readonly LongPixel INTERNAL_ZERO = new();
    public static LongPixel Zero => INTERNAL_ZERO;

    public LongPixel ColorComponentMap(Func<long, long> func) => new() { Value = func(Value) };

    public LongPixel ComponentMap(Func<long, long> func) => ColorComponentMap(func);

    public LongPixel Add(LongPixel right) =>
        new()
        {
            Value = Value + right.Value
        };

    public LongPixel Subtract(LongPixel right) =>
        new()
        {
            Value = Value - right.Value
        };

    public LongPixel Multiply(LongPixel right) =>
        new()
        {
            Value = Value * right.Value
        };

    public LongPixel Divide(LongPixel right) =>
        new()
        {
            Value = Value / right.Value
        };

    public LongPixel Add(decimal right) => ComponentMap(channel => channel + (long)right);

    public LongPixel Subtract(decimal right) => ComponentMap(channel => channel - (long)right);

    public LongPixel Multiply(decimal right) => ComponentMap(channel => channel * (long)right);

    public LongPixel Divide(decimal right) => ComponentMap(channel => channel / (long)right);

    public LongPixel Add(double right) => ComponentMap(channel => channel + (long)right);

    public LongPixel Subtract(double right) => ComponentMap(channel => channel - (long)right);

    public LongPixel Multiply(double right) => ComponentMap(channel => channel * (long)right);

    public LongPixel Divide(double right) => ComponentMap(channel => channel / (long)right);

    public LongPixel Add<S>(IScalar<S> right) where S : notnull, IScalar<S> =>
        Add(right.AsDouble());

    public LongPixel Subtract<S>(IScalar<S> right) where S : notnull, IScalar<S> =>
        Subtract(right.AsDouble());

    public LongPixel Multiply<S>(IScalar<S> right) where S : notnull, IScalar<S> =>
        Multiply(right.AsDouble());

    public LongPixel Divide<S>(IScalar<S> right) where S : notnull, IScalar<S> =>
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

    public static LongPixel FromDoubleVector(Vector<double> vector) => (long)vector[0];

    public IEnumerator<long> GetEnumerator()
    {
        yield return Value;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public double Distance(LongPixel otherPixel) =>
        Math.Abs(Value - otherPixel.Value);

    public static implicit operator long(LongPixel pixel) => pixel.Value;

    public static implicit operator LongPixel(long value) => new(value);
}
