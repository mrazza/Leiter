namespace Leiter.Platform.Linux;

using Leiter.Core;
using Leiter.Pixels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Contains extension methods for interoperating between the Linux platform (via ImageSharp) <see cref="Rgb24"/>
/// and Leiter types.
/// </summary>
public static class ColorExtensions
{
    /// <summary>
    /// Converts a <see cref="Rgb24"/> to a <see cref="Rgb8"/>.
    /// </summary>
    /// <param name="pixel">The pixel to convert.</param>
    /// <returns>The Leiter <see cref="Rgb8"/> value with the same data.</returns>
    public static Rgb8 ToRgb8(this Rgb24 pixel)
        => new(pixel.R, pixel.G, pixel.B);

    /// <summary>
    /// Converts a Leiter <see cref="Rgb8"/> to a <see cref="Rgb24"/>.
    /// </summary>
    /// <param name="pixel">The Leiter pixel to convert.</param>
    /// <returns>The <see cref="Rgb24"/> with the same data.</returns>
    public static Rgb24 ToRgb24(this Rgb8 pixel)
        => new(pixel.R, pixel.G, pixel.B);
}
