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
        return self.ToRgb64Unchecked(colorSpace);
    }

    internal static Rgb64 ToRgb64Unchecked(this Xyz32 self, RgbColorSpace colorSpace) =>
        XyzVectorToRgb64Unchecked(new(self.X, self.Y, self.Z), colorSpace);

    private static Rgb64 XyzVectorToRgb64Unchecked(Vector3D xyz, RgbColorSpace colorSpace)
    {
        double r = 3.2404542 * xyz.X - 1.5371385 * xyz.Y - 0.4985314 * xyz.Z;
        double g = -0.9692660 * xyz.X + 1.8760108 * xyz.Y + 0.0415560 * xyz.Z;
        double b = 0.0556434 * xyz.X - 0.2040259 * xyz.Y + 1.0572252 * xyz.Z;
        var rgb64 = new Rgb64(r, g, b);

        return colorSpace == RgbColorSpace.SRgb
            ? RgbColorSpaceConversions.LinearRgbToSRgb(rgb64)
            : rgb64;
    }

    internal static Rgb8 ToRgb8Unchecked(this Xyz32 self, RgbColorSpace colorSpace) =>
        self.ToRgb64Unchecked(colorSpace).ToRgb8();

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

    internal static Xyz32 ToXyz32Unchecked(this Rgb8 self, RgbColorSpace colorSpace) =>
        self.ToRgb64().ToXyz32Unchecked(colorSpace);

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
        return self.ToXyz32Unchecked(colorSpace);
    }

    internal static Xyz32 ToXyz32Unchecked(this Rgb64 self, RgbColorSpace colorSpace)
    {
        Vector3D xyz = self.ToXyzVectorUnchecked(colorSpace);
        return new((float)xyz.X, (float)xyz.Y, (float)xyz.Z);
    }

    private static Vector3D ToXyzVectorUnchecked(this Rgb64 self, RgbColorSpace colorSpace)
    {
        var rgb64 = colorSpace == RgbColorSpace.SRgb
            ? RgbColorSpaceConversions.SRgbToLinearRgb(self)
            : self;

        return new(
            0.4124564 * rgb64.R + 0.3575761 * rgb64.G + 0.1804375 * rgb64.B,
            0.2126729 * rgb64.R + 0.7151522 * rgb64.G + 0.0721750 * rgb64.B,
            0.0193339 * rgb64.R + 0.1191920 * rgb64.G + 0.9503041 * rgb64.B);
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
    /// Because sRGB and LinearRgb are defined relative to a D65 white point, converting directly from RGB to CIELAB under
    /// <see cref="Illuminant.E"/> applies Bradford chromatic adaptation from D65 to Illuminant E so neutral grays remain achromatic (<c>a* = b* = 0</c>).
    /// By contrast, <see cref="ToLab32(Xyz32, Illuminant)"/> treats its input XYZ coordinates as already expressed under <paramref name="illuminant"/>.
    /// </remarks>
    /// <param name="self">The 8-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <param name="illuminant">The reference white illuminant to normalize against.</param>
    /// <returns>A <see cref="Lab32"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Lab32 ToLab32(this Rgb8 self, RgbColorSpace colorSpace, Illuminant illuminant) =>
        self.ToRgb64().ToLab32(colorSpace, illuminant);

    internal static Lab32 ToLab32Unchecked(
        this Rgb8 self,
        RgbColorSpace colorSpace,
        Illuminant illuminant,
        Xyz32 referenceWhite) =>
        self.ToRgb64().ToLab32Unchecked(colorSpace, illuminant, referenceWhite);

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
    /// Because sRGB and LinearRgb are defined relative to a D65 white point, converting from <see cref="Rgb64"/> to <see cref="Lab32"/> under
    /// <see cref="Illuminant.E"/> applies Bradford chromatic adaptation from D65 to Illuminant E before CIELAB normalization so neutral grays
    /// remain achromatic (<c>a* = b* = 0</c>). By contrast, <see cref="ToLab32(Xyz32, Illuminant)"/> treats input XYZ as already under <paramref name="illuminant"/>.
    /// </remarks>
    /// <param name="self">The 64-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <param name="illuminant">The reference white illuminant to normalize against.</param>
    /// <returns>A <see cref="Lab32"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Lab32 ToLab32(this Rgb64 self, RgbColorSpace colorSpace, Illuminant illuminant)
    {
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Xyz32 referenceWhite = GetReferenceWhite(illuminant);
        return self.ToLab32Unchecked(colorSpace, illuminant, referenceWhite);
    }

    internal static Lab32 ToLab32Unchecked(
        this Rgb64 self,
        RgbColorSpace colorSpace,
        Illuminant illuminant,
        Xyz32 referenceWhite)
    {
        Xyz32 xyz = self.ToXyz32Unchecked(colorSpace);
        if (illuminant == Illuminant.E)
        {
            xyz = AdaptD65ToE(xyz);
        }

        return xyz.ToLab32Unchecked(referenceWhite);
    }

    private readonly record struct Matrix3x3(
        double M11, double M12, double M13,
        double M21, double M22, double M23,
        double M31, double M32, double M33)
    {
        public Xyz32 Multiply(Xyz32 xyz) => new(
            (float)((M11 * xyz.X) + (M12 * xyz.Y) + (M13 * xyz.Z)),
            (float)((M21 * xyz.X) + (M22 * xyz.Y) + (M23 * xyz.Z)),
            (float)((M31 * xyz.X) + (M32 * xyz.Y) + (M33 * xyz.Z)));

        public Vector3D Multiply(Vector3D xyz) => new(
            (M11 * xyz.X) + (M12 * xyz.Y) + (M13 * xyz.Z),
            (M21 * xyz.X) + (M22 * xyz.Y) + (M23 * xyz.Z),
            (M31 * xyz.X) + (M32 * xyz.Y) + (M33 * xyz.Z));
    }

    private static readonly Xyz32 XyzReferenceWhiteD65 = new(0.95047f, 1.00000f, 1.08883f);
    private static readonly Xyz32 XyzReferenceWhiteE = new(1.0000f, 1.0000f, 1.0000f);
    private static readonly Vector3D Xyz64ReferenceWhiteD65 = new(0.95047, 1.00000, 1.08883);
    private static readonly Vector3D Xyz64ReferenceWhiteE = new(1.0, 1.0, 1.0);

    // Bradford chromatic adaptation matrix from D65 (0.95047, 1.00000, 1.08883) to Illuminant E (1.0, 1.0, 1.0).
    private static readonly Matrix3x3 BradfordD65ToEMatrix = new(
         1.0502615720,  0.0270756823, -0.0232522996,
         0.0390650056,  0.9729500478, -0.0092578832,
        -0.0024046624,  0.0026445615,  0.9180873026);

    // Inverse Bradford chromatic adaptation matrix from Illuminant E back to D65.
    private static readonly Matrix3x3 BradfordEToD65Matrix = new(
         0.9531874685, -0.0265906098,  0.0238731412,
        -0.0382467124,  1.0288407748,  0.0094060376,
         0.0026067672, -0.0030332346,  1.0892564674);

    private static Xyz32 AdaptD65ToE(Xyz32 xyz) => BradfordD65ToEMatrix.Multiply(xyz);

    private static Xyz32 AdaptEToD65(Xyz32 xyz) => BradfordEToD65Matrix.Multiply(xyz);

    private static Vector3D AdaptD65ToE(Vector3D xyz) => BradfordD65ToEMatrix.Multiply(xyz);

    private static Vector3D AdaptEToD65(Vector3D xyz) => BradfordEToD65Matrix.Multiply(xyz);

    private static Xyz32 GetReferenceWhite(Illuminant illuminant) => illuminant switch
    {
        Illuminant.D65 => XyzReferenceWhiteD65,
        Illuminant.E => XyzReferenceWhiteE,
        _ => throw new ArgumentOutOfRangeException(nameof(illuminant), illuminant, $"Unsupported {nameof(Illuminant)} value.")
    };

    private static Vector3D GetReferenceWhite64(Illuminant illuminant) => illuminant switch
    {
        Illuminant.D65 => Xyz64ReferenceWhiteD65,
        Illuminant.E => Xyz64ReferenceWhiteE,
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
    /// <remarks>
    /// Treats <paramref name="self"/> as tristimulus coordinates already expressed under <paramref name="illuminant"/> and normalizes directly
    /// against that illuminant's reference white without applying chromatic adaptation. By contrast, RGB-to-CIELAB overloads adapt from D65 when
    /// targeting a non-D65 illuminant.
    /// </remarks>
    /// <param name="self">The XYZ pixel on which to operate.</param>
    /// <param name="illuminant">The reference white illuminant to normalize against.</param>
    /// <returns>A LAB pixel representing the same color under <paramref name="illuminant"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="illuminant"/> is not a defined <see cref="Illuminant"/> value.</exception>
    public static Lab32 ToLab32(this Xyz32 self, Illuminant illuminant) =>
        self.ToLab32Unchecked(GetReferenceWhite(illuminant));

    internal static Lab32 ToLab32Unchecked(this Xyz32 self, Xyz32 referenceWhite)
    {
        float fx = LabTransfer(self.X / referenceWhite.X);
        float fy = LabTransfer(self.Y / referenceWhite.Y);
        float fz = LabTransfer(self.Z / referenceWhite.Z);
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
    /// <remarks>
    /// Produces tristimulus coordinates expressed directly under <paramref name="illuminant"/> using its reference white without applying
    /// chromatic adaptation to D65. By contrast, CIELAB-to-RGB overloads adapt from <paramref name="illuminant"/> back to D65 before converting to RGB.
    /// </remarks>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates.</param>
    /// <returns>An <see cref="Xyz32"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="illuminant"/> is not a defined <see cref="Illuminant"/> value.</exception>
    public static Xyz32 ToXyz32(this Lab32 self, Illuminant illuminant) =>
        self.ToXyz32Unchecked(GetReferenceWhite(illuminant));

    internal static Xyz32 ToXyz32Unchecked(this Lab32 self, Xyz32 referenceWhite)
    {
        float fy = (self.L + 16f) / 116f;
        float fx = fy + (self.A / 500f);
        float fz = fy - (self.B / 200f);
        return new(
            referenceWhite.X * InverseLabTransfer(fx),
            referenceWhite.Y * InverseLabTransfer(fy),
            referenceWhite.Z * InverseLabTransfer(fz));
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
    /// Because sRGB and LinearRgb use a D65 white point, converting from CIELAB under <see cref="Illuminant.E"/> applies inverse Bradford
    /// chromatic adaptation from Illuminant E back to D65 before the XYZ-to-RGB transform. By contrast, <see cref="ToXyz32(Lab32, Illuminant)"/>
    /// leaves the resulting XYZ coordinates under <paramref name="illuminant"/>.
    /// </remarks>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates.</param>
    /// <returns>An <see cref="Rgb64"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Rgb64 ToRgb64(this Lab32 self, RgbColorSpace colorSpace, Illuminant illuminant)
    {
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Xyz32 referenceWhite = GetReferenceWhite(illuminant);
        return self.ToRgb64Unchecked(colorSpace, illuminant, referenceWhite);
    }

    internal static Rgb64 ToRgb64Unchecked(
        this Lab32 self,
        RgbColorSpace colorSpace,
        Illuminant illuminant,
        Xyz32 referenceWhite)
    {
        Xyz32 xyz = self.ToXyz32Unchecked(referenceWhite);
        if (illuminant == Illuminant.E)
        {
            xyz = AdaptEToD65(xyz);
        }

        return xyz.ToRgb64Unchecked(colorSpace);
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
    /// Because sRGB and LinearRgb use a D65 white point, converting from CIELAB under <see cref="Illuminant.E"/> applies inverse Bradford
    /// chromatic adaptation from Illuminant E back to D65 before the XYZ-to-RGB transform.
    /// </remarks>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates.</param>
    /// <returns>An <see cref="Rgb8"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Rgb8 ToRgb8(this Lab32 self, RgbColorSpace colorSpace, Illuminant illuminant) =>
        self.ToRgb64(colorSpace, illuminant).ToRgb8();

    internal static Rgb8 ToRgb8Unchecked(
        this Lab32 self,
        RgbColorSpace colorSpace,
        Illuminant illuminant,
        Xyz32 referenceWhite) =>
        self.ToRgb64Unchecked(colorSpace, illuminant, referenceWhite).ToRgb8();

    /// <summary>
    /// Converts a matrix of <see cref="Xyz32"/> pixels (D65 white point) to <see cref="Rgb8"/> in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <param name="source">The source matrix of CIE XYZ pixels assumed to have a D65 white point.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing rounded, byte-clamped RGB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Matrix<Rgb8> ToRgb8(this IReadOnlyMatrix<Xyz32> source, RgbColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        return source.Map(pixel => pixel.ToRgb8Unchecked(colorSpace));
    }

    /// <summary>
    /// Converts a matrix of normalized <see cref="Rgb64"/> pixels in [0, 1] to 8-bit per channel <see cref="Rgb8"/>.
    /// </summary>
    /// <param name="source">The source matrix of 64-bit floating-point RGB pixels.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing rounded, byte-clamped RGB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static Matrix<Rgb8> ToRgb8(this IReadOnlyMatrix<Rgb64> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Map(static pixel => pixel.ToRgb8());
    }

    /// <summary>
    /// Converts a matrix of <see cref="Lab32"/> pixels under <paramref name="illuminant"/> to <see cref="Rgb8"/> in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies inverse Bradford chromatic adaptation from Illuminant E back to D65.
    /// </remarks>
    /// <param name="source">The source matrix of CIELAB pixels.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing rounded, byte-clamped RGB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Matrix<Rgb8> ToRgb8(this IReadOnlyMatrix<Lab32> source, RgbColorSpace colorSpace, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Xyz32 referenceWhite = GetReferenceWhite(illuminant);
        return source.Map(pixel => pixel.ToRgb8Unchecked(colorSpace, illuminant, referenceWhite));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Xyz32"/> pixels (D65 white point) to <see cref="Rgb64"/> in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <param name="source">The source matrix of CIE XYZ pixels assumed to have a D65 white point.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <returns>A new <see cref="Matrix{Rgb64}"/> containing converted 64-bit RGB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Matrix<Rgb64> ToRgb64(this IReadOnlyMatrix<Xyz32> source, RgbColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        return source.Map(pixel => pixel.ToRgb64Unchecked(colorSpace));
    }

    /// <summary>
    /// Converts a matrix of 8-bit <see cref="Rgb8"/> pixels to normalized <see cref="Rgb64"/>.
    /// </summary>
    /// <param name="source">The source matrix of 8-bit RGB pixels.</param>
    /// <returns>A new <see cref="Matrix{Rgb64}"/> containing normalized RGB pixels in <c>[0, 1]</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static Matrix<Rgb64> ToRgb64(this IReadOnlyMatrix<Rgb8> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Map(static pixel => pixel.ToRgb64());
    }

    /// <summary>
    /// Converts a matrix of <see cref="Lab32"/> pixels under <paramref name="illuminant"/> to <see cref="Rgb64"/> in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies inverse Bradford chromatic adaptation from Illuminant E back to D65.
    /// </remarks>
    /// <param name="source">The source matrix of CIELAB pixels.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Rgb64}"/> containing converted 64-bit RGB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Matrix<Rgb64> ToRgb64(this IReadOnlyMatrix<Lab32> source, RgbColorSpace colorSpace, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Xyz32 referenceWhite = GetReferenceWhite(illuminant);
        return source.Map(pixel => pixel.ToRgb64Unchecked(colorSpace, illuminant, referenceWhite));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Rgb8"/> pixels in <paramref name="colorSpace"/> to <see cref="Xyz32"/>.
    /// </summary>
    /// <param name="source">The source matrix of 8-bit RGB pixels assumed to have a D65 white point.</param>
    /// <param name="colorSpace">The RGB color space of the input pixels.</param>
    /// <returns>A new <see cref="Matrix{Xyz32}"/> containing CIE XYZ pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Matrix<Xyz32> ToXyz32(this IReadOnlyMatrix<Rgb8> source, RgbColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        return source.Map(pixel => pixel.ToXyz32Unchecked(colorSpace));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Rgb64"/> pixels in <paramref name="colorSpace"/> to <see cref="Xyz32"/>.
    /// </summary>
    /// <param name="source">The source matrix of 64-bit RGB pixels assumed to have a D65 white point.</param>
    /// <param name="colorSpace">The RGB color space of the input pixels.</param>
    /// <returns>A new <see cref="Matrix{Xyz32}"/> containing CIE XYZ pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Matrix<Xyz32> ToXyz32(this IReadOnlyMatrix<Rgb64> source, RgbColorSpace colorSpace)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        return source.Map(pixel => pixel.ToXyz32Unchecked(colorSpace));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Lab32"/> pixels under <paramref name="illuminant"/> to <see cref="Xyz32"/>.
    /// </summary>
    /// <remarks>
    /// Produces <see cref="Xyz32"/> coordinates directly under <paramref name="illuminant"/> without applying chromatic adaptation to D65.
    /// </remarks>
    /// <param name="source">The source matrix of CIELAB pixels.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Xyz32}"/> containing CIE XYZ pixels under <paramref name="illuminant"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="illuminant"/> is not a defined <see cref="Illuminant"/> value.</exception>
    public static Matrix<Xyz32> ToXyz32(this IReadOnlyMatrix<Lab32> source, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Xyz32 referenceWhite = GetReferenceWhite(illuminant);
        return source.Map(pixel => pixel.ToXyz32Unchecked(referenceWhite));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Rgb8"/> pixels in <paramref name="colorSpace"/> to <see cref="Lab32"/> under <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies Bradford chromatic adaptation from D65 to Illuminant E before CIELAB normalization.
    /// </remarks>
    /// <param name="source">The source matrix of 8-bit RGB pixels.</param>
    /// <param name="colorSpace">The RGB color space of the input pixels.</param>
    /// <param name="illuminant">The target reference white illuminant (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Lab32}"/> containing CIELAB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Matrix<Lab32> ToLab32(this IReadOnlyMatrix<Rgb8> source, RgbColorSpace colorSpace, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Xyz32 referenceWhite = GetReferenceWhite(illuminant);
        return source.Map(pixel => pixel.ToLab32Unchecked(colorSpace, illuminant, referenceWhite));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Rgb64"/> pixels in <paramref name="colorSpace"/> to <see cref="Lab32"/> under <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies Bradford chromatic adaptation from D65 to Illuminant E before CIELAB normalization.
    /// </remarks>
    /// <param name="source">The source matrix of 64-bit RGB pixels.</param>
    /// <param name="colorSpace">The RGB color space of the input pixels.</param>
    /// <param name="illuminant">The target reference white illuminant (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Lab32}"/> containing CIELAB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Matrix<Lab32> ToLab32(this IReadOnlyMatrix<Rgb64> source, RgbColorSpace colorSpace, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Xyz32 referenceWhite = GetReferenceWhite(illuminant);
        return source.Map(pixel => pixel.ToLab32Unchecked(colorSpace, illuminant, referenceWhite));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Xyz32"/> pixels to <see cref="Lab32"/> under <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// Treats the input <see cref="Xyz32"/> values as already expressed under <paramref name="illuminant"/> without applying chromatic adaptation.
    /// </remarks>
    /// <param name="source">The source matrix of CIE XYZ pixels.</param>
    /// <param name="illuminant">The reference white illuminant to normalize against (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Lab32}"/> containing CIELAB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="illuminant"/> is not a defined <see cref="Illuminant"/> value.</exception>
    public static Matrix<Lab32> ToLab32(this IReadOnlyMatrix<Xyz32> source, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Xyz32 referenceWhite = GetReferenceWhite(illuminant);
        return source.Map(pixel => pixel.ToLab32Unchecked(referenceWhite));
    }

    /// <summary>
    /// Converts an 8-bit per channel <see cref="Rgb8"/> pixel in the specified <see cref="RgbColorSpace"/> to a 64-bit <see cref="Lab64"/> pixel.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point.
    /// </remarks>
    /// <param name="self">The 8-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <returns>A <see cref="Lab64"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Lab64 ToLab64(this Rgb8 self, RgbColorSpace colorSpace) =>
        self.ToLab64(colorSpace, Illuminant.D65);

    /// <summary>
    /// Converts an 8-bit per channel <see cref="Rgb8"/> pixel in the specified <see cref="RgbColorSpace"/> to a <see cref="Lab64"/> pixel under <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// Because sRGB and LinearRgb are defined relative to a D65 white point, converting directly from RGB to CIELAB under
    /// <see cref="Illuminant.E"/> applies Bradford chromatic adaptation from D65 to Illuminant E so neutral grays remain achromatic (<c>a* = b* = 0</c>).
    /// By contrast, <see cref="ToLab64(Xyz32, Illuminant)"/> treats its input XYZ coordinates as already expressed under <paramref name="illuminant"/>.
    /// </remarks>
    /// <param name="self">The 8-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <param name="illuminant">The reference white illuminant to normalize against.</param>
    /// <returns>A <see cref="Lab64"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Lab64 ToLab64(this Rgb8 self, RgbColorSpace colorSpace, Illuminant illuminant) =>
        self.ToRgb64().ToLab64(colorSpace, illuminant);

    internal static Lab64 ToLab64Unchecked(
        this Rgb8 self,
        RgbColorSpace colorSpace,
        Illuminant illuminant,
        Vector3D referenceWhite) =>
        self.ToRgb64().ToLab64Unchecked(colorSpace, illuminant, referenceWhite);

    /// <summary>
    /// Converts an <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/> to a 64-bit <see cref="Lab64"/> pixel.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point.
    /// </remarks>
    /// <param name="self">The 64-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <returns>A <see cref="Lab64"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Lab64 ToLab64(this Rgb64 self, RgbColorSpace colorSpace) =>
        self.ToLab64(colorSpace, Illuminant.D65);

    /// <summary>
    /// Converts an <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/> to a <see cref="Lab64"/> pixel under <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// Because sRGB and LinearRgb are defined relative to a D65 white point, converting from <see cref="Rgb64"/> to <see cref="Lab64"/> under
    /// <see cref="Illuminant.E"/> applies Bradford chromatic adaptation from D65 to Illuminant E before CIELAB normalization so neutral grays
    /// remain achromatic (<c>a* = b* = 0</c>). By contrast, <see cref="ToLab64(Xyz32, Illuminant)"/> treats input XYZ as already under <paramref name="illuminant"/>.
    /// </remarks>
    /// <param name="self">The 64-bit RGB pixel to convert.</param>
    /// <param name="colorSpace">The RGB color space the input pixel is in.</param>
    /// <param name="illuminant">The reference white illuminant to normalize against.</param>
    /// <returns>A <see cref="Lab64"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Lab64 ToLab64(this Rgb64 self, RgbColorSpace colorSpace, Illuminant illuminant)
    {
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Vector3D referenceWhite = GetReferenceWhite64(illuminant);
        return self.ToLab64Unchecked(colorSpace, illuminant, referenceWhite);
    }

    internal static Lab64 ToLab64Unchecked(
        this Rgb64 self,
        RgbColorSpace colorSpace,
        Illuminant illuminant,
        Vector3D referenceWhite)
    {
        Vector3D xyz = self.ToXyzVectorUnchecked(colorSpace);
        if (illuminant == Illuminant.E)
        {
            xyz = AdaptD65ToE(xyz);
        }

        return ToLab64FromXyzVectorUnchecked(xyz, referenceWhite);
    }

    /// <summary>
    /// Converts from an <see cref="Xyz32"/> pixel to a 64-bit <see cref="Lab64"/> pixel.
    /// </summary>
    /// <remarks>
    /// This method assumes a D65 white point for the XYZ pixel.
    /// </remarks>
    /// <param name="self">The XYZ pixel on which to operate.</param>
    /// <returns>A 64-bit CIELAB pixel of the same color as the XYZ pixel.</returns>
    public static Lab64 ToLab64(this Xyz32 self) =>
        self.ToLab64(Illuminant.D65);

    /// <summary>
    /// Converts an <see cref="Xyz32"/> pixel to a 64-bit <see cref="Lab64"/> pixel under the specified <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// Treats <paramref name="self"/> as tristimulus coordinates already expressed under <paramref name="illuminant"/> and normalizes directly
    /// against that illuminant's reference white without applying chromatic adaptation.
    /// </remarks>
    /// <param name="self">The XYZ pixel on which to operate.</param>
    /// <param name="illuminant">The reference white illuminant to normalize against.</param>
    /// <returns>A 64-bit CIELAB pixel representing the same color under <paramref name="illuminant"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="illuminant"/> is not a defined <see cref="Illuminant"/> value.</exception>
    public static Lab64 ToLab64(this Xyz32 self, Illuminant illuminant) =>
        self.ToLab64Unchecked(GetReferenceWhite64(illuminant));

    internal static Lab64 ToLab64Unchecked(this Xyz32 self, Vector3D referenceWhite) =>
        ToLab64FromXyzVectorUnchecked(new(self.X, self.Y, self.Z), referenceWhite);

    private static Lab64 ToLab64FromXyzVectorUnchecked(Vector3D xyz, Vector3D referenceWhite)
    {
        double fx = LabTransfer64(xyz.X / referenceWhite.X);
        double fy = LabTransfer64(xyz.Y / referenceWhite.Y);
        double fz = LabTransfer64(xyz.Z / referenceWhite.Z);
        return new(116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz));
    }

    /// <summary>
    /// Converts a <see cref="Lab64"/> pixel to an <see cref="Xyz32"/> pixel assuming a D65 white point.
    /// </summary>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <returns>An <see cref="Xyz32"/> pixel representing the same color.</returns>
    public static Xyz32 ToXyz32(this Lab64 self) =>
        self.ToXyz32(Illuminant.D65);

    /// <summary>
    /// Converts a <see cref="Lab64"/> pixel to an <see cref="Xyz32"/> pixel under the specified <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// Produces tristimulus coordinates expressed directly under <paramref name="illuminant"/> using its reference white without applying
    /// chromatic adaptation to D65. By contrast, CIELAB-to-RGB overloads adapt from <paramref name="illuminant"/> back to D65 before converting to RGB.
    /// </remarks>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates.</param>
    /// <returns>An <see cref="Xyz32"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="illuminant"/> is not a defined <see cref="Illuminant"/> value.</exception>
    public static Xyz32 ToXyz32(this Lab64 self, Illuminant illuminant)
    {
        Vector3D xyz = self.ToXyzVectorUnchecked(GetReferenceWhite64(illuminant));
        return new((float)xyz.X, (float)xyz.Y, (float)xyz.Z);
    }

    internal static Xyz32 ToXyz32Unchecked(this Lab64 self, Vector3D referenceWhite)
    {
        Vector3D xyz = self.ToXyzVectorUnchecked(referenceWhite);
        return new((float)xyz.X, (float)xyz.Y, (float)xyz.Z);
    }

    private static Vector3D ToXyzVectorUnchecked(this Lab64 self, Vector3D referenceWhite)
    {
        double fy = (self.L + 16.0) / 116.0;
        double fx = fy + (self.A / 500.0);
        double fz = fy - (self.B / 200.0);
        return new(
            referenceWhite.X * InverseLabTransfer64(fx),
            referenceWhite.Y * InverseLabTransfer64(fy),
            referenceWhite.Z * InverseLabTransfer64(fz));
    }

    /// <summary>
    /// Converts a <see cref="Lab64"/> pixel (D65 white point) to an <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <returns>An <see cref="Rgb64"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Rgb64 ToRgb64(this Lab64 self, RgbColorSpace colorSpace) =>
        self.ToRgb64(colorSpace, Illuminant.D65);

    /// <summary>
    /// Converts a <see cref="Lab64"/> pixel under <paramref name="illuminant"/> to an <see cref="Rgb64"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// Because sRGB and LinearRgb use a D65 white point, converting from CIELAB under <see cref="Illuminant.E"/> applies inverse Bradford
    /// chromatic adaptation from Illuminant E back to D65 before the XYZ-to-RGB transform. By contrast, <see cref="ToXyz32(Lab64, Illuminant)"/>
    /// leaves the resulting XYZ coordinates under <paramref name="illuminant"/>.
    /// </remarks>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates.</param>
    /// <returns>An <see cref="Rgb64"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Rgb64 ToRgb64(this Lab64 self, RgbColorSpace colorSpace, Illuminant illuminant)
    {
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Vector3D referenceWhite = GetReferenceWhite64(illuminant);
        return self.ToRgb64Unchecked(colorSpace, illuminant, referenceWhite);
    }

    internal static Rgb64 ToRgb64Unchecked(
        this Lab64 self,
        RgbColorSpace colorSpace,
        Illuminant illuminant,
        Vector3D referenceWhite)
    {
        Vector3D xyz = self.ToXyzVectorUnchecked(referenceWhite);
        if (illuminant == Illuminant.E)
        {
            xyz = AdaptEToD65(xyz);
        }

        return XyzVectorToRgb64Unchecked(xyz, colorSpace);
    }

    /// <summary>
    /// Converts a <see cref="Lab64"/> pixel (D65 white point) to a rounded, byte-clamped <see cref="Rgb8"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <returns>An <see cref="Rgb8"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> is not a defined <see cref="RgbColorSpace"/> value.</exception>
    public static Rgb8 ToRgb8(this Lab64 self, RgbColorSpace colorSpace) =>
        self.ToRgb8(colorSpace, Illuminant.D65);

    /// <summary>
    /// Converts a <see cref="Lab64"/> pixel under <paramref name="illuminant"/> to a rounded, byte-clamped <see cref="Rgb8"/> pixel in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// Because sRGB and LinearRgb use a D65 white point, converting from CIELAB under <see cref="Illuminant.E"/> applies inverse Bradford
    /// chromatic adaptation from Illuminant E back to D65 before the XYZ-to-RGB transform.
    /// </remarks>
    /// <param name="self">The CIELAB pixel to convert.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates.</param>
    /// <returns>An <see cref="Rgb8"/> pixel representing the same color.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Rgb8 ToRgb8(this Lab64 self, RgbColorSpace colorSpace, Illuminant illuminant) =>
        self.ToRgb64(colorSpace, illuminant).ToRgb8();

    internal static Rgb8 ToRgb8Unchecked(
        this Lab64 self,
        RgbColorSpace colorSpace,
        Illuminant illuminant,
        Vector3D referenceWhite) =>
        self.ToRgb64Unchecked(colorSpace, illuminant, referenceWhite).ToRgb8();

    /// <summary>
    /// Converts a matrix of <see cref="Lab64"/> pixels under <paramref name="illuminant"/> to <see cref="Rgb8"/> in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies inverse Bradford chromatic adaptation from Illuminant E back to D65.
    /// </remarks>
    /// <param name="source">The source matrix of 64-bit CIELAB pixels.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing rounded, byte-clamped RGB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Matrix<Rgb8> ToRgb8(this IReadOnlyMatrix<Lab64> source, RgbColorSpace colorSpace, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Vector3D referenceWhite = GetReferenceWhite64(illuminant);
        return source.Map(pixel => pixel.ToRgb8Unchecked(colorSpace, illuminant, referenceWhite));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Lab64"/> pixels under <paramref name="illuminant"/> to <see cref="Rgb64"/> in the specified <see cref="RgbColorSpace"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies inverse Bradford chromatic adaptation from Illuminant E back to D65.
    /// </remarks>
    /// <param name="source">The source matrix of 64-bit CIELAB pixels.</param>
    /// <param name="colorSpace">The target RGB color space.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Rgb64}"/> containing converted 64-bit RGB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Matrix<Rgb64> ToRgb64(this IReadOnlyMatrix<Lab64> source, RgbColorSpace colorSpace, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Vector3D referenceWhite = GetReferenceWhite64(illuminant);
        return source.Map(pixel => pixel.ToRgb64Unchecked(colorSpace, illuminant, referenceWhite));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Lab64"/> pixels under <paramref name="illuminant"/> to <see cref="Xyz32"/>.
    /// </summary>
    /// <remarks>
    /// Produces <see cref="Xyz32"/> coordinates directly under <paramref name="illuminant"/> without applying chromatic adaptation to D65.
    /// </remarks>
    /// <param name="source">The source matrix of 64-bit CIELAB pixels.</param>
    /// <param name="illuminant">The reference white illuminant used by the input CIELAB coordinates (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Xyz32}"/> containing CIE XYZ pixels under <paramref name="illuminant"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="illuminant"/> is not a defined <see cref="Illuminant"/> value.</exception>
    public static Matrix<Xyz32> ToXyz32(this IReadOnlyMatrix<Lab64> source, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Vector3D referenceWhite = GetReferenceWhite64(illuminant);
        return source.Map(pixel => pixel.ToXyz32Unchecked(referenceWhite));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Rgb8"/> pixels in <paramref name="colorSpace"/> to <see cref="Lab64"/> under <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies Bradford chromatic adaptation from D65 to Illuminant E before CIELAB normalization.
    /// </remarks>
    /// <param name="source">The source matrix of 8-bit RGB pixels.</param>
    /// <param name="colorSpace">The RGB color space of the input pixels.</param>
    /// <param name="illuminant">The target reference white illuminant (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Lab64}"/> containing 64-bit CIELAB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Matrix<Lab64> ToLab64(this IReadOnlyMatrix<Rgb8> source, RgbColorSpace colorSpace, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Vector3D referenceWhite = GetReferenceWhite64(illuminant);
        return source.Map(pixel => pixel.ToLab64Unchecked(colorSpace, illuminant, referenceWhite));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Rgb64"/> pixels in <paramref name="colorSpace"/> to <see cref="Lab64"/> under <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// When <paramref name="illuminant"/> is <see cref="Illuminant.E"/>, applies Bradford chromatic adaptation from D65 to Illuminant E before CIELAB normalization.
    /// </remarks>
    /// <param name="source">The source matrix of 64-bit RGB pixels.</param>
    /// <param name="colorSpace">The RGB color space of the input pixels.</param>
    /// <param name="illuminant">The target reference white illuminant (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Lab64}"/> containing 64-bit CIELAB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is not a defined enum value.</exception>
    public static Matrix<Lab64> ToLab64(this IReadOnlyMatrix<Rgb64> source, RgbColorSpace colorSpace, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Vector3D referenceWhite = GetReferenceWhite64(illuminant);
        return source.Map(pixel => pixel.ToLab64Unchecked(colorSpace, illuminant, referenceWhite));
    }

    /// <summary>
    /// Converts a matrix of <see cref="Xyz32"/> pixels to <see cref="Lab64"/> under <paramref name="illuminant"/>.
    /// </summary>
    /// <remarks>
    /// Treats the input <see cref="Xyz32"/> values as already expressed under <paramref name="illuminant"/> without applying chromatic adaptation.
    /// </remarks>
    /// <param name="source">The source matrix of CIE XYZ pixels.</param>
    /// <param name="illuminant">The reference white illuminant to normalize against (default <see cref="Illuminant.D65"/>).</param>
    /// <returns>A new <see cref="Matrix{Lab64}"/> containing 64-bit CIELAB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="illuminant"/> is not a defined <see cref="Illuminant"/> value.</exception>
    public static Matrix<Lab64> ToLab64(this IReadOnlyMatrix<Xyz32> source, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(source);
        Vector3D referenceWhite = GetReferenceWhite64(illuminant);
        return source.Map(pixel => pixel.ToLab64Unchecked(referenceWhite));
    }

    /// <summary>
    /// Widens a matrix of 32-bit <see cref="Lab32"/> pixels to 64-bit <see cref="Lab64"/> pixels.
    /// </summary>
    /// <param name="source">The source matrix of 32-bit CIELAB pixels.</param>
    /// <returns>A new <see cref="Matrix{Lab64}"/> containing widened CIELAB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static Matrix<Lab64> ToLab64(this IReadOnlyMatrix<Lab32> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Map(static pixel => pixel.ToLab64());
    }

    /// <summary>
    /// Narrows a matrix of 64-bit <see cref="Lab64"/> pixels to 32-bit <see cref="Lab32"/> pixels.
    /// </summary>
    /// <param name="source">The source matrix of 64-bit CIELAB pixels.</param>
    /// <returns>A new <see cref="Matrix{Lab32}"/> containing narrowed CIELAB pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static Matrix<Lab32> ToLab32(this IReadOnlyMatrix<Lab64> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Map(static pixel => pixel.ToLab32());
    }

    private const float LabEpsilon = 0.008856f; // (6/29)^3
    private const float LabKappa = 903.3f; // (29/3)^3
    private const double LabEpsilon64 = 216.0 / 24389.0; // (6/29)^3
    private const double LabKappa64 = 24389.0 / 27.0; // (29/3)^3

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

    private static double LabTransfer64(double channel) =>
        channel > LabEpsilon64 ? Math.Cbrt(channel) : ((LabKappa64 * channel) + 16.0) / 116.0;

    private static double InverseLabTransfer64(double f)
    {
        double cubed = f * f * f;
        return cubed > LabEpsilon64 ? cubed : ((116.0 * f) - 16.0) / LabKappa64;
    }
}
