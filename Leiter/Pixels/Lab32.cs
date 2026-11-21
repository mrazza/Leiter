namespace Leiter.Pixels;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
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
    public Vector256<double> ToDoubleVector() => Vector256.Create((double)L, A, B, 0.0);

    /// <summary>
    /// Converts the CIELAB coordinates of this pixel into a three-dimensional <see cref="Vector3D"/> <c>(L, A, B)</c>.
    /// </summary>
    /// <returns>A <see cref="Vector3D"/> containing <c>(L, A, B)</c> widened to <see cref="double"/>.</returns>
    public Vector3D ToVector3D() => new(L, A, B);

    /// <summary>
    /// Widens the 32-bit floating-point CIELAB channels of this pixel to a 64-bit <see cref="Lab64"/> pixel.
    /// </summary>
    /// <returns>A <see cref="Lab64"/> pixel containing <c>(L, A, B)</c> widened to <see cref="double"/>.</returns>
    public Lab64 ToLab64() => new(L, A, B);

    /// <inheritdoc/>
    public static Lab32 FromDoubleVector(Vector256<double> vector) => new((float)vector[0], (float)vector[1], (float)vector[2]);

    /// <inheritdoc/>
    public IEnumerator<float> GetEnumerator()
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
    /// Delegates to <see cref="DeltaE2000(Lab32)"/> (CIEDE2000). For fast squared Euclidean (CIE76) distance, see <see cref="EuclideanDistanceSquared"/> or <see cref="DistanceSquared"/>.
    /// </remarks>
    /// <param name="other">The other pixel on which to operate.</param>
    /// <returns>The CIEDE2000 color distance between the two pixels.</returns>
    public double Distance(Lab32 other) => DeltaE2000(other);

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
    public double DeltaE2000(Lab32 other)
    {
        if (L == other.L && A == other.A && B == other.B)
            return 0.0;

        return Lab64.ComputeDeltaE2000(L, A, B, other.L, other.A, other.B);
    }

    /// <summary>
    /// Calculates the squared Euclidean (CIE76) color distance between the current pixel and <paramref name="other"/> in CIELAB space.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="EuclideanDistanceSquared"/> (<c>ΔL² + Δa² + Δb²</c>), NOT squared <see cref="DeltaE2000"/>.
    /// </remarks>
    /// <param name="other">The other pixel to compare with.</param>
    /// <returns>The squared CIE76 Euclidean color distance between the two pixels.</returns>
    public double DistanceSquared(Lab32 other) => EuclideanDistanceSquared(other);

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
        double dl = (double)L - other.L;
        double da = (double)A - other.A;
        double db = (double)B - other.B;
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