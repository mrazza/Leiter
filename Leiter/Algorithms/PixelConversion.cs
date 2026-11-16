namespace Leiter.Algorithms;

using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Provides extension methods to convert between pixel types.
/// </summary>
public static class PixelConversion
{
    /// <summary>
    /// Converts an <see cref="Xyz32"/> pixel to an 8-bit per channel <see cref="Rgb8"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point for the XYZ pixel. Values outside [0, 255] are clamped.
    /// </remarks>
    /// <param name="self">The XYZ pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <returns>An <see cref="Rgb8"/> pixel representing the same color.</returns>
    /// <exception cref="NotImplementedException">Thrown if conversion to the specified color space is not implemented.</exception>
    public static Rgb8 ToRgb8(this Xyz32 self, RgbColorSpace colorSpace) =>
        self.ToRgb64(colorSpace).ToRgb8();

    private static byte ClampToByte(double value) =>
        double.IsNaN(value) ? (byte)0 : (byte)Math.Clamp(value, byte.MinValue, byte.MaxValue);

    /// <summary>
    /// Converts an <see cref="Rgb64"/> pixel with channel values in [0, 1] to an 8-bit per channel <see cref="Rgb8"/> pixel.
    /// </summary>
    /// <remarks>
    /// Channel values are scaled by 255 and clamped to [0, 255].
    /// </remarks>
    /// <param name="self">The 64-bit floating-point RGB pixel to convert.</param>
    /// <returns>An <see cref="Rgb8"/> pixel with byte-clamped channels.</returns>
    public static Rgb8 ToRgb8(this Rgb64 self) =>
        new(ClampToByte(self.R * 255.0), ClampToByte(self.G * 255.0), ClampToByte(self.B * 255.0));

    /// <summary>
    /// Converts an <see cref="Xyz32"/> pixel to an <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point for the XYZ pixel.
    /// </remarks>
    /// <param name="self">The XYZ pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <returns>An <see cref="Rgb64"/> pixel representing the same color.</returns>
    /// <exception cref="NotImplementedException">Thrown if conversion to the specified color space is not implemented.</exception>
    public static Rgb64 ToRgb64(this Xyz32 self, RgbColorSpace colorSpace)
    {
        if (colorSpace is not (RgbColorSpace.LinearRGB or RgbColorSpace.sRGB))
            throw new NotImplementedException($"Conversion from color space {colorSpace} is not implemented.");

        double r = 3.2404542 * self.X - 1.5371385 * self.Y - 0.4985314 * self.Z;
        double g = -0.9692660 * self.X + 1.8760108 * self.Y + 0.0415560 * self.Z;
        double b = 0.0556434 * self.X - 0.2040259 * self.Y + 1.0572252 * self.Z;
        var rgb64 = new Rgb64(r, g, b);

        return colorSpace == RgbColorSpace.sRGB
            ? RgbColorSpaceConversions.LinearRgbToSRgb(rgb64)
            : rgb64;
    }

    /// <summary>
    /// Converts an 8-bit per channel <see cref="Rgb8"/> pixel to an <see cref="Rgb64"/> pixel with channel values normalized to [0, 1].
    /// </summary>
    /// <param name="self">The 8-bit RGB pixel to convert.</param>
    /// <returns>An <see cref="Rgb64"/> pixel with normalized floating-point channels.</returns>
    public static Rgb64 ToRgb64(this Rgb8 self) => new(self.R / 255.0, self.G / 255.0, self.B / 255.0);

    /// <summary>
    /// Converts an 8-bit per channel <see cref="Rgb8"/> pixel in the specified <see cref="RgbColorSpace"/> to an <see cref="Xyz32"/> pixel.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point for the RGB pixel.
    /// </remarks>
    /// <param name="self">The 8-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <returns>An <see cref="Xyz32"/> pixel representing the same color.</returns>
    /// <exception cref="NotImplementedException">Thrown if the specified color space conversion is not implemented.</exception>
    public static Xyz32 ToXyz32(this Rgb8 self, RgbColorSpace colorSpace) =>
        self.ToRgb64().ToXyz32(colorSpace);

