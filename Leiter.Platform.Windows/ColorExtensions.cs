namespace Leiter.Platform.Windows;

using System.Drawing;
using System.Runtime.Versioning;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Contains extension methods for interoping between the Windows platform <see cref="Color"/>
/// and Leiter types.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ColorExtensions
{
    /// <summary>
    /// Converts a <see cref="Color"/> to a <see cref="Rgb8"/>.
    /// </summary>
    /// <param name="pixel">The pixel to convert.</param>
    /// <returns>The Leiter Rgb8 value with the same data.</returns>
    public static Rgb8 ToRgb8(this Color pixel)
        => new(pixel.R, pixel.G, pixel.B);

    /// <summary>
    /// Converts a Leiter <see cref="Rgb8"/> to a <see cref="Color"/>.
    /// </summary>
    /// <param name="matrix">The Leiter pixel to a <see cref="Color"/>.</param>
    /// <returns>The Rgb8 with the same data.</returns>
    public static Color ToColor(this Rgb8 pixel)
        => Color.FromArgb(pixel.R, pixel.G, pixel.B);
}