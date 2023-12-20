namespace Leiter.Algorithms;

using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Provides extension methods to convert between pixel types.
/// </summary>
public static class PixelConversion
{
    public static Rgb8 ToRgb8(this Xyz32 self, RgbColorSpace colorSpace) =>
        self.ToRgb64(colorSpace).ToRgb8();

    public static Rgb8 ToRgb8(this Rgb64 self) => new((byte)(self.R * 255), (byte)(self.G * 255), (byte)(self.B * 255));

    public static Rgb64 ToRgb64(this Xyz32 self, RgbColorSpace colorSpace)
    {
        SequentialMatrix<DoublePixel> mat = colorSpace switch
        {
            RgbColorSpace.LinearRGB or RgbColorSpace.sRGB => new(new DoublePixel[,]
            {
                {3.2404542,  -1.5371385,  -0.4985314},
                {-0.9692660,  1.8760108,  0.0415560},
                {0.0556434,  -0.2040259,  1.0572252}
            }),
            _ => throw new NotImplementedException($"Conversion from color space {colorSpace} is not implemented.")
        };
        SequentialMatrix<DoublePixel> pixel = new(new DoublePixel[,]
            {
                {self.X},
                {self.Y},
                {self.Z}
            });

        var result = mat.Multiply(pixel);
        var rgb64 = new Rgb64(result.GetElement(0), result.GetElement(1), result.GetElement(2));

        return colorSpace switch
        {
            RgbColorSpace.sRGB => RgbColorSpaceConversions.LinearRgbToSRgb(rgb64),
            _ => rgb64
        };
    }

    public static Rgb64 ToRgb64(this Rgb8 self) => new(self.R / 255.0, self.G / 255.0, self.B / 255.0);

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
        var rgb64 = colorSpace switch
        {
            RgbColorSpace.sRGB => RgbColorSpaceConversions.SRgbToLinearRgb(self),
            _ => self
        };
        SequentialMatrix<DoublePixel> mat = colorSpace switch
        {
            RgbColorSpace.LinearRGB or RgbColorSpace.sRGB => new(new DoublePixel[,]
            {
                {0.4124564,  0.3575761,  0.1804375},
                {0.2126729,  0.7151522,  0.0721750},
                {0.0193339,  0.1191920,  0.9503041}
            }),
            _ => throw new NotImplementedException($"Conversion from color space {colorSpace} is not implemented.")
        };
        SequentialMatrix<DoublePixel> pixel = new(new DoublePixel[,]
            {
                {rgb64.R},
                {rgb64.G},
                {rgb64.B}
            });

        var result = mat.Multiply(pixel);
        return new((float)result.GetElement(0).Value, (float)result.GetElement(1).Value, (float)result.GetElement(2).Value);
    }

    public static Lab32 ToLab32(this Rgb8 self, RgbColorSpace colorSpace) =>
        self.ToXyz32(colorSpace).ToLab32();

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
        const float espilon = 0.008856f; // (6/29)^3
        const float kappa = 903.3f; // (29/3)^3
        Xyz32 fPixel = self.Divide(XyzReferenceWhiteD65)
            .ComponentMap(channel => channel > espilon ? (float)Math.Cbrt(channel) : (kappa * channel + 16) / 116);
        return new((float)(116 * fPixel.Y - 16), (float)(500 * (fPixel.X - fPixel.Y)), (float)(200 * (fPixel.Y - fPixel.Z)));
    }
}