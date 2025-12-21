namespace Leiter.Algorithms.Quantization;

using Leiter.Pixels;

public class UniformQuantizer
{
    public static Rgb8 QuantizeToMidpoint(Rgb8 pixel, int levelsPerChannel)
    {
        if (levelsPerChannel > 256 || levelsPerChannel < 1)
            throw new ArgumentException("Levels per channel must be between 1 and 256.");

        if (levelsPerChannel == 256)
            return pixel;

        return pixel.ComponentMap((channel) => (byte)((int)(channel * levelsPerChannel / 256.0) * (256.0 / levelsPerChannel) + (256.0 / levelsPerChannel / 2.0)));
    }

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