    /// <summary>
    /// Converts from <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/> to an <see cref="Xyz32"/> pixel.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point for the RGB pixel.
    /// </remarks>
    /// <param name="self">The RGB pixel to operate on.</param>
    /// <param name="colorSpace">The RGB color space the RGB pixel is in.</param>
    /// <returns>An XYZ pixel representing the same color.</returns>
    /// <exception cref="NotImplementedException">Thrown if the specified color space conversion is not implemented.</exception>
    public static Xyz32 ToXyz32(this Rgb64 self, RgbColorSpace colorSpace)
    {
        if (colorSpace is not (RgbColorSpace.LinearRGB or RgbColorSpace.sRGB))
            throw new NotImplementedException($"Conversion from color space {colorSpace} is not implemented.");

        var rgb64 = colorSpace == RgbColorSpace.sRGB
            ? RgbColorSpaceConversions.SRgbToLinearRgb(self)
            : self;

        double x = 0.4124564 * rgb64.R + 0.3575761 * rgb64.G + 0.1804375 * rgb64.B;
        double y = 0.2126729 * rgb64.R + 0.7151522 * rgb64.G + 0.0721750 * rgb64.B;
        double z = 0.0193339 * rgb64.R + 0.1191920 * rgb64.G + 0.9503041 * rgb64.B;
        return new((float)x, (float)y, (float)z);
    }

    /// <summary>
    /// Converts an 8-bit per channel <see cref="Rgb8"/> pixel in the specified <see cref="RgbColorSpace"/> to a <see cref="Lab32"/> pixel.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point.
    /// </remarks>
    /// <param name="self">The 8-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <returns>A <see cref="Lab32"/> pixel representing the same color.</returns>
    /// <exception cref="NotImplementedException">Thrown if the specified color space conversion is not implemented.</exception>
    public static Lab32 ToLab32(this Rgb8 self, RgbColorSpace colorSpace) =>
        self.ToXyz32(colorSpace).ToLab32();

    /// <summary>
    /// Converts an <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/> to a <see cref="Lab32"/> pixel.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point.
    /// </remarks>
    /// <param name="self">The 64-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <returns>A <see cref="Lab32"/> pixel representing the same color.</returns>
    /// <exception cref="NotImplementedException">Thrown if the specified color space conversion is not implemented.</exception>
    public static Lab32 ToLab32(this Rgb64 self, RgbColorSpace colorSpace) =>
        self.ToXyz32(colorSpace).ToLab32();

    private static readonly Xyz32 XyzReferenceWhiteD65 = new(0.9504f, 1.0000f, 1.0888f);

    /// <summary>
    /// Converts from an <see cref="Xyz32"/> pixel to a <see cref="Lab32"/> pixel.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point for the XYZ pixel.
    /// </remarks>
    /// <param name="self">The XYZ pixel on which to operate.</param>
    /// <returns>A LAB pixel of the same color as the XYZ pixel.</returns>
    public static Lab32 ToLab32(this Xyz32 self)
    {
        float fx = LabTransfer(self.X / XyzReferenceWhiteD65.X);
        float fy = LabTransfer(self.Y / XyzReferenceWhiteD65.Y);
        float fz = LabTransfer(self.Z / XyzReferenceWhiteD65.Z);
        return new(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
    }

    /// <summary>
    /// Evaluates the piecewise CIE L*a*b* transfer function on a normalized tristimulus ratio.
    /// </summary>
    /// <remarks>
    /// Applies a cube-root transfer for ratios above epsilon (0.008856) and a linear segment with slope kappa / 116 below.
    /// </remarks>
    /// <param name="channel">The normalized tristimulus value ratio relative to the reference white point.</param>
    /// <returns>The transformed transfer value.</returns>
    private static float LabTransfer(float channel)
    {
        const float epsilon = 0.008856f; // (6/29)^3
        const float kappa = 903.3f; // (29/3)^3
        return channel > epsilon ? (float)Math.Cbrt(channel) : (kappa * channel + 16f) / 116f;
    }
}