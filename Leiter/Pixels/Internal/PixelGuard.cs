namespace Leiter.Pixels.Internal;

using System;
using System.Runtime.CompilerServices;

/// <summary>
/// Provides shared internal validation guards for pixel types.
/// </summary>
internal static class PixelGuard
{
    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if any channel of <paramref name="color"/> is not finite.
    /// </summary>
    /// <param name="color">The RGB color to validate.</param>
    /// <param name="paramName">The automatically captured parameter expression name for <paramref name="color"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when any channel of <paramref name="color"/> is <see cref="double.NaN"/> or infinite.
    /// </exception>
    public static void ThrowIfNotFinite(
        Rgb64 color,
        [CallerArgumentExpression(nameof(color))] string? paramName = null)
    {
        if (!double.IsFinite(color.R) || !double.IsFinite(color.G) || !double.IsFinite(color.B))
            throw new ArgumentOutOfRangeException(paramName, color, "All RGB channels must be finite.");
    }
}
