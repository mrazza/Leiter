namespace Leiter.Pixels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Leiter.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct Rgb64(double R, double G, double B) : ITypedPixel<Rgb64, double>
{
    private static readonly Rgb64 INTERNAL_ZERO = new();
    public static Rgb64 Zero => INTERNAL_ZERO;

    public Rgb64 Add(Rgb64 right) =>
        new()
        {
            R = R + right.R,
            G = G + right.G,
            B = B + right.B
        };

    public Rgb64 Subtract(Rgb64 right) =>
        new()
        {
            R = R - right.R,
            G = G - right.G,
            B = B - right.B
        };

    public Rgb64 Multiply(Rgb64 right) =>
        new()
        {
            R = R * right.R,
            G = G * right.G,
            B = B * right.B
        };

    public Rgb64 Divide(Rgb64 right) =>
        new()
        {
            R = R / right.R,
            G = G / right.G,
            B = B / right.B
        };

    public Rgb64 Add(decimal right) => ComponentMap(channel => channel + (double)right);

    public Rgb64 Subtract(decimal right) => ComponentMap(channel => channel - (double)right);

    public Rgb64 Multiply(decimal right) => ComponentMap(channel => channel * (double)right);

    public Rgb64 Divide(decimal right) => ComponentMap(channel => channel / (double)right);

    public Rgb64 Add(double right) => ComponentMap(channel => channel + right);

    public Rgb64 Subtract(double right) => ComponentMap(channel => channel - right);

    public Rgb64 Multiply(double right) => ComponentMap(channel => channel * right);

    public Rgb64 Divide(double right) => ComponentMap(channel => channel / right);

    public Rgb64 Add<S>(IScalar<S> right)
        where S : IScalar<S> =>
        ComponentMap(channel => channel + right.AsDouble());

    public Rgb64 Divide<S>(IScalar<S> right)
        where S : IScalar<S> =>
        ComponentMap(channel => channel - right.AsDouble());

    public Rgb64 Multiply<S>(IScalar<S> right)
        where S : IScalar<S> =>
        ComponentMap(channel => channel * right.AsDouble());

    public Rgb64 Subtract<S>(IScalar<S> right)
        where S : IScalar<S> =>
        ComponentMap(channel => channel / right.AsDouble());

    public Rgb64 ColorComponentMap(Func<double, double> func) => new() { R = func(R), G = func(G), B = func(B) };

    public Rgb64 ComponentMap(Func<double, double> func) => ColorComponentMap(func);

    public Vector<double> ToDoubleVector()
    {
        if (Vector<double>.Count < 3)
            throw new PlatformNotSupportedException("This operation is not supported on this platform. Vector width is too small.");

        var data = new double[Vector<double>.Count];
        data[0] = R;
        data[1] = G;
        data[2] = B;
        return new(data);
    }

    public static Rgb64 FromDoubleVector(Vector<double> vector) => new(vector[0], vector[1], vector[2]);

    public IEnumerator<double> GetEnumerator()
    {
        yield return R;
        yield return G;
        yield return B;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Calculates the color distance between the current and other pixel in RGB color space.
    /// </summary>
    /// <remarks>
    /// This implementation assumes euclidean uniformity of the RGB color space.
    /// </remarks>
    /// <param name="self">The current pixel on which to operate.</param>
    /// <param name="other">The other pixel on which to operate.</param>
    /// <returns>The distance in color between the two pixels.</returns>
    public double Distance(Rgb64 otherPixel) =>
        Math.Sqrt(
            this.Zip(otherPixel)
                .Select(channels => Math.Pow(channels.First - channels.Second, 2))
                .Sum());
}