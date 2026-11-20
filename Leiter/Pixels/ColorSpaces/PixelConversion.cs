namespace Leiter.Pixels.ColorSpaces;

using Leiter.Core;

/// <summary>
/// Provides extension methods to convert between pixel types and color spaces.
/// </summary>
public static class PixelConversion
{
    /// <summary>
    /// Converts an <see cref="Xyz32"/> pixel to an 8-bit per channel <see cref="Rgb8"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point for the XYZ pixel. Values outside [0, 255] are rounded using <see cref="MidpointRounding.AwayFromZero"/> and clamped.
    /// </remarks>
    /// <param name="self">The XYZ pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <returns>An <see cref="Rgb8"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Rgb8 ToRgb8(this Xyz32 self, RgbColorSpace colorSpace) =>
        self.ToRgb64(colorSpace).ToRgb8();

    /// <summary>
    /// Converts an <see cref="Rgb64"/> pixel with channel values in [0, 1] to an 8-bit per channel <see cref="Rgb8"/> pixel.
    /// </summary>
    /// <remarks>
    /// Channel values are scaled by 255, rounded using <see cref="MidpointRounding.AwayFromZero"/>, and clamped to [0, 255].
    /// </remarks>
    /// <param name="self">The 64-bit floating-point RGB pixel to convert.</param>
    /// <returns>An <see cref="Rgb8"/> pixel with rounded, byte-clamped channels.</returns>
    public static Rgb8 ToRgb8(this Rgb64 self) =>
        Rgb8.FromNormalized(self.R, self.G, self.B);

    /// <summary>
    /// Converts a normalized scalar <see cref="DoublePixel"/> in <c>[0, 1]</c> to a neutral grayscale <see cref="Rgb8"/> pixel.
    /// </summary>
    /// <param name="self">The scalar pixel whose value is scaled by 255, rounded away from zero, and clamped to [0, 255].</param>
    /// <returns>An <see cref="Rgb8"/> pixel with equal R, G, and B channels.</returns>
    public static Rgb8 ToGrayscaleRgb8(this DoublePixel self)
    {
        byte channel = Rgb8.ClampRoundedByte(self.Value * 255.0);
        return new Rgb8(channel, channel, channel);
    }

