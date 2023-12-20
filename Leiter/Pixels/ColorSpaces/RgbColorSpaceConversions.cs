namespace Leiter.Pixels.ColorSpaces;

/// <summary>
/// Provides methods to convert between RGB color spaces.
/// </summary>
public static class RgbColorSpaceConversions
{
    /// <summary>
    /// Converts a <see cref="Rgb64"/> pixel from sRGB to linear RGB.
    /// </summary>
    /// <remarks>
    /// Performs gamma-expansion on the sRGB pixel into linear-light pixels.
    /// This method assumes a D65 white point for the sRGB pixel.
    /// </remarks>
    /// <param name="pixel">The sRGB pixel to convert.</param>
    /// <returns>A new <see cref="Rgb64"/> instance in the linear RGB color space.</returns>
    public static Rgb64 SRgbToLinearRgb(Rgb64 pixel) =>
        pixel.ColorComponentMap((channel) => (channel > 0.04045) ? Math.Pow((channel + 0.055) / 1.055, 2.4) : (channel / 12.92));

    /// <summary>
    /// Converts a <see cref="Rgb64"/> pixel from linear RGB to sRGB.
    /// </summary>
    /// <remarks>
    /// Performs gamma-translation on the linear RGB pixel into sRGB color space.
    /// This method assumes a D65 white point for the sRGB pixel.
    /// </remarks>
    /// <param name="pixel">The linear RGB pixel to convert.</param>
    /// <returns>A new <see cref="Rgb64"/> instance in the sRGB color space.</returns>
    public static Rgb64 LinearRgbToSRgb(Rgb64 pixel) =>
        pixel.ColorComponentMap((channel) => (channel > 0.0031308) ? 1.055 * Math.Pow(channel, 1.0 / 2.4) - 0.055 : (channel * 12.92));
}