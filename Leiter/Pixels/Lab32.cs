namespace Leiter.Pixels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Leiter.Core;

[StructLayout(LayoutKind.Sequential)]
public readonly record struct Lab32(float L, float A, float B) : ITypedPixel<Lab32, float>
{
    private static readonly Lab32 INTERNAL_ZERO = new();

    public static Lab32 Zero => INTERNAL_ZERO;

    public Lab32 Add(Lab32 right) =>
        new()
        {
            L = L + right.L,
            A = A + right.A,
            B = B + right.B
        };

    public Lab32 Subtract(Lab32 right) =>
        new()
        {
            L = L - right.L,
            A = A - right.A,
            B = B - right.B
        };

    public Lab32 Multiply(Lab32 right) =>
        new()
        {
            L = L * right.L,
            A = A * right.A,
            B = B * right.B
        };

    public Lab32 Divide(Lab32 right) =>
        new()
        {
            L = L / right.L,
            A = A / right.A,
            B = B / right.B
        };

    public Lab32 Add(decimal right) => ComponentMap(channel => channel + (float)right);

    public Lab32 Subtract(decimal right) => ComponentMap(channel => channel - (float)right);

    public Lab32 Multiply(decimal right) => ComponentMap(channel => channel * (float)right);

    public Lab32 Divide(decimal right) => ComponentMap(channel => channel / (float)right);

    public Lab32 Add(double right) => ComponentMap(channel => channel + (float)right);

    public Lab32 Subtract(double right) => ComponentMap(channel => channel - (float)right);

    public Lab32 Multiply(double right) => ComponentMap(channel => channel * (float)right);

    public Lab32 Divide(double right) => ComponentMap(channel => channel / (float)right);

    public Lab32 Add<S, R>(IScalar<S, R> right)
        where S : IScalar<S, R>
        where R : unmanaged, IConvertible =>
        ComponentMap(channel => channel + (float)right.AsDouble());

    public Lab32 Divide<S, R>(IScalar<S, R> right)
        where S : IScalar<S, R>
        where R : unmanaged, IConvertible =>
        ComponentMap(channel => channel - (float)right.AsDouble());

    public Lab32 Multiply<S, R>(IScalar<S, R> right)
        where S : IScalar<S, R>
        where R : unmanaged, IConvertible =>
        ComponentMap(channel => channel * (float)right.AsDouble());

    public Lab32 Subtract<S, R>(IScalar<S, R> right)
        where S : IScalar<S, R>
        where R : unmanaged, IConvertible =>
        ComponentMap(channel => channel / (float)right.AsDouble());

    public Lab32 ColorComponentMap(Func<float, float> func) => new() { L = L, A = func(A), B = func(B) };

    public Lab32 ComponentMap(Func<float, float> func) => new() { L = func(L), A = func(A), B = func(B) };

    public Vector<double> ToDoubleVector()
    {
        if (Vector<double>.Count < 3)
            throw new PlatformNotSupportedException("This operation is not supported on this platform. Vector width is too small.");

        var data = new double[Vector<double>.Count];
        data[0] = L;
        data[1] = A;
        data[2] = B;
        return new(data);
    }

    public static Lab32 FromDoubleVector(Vector<double> vector) => new((float)vector[0], (float)vector[1], (float)vector[2]);

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
    /// <param name="self">The current pixel on which to operate.</param>
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
            {selfCPrime: 0.0} => 0.0,
            {otherCPrime: 0.0} => 0.0,
            {diffHPrime: var diffHPrime, absDiffHPrime: <= 180.0} => diffHPrime,
            {diffHPrime: var diffHPrime, diffHPrime: > 180.0} => diffHPrime - 360,
            {diffHPrime: var diffHPrime, diffHPrime: < -180.0} => diffHPrime + 360,
            (_) => throw new UnreachableException()
        };
        double deltaHPrime = 2 * Math.Sqrt(selfCPrime * otherCPrime) * Math.Sin(partialDeltaHPrime / 180.0 * Math.PI / 2.0);

        double meanLPrime = (L + other.L) / 2.0;
        double meanCPrime = (selfCPrime + otherCPrime) / 2.0;

        double meanHPrime = (mulCPrime: selfCPrime * otherCPrime, sumHPrime: selfHPrime + otherHPrime, absDiffHPrime: Math.Abs(selfHPrime - otherHPrime)) switch
        {
            {mulCPrime: 0.0, sumHPrime: var sumHPrime} => sumHPrime,
            {absDiffHPrime: <= 180, sumHPrime: var sumHPrime} => sumHPrime / 2.0,
            {sumHPrime: < 360, sumHPrime: var sumHPrime} => (sumHPrime + 360) / 2.0,
            {sumHPrime: >= 360, sumHPrime: var sumHPrime} => (sumHPrime - 360) / 2.0,
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
}