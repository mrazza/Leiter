namespace Leiter.Pixels;

using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Leiter.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct Rgb8(byte R, byte G, byte B) : ITypedPixel<Rgb8, byte>
{
    private static readonly Rgb8 INTERNAL_ZERO = new();
    public static Rgb8 Zero => INTERNAL_ZERO;

    public Rgb8 Add(Rgb8 right) =>
        new()
        {
            R = (byte)(R + right.R),
            G = (byte)(G + right.G),
            B = (byte)(B + right.B)
        };

    public Rgb8 Subtract(Rgb8 right) =>
        new()
        {
            R = (byte)(R - right.R),
            G = (byte)(G - right.G),
            B = (byte)(B - right.B)
        };

    public Rgb8 Multiply(Rgb8 right) =>
        new()
        {
            R = (byte)(R * right.R),
            G = (byte)(G * right.G),
            B = (byte)(B * right.B)
        };

    public Rgb8 Divide(Rgb8 right) =>
        new()
        {
            R = (byte)(R / right.R),
            G = (byte)(G / right.G),
            B = (byte)(B / right.B)
        };

    public Rgb8 Add(decimal right) => ComponentMap(channel => (byte)(channel + right));

    public Rgb8 Subtract(decimal right) => ComponentMap(channel => (byte)(channel - right));

    public Rgb8 Multiply(decimal right) => ComponentMap(channel => (byte)(channel * right));

    public Rgb8 Divide(decimal right) => ComponentMap(channel => (byte)(channel / right));

    public Rgb8 Add(double right) => ComponentMap(channel => (byte)(channel + right));

    public Rgb8 Subtract(double right) => ComponentMap(channel => (byte)(channel - right));

    public Rgb8 Multiply(double right) => ComponentMap(channel => (byte)(channel * right));

    public Rgb8 Divide(double right) => ComponentMap(channel => (byte)(channel / right));

    public Rgb8 Add<S>(IScalar<S> right) where S : IScalar<S> =>
        Add((decimal)Convert.ToByte(right.AsDouble()));

    public Rgb8 Subtract<S>(IScalar<S> right) where S : IScalar<S> =>
        Subtract((decimal)Convert.ToByte(right.AsDouble()));

    public Rgb8 Multiply<S>(IScalar<S> right) where S : IScalar<S> =>
        ComponentMap(channel => (byte)(channel * right.AsDouble()));

    public Rgb8 Divide<S>(IScalar<S> right) where S : IScalar<S> =>
        ComponentMap(channel => (byte)(channel / right.AsDouble()));

    public Rgb8 ColorComponentMap(Func<byte, byte> func) => new() { R = func(R), G = func(G), B = func(B) };

    public Rgb8 ComponentMap(Func<byte, byte> func) => ColorComponentMap(func);

    public Vector<double> ToDoubleVector()
    {
        if (Vector<double>.Count < 3)
            throw new PlatformNotSupportedException("This operation is not supported on this platform. Vector width is too small.");

        var data = new double[Vector<double>.Count];
        data[0] = R / 255.0;
        data[1] = G / 255.0;
        data[2] = B / 255.0;
        return new(data);
    }

    public static Rgb8 FromDoubleVector(Vector<double> vector) => new((byte)(vector[0] * 255), (byte)(vector[1] * 255), (byte)(vector[2] * 255));

    public IEnumerator<byte> GetEnumerator()
    {
        yield return R;
        yield return G;
        yield return B;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public double Distance(Rgb8 otherPixel) =>
        Math.Sqrt(
            this.Zip(otherPixel)
                .Select(channels => Math.Pow(channels.First - channels.Second, 2))
                .Sum());
}
