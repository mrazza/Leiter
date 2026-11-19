namespace Leiter.Algorithms.Internal;

using System;
using Leiter.Core;

/// <summary>
/// Provides shared geometric scaling and argument validation helpers for resolution-adaptive algorithm options.
/// </summary>
internal static class OptionTuning
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which baseline option values apply with scale <c>1.0</c>.
    /// </summary>
    public const double DefaultReferenceDimension = 800.0;

    /// <summary>
    /// The mean 3D CIELAB color dispersion across <see cref="Stylization.Watercolor.WatercolorPalettes.Defaults"/> used to normalize color spread adaptation.
    /// </summary>
    public const double ReferenceLabDispersion = 25.14;

    /// <summary>
    /// The canonical normalized Frobenius gradient magnitude baseline used to normalize scene clutter adaptation.
    /// </summary>
    public const double ReferenceClutterGradient = 0.06;

    /// <summary>
    /// The lower bound for resolution-scaled Perlin noise base frequencies in cycles per pixel.
    /// </summary>
    public const double MinFrequency = 1e-5;

    /// <summary>
    /// The Nyquist upper bound for resolution-scaled Perlin noise base frequencies in cycles per pixel.
    /// </summary>
    public const double MaxNyquistFrequency = 0.5;

    /// <summary>
    /// Validates that <paramref name="size"/> has non-negative dimensions and <paramref name="referenceDimension"/> is finite and positive.
    /// </summary>
    public static void ValidateSizeAndReference(Size size, double referenceDimension)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size.Width, nameof(size));
        ArgumentOutOfRangeException.ThrowIfNegative(size.Height, nameof(size));

        if (!double.IsFinite(referenceDimension) || referenceDimension <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(referenceDimension), referenceDimension, "Reference dimension must be a finite positive number.");
        }
    }

    /// <summary>
    /// Validates that <paramref name="image"/> is non-null and has valid dimensions alongside a positive finite <paramref name="referenceDimension"/>.
    /// </summary>
    public static void ValidateImageAndReference<T>(IReadOnlyMatrix<T> image, double referenceDimension)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(image);
        ValidateSizeAndReference(image.Size, referenceDimension);
    }

    /// <summary>
    /// Validates inputs and computes aspect-neutral linear and area scale factors relative to <paramref name="referenceDimension"/>.
    /// </summary>
    public static (double LinearScale, double AreaScale, long TotalPixels) ComputeScales(Size size, double referenceDimension)
    {
        ValidateSizeAndReference(size, referenceDimension);

        long totalPixels = (long)size.Width * size.Height;
        if (totalPixels == 0)
        {
            return (1.0, 1.0, 0L);
        }

        double referenceArea = referenceDimension * referenceDimension;
        double areaScale = totalPixels / referenceArea;
        double linearScale = Math.Sqrt(areaScale);
        return (linearScale, areaScale, totalPixels);
    }

    /// <summary>
    /// Computes the adjacent-pixel edge difference attenuation factor under Gaussian pre-smoothing scaled by <paramref name="linearScale"/>.
    /// </summary>
    public static double ComputeBlurredEdgeAttenuation(double linearScale, double baseSigma = 0.8)
    {
        double twoPiSigmaSq = 2.0 * Math.PI * baseSigma * baseSigma;
        return Math.Sqrt((twoPiSigmaSq + 1.0) / ((twoPiSigmaSq * linearScale * linearScale) + 1.0));
    }

    /// <summary>
    /// Rounds a scaled pixel-count threshold and clamps it cleanly to a safe fraction of <paramref name="totalPixels"/>.
    /// </summary>
    public static int ClampRegionSize(double scaledSize, long totalPixels, int maxFractionDivisor)
    {
        int rounded = RoundPositiveInt(scaledSize);
        if (totalPixels <= 0)
        {
            return rounded;
        }

        long maxAllowed = Math.Max(1L, totalPixels / maxFractionDivisor);
        long clamped = Math.Min(rounded, maxAllowed);
        return (int)Math.Min(clamped, int.MaxValue);
    }

    /// <summary>
    /// Clamps a scaled spatial frequency to <c>[MinFrequency, MaxNyquistFrequency]</c>.
    /// </summary>
    public static double ClampFrequency(double scaledFrequency) =>
        Math.Clamp(scaledFrequency, MinFrequency, MaxNyquistFrequency);

    /// <summary>
    /// Rounds a scaled scalar value away from zero and enforces a minimum integer value.
    /// </summary>
    public static int RoundPositiveInt(double value, int minValue = 1) =>
        Math.Max(minValue, (int)Math.Round(value, MidpointRounding.AwayFromZero));
}