    /// <summary>
    /// Converts a matrix of normalized scalar <see cref="DoublePixel"/> values in <c>[0, 1]</c> to a grayscale <see cref="Matrix{Rgb8}"/> image.
    /// </summary>
    /// <param name="source">The scalar image matrix to convert.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing rounded grayscale RGB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static Matrix<Rgb8> ToGrayscaleRgb8(this IReadOnlyMatrix<DoublePixel> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Map(static pixel => pixel.ToGrayscaleRgb8());
    }

    /// <summary>
    /// Converts a boolean mask matrix to an <see cref="Rgb8"/> matrix using <paramref name="trueColor"/> for <see langword="true"/> cells
    /// and <paramref name="falseColor"/> for <see langword="false"/> cells.
    /// </summary>
    /// <param name="mask">The boolean mask matrix to convert.</param>
    /// <param name="trueColor">The RGB color assigned to <see langword="true"/> entries.</param>
    /// <param name="falseColor">The RGB color assigned to <see langword="false"/> entries.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> visualizing the boolean mask.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    public static Matrix<Rgb8> ToRgb8(this IReadOnlyMatrix<bool> mask, Rgb8 trueColor, Rgb8 falseColor)
    {
        ArgumentNullException.ThrowIfNull(mask);
        return mask.Map(value => value ? trueColor : falseColor);
    }

    /// <summary>
    /// Converts a boolean mask matrix to a binary grayscale <see cref="Matrix{Rgb8}"/> (<c>(255, 255, 255)</c> for <see langword="true"/>, <c>(0, 0, 0)</c> for <see langword="false"/>).
    /// </summary>
    /// <param name="mask">The boolean mask matrix to convert.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing black-and-white mask pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    public static Matrix<Rgb8> ToGrayscaleRgb8(this IReadOnlyMatrix<bool> mask) =>
        mask.ToRgb8(new Rgb8(255, 255, 255), Rgb8.Zero);

    /// <summary>
    /// Converts an <see cref="Xyz32"/> pixel to an <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point for the XYZ pixel.
    /// </remarks>
    /// <param name="self">The XYZ pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <returns>An <see cref="Rgb64"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Rgb64 ToRgb64(this Xyz32 self, RgbColorSpace colorSpace)
    {
        Guard.ThrowIfUndefinedEnum(colorSpace);

        double r = 3.2404542 * self.X - 1.5371385 * self.Y - 0.4985314 * self.Z;
        double g = -0.9692660 * self.X + 1.8760108 * self.Y + 0.0415560 * self.Z;
        double b = 0.0556434 * self.X - 0.2040259 * self.Y + 1.0572252 * self.Z;
        var rgb64 = new Rgb64(r, g, b);

        return colorSpace == RgbColorSpace.SRgb
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Xyz32 ToXyz32(this Rgb64 self, RgbColorSpace colorSpace)
    {
        Guard.ThrowIfUndefinedEnum(colorSpace);

        var rgb64 = colorSpace == RgbColorSpace.SRgb
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Lab32 ToLab32(this Rgb8 self, RgbColorSpace colorSpace) =>
        self.ToLab32(colorSpace, Illuminant.D65);

    /// <summary>
    /// Converts an 8-bit per channel <see cref="Rgb8"/> pixel in the specified <see cref="RgbColorSpace"/> to a <see cref="Lab32"/> pixel under <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies Bradford chromatic adaptation from D65 to Illuminant E before CIELAB normalization.
    /// </remarks>
    /// <param name="self">The 8-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <param name="illuminant">The reference white illuminant to normalize against.</param>
    /// <returns>A <see cref="Lab32"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Lab32 ToLab32(this Rgb8 self, RgbColorSpace colorSpace, Illuminant illuminant) =>
        self.ToRgb64().ToLab32(colorSpace, illuminant);

    /// <summary>
    /// Converts an <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/> to a <see cref="Lab32"/> pixel.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point.
    /// </remarks>
    /// <param name="self">The 64-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <returns>A <see cref="Lab32"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Lab32 ToLab32(this Rgb64 self, RgbColorSpace colorSpace) =>
        self.ToLab32(colorSpace, Illuminant.D65);

    /// <summary>
    /// Converts an <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/> to a <see cref="Lab32"/> pixel under <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies Bradford chromatic adaptation from D65 to Illuminant E before CIELAB normalization.
    /// </remarks>
    /// <param name="self">The 64-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <param name="illuminant">The reference white illuminant to normalize against.</param>
    /// <returns>A <see cref="Lab32"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Lab32 ToLab32(this Rgb64 self, RgbColorSpace colorSpace, Illuminant illuminant)
    {
        Xyz32 xyz = self.ToXyz32(colorSpace);
        if (illuminant == Illuminant.E)
        {
            xyz = AdaptD65ToE(xyz);
        }

        return xyz.ToLab32(illuminant);
    }

    private static readonly Xyz32 XyzReferenceWhiteD65 = new(0.95047f, 1.00000f, 1.08883f);
    private static readonly Xyz32 XyzReferenceWhiteE = new(1.0000f, 1.0000f, 1.0000f);

    private static Xyz32 AdaptD65ToE(Xyz32 xyz)
    {
        double x = (1.0502615720 * xyz.X) + (0.0270756823 * xyz.Y) - (0.0232522996 * xyz.Z);
        double y = (0.0390650056 * xyz.X) + (0.9729500478 * xyz.Y) - (0.0092578832 * xyz.Z);
        double z = (-0.0024046624 * xyz.X) + (0.0026445615 * xyz.Y) + (0.9180873026 * xyz.Z);
        return new((float)x, (float)y, (float)z);
    }

    private static Xyz32 AdaptEToD65(Xyz32 xyz)
    {
        double x = (0.9531874685 * xyz.X) - (0.0265906098 * xyz.Y) + (0.0238731412 * xyz.Z);
        double y = (-0.0382467124 * xyz.X) + (1.0288407748 * xyz.Y) + (0.0094060376 * xyz.Z);
        double z = (0.0026067672 * xyz.X) - (0.0030332346 * xyz.Y) + (1.0892564674 * xyz.Z);
        return new((float)x, (float)y, (float)z);
    }

    private static Xyz32 GetReferenceWhite(Illuminant illuminant) => illuminant switch
    {
        Illuminant.D65 => XyzReferenceWhiteD65,
        Illuminant.E => XyzReferenceWhiteE,
        _ => throw new ArgumentOutOfRangeException(nameof(illuminant), illuminant, $"Unsupported {nameof(Illuminant)} value.")
    };

    /// <summary>
    /// Converts from an <see cref="Xyz32"/> pixel to a <see cref="Lab32"/> pixel.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point for the XYZ pixel.
    /// </remarks>
    /// <param name="self">The XYZ pixel on which to operate.</param>
    /// <returns>A LAB pixel of the same color as the XYZ pixel.</returns>
    public static Lab32 ToLab32(this Xyz32 self) =>
        self.ToLab32(Illuminant.D65);

    /// <summary>
    /// Converts an <see cref="Xyz32"/> pixel to a <see cref="Lab32"/> pixel under the specified <paramref name="illuminant"/>.
    /// </summary>
    /// <param name="self">The XYZ pixel on which to operate.</param>
    /// <param name="illuminant">The reference white illuminant to normalize against.</param>
    /// <returns>A LAB pixel representing the same color under <paramref name="illuminant"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="illuminant"/> is not a defined <see cref="Illuminant"/> value.</exception>
    public static Lab32 ToLab32(this Xyz32 self, Illuminant illuminant)
    {
        Xyz32 white = GetReferenceWhite(illuminant);
        float fx = LabTransfer(self.X / white.X);
        float fy = LabTransfer(self.Y / white.Y);
        float fz = LabTransfer(self.Z / white.Z);
        return new(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
    }

    /// <summary>
    /// Converts a <see cref="Lab32"/> pixel to an <see cref="Xyz32"/> pixel assuming a D65 white point.
    /// </summary>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <returns>An <see cref="Xyz32"/> pixel representing the same color.</returns>
    public static Xyz32 ToXyz32(this Lab32 self) =>
        self.ToXyz32(Illuminant.D65);

    /// <summary>
    /// Converts a <see cref="Lab32"/> pixel to an <see cref="Xyz32"/> pixel under the specified <paramref name="illuminant"/>.
    /// </summary>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates.</param>
    /// <returns>An <see cref="Xyz32"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="illuminant"/> is not a defined <see cref="Illuminant"/> value.</exception>
    public static Xyz32 ToXyz32(this Lab32 self, Illuminant illuminant)
    {
        Xyz32 white = GetReferenceWhite(illuminant);
        float fy = (self.L + 16f) / 116f;
        float fx = fy + (self.A / 500f);
        float fz = fy - (self.B / 200f);
        return new(
            white.X * InverseLabTransfer(fx),
            white.Y * InverseLabTransfer(fy),
            white.Z * InverseLabTransfer(fz));
    }

    /// <summary>
    /// Converts a <see cref="Lab32"/> pixel (D65 white point) to an <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <returns>An <see cref="Rgb64"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Rgb64 ToRgb64(this Lab32 self, RgbColorSpace colorSpace) =>
        self.ToRgb64(colorSpace, Illuminant.D65);

    /// <summary>
    /// Converts a <see cref="Lab32"/> pixel under <paramref name="illuminant"/> to an <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies inverse Bradford chromatic adaptation from Illuminant E back to D65.
    /// </remarks>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates.</param>
    /// <returns>An <see cref="Rgb64"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Rgb64 ToRgb64(this Lab32 self, RgbColorSpace colorSpace, Illuminant illuminant)
    {
        Xyz32 xyz = self.ToXyz32(illuminant);
        if (illuminant == Illuminant.E)
        {
            xyz = AdaptEToD65(xyz);
        }

        return xyz.ToRgb64(colorSpace);
    }

    /// <summary>
    /// Converts a <see cref="Lab32"/> pixel (D65 white point) to a rounded, byte-clamped <see cref="Rgb8"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <returns>An <see cref="Rgb8"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Rgb8 ToRgb8(this Lab32 self, RgbColorSpace colorSpace) =>
        self.ToRgb8(colorSpace, Illuminant.D65);

    /// <summary>
    /// Converts a <see cref="Lab32"/> pixel under <paramref name="illuminant"/> to a rounded, byte-clamped <see cref="Rgb8"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies inverse Bradford chromatic adaptation from Illuminant E back to D65.
    /// </remarks>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates.</param>
    /// <returns>An <see cref="Rgb8"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Rgb8 ToRgb8(this Lab32 self, RgbColorSpace colorSpace, Illuminant illuminant) =>
        self.ToRgb64(colorSpace, illuminant).ToRgb8();

    private const float LabEpsilon = 0.008856f; // (6/29)^3
    private const float LabKappa = 903.3f; // (29/3)^3

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
        return channel > LabEpsilon ? (float)Math.Cbrt(channel) : (LabKappa * channel + 16f) / 116f;
    }

    private static float InverseLabTransfer(float f)
    {
        float cubed = f * f * f;
        return cubed > LabEpsilon ? cubed : (116f * f - 16f) / LabKappa;
    }
}
