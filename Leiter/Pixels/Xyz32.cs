namespace Leiter.Pixels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
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

    /// <summary>
    /// Linearly interpolates component-wise between <paramref name="from"/> and <paramref name="to"/> by <paramref name="amount"/>.
    /// </summary>
    /// <param name="from">The starting color returned when <paramref name="amount"/> is <c>0.0</c>.</param>
    /// <param name="to">The target color returned when <paramref name="amount"/> is <c>1.0</c>.</param>
    /// <param name="amount">The interpolation factor, typically in <c>[0.0, 1.0]</c>.</param>
    /// <returns>The component-wise linearly interpolated <see cref="Xyz32"/> color.</returns>
    public static Xyz32 Lerp(Xyz32 from, Xyz32 to, double amount) =>
        new(
            (float)ScalarMath.Lerp(from.X, to.X, amount),
            (float)ScalarMath.Lerp(from.Y, to.Y, amount),
            (float)ScalarMath.Lerp(from.Z, to.Z, amount));

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
    public Vector256<double> ToDoubleVector() => Vector256.Create((double)X, Y, Z, 0.0);

    /// <inheritdoc/>
    public static Xyz32 FromDoubleVector(Vector256<double> vector) => new((float)vector[0], (float)vector[1], (float)vector[2]);

    /// <inheritdoc/>
    public IEnumerator<float> GetEnumerator()
    {
        yield return X;
        yield return Y;
        yield return Z;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Calculates the squared color distance between the current and other pixel in XYZ color space.
    /// </summary>
    /// <remarks>
    /// This naive implementation assumes Euclidean uniformity of the XYZ color space.
    /// </remarks>
    /// <param name="otherPixel">The other pixel on which to operate.</param>
    /// <returns>The squared Euclidean distance in color between the two pixels.</returns>
    public double DistanceSquared(Xyz32 otherPixel)
    {
        double dx = (double)X - otherPixel.X;
        double dy = (double)Y - otherPixel.Y;
        double dz = (double)Z - otherPixel.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    /// <summary>
    /// Calculates the color distance between the current and other pixel in XYZ color space.
    /// </summary>
    /// <remarks>
    /// This naive implementation assumes Euclidean uniformity of the XYZ color space.
    /// </remarks>
    /// <param name="otherPixel">The other pixel on which to operate.</param>
    /// <returns>The distance in color between the two pixels.</returns>
    public double Distance(Xyz32 otherPixel) => Math.Sqrt(DistanceSquared(otherPixel));
}