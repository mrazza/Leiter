namespace Leiter.Pixels;

using System.Collections;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
using Leiter.Core;

/// <summary>
/// Represents a color in the RGB color space using 8-bit unsigned integer channels.
/// </summary>
/// <param name="R">The red channel value.</param>
/// <param name="G">The green channel value.</param>
/// <param name="B">The blue channel value.</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Rgb8(byte R, byte G, byte B) : ITypedPixel<Rgb8, byte>
{
    /// <inheritdoc/>
    public static Rgb8 Zero => default;

    private static byte ClampToByte(int value) => (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);
    private static byte ClampToByte(decimal value) => (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);

    /// <summary>
    /// Clamps a 64-bit floating-point value to an 8-bit unsigned byte range [0, 255].
    /// </summary>
    /// <remarks>
    /// If <paramref name="value"/> is <see cref="double.NaN"/>, returns <c>0</c>; otherwise, finite values are clamped to the range [0, 255].
    /// </remarks>
    /// <param name="value">The floating-point value to clamp.</param>
    /// <returns>A byte clamped to the range [0, 255], or <c>0</c> if <paramref name="value"/> is <see cref="double.NaN"/>.</returns>
    private static byte ClampToByte(double value) => double.IsNaN(value) ? (byte)0 : (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);

    /// <summary>
    /// Rounds a floating-point channel value in the [0, 255] range using <see cref="MidpointRounding.AwayFromZero"/> and clamps it to <see cref="byte"/> range.
    /// </summary>
    /// <remarks>
    /// Returns <c>0</c> when <paramref name="value"/> is <see cref="double.NaN"/> or non-positive, and <c>255</c> when <paramref name="value"/> is at least <c>255</c>.
    /// </remarks>
    /// <param name="value">The floating-point channel value in [0, 255] units.</param>
    /// <returns>The rounded and clamped 8-bit channel value.</returns>
    public static byte ClampRoundedByte(double value)
    {
        if (double.IsNaN(value) || value <= 0.0)
            return 0;
        if (value >= 255.0)
            return 255;

        return (byte)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Creates an <see cref="Rgb8"/> pixel from normalized [0, 1] floating-point channel values using <see cref="MidpointRounding.AwayFromZero"/> rounding and byte clamping.
    /// </summary>
    /// <param name="r">The normalized red channel value.</param>
    /// <param name="g">The normalized green channel value.</param>
    /// <param name="b">The normalized blue channel value.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with rounded, byte-clamped channels.</returns>
    public static Rgb8 FromNormalized(double r, double g, double b) =>
        new(ClampRoundedByte(r * 255.0), ClampRoundedByte(g * 255.0), ClampRoundedByte(b * 255.0));

    /// <summary>
    /// Linearly interpolates component-wise between <paramref name="from"/> and <paramref name="to"/> by <paramref name="amount"/>,
    /// rounding away from zero and clamping each channel to <c>[0, 255]</c>.
    /// </summary>
    /// <param name="from">The starting color returned when <paramref name="amount"/> is <c>0.0</c>.</param>
    /// <param name="to">The target color returned when <paramref name="amount"/> is <c>1.0</c>.</param>
    /// <param name="amount">The interpolation factor, typically in <c>[0.0, 1.0]</c>.</param>
    /// <returns>The interpolated, rounded, and byte-clamped <see cref="Rgb8"/> color.</returns>
    public static Rgb8 Lerp(Rgb8 from, Rgb8 to, double amount) =>
        new(
            ClampRoundedByte(ScalarMath.Lerp(from.R, to.R, amount)),
            ClampRoundedByte(ScalarMath.Lerp(from.G, to.G, amount)),
            ClampRoundedByte(ScalarMath.Lerp(from.B, to.B, amount)));

    /// <summary>
    /// Adds another <see cref="Rgb8"/> pixel to this pixel component-wise, saturating each channel to [0, 255].
    /// </summary>
    /// <param name="right">The pixel to add.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel sums.</returns>
    public Rgb8 Add(Rgb8 right) =>
        new()
        {
            R = ClampToByte(R + right.R),
            G = ClampToByte(G + right.G),
            B = ClampToByte(B + right.B)
        };

    /// <summary>
    /// Subtracts another <see cref="Rgb8"/> pixel from this pixel component-wise, saturating each channel to [0, 255].
    /// </summary>
    /// <param name="right">The pixel to subtract.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel differences.</returns>
    public Rgb8 Subtract(Rgb8 right) =>
        new()
        {
            R = ClampToByte(R - right.R),
            G = ClampToByte(G - right.G),
            B = ClampToByte(B - right.B)
        };

    /// <summary>
    /// Multiplies this pixel by another <see cref="Rgb8"/> pixel component-wise, saturating each raw byte channel product to [0, 255].
    /// </summary>
    /// <remarks>
    /// Operates directly on raw byte channel values (without normalizing to [0, 1]).
    /// </remarks>
    /// <param name="right">The pixel to multiply by.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel products.</returns>
    public Rgb8 Multiply(Rgb8 right) =>
        new()
        {
            R = ClampToByte(R * right.R),
            G = ClampToByte(G * right.G),
            B = ClampToByte(B * right.B)
        };

    /// <summary>
    /// Divides this pixel by another <see cref="Rgb8"/> pixel component-wise using integer division.
    /// </summary>
    /// <param name="right">The divisor pixel.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with integer division quotients.</returns>
    public Rgb8 Divide(Rgb8 right) =>
        new()
        {
            R = (byte)(R / right.R),
            G = (byte)(G / right.G),
            B = (byte)(B / right.B)
        };

    /// <summary>
    /// Adds a decimal scalar to each color channel, saturating each channel to [0, 255].
    /// </summary>
    /// <param name="right">The scalar value to add.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel sums.</returns>
    public Rgb8 Add(decimal right) => new(ClampToByte(R + right), ClampToByte(G + right), ClampToByte(B + right));

    /// <summary>
    /// Subtracts a decimal scalar from each color channel, saturating each channel to [0, 255].
    /// </summary>
    /// <param name="right">The scalar value to subtract.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel differences.</returns>
    public Rgb8 Subtract(decimal right) => new(ClampToByte(R - right), ClampToByte(G - right), ClampToByte(B - right));

    /// <summary>
    /// Multiplies each color channel by a decimal scalar, saturating each channel to [0, 255].
    /// </summary>
    /// <param name="right">The scalar multiplier.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel products.</returns>
    public Rgb8 Multiply(decimal right) => new(ClampToByte(R * right), ClampToByte(G * right), ClampToByte(B * right));

    /// <summary>
    /// Divides each color channel by a decimal scalar using integer division clamped to [0, 255].
    /// </summary>
    /// <param name="right">The scalar divisor.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with clamped quotients.</returns>
    public Rgb8 Divide(decimal right) => new(ClampToByte(R / right), ClampToByte(G / right), ClampToByte(B / right));

    /// <summary>
    /// Adds a double scalar to each color channel, saturating each channel to [0, 255].
    /// </summary>
    /// <param name="right">The scalar value to add.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel sums.</returns>
    public Rgb8 Add(double right) => new(ClampToByte(R + right), ClampToByte(G + right), ClampToByte(B + right));

    /// <summary>
    /// Subtracts a double scalar from each color channel, saturating each channel to [0, 255].
    /// </summary>
    /// <param name="right">The scalar value to subtract.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel differences.</returns>
    public Rgb8 Subtract(double right) => new(ClampToByte(R - right), ClampToByte(G - right), ClampToByte(B - right));

    /// <summary>
    /// Multiplies each color channel by a double scalar, saturating each channel to [0, 255].
    /// </summary>
    /// <param name="right">The scalar multiplier.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel products.</returns>
    public Rgb8 Multiply(double right) => new(ClampToByte(R * right), ClampToByte(G * right), ClampToByte(B * right));

    /// <summary>
    /// Divides each color channel by a double scalar using integer division clamped to [0, 255].
    /// </summary>
    /// <param name="right">The scalar divisor.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with clamped quotients.</returns>
    public Rgb8 Divide(double right) => new(ClampToByte(R / right), ClampToByte(G / right), ClampToByte(B / right));

    /// <summary>
    /// Adds a scalar value to each color channel, saturating each channel to [0, 255].
    /// </summary>
    /// <typeparam name="S">The scalar type.</typeparam>
    /// <param name="right">The scalar value to add.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel sums.</returns>
    public Rgb8 Add<S>(IScalar<S> right) where S : IScalar<S> => Add(right.AsDouble());

    /// <summary>
    /// Subtracts a scalar value from each color channel, saturating each channel to [0, 255].
    /// </summary>
    /// <typeparam name="S">The scalar type.</typeparam>
    /// <param name="right">The scalar value to subtract.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel differences.</returns>
    public Rgb8 Subtract<S>(IScalar<S> right) where S : IScalar<S> => Subtract(right.AsDouble());

    /// <summary>
    /// Multiplies each color channel by a scalar value, saturating each channel to [0, 255].
    /// </summary>
    /// <typeparam name="S">The scalar type.</typeparam>
    /// <param name="right">The scalar multiplier.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with saturated channel products.</returns>
    public Rgb8 Multiply<S>(IScalar<S> right) where S : IScalar<S> => Multiply(right.AsDouble());

    /// <summary>
    /// Divides each color channel by a scalar value using integer division clamped to [0, 255].
    /// </summary>
    /// <typeparam name="S">The scalar type.</typeparam>
    /// <param name="right">The scalar divisor.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with clamped quotients.</returns>
    public Rgb8 Divide<S>(IScalar<S> right) where S : IScalar<S> => Divide(right.AsDouble());

    /// <inheritdoc/>
    public Rgb8 ColorComponentMap(Func<byte, byte> func) => new() { R = func(R), G = func(G), B = func(B) };

    /// <inheritdoc/>
    public Rgb8 ComponentMap(Func<byte, byte> func) => ColorComponentMap(func);

    /// <summary>
    /// Converts this pixel to a <see cref="Vector256{Double}"/>, normalizing channel values from [0, 255] to [0.0, 1.0].
    /// </summary>
    /// <returns>A double vector containing the normalized RGB channel values.</returns>
    public Vector256<double> ToDoubleVector() =>
        Vector256.Create(R / 255.0, G / 255.0, B / 255.0, 0.0);

    /// <summary>
    /// Creates an <see cref="Rgb8"/> pixel from a <see cref="Vector256{Double}"/> by scaling normalized components by 255.0 with <see cref="MidpointRounding.AwayFromZero"/> rounding and clamping to [0, 255].
    /// </summary>
    /// <param name="vector">The double vector containing normalized RGB values.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with rounded, clamped byte channels.</returns>
    public static Rgb8 FromDoubleVector(Vector256<double> vector) => FromNormalized(vector[0], vector[1], vector[2]);

    /// <inheritdoc/>
    public IEnumerator<byte> GetEnumerator()
    {
        yield return R;
        yield return G;
        yield return B;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Returns the hash code for this pixel packed into a 24-bit integer as <c>(R &lt;&lt; 16) | (G &lt;&lt; 8) | B</c>.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code representing the packed RGB channels.</returns>
    public override int GetHashCode()
    {
        return R << 16 | G << 8 | B;
    }

    /// <summary>
    /// Calculates the squared Euclidean color distance between this pixel and another pixel in RGB color space.
    /// </summary>
    /// <remarks>
    /// This implementation assumes Euclidean uniformity of the RGB color space and avoids the square root operation.
    /// </remarks>
    /// <param name="otherPixel">The other pixel on which to operate.</param>
    /// <returns>The squared Euclidean distance in color between the two pixels.</returns>
    public double DistanceSquared(Rgb8 otherPixel)
    {
        double dr = R - otherPixel.R;
        double dg = G - otherPixel.G;
        double db = B - otherPixel.B;
        return (dr * dr) + (dg * dg) + (db * db);
    }

    /// <summary>
    /// Calculates the Euclidean color distance between this pixel and another pixel in RGB color space.
    /// </summary>
    /// <remarks>
    /// This implementation assumes Euclidean uniformity of the RGB color space.
    /// </remarks>
    /// <param name="otherPixel">The other pixel on which to operate.</param>
    /// <returns>The Euclidean distance in color between the two pixels.</returns>
    public double Distance(Rgb8 otherPixel) => Math.Sqrt(DistanceSquared(otherPixel));
}
