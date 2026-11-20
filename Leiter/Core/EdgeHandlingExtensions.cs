namespace Leiter.Core;

/// <summary>
/// Provides coordinate and index resolution helpers for <see cref="EdgeHandling"/> border strategies.
/// </summary>
internal static class EdgeHandlingExtensions
{
    /// <summary>
    /// Resolves an integer index against an axis of length <paramref name="length"/> using the specified <paramref name="edgeHandling"/> strategy.
    /// </summary>
    /// <param name="edgeHandling">The border handling strategy.</param>
    /// <param name="index">The integer coordinate index to resolve.</param>
    /// <param name="length">The positive length of the axis.</param>
    /// <returns>A valid zero-based index in <c>[0, length - 1]</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="length"/> is less than or equal to zero, or <paramref name="edgeHandling"/> is not a supported <see cref="EdgeHandling"/> value.
    /// </exception>
    public static int ResolveIndex(this EdgeHandling edgeHandling, int index, int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(length, 0);

        return edgeHandling switch
        {
            EdgeHandling.Extend => Math.Clamp(index, 0, length - 1),
            _ => throw new ArgumentOutOfRangeException(nameof(edgeHandling), edgeHandling, $"Unsupported {nameof(EdgeHandling)} value.")
        };
    }

    /// <summary>
    /// Resolves a floating-point coordinate against an axis of length <paramref name="length"/> using the specified <paramref name="edgeHandling"/> strategy.
    /// </summary>
    /// <param name="edgeHandling">The border handling strategy.</param>
    /// <param name="coord">The floating-point coordinate to resolve. Positive and negative infinities clamp to the axis bounds.</param>
    /// <param name="length">The positive length of the axis.</param>
    /// <returns>A resolved coordinate in <c>[0, length - 1]</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="length"/> is less than or equal to zero, <paramref name="coord"/> is <see cref="double.NaN"/>,
    /// or <paramref name="edgeHandling"/> is not a supported <see cref="EdgeHandling"/> value.
    /// </exception>
    public static double ResolveCoordinate(this EdgeHandling edgeHandling, double coord, int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(length, 0);
        if (double.IsNaN(coord))
            throw new ArgumentOutOfRangeException(nameof(coord), coord, "Coordinate cannot be NaN.");

        return edgeHandling switch
        {
            EdgeHandling.Extend => Math.Clamp(coord, 0.0, length - 1),
            _ => throw new ArgumentOutOfRangeException(nameof(edgeHandling), edgeHandling, $"Unsupported {nameof(EdgeHandling)} value.")
        };
    }
}
