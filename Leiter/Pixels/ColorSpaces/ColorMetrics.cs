namespace Leiter.Pixels.ColorSpaces;

/// <summary>
/// Provides hue, intensity, luma, saturation, chroma, and lightness metrics for RGB and CIELAB pixels.
/// </summary>
public static class ColorMetrics
{
    /// <summary>
    /// Computes the HSV/HSL hue angle in degrees in <c>[0, 360)</c> for an <see cref="Rgb64"/> pixel.
    /// </summary>
    /// <remarks>
    /// Returns <c>0</c> for achromatic pixels where <c>max(R, G, B) - min(R, G, B) &lt;= 1e-12</c>.
    /// </remarks>
    /// <param name="pixel">The pixel to evaluate.</param>
    /// <returns>The hue angle in degrees in <c>[0, 360)</c>.</returns>
    public static double GetHueDegrees(this Rgb64 pixel)
    {
        double r = pixel.R;
        double g = pixel.G;
        double b = pixel.B;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        if (delta <= 1e-12)
            return 0.0;

        double sector;
        if (max == r)
            sector = ((g - b) / delta) % 6.0;
        else if (max == g)
            sector = ((b - r) / delta) + 2.0;
        else
            sector = ((r - g) / delta) + 4.0;

        return WrapHueDegrees(sector * 60.0);
    }

    /// <summary>
    /// Computes the HSV/HSL hue angle in degrees in <c>[0, 360)</c> for an <see cref="Rgb8"/> pixel.
    /// </summary>
    /// <param name="pixel">The pixel to evaluate.</param>
    /// <returns>The hue angle in degrees in <c>[0, 360)</c>.</returns>
    public static double GetHueDegrees(this Rgb8 pixel) => pixel.ToRgb64().GetHueDegrees();

    /// <summary>
    /// Computes the HSI intensity <c>(R + G + B) / 3</c> for an <see cref="Rgb64"/> pixel.
    /// </summary>
    /// <param name="pixel">The pixel to evaluate.</param>
    /// <returns>The mean channel intensity.</returns>
    public static double GetIntensity(this Rgb64 pixel) => (pixel.R + pixel.G + pixel.B) / 3.0;

    /// <summary>
    /// Computes the normalized HSI intensity in <c>[0, 1]</c> for an <see cref="Rgb8"/> pixel.
    /// </summary>
    /// <param name="pixel">The pixel to evaluate.</param>
    /// <returns>The normalized intensity in <c>[0, 1]</c>.</returns>
    public static double GetIntensity(this Rgb8 pixel) => (pixel.R + pixel.G + pixel.B) / (3.0 * 255.0);

    /// <summary>
    /// Computes the Rec. 601 luma <c>0.299 * R + 0.587 * G + 0.114 * B</c> for an <see cref="Rgb64"/> pixel.
    /// </summary>
    /// <param name="pixel">The pixel to evaluate.</param>
    /// <returns>The Rec. 601 luminance value.</returns>
    public static double GetLuma(this Rgb64 pixel) =>
        (0.299 * pixel.R) + (0.587 * pixel.G) + (0.114 * pixel.B);

    /// <summary>
    /// Computes the normalized Rec. 601 luma in <c>[0, 1]</c> for an <see cref="Rgb8"/> pixel.
    /// </summary>
    /// <param name="pixel">The pixel to evaluate.</param>
    /// <returns>The normalized Rec. 601 luminance in <c>[0, 1]</c>.</returns>
    public static double GetLuma(this Rgb8 pixel) =>
        ((0.299 * pixel.R) + (0.587 * pixel.G) + (0.114 * pixel.B)) / 255.0;

    /// <summary>
    /// Computes the HSV saturation in <c>[0, 1]</c> for an <see cref="Rgb64"/> pixel.
    /// </summary>
    /// <param name="pixel">The pixel to evaluate.</param>
    /// <returns>The saturation in <c>[0, 1]</c>, or <c>0</c> for black.</returns>
    public static double GetSaturation(this Rgb64 pixel)
    {
        double max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
        if (max <= 1e-12)
            return 0.0;

        double min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
        return Math.Clamp((max - min) / max, 0.0, 1.0);
    }

    /// <summary>
    /// Computes the HSV saturation in <c>[0, 1]</c> for an <see cref="Rgb8"/> pixel.
    /// </summary>
    /// <param name="pixel">The pixel to evaluate.</param>
    /// <returns>The saturation in <c>[0, 1]</c>.</returns>
    public static double GetSaturation(this Rgb8 pixel) => pixel.ToRgb64().GetSaturation();

    /// <summary>
    /// Computes the CIELCh chroma <c>C* = sqrt(a*² + b*²)</c> for a <see cref="Lab32"/> pixel.
    /// </summary>
    /// <param name="pixel">The CIELAB pixel to evaluate.</param>
    /// <returns>The non-negative chroma magnitude.</returns>
    public static double GetChroma(this Lab32 pixel) =>
        Math.Sqrt(((double)pixel.A * pixel.A) + ((double)pixel.B * pixel.B));

    /// <summary>
    /// Computes the cylindrical CIELCh hue angle in degrees in <c>[0, 360)</c> for a <see cref="Lab32"/> pixel.
    /// </summary>
    /// <remarks>
    /// Returns <c>0</c> for near-neutral pixels where chroma is at most <c>1e-6</c>.
    /// </remarks>
    /// <param name="pixel">The CIELAB pixel to evaluate.</param>
    /// <returns>The hue angle in degrees in <c>[0, 360)</c>.</returns>
    public static double GetHueDegrees(this Lab32 pixel)
    {
        if (pixel.GetChroma() <= 1e-6)
            return 0.0;

        return WrapHueDegrees(Math.Atan2(pixel.B, pixel.A) * (180.0 / Math.PI));
    }

    /// <summary>
    /// Computes lightness normalized to <c>[0, 1]</c> (<c>L* / 100</c> clamped) for a <see cref="Lab32"/> pixel.
    /// </summary>
    /// <param name="pixel">The CIELAB pixel to evaluate.</param>
    /// <returns>The clamped normalized lightness in <c>[0, 1]</c>.</returns>
    public static double GetNormalizedLightness(this Lab32 pixel) =>
        Math.Clamp(pixel.L / 100.0, 0.0, 1.0);

    /// <summary>
    /// Computes the shortest circular angular difference in <c>[0, 180]</c> between two hue angles in degrees.
    /// </summary>
    /// <param name="hue1Degrees">The first hue angle in degrees.</param>
    /// <param name="hue2Degrees">The second hue angle in degrees.</param>
    /// <returns>The shortest angular distance in degrees in <c>[0, 180]</c>.</returns>
    public static double HueDifferenceDegrees(double hue1Degrees, double hue2Degrees)
    {
        double diff = Math.Abs(hue1Degrees - hue2Degrees) % 360.0;
        return diff > 180.0 ? 360.0 - diff : diff;
    }

    private static double WrapHueDegrees(double degrees)
    {
        if (degrees < 0.0)
            degrees += 360.0;

        return degrees >= 360.0 ? 0.0 : degrees;
    }
}
