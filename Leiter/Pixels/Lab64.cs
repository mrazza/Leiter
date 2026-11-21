namespace Leiter.Pixels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Leiter.Core;

/// <summary>
/// Represents a color in the CIELAB (L*a*b*) color space using 64-bit floating-point channels.
/// </summary>
/// <param name="L">The lightness channel (L*).</param>
/// <param name="A">The green-red chromaticity channel (a*).</param>
/// <param name="B">The blue-yellow chromaticity channel (b*).</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Lab64(double L, double A, double B) : ITypedPixel<Lab64, double>
{
    private const double Pow25To7 = 6_103_515_625.0; // 25^7
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    /// <inheritdoc/>
    public static Lab64 Zero => default;

    /// <summary>
    /// Linearly interpolates component-wise between <paramref name="from"/> and <paramref name="to"/> by <paramref name="amount"/>.
    /// </summary>
    /// <param name="from">The starting color returned when <paramref name="amount"/> is <c>0.0</c>.</param>
    /// <param name="to">The target color returned when <paramref name="amount"/> is <c>1.0</c>.</param>
    /// <param name="amount">The interpolation factor, typically in <c>[0.0, 1.0]</c>.</param>
    /// <returns>The component-wise linearly interpolated <see cref="Lab64"/> color.</returns>
    public static Lab64 Lerp(Lab64 from, Lab64 to, double amount) =>
        new(
            ScalarMath.Lerp(from.L, to.L, amount),
            ScalarMath.Lerp(from.A, to.A, amount),
            ScalarMath.Lerp(from.B, to.B, amount));

    /// <inheritdoc/>
    public Lab64 Add(Lab64 right) =>
        new()
        {
            L = L + right.L,
            A = A + right.A,
            B = B + right.B
        };

    /// <inheritdoc/>
    public Lab64 Subtract(Lab64 right) =>
        new()
        {
            L = L - right.L,
            A = A - right.A,
            B = B - right.B
        };

    /// <inheritdoc/>
    public Lab64 Multiply(Lab64 right) =>
        new()
        {
            L = L * right.L,
            A = A * right.A,
            B = B * right.B
        };

    /// <inheritdoc/>
    public Lab64 Divide(Lab64 right) =>
        new()
        {
            L = L / right.L,
            A = A / right.A,
            B = B / right.B
        };

    /// <inheritdoc/>
    public Lab64 Add(decimal right)
    {
        double v = (double)right;
        return new(L + v, A + v, B + v);
    }

    /// <inheritdoc/>
    public Lab64 Subtract(decimal right)
    {
        double v = (double)right;
        return new(L - v, A - v, B - v);
    }

    /// <inheritdoc/>
    public Lab64 Multiply(decimal right)
    {
        double v = (double)right;
        return new(L * v, A * v, B * v);
    }

    /// <inheritdoc/>
    public Lab64 Divide(decimal right)
    {
        double v = (double)right;
        return new(L / v, A / v, B / v);
    }

    /// <inheritdoc/>
    public Lab64 Add(double right) => new(L + right, A + right, B + right);

    /// <inheritdoc/>
    public Lab64 Subtract(double right) => new(L - right, A - right, B - right);

    /// <inheritdoc/>
    public Lab64 Multiply(double right) => new(L * right, A * right, B * right);

    /// <inheritdoc/>
    public Lab64 Divide(double right) => new(L / right, A / right, B / right);

    /// <inheritdoc/>
    public Lab64 Add<S>(IScalar<S> right) where S : IScalar<S> => Add(right.AsDouble());

    /// <inheritdoc/>
    public Lab64 Divide<S>(IScalar<S> right) where S : IScalar<S> => Divide(right.AsDouble());

    /// <inheritdoc/>
    public Lab64 Multiply<S>(IScalar<S> right) where S : IScalar<S> => Multiply(right.AsDouble());

    /// <inheritdoc/>
    public Lab64 Subtract<S>(IScalar<S> right) where S : IScalar<S> => Subtract(right.AsDouble());

    /// <summary>
    /// Adds two <see cref="Lab64"/> colors component-wise.
    /// </summary>
    /// <param name="left">The left color operand.</param>
    /// <param name="right">The right color operand.</param>
    /// <returns>The component-wise sum of the two colors.</returns>
    public static Lab64 operator +(Lab64 left, Lab64 right) => new(left.L + right.L, left.A + right.A, left.B + right.B);

    /// <summary>
    /// Subtracts the right <see cref="Lab64"/> color from the left color component-wise.
    /// </summary>
    /// <param name="left">The left color operand.</param>
    /// <param name="right">The right color operand.</param>
    /// <returns>The component-wise difference of the two colors.</returns>
    public static Lab64 operator -(Lab64 left, Lab64 right) => new(left.L - right.L, left.A - right.A, left.B - right.B);

    /// <summary>
    /// Negates all three channels of a <see cref="Lab64"/> color.
    /// </summary>
    /// <param name="pixel">The color to negate.</param>
    /// <returns>A color with negated channels.</returns>
    public static Lab64 operator -(Lab64 pixel) => new(-pixel.L, -pixel.A, -pixel.B);

    /// <summary>
    /// Multiplies all three channels of a <see cref="Lab64"/> color by a scalar factor.
    /// </summary>
    /// <param name="pixel">The color operand.</param>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <returns>The scaled <see cref="Lab64"/> color.</returns>
    public static Lab64 operator *(Lab64 pixel, double scalar) => new(pixel.L * scalar, pixel.A * scalar, pixel.B * scalar);

    /// <summary>
    /// Multiplies a scalar factor by all three channels of a <see cref="Lab64"/> color.
    /// </summary>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <param name="pixel">The color operand.</param>
    /// <returns>The scaled <see cref="Lab64"/> color.</returns>
    public static Lab64 operator *(double scalar, Lab64 pixel) => new(pixel.L * scalar, pixel.A * scalar, pixel.B * scalar);

    /// <summary>
    /// Divides all three channels of a <see cref="Lab64"/> color by a scalar divisor.
    /// </summary>
    /// <param name="pixel">The color operand.</param>
    /// <param name="scalar">The scalar divisor.</param>
    /// <returns>The scaled <see cref="Lab64"/> color.</returns>
    public static Lab64 operator /(Lab64 pixel, double scalar) => new(pixel.L / scalar, pixel.A / scalar, pixel.B / scalar);

    /// <summary>
    /// Maps the chromaticity channels (<see cref="A"/> and <see cref="B"/>) using the specified mapping function while preserving the lightness channel (<see cref="L"/>).
    /// </summary>
    /// <param name="func">The mapping function to apply to the chromaticity channels.</param>
    /// <returns>A new <see cref="Lab64"/> pixel with the mapped chromaticity channels and original lightness.</returns>
    public Lab64 ColorComponentMap(Func<double, double> func) => new() { L = L, A = func(A), B = func(B) };

    /// <inheritdoc/>
    public Lab64 ComponentMap(Func<double, double> func) => new() { L = func(L), A = func(A), B = func(B) };

    /// <inheritdoc/>
    public Vector256<double> ToDoubleVector() => Vector256.Create(L, A, B, 0.0);

    /// <summary>
    /// Converts the CIELAB coordinates of this pixel into a three-dimensional <see cref="Vector3D"/> <c>(L, A, B)</c>.
    /// </summary>
    /// <returns>A <see cref="Vector3D"/> containing <c>(L, A, B)</c>.</returns>
    public Vector3D ToVector3D() => new(L, A, B);

    /// <summary>
    /// Narrows the 64-bit floating-point CIELAB channels of this pixel to a 32-bit <see cref="Lab32"/> pixel.
    /// </summary>
    /// <returns>A <see cref="Lab32"/> pixel with channels cast to <see cref="float"/>.</returns>
    public Lab32 ToLab32() => new((float)L, (float)A, (float)B);

    /// <inheritdoc/>
    public static Lab64 FromDoubleVector(Vector256<double> vector) => new(vector[0], vector[1], vector[2]);

    /// <inheritdoc/>
    public IEnumerator<double> GetEnumerator()
    {
        yield return L;
        yield return A;
        yield return B;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Calculates the color distance between the current and other pixel in L*a*b* color space using <see cref="DeltaE2000"/>.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="DeltaE2000(Lab64)"/> (CIEDE2000). For fast squared Euclidean (CIE76) distance, see <see cref="EuclideanDistanceSquared"/> or <see cref="DistanceSquared"/>.
    /// </remarks>
    /// <param name="other">The other pixel on which to operate.</param>
    /// <returns>The CIEDE2000 color distance between the two pixels.</returns>
    public double Distance(Lab64 other) => DeltaE2000(other);

    /// <summary>
    /// Calculates the CIEDE2000 color difference between the current and other pixel in L*a*b* color space.
    /// </summary>
    /// <remarks>
    /// This implementation uses the CIEDE2000 method for calculating color distance as this method
    /// attempts to account for the non-uniformity of the L*a*b* color space.
    ///
    /// Implementation based on
    /// "The CIEDE2000 Color-Difference Formula: Implementation Notes, Supplementary Test Data, and Mathematical Observations,",
    /// G. Sharma, W. Wu, E. N. Dalal, Color Research and Application, vol. 30. No. 1, pp. 21-30, February 2005.
    /// </remarks>
    /// <param name="other">The other pixel on which to operate.</param>
    /// <returns>The CIEDE2000 color difference between the two pixels.</returns>
    public double DeltaE2000(Lab64 other)
    {
        if (L == other.L && A == other.A && B == other.B)
            return 0.0;

        return ComputeDeltaE2000(L, A, B, other.L, other.A, other.B);
    }

    internal static double ComputeDeltaE2000(double l1, double a1, double b1, double l2, double a2, double b2)
    {
        const double kL = 1.0;
        const double kC = 1.0;
        const double kH = 1.0;

        double selfCStar = Math.Sqrt((a1 * a1) + (b1 * b1));
        double otherCStar = Math.Sqrt((a2 * a2) + (b2 * b2));
        double meanCStar = (selfCStar + otherCStar) * 0.5;
        double meanCStar7 = Pow7(meanCStar);

        double g = 0.5 * (1.0 - Math.Sqrt(meanCStar7 / (meanCStar7 + Pow25To7)));

        double selfAPrime = (1.0 + g) * a1;
        double otherAPrime = (1.0 + g) * a2;

        double selfCPrime = Math.Sqrt((selfAPrime * selfAPrime) + (b1 * b1));
        double otherCPrime = Math.Sqrt((otherAPrime * otherAPrime) + (b2 * b2));

        double selfHPrime = b1 == 0.0 && selfAPrime == 0.0 ? 0.0 : Math.Atan2(b1, selfAPrime) * RadToDeg;
        double otherHPrime = b2 == 0.0 && otherAPrime == 0.0 ? 0.0 : Math.Atan2(b2, otherAPrime) * RadToDeg;
        if (selfHPrime < 0.0) selfHPrime += 360.0;
        if (otherHPrime < 0.0) otherHPrime += 360.0;

        double deltaLPrime = l2 - l1;
        double deltaCPrime = otherCPrime - selfCPrime;

        double partialDeltaHPrime;
        if (selfCPrime == 0.0 || otherCPrime == 0.0)
        {
            partialDeltaHPrime = 0.0;
        }
        else
        {
            double diffHPrime = otherHPrime - selfHPrime;
            partialDeltaHPrime = Math.Abs(diffHPrime) <= 180.0
                ? diffHPrime
                : diffHPrime > 180.0
                    ? diffHPrime - 360.0
                    : diffHPrime + 360.0;
        }
        double deltaHPrime = 2.0 * Math.Sqrt(selfCPrime * otherCPrime) * Math.Sin(partialDeltaHPrime * 0.5 * DegToRad);

        double meanLPrime = (l1 + l2) * 0.5;
        double meanCPrime = (selfCPrime + otherCPrime) * 0.5;

        double meanHPrime;
        double sumHPrime = selfHPrime + otherHPrime;
        if (selfCPrime * otherCPrime == 0.0)
        {
            meanHPrime = sumHPrime;
        }
        else if (Math.Abs(selfHPrime - otherHPrime) <= 180.0)
        {
            meanHPrime = sumHPrime * 0.5;
        }
        else
        {
            meanHPrime = sumHPrime < 360.0
                ? (sumHPrime + 360.0) * 0.5
                : (sumHPrime - 360.0) * 0.5;
        }

        double t = 1.0 -
                       (0.17 * Math.Cos((meanHPrime - 30.0) * DegToRad)) +
                       (0.24 * Math.Cos((2.0 * meanHPrime) * DegToRad)) +
                       (0.32 * Math.Cos(((3.0 * meanHPrime) + 6.0) * DegToRad)) -
                       (0.20 * Math.Cos(((4.0 * meanHPrime) - 63.0) * DegToRad));

        double deltaTheta = 30.0 * Math.Exp(-Square((meanHPrime - 275.0) / 25.0));
        double meanCPrime7 = Pow7(meanCPrime);
        double rC = 2.0 * Math.Sqrt(meanCPrime7 / (meanCPrime7 + Pow25To7));
        double meanLPrimeMinus50Sq = Square(meanLPrime - 50.0);
        double sL = 1.0 + ((0.015 * meanLPrimeMinus50Sq) / Math.Sqrt(20.0 + meanLPrimeMinus50Sq));
        double sC = 1.0 + (0.045 * meanCPrime);
        double sH = 1.0 + (0.015 * meanCPrime * t);
        double rT = -Math.Sin((2.0 * deltaTheta) * DegToRad) * rC;

        double deltaLTerm = deltaLPrime / (kL * sL);
        double deltaCTerm = deltaCPrime / (kC * sC);
        double deltaHTerm = deltaHPrime / (kH * sH);

        return Math.Sqrt(
            Square(deltaLTerm) +
            Square(deltaCTerm) +
            Square(deltaHTerm) +
            (rT * deltaCTerm * deltaHTerm));
    }

    /// <summary>
    /// Calculates the squared Euclidean (CIE76) color distance between the current pixel and <paramref name="other"/> in CIELAB space.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="EuclideanDistanceSquared"/> (<c>ΔL² + Δa² + Δb²</c>), NOT squared <see cref="DeltaE2000"/>.
    /// </remarks>
    /// <param name="other">The other pixel to compare with.</param>
    /// <returns>The squared CIE76 Euclidean color distance between the two pixels.</returns>
    public double DistanceSquared(Lab64 other) => EuclideanDistanceSquared(other);

    /// <summary>
    /// Calculates the squared Euclidean (CIE76) color distance between the current pixel and <paramref name="other"/> in CIELAB space.
    /// </summary>
    /// <remarks>
    /// Computes <c>ΔL² + Δa² + Δb²</c> without heap allocations or square roots, suitable for fast inner-loop range checks.
    /// </remarks>
    /// <param name="other">The other pixel to compare with.</param>
    /// <returns>The squared CIE76 Euclidean color distance between the two pixels.</returns>
    public double EuclideanDistanceSquared(Lab64 other)
    {
        double dl = L - other.L;
        double da = A - other.A;
        double db = B - other.B;
        return (dl * dl) + (da * da) + (db * db);
    }

    /// <summary>
    /// Calculates the Euclidean (CIE76) color distance between the current pixel and <paramref name="other"/> in CIELAB space.
    /// </summary>
    /// <remarks>
    /// Computes <c>sqrt(ΔL² + Δa² + Δb²)</c> without heap allocations, suitable for saliency and Mean-Shift clustering formulated in CIE76.
    /// </remarks>
    /// <param name="other">The other pixel to compare with.</param>
    /// <returns>The CIE76 Euclidean color distance between the two pixels.</returns>
    public double EuclideanDistance(Lab64 other) => Math.Sqrt(EuclideanDistanceSquared(other));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Square(double x) => x * x;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Pow7(double x)
    {
        double x2 = x * x;
        double x4 = x2 * x2;
        return x4 * x2 * x;
    }
}
