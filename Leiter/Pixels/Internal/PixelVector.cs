namespace Leiter.Pixels.Internal;

using System.Numerics;

/// <summary>
/// Provides shared SIMD vector construction helpers for pixel types.
/// </summary>
internal static class PixelVector
{
    /// <summary>
    /// Creates a <see cref="Vector{Double}"/> with <paramref name="c0"/> in lane 0 and zeros in all remaining lanes.
    /// </summary>
    /// <param name="c0">The value for lane 0.</param>
    /// <returns>A <see cref="Vector{Double}"/> initialized with <paramref name="c0"/>.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when <see cref="Vector{Double}.Count"/> is less than 1.</exception>
    public static Vector<double> Create(double c0)
    {
        if (Vector<double>.Count < 1)
            throw new PlatformNotSupportedException("This operation is not supported on this platform. Vector width is too small.");

        Span<double> data = stackalloc double[Vector<double>.Count];
        data[0] = c0;
        return new(data);
    }

    /// <summary>
    /// Creates a <see cref="Vector{Double}"/> with <paramref name="c0"/>, <paramref name="c1"/>, and <paramref name="c2"/>
    /// in lanes 0 through 2 and zeros in all remaining lanes.
    /// </summary>
    /// <param name="c0">The value for lane 0.</param>
    /// <param name="c1">The value for lane 1.</param>
    /// <param name="c2">The value for lane 2.</param>
    /// <returns>A <see cref="Vector{Double}"/> initialized with the specified components.</returns>
    /// <exception cref="PlatformNotSupportedException">Thrown when <see cref="Vector{Double}.Count"/> is less than 3.</exception>
    public static Vector<double> Create(double c0, double c1, double c2)
    {
        if (Vector<double>.Count < 3)
            throw new PlatformNotSupportedException("This operation is not supported on this platform. Vector width is too small.");

        Span<double> data = stackalloc double[Vector<double>.Count];
        data[0] = c0;
        data[1] = c1;
        data[2] = c2;
        return new(data);
    }
}
