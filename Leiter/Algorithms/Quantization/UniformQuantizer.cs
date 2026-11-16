namespace Leiter.Algorithms.Quantization;

using Leiter.Pixels;

/// <summary>
/// Provides methods for uniform quantization of pixel color channels to interval midpoints.
/// </summary>
public class UniformQuantizer
{
    /// <summary>
    /// Quantizes each channel of an <see cref="Rgb8"/> pixel to the midpoint of the corresponding quantization interval using the specified number of levels.
    /// </summary>
    /// <param name="pixel">The RGB pixel to quantize.</param>
    /// <param name="levelsPerChannel">The number of quantization levels per channel, between 1 and 256 inclusive.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with channels quantized to interval midpoints.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="levelsPerChannel"/> is not between 1 and 256.</exception>
    public static Rgb8 QuantizeToMidpoint(Rgb8 pixel, int levelsPerChannel)
    {
        if (levelsPerChannel > 256 || levelsPerChannel < 1)
            throw new ArgumentException("Levels per channel must be between 1 and 256.");

        if (levelsPerChannel == 256)
            return pixel;

        return pixel.ComponentMap((channel) => (byte)((int)(channel * levelsPerChannel / 256.0) * (256.0 / levelsPerChannel) + (256.0 / levelsPerChannel / 2.0)));
    }

    /// <summary>
    /// Quantizes each channel of an <see cref="Rgb8"/> pixel to the midpoint of the corresponding quantization interval using bit truncation.
    /// </summary>
    /// <param name="pixel">The RGB pixel to quantize.</param>
    /// <param name="bitsPerChannel">The number of most significant bits to retain per channel, between 1 and 8 inclusive.</param>
    /// <returns>A new <see cref="Rgb8"/> pixel with channels quantized to interval midpoints.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="bitsPerChannel"/> is not between 1 and 8.</exception>
    public static Rgb8 QuantizeBitsToMidpoint(Rgb8 pixel, int bitsPerChannel)
    {
        if (bitsPerChannel > 8 || bitsPerChannel < 1)
            throw new ArgumentException("Bits per channel must be between 1 and 8.");

        if (bitsPerChannel == 8)
            return pixel;

        var shift = 8 - bitsPerChannel;
        return pixel.ComponentMap((channel) => (byte)(((channel >> shift) << shift) + (1 << (shift - 1))));
    }
}