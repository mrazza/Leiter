namespace Leiter.Pixels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using Leiter.Core;

/// <summary>
/// Represents a color in the CIE 1931 XYZ color space using 32-bit floating-point channels.
/// </summary>
/// <param name="X">The X tristimulus component.</param>
/// <param name="Y">The Y luminance component.</param>
/// <param name="Z">The Z tristimulus component.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Xyz32(float X, float Y, float Z) : ITypedPixel<Xyz32, float>
{
    /// <inheritdoc/>
    public static Xyz32 Zero => default;

    /// <inheritdoc/>
    public Xyz32 Add(Xyz32 right) =>
        new()
        {
            X = X + right.X,
            Y = Y + right.Y,
            Z = Z + right.Z
        };

    /// <inheritdoc/>
    public Xyz32 Subtract(Xyz32 right) =>
        new()
        {
            X = X - right.X,
            Y = Y - right.Y,
            Z = Z - right.Z
        };

    /// <inheritdoc/>
    public Xyz32 Multiply(Xyz32 right) =>
        new()
        {
            X = X * right.X,
            Y = Y * right.Y,
            Z = Z * right.Z
        };

    /// <inheritdoc/>
    public Xyz32 Divide(Xyz32 right) =>
        new()
        {
            X = X / right.X,
            Y = Y / right.Y,
            Z = Z / right.Z
        };

    /// <inheritdoc/>
    public Xyz32 Add(decimal right)
    {
        float v = (float)right;
        return new(X + v, Y + v, Z + v);
    }

    /// <inheritdoc/>
    public Xyz32 Subtract(decimal right)
    {
        float v = (float)right;
        return new(X - v, Y - v, Z - v);
    }

    /// <inheritdoc/>
    public Xyz32 Multiply(decimal right)
    {
        float v = (float)right;
        return new(X * v, Y * v, Z * v);
    }

    /// <inheritdoc/>
    public Xyz32 Divide(decimal right)
    {
        float v = (float)right;
        return new(X / v, Y / v, Z / v);
    }

    /// <inheritdoc/>
    public Xyz32 Add(double right)
    {
        float v = (float)right;
        return new(X + v, Y + v, Z + v);
    }

    /// <inheritdoc/>
    public Xyz32 Subtract(double right)
    {
        float v = (float)right;
        return new(X - v, Y - v, Z - v);
    }

    /// <inheritdoc/>
    public Xyz32 Multiply(double right)
    {
        float v = (float)right;
        return new(X * v, Y * v, Z * v);
    }

    /// <inheritdoc/>
    public Xyz32 Divide(double right)
    {
        float v = (float)right;
        return new(X / v, Y / v, Z / v);
    }

    /// <inheritdoc/>
    public Xyz32 Add<S>(IScalar<S> right) where S : IScalar<S> => Add(right.AsDouble());

    /// <inheritdoc/>
    public Xyz32 Divide<S>(IScalar<S> right) where S : IScalar<S> => Divide(right.AsDouble());

    /// <inheritdoc/>
    public Xyz32 Multiply<S>(IScalar<S> right) where S : IScalar<S> => Multiply(right.AsDouble());

    /// <inheritdoc/>
    public Xyz32 Subtract<S>(IScalar<S> right) where S : IScalar<S> => Subtract(right.AsDouble());

    /// <summary>
    /// Maps the <see cref="X"/> and <see cref="Z"/> chromaticity components using the specified mapping function while preserving luminance <see cref="Y"/>.
    /// </summary>
    /// <param name="func">The mapping function to apply to the X and Z components.</param>
    /// <returns>A new <see cref="Xyz32"/> pixel with mapped X and Z components and preserved Y luminance.</returns>
    public Xyz32 ColorComponentMap(Func<float, float> func) => new() { X = func(X), Y = Y, Z = func(Z) };

    /// <inheritdoc/>
    public Xyz32 ComponentMap(Func<float, float> func) => new() { X = func(X), Y = func(Y), Z = func(Z) };

    /// <inheritdoc/>
    public Vector<double> ToDoubleVector()
    {
        if (Vector<double>.Count < 3)
            throw new PlatformNotSupportedException("This operation is not supported on this platform. Vector width is too small.");

        Span<double> data = stackalloc double[Vector<double>.Count];
        data[0] = X;
        data[1] = Y;
        data[2] = Z;
        return new(data);
    }

    /// <inheritdoc/>
    public static Xyz32 FromDoubleVector(Vector<double> vector) => new((float)vector[0], (float)vector[1], (float)vector[2]);

    /// <inheritdoc/>
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
    /// This naive implementation assumes Euclidean uniformity of the XYZ color space.
    /// </remarks>
    /// <param name="otherPixel">The other pixel on which to operate.</param>
    /// <returns>The distance in color between the two pixels.</returns>
    public double Distance(Xyz32 otherPixel)
    {
        double dx = X - otherPixel.X;
        double dy = Y - otherPixel.Y;
        double dz = Z - otherPixel.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}