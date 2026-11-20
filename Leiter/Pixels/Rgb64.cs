namespace Leiter.Pixels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
using Leiter.Core;

/// <summary>
/// Represents a color in the RGB color space using 64-bit floating-point channels.
/// </summary>
/// <param name="R">The red channel value.</param>
/// <param name="G">The green channel value.</param>
/// <param name="B">The blue channel value.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Rgb64(double R, double G, double B) : ITypedPixel<Rgb64, double>
{
    /// <inheritdoc/>
    public static Rgb64 Zero => default;

    /// <summary>
    /// Linearly interpolates component-wise between <paramref name="from"/> and <paramref name="to"/> by <paramref name="amount"/>.
    /// </summary>
    /// <param name="from">The starting color returned when <paramref name="amount"/> is <c>0.0</c>.</param>
    /// <param name="to">The target color returned when <paramref name="amount"/> is <c>1.0</c>.</param>
    /// <param name="amount">The interpolation factor, typically in <c>[0.0, 1.0]</c>.</param>
    /// <returns>The component-wise linearly interpolated <see cref="Rgb64"/> color.</returns>
    public static Rgb64 Lerp(Rgb64 from, Rgb64 to, double amount) =>
        new(
            ScalarMath.Lerp(from.R, to.R, amount),
            ScalarMath.Lerp(from.G, to.G, amount),
            ScalarMath.Lerp(from.B, to.B, amount));

    /// <inheritdoc/>
    public Rgb64 Add(Rgb64 right) =>
        new()
        {
            R = R + right.R,
            G = G + right.G,
            B = B + right.B
        };

    /// <inheritdoc/>
    public Rgb64 Subtract(Rgb64 right) =>
        new()
        {
            R = R - right.R,
            G = G - right.G,
            B = B - right.B
        };

    /// <inheritdoc/>
    public Rgb64 Multiply(Rgb64 right) =>
        new()
        {
            R = R * right.R,
            G = G * right.G,
            B = B * right.B
        };

    /// <inheritdoc/>
    public Rgb64 Divide(Rgb64 right) =>
        new()
        {
            R = R / right.R,
            G = G / right.G,
            B = B / right.B
        };

    /// <inheritdoc/>
    public Rgb64 Add(decimal right)
    {
        double v = (double)right;
        return new(R + v, G + v, B + v);
    }

    /// <inheritdoc/>
    public Rgb64 Subtract(decimal right)
    {
        double v = (double)right;
        return new(R - v, G - v, B - v);
    }

    /// <inheritdoc/>
    public Rgb64 Multiply(decimal right)
    {
        double v = (double)right;
        return new(R * v, G * v, B * v);
    }

    /// <inheritdoc/>
    public Rgb64 Divide(decimal right)
    {
        double v = (double)right;
        return new(R / v, G / v, B / v);
    }

    /// <inheritdoc/>
    public Rgb64 Add(double right) => new(R + right, G + right, B + right);

    /// <inheritdoc/>
    public Rgb64 Subtract(double right) => new(R - right, G - right, B - right);

    /// <inheritdoc/>
    public Rgb64 Multiply(double right) => new(R * right, G * right, B * right);

    /// <inheritdoc/>
    public Rgb64 Divide(double right) => new(R / right, G / right, B / right);

    /// <inheritdoc/>
    public Rgb64 Add<S>(IScalar<S> right) where S : IScalar<S> => Add(right.AsDouble());

    /// <inheritdoc/>
    public Rgb64 Divide<S>(IScalar<S> right) where S : IScalar<S> => Divide(right.AsDouble());

    /// <inheritdoc/>
    public Rgb64 Multiply<S>(IScalar<S> right) where S : IScalar<S> => Multiply(right.AsDouble());

    /// <inheritdoc/>
    public Rgb64 Subtract<S>(IScalar<S> right) where S : IScalar<S> => Subtract(right.AsDouble());

    /// <inheritdoc/>
    public Rgb64 ColorComponentMap(Func<double, double> func) => new() { R = func(R), G = func(G), B = func(B) };

    /// <inheritdoc/>
    public Rgb64 ComponentMap(Func<double, double> func) => ColorComponentMap(func);

    /// <inheritdoc/>
    public Vector256<double> ToDoubleVector() => Vector256.Create(R, G, B, 0.0);

    /// <summary>
    /// Converts the RGB channels of this pixel into a three-dimensional <see cref="Vector3D"/> <c>(R, G, B)</c>.
    /// </summary>
    /// <returns>A <see cref="Vector3D"/> containing <c>(R, G, B)</c>.</returns>
    public Vector3D ToVector3D() => new(R, G, B);

    /// <summary>
    /// Computes the squared Euclidean magnitude across the red, green, and blue channels (<c>R^2 + G^2 + B^2</c>).
    /// </summary>
    /// <returns>The squared Euclidean norm of this pixel's channels.</returns>
    public double NormSquared() => (R * R) + (G * G) + (B * B);

    /// <summary>
    /// Computes the Euclidean magnitude across the red, green, and blue channels (<c>sqrt(R^2 + G^2 + B^2)</c>).
    /// </summary>
    /// <returns>The Euclidean norm of this pixel's channels.</returns>
    public double Norm() => Math.Sqrt(NormSquared());

    /// <inheritdoc/>
    public static Rgb64 FromDoubleVector(Vector256<double> vector) => new(vector[0], vector[1], vector[2]);

    /// <inheritdoc/>
    public IEnumerator<double> GetEnumerator()
    {
        yield return R;
        yield return G;
        yield return B;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Calculates the squared color distance between the current and other pixel in RGB color space.
    /// </summary>
    /// <remarks>
    /// This implementation assumes Euclidean uniformity of the RGB color space and avoids the square root operation.
    /// </remarks>
    /// <param name="otherPixel">The other pixel on which to operate.</param>
    /// <returns>The squared distance in color between the two pixels.</returns>
    public double DistanceSquared(Rgb64 otherPixel)
    {
        double dr = R - otherPixel.R;
        double dg = G - otherPixel.G;
        double db = B - otherPixel.B;
        return (dr * dr) + (dg * dg) + (db * db);
    }

    /// <summary>
    /// Calculates the color distance between the current and other pixel in RGB color space.
    /// </summary>
    /// <remarks>
    /// This implementation assumes Euclidean uniformity of the RGB color space.
    /// </remarks>
    /// <param name="otherPixel">The other pixel on which to operate.</param>
    /// <returns>The distance in color between the two pixels.</returns>
    public double Distance(Rgb64 otherPixel) => Math.Sqrt(DistanceSquared(otherPixel));
}