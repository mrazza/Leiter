namespace Leiter.Pixels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Leiter.Core;

/// <summary>
/// Represents a color in the CIELAB (L*a*b*) color space using 32-bit floating-point channels.
/// </summary>
/// <param name="L">The lightness channel (L*).</param>
/// <param name="A">The green-red chromaticity channel (a*).</param>
/// <param name="B">The blue-yellow chromaticity channel (b*).</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct Lab32(float L, float A, float B) : ITypedPixel<Lab32, float>
{
    /// <inheritdoc/>
    public static Lab32 Zero => default;

    /// <summary>
    /// Linearly interpolates component-wise between <paramref name="from"/> and <paramref name="to"/> by <paramref name="amount"/>.
    /// </summary>
    /// <param name="from">The starting color returned when <paramref name="amount"/> is <c>0.0</c>.</param>
    /// <param name="to">The target color returned when <paramref name="amount"/> is <c>1.0</c>.</param>
    /// <param name="amount">The interpolation factor, typically in <c>[0.0, 1.0]</c>.</param>
    /// <returns>The component-wise linearly interpolated <see cref="Lab32"/> color.</returns>
    public static Lab32 Lerp(Lab32 from, Lab32 to, double amount) =>
        new(
            (float)ScalarMath.Lerp(from.L, to.L, amount),
            (float)ScalarMath.Lerp(from.A, to.A, amount),
            (float)ScalarMath.Lerp(from.B, to.B, amount));

    /// <inheritdoc/>
    public Lab32 Add(Lab32 right) =>
        new()
        {
            L = L + right.L,
            A = A + right.A,
            B = B + right.B
        };

    /// <inheritdoc/>
    public Lab32 Subtract(Lab32 right) =>
        new()
        {
            L = L - right.L,
            A = A - right.A,
            B = B - right.B
        };

    /// <inheritdoc/>
    public Lab32 Multiply(Lab32 right) =>
        new()
        {
            L = L * right.L,
            A = A * right.A,
            B = B * right.B
        };

    /// <inheritdoc/>
    public Lab32 Divide(Lab32 right) =>
        new()
        {
            L = L / right.L,
            A = A / right.A,
            B = B / right.B
        };

    /// <inheritdoc/>
    public Lab32 Add(decimal right)
    {
        float v = (float)right;
        return new(L + v, A + v, B + v);
    }

    /// <inheritdoc/>
    public Lab32 Subtract(decimal right)
    {
        float v = (float)right;
        return new(L - v, A - v, B - v);
    }

    /// <inheritdoc/>
    public Lab32 Multiply(decimal right)
    {
        float v = (float)right;
        return new(L * v, A * v, B * v);
    }

    /// <inheritdoc/>
    public Lab32 Divide(decimal right)
    {
        float v = (float)right;
        return new(L / v, A / v, B / v);
    }

    /// <inheritdoc/>
    public Lab32 Add(double right)
    {
        float v = (float)right;
        return new(L + v, A + v, B + v);
    }

    /// <inheritdoc/>
    public Lab32 Subtract(double right)
    {
        float v = (float)right;
        return new(L - v, A - v, B - v);
    }

    /// <inheritdoc/>
    public Lab32 Multiply(double right)
    {
        float v = (float)right;
        return new(L * v, A * v, B * v);
    }

    /// <inheritdoc/>
    public Lab32 Divide(double right)
    {
        float v = (float)right;
        return new(L / v, A / v, B / v);
    }

    /// <inheritdoc/>
    public Lab32 Add<S>(IScalar<S> right) where S : IScalar<S> => Add(right.AsDouble());

    /// <inheritdoc/>
    public Lab32 Divide<S>(IScalar<S> right) where S : IScalar<S> => Divide(right.AsDouble());

    /// <inheritdoc/>
    public Lab32 Multiply<S>(IScalar<S> right) where S : IScalar<S> => Multiply(right.AsDouble());

    /// <inheritdoc/>
    public Lab32 Subtract<S>(IScalar<S> right) where S : IScalar<S> => Subtract(right.AsDouble());

    /// <summary>
    /// Maps the chromaticity channels (<see cref="A"/> and <see cref="B"/>) using the specified mapping function while preserving the lightness channel (<see cref="L"/>).
    /// </summary>
    /// <param name="func">The mapping function to apply to the chromaticity channels.</param>
    /// <returns>A new <see cref="Lab32"/> pixel with the mapped chromaticity channels and original lightness.</returns>
    public Lab32 ColorComponentMap(Func<float, float> func) => new() { L = L, A = func(A), B = func(B) };

    /// <inheritdoc/>
    public Lab32 ComponentMap(Func<float, float> func) => new() { L = func(L), A = func(A), B = func(B) };

    /// <inheritdoc/>
    public Vector<double> ToDoubleVector()
    {
        if (Vector<double>.Count < 3)
            throw new PlatformNotSupportedException("This operation is not supported on this platform. Vector width is too small.");

        Span<double> data = stackalloc double[Vector<double>.Count];
        data[0] = L;
        data[1] = A;
        data[2] = B;
        return new(data);
    }

    /// <inheritdoc/>
    public static Lab32 FromDoubleVector(Vector<double> vector) => new((float)vector[0], (float)vector[1], (float)vector[2]);

    /// <inheritdoc/>
    public IEnumerator<float> GetEnumerator()
    {
        yield return L;
        yield return A;
        yield return B;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Calculates the color distance between the current and other pixel in L*a*b* color space.
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
    /// <returns>The distance in color between the two pixels.</returns>
    public double Distance(Lab32 other)
    {
        const double kL = 1.0;
        const double kC = 1.0;
        const double kH = 1.0;

        double selfCStar = Math.Sqrt(A * A + B * B);
        double otherCStar = Math.Sqrt(other.A * other.A + other.B * other.B);
        double meanCStar = (selfCStar + otherCStar) / 2.0;

        double G = 0.5 * (1 - Math.Sqrt(Math.Pow(meanCStar, 7) /
                                       (Math.Pow(meanCStar, 7) + Math.Pow(25, 7))));

        double selfAPrime = (1 + G) * A;
        double otherAPrime = (1 + G) * other.A;

        double selfCPrime = Math.Sqrt(selfAPrime * selfAPrime + B * B);
        double otherCPrime = Math.Sqrt(otherAPrime * otherAPrime + other.B * other.B);

        double selfHPrime = B == 0 && selfAPrime == 0 ? 0 : Math.Atan2(B, selfAPrime) * 180.0 / Math.PI;
        double otherHPrime = other.B == 0 && otherAPrime == 0 ? 0 : Math.Atan2(other.B, otherAPrime) * 180.0 / Math.PI;
        if (selfHPrime < 0.0) selfHPrime += 360;
        if (otherHPrime < 0.0) otherHPrime += 360;

        double deltaLPrime = other.L - L;
        double deltaCPrime = otherCPrime - selfCPrime;

        double partialDeltaHPrime = (selfCPrime, otherCPrime, diffHPrime: otherHPrime - selfHPrime, absDiffHPrime: Math.Abs(otherHPrime - selfHPrime)) switch
        {
            { selfCPrime: 0.0 } => 0.0,
            { otherCPrime: 0.0 } => 0.0,
            { diffHPrime: var diffHPrime, absDiffHPrime: <= 180.0 } => diffHPrime,
            { diffHPrime: var diffHPrime, diffHPrime: > 180.0 } => diffHPrime - 360,
            { diffHPrime: var diffHPrime, diffHPrime: < -180.0 } => diffHPrime + 360,
            (_) => throw new UnreachableException()
        };
        double deltaHPrime = 2 * Math.Sqrt(selfCPrime * otherCPrime) * Math.Sin(partialDeltaHPrime / 180.0 * Math.PI / 2.0);

        double meanLPrime = (L + other.L) / 2.0;
        double meanCPrime = (selfCPrime + otherCPrime) / 2.0;

        double meanHPrime = (mulCPrime: selfCPrime * otherCPrime, sumHPrime: selfHPrime + otherHPrime, absDiffHPrime: Math.Abs(selfHPrime - otherHPrime)) switch
        {
            { mulCPrime: 0.0, sumHPrime: var sumHPrime } => sumHPrime,
            { absDiffHPrime: <= 180, sumHPrime: var sumHPrime } => sumHPrime / 2.0,
            { sumHPrime: < 360, sumHPrime: var sumHPrime } => (sumHPrime + 360) / 2.0,
            { sumHPrime: >= 360, sumHPrime: var sumHPrime } => (sumHPrime - 360) / 2.0,
            (_) => throw new UnreachableException()
        };

        double t = 1 -
                       0.17 * Math.Cos((meanHPrime - 30) / 180.0 * Math.PI) +
                       0.24 * Math.Cos(2.0 * meanHPrime / 180.0 * Math.PI) +
                       0.32 * Math.Cos((3.0 * meanHPrime + 6) / 180.0 * Math.PI) -
                       0.20 * Math.Cos((4.0 * meanHPrime - 63.0) / 180.0 * Math.PI);

        double deltaTheta = 30 * Math.Exp(-Math.Pow((meanHPrime - 275.0) / 25.0, 2));
        double rC = 2.0 * Math.Sqrt(Math.Pow(meanCPrime, 7) / (Math.Pow(meanCPrime, 7) + Math.Pow(25, 7)));
        double sL = 1 + 0.015 * Math.Pow(meanLPrime - 50, 2) / Math.Sqrt(20 + Math.Pow(meanLPrime - 50, 2));
        double sC = 1 + 0.045 * meanCPrime;
        double sH = 1 + 0.015 * meanCPrime * t;
        double rT = -Math.Sin(2.0 * deltaTheta / 180 * Math.PI) * rC;

        return Math.Sqrt(
            Math.Pow(deltaLPrime / (kL * sL), 2) +
            Math.Pow(deltaCPrime / (kC * sC), 2) +
            Math.Pow(deltaHPrime / (kH * sH), 2) +
            rT *
                (deltaCPrime / (kC * sC)) *
                (deltaHPrime / (kH * sH))
        );
    }

    /// <summary>
    /// Calculates the squared Euclidean (CIE76) color distance between the current pixel and <paramref name="other"/> in CIELAB space.
    /// </summary>
    /// <remarks>
    /// Computes <c>ΔL² + Δa² + Δb²</c> without heap allocations or square roots, suitable for fast inner-loop range checks.
    /// </remarks>
    /// <param name="other">The other pixel to compare with.</param>
    /// <returns>The squared CIE76 Euclidean color distance between the two pixels.</returns>
    public double EuclideanDistanceSquared(Lab32 other)
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
    public double EuclideanDistance(Lab32 other) => Math.Sqrt(EuclideanDistanceSquared(other));
}