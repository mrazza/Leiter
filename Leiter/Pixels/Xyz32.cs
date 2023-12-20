namespace Leiter.Pixels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Leiter.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct Xyz32(float X, float Y, float Z) : ITypedPixel<Xyz32, float>
{
    private static readonly Xyz32 INTERNAL_ZERO = new();

    public static Xyz32 Zero => INTERNAL_ZERO;

    public Xyz32 Add(Xyz32 right) =>
        new()
        {
            X = X + right.X,
            Y = Y + right.Y,
            Z = Z + right.Z
        };

    public Xyz32 Subtract(Xyz32 right) =>
        new()
        {
            X = X - right.X,
            Y = Y - right.Y,
            Z = Z - right.Z
        };

    public Xyz32 Multiply(Xyz32 right) =>
        new()
        {
            X = X * right.X,
            Y = Y * right.Y,
            Z = Z * right.Z
        };

    public Xyz32 Divide(Xyz32 right) =>
        new()
        {
            X = X / right.X,
            Y = Y / right.Y,
            Z = Z / right.Z
        };

    public Xyz32 Add(decimal right) => ComponentMap(channel => channel + (float)right);

    public Xyz32 Subtract(decimal right) => ComponentMap(channel => channel - (float)right);

    public Xyz32 Multiply(decimal right) => ComponentMap(channel => channel * (float)right);

    public Xyz32 Divide(decimal right) => ComponentMap(channel => channel / (float)right);

    public Xyz32 Add(double right) => ComponentMap(channel => channel + (float)right);

    public Xyz32 Subtract(double right) => ComponentMap(channel => channel - (float)right);

    public Xyz32 Multiply(double right) => ComponentMap(channel => channel * (float)right);

    public Xyz32 Divide(double right) => ComponentMap(channel => channel / (float)right);

    public Xyz32 Add<S, R>(IScalar<S, R> right)
        where S : IScalar<S, R>
        where R : unmanaged, IConvertible =>
        ComponentMap(channel => channel + (float)right.AsDouble());

    public Xyz32 Divide<S, R>(IScalar<S, R> right)
        where S : IScalar<S, R>
        where R : unmanaged, IConvertible =>
        ComponentMap(channel => channel - (float)right.AsDouble());

    public Xyz32 Multiply<S, R>(IScalar<S, R> right)
        where S : IScalar<S, R>
        where R : unmanaged, IConvertible =>
        ComponentMap(channel => channel * (float)right.AsDouble());

    public Xyz32 Subtract<S, R>(IScalar<S, R> right)
        where S : IScalar<S, R>
        where R : unmanaged, IConvertible =>
        ComponentMap(channel => channel / (float)right.AsDouble());

    public Xyz32 ColorComponentMap(Func<float, float> func) => new() { X = func(X), Y = Y, Z = func(Z) };

    public Xyz32 ComponentMap(Func<float, float> func) => new() { X = func(X), Y = func(Y), Z = func(Z) };

    public Vector<double> ToDoubleVector()
    {
        if (Vector<double>.Count < 3)
            throw new PlatformNotSupportedException("This operation is not supported on this platform. Vector width is too small.");

        var data = new double[Vector<double>.Count];
        data[0] = X;
        data[1] = Y;
        data[2] = Z;
        return new(data);
    }

    public static Xyz32 FromDoubleVector(Vector<double> vector) => new((float)vector[0], (float)vector[1], (float)vector[2]);

    public IEnumerator<float> GetEnumerator()
    {
        yield return X;
        yield return Y;
        yield return Z;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Calculates the color distance between the current and other pixel in XYZ color space.
    /// </summary>
    /// <remarks>
    /// This naive implementation assumes euclidean uniformity of the XYZ color space.
    /// </remarks>
    /// <param name="self">The current pixel on which to operate.</param>
    /// <param name="other">The other pixel on which to operate.</param>
    /// <returns>The distance in color between the two pixels.</returns>
    public double Distance(Xyz32 otherPixel) =>
        Math.Sqrt(
            this.Zip(otherPixel)
                .Select(channels => Math.Pow(channels.First - channels.Second, 2))
                .Sum());
}