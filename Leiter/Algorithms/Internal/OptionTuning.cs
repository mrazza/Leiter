namespace Leiter.Algorithms.Internal;

using System;
using Leiter.Core;
using Leiter.Pixels;

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
        Guard.ThrowIfNegative(size);
        Guard.ThrowIfNotFinitePositive(referenceDimension);
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
    /// Validates <paramref name="image"/> and <paramref name="referenceDimension"/>, analyzes canonical image statistics,
    /// and tunes <typeparamref name="TOptions"/> using its default baseline.
    /// </summary>
    public static TOptions TuneForImage<TOptions>(IReadOnlyMatrix<Rgb8> image, double referenceDimension)
        where TOptions : class, IProfileTunableOptions<TOptions> =>
        TuneForImage(image, TOptions.Default, referenceDimension);

    /// <summary>
    /// Validates <paramref name="image"/>, <paramref name="baseline"/>, and <paramref name="referenceDimension"/>,
    /// analyzes canonical image statistics, and tunes <typeparamref name="TOptions"/> from <paramref name="baseline"/>.
    /// </summary>
    public static TOptions TuneForImage<TOptions>(IReadOnlyMatrix<Rgb8> image, TOptions baseline, double referenceDimension)
        where TOptions : class, IProfileTunableOptions<TOptions>
    {
        ValidateImageAndReference(image, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();

        ImageTuningProfile profile = ImageTuningProfile.Analyze(image);
        return TOptions.ForProfile(in profile, baseline, referenceDimension);
    }

    /// <summary>
    /// Validates <paramref name="labImage"/> and <paramref name="referenceDimension"/>, analyzes canonical CIELAB statistics,
    /// and tunes <typeparamref name="TOptions"/> using its default baseline.
    /// </summary>
    public static TOptions TuneForLabImage<TOptions>(IReadOnlyMatrix<Lab32> labImage, double referenceDimension)
        where TOptions : class, IProfileTunableOptions<TOptions> =>
        TuneForLabImage(labImage, TOptions.Default, referenceDimension);

    /// <summary>
    /// Validates <paramref name="labImage"/>, <paramref name="baseline"/>, and <paramref name="referenceDimension"/>,
    /// analyzes canonical CIELAB statistics, and tunes <typeparamref name="TOptions"/> from <paramref name="baseline"/>.
    /// </summary>
    public static TOptions TuneForLabImage<TOptions>(IReadOnlyMatrix<Lab32> labImage, TOptions baseline, double referenceDimension)
        where TOptions : class, IProfileTunableOptions<TOptions>
    {
        ValidateImageAndReference(labImage, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();

        ImageTuningProfile profile = ImageTuningProfile.Analyze(labImage);
        return TOptions.ForProfile(in profile, baseline, referenceDimension);
    }

    /// <summary>
    /// Validates <paramref name="image"/> and <paramref name="referenceDimension"/>, and tunes size-only <typeparamref name="TOptions"/>
    /// using its default baseline.
    /// </summary>
    public static TOptions TuneForSizeImage<TOptions, TPixel>(IReadOnlyMatrix<TPixel> image, double referenceDimension)
        where TOptions : class, ISizeTunableOptions<TOptions>
        where TPixel : struct =>
        TuneForSizeImage(image, TOptions.Default, referenceDimension);

    /// <summary>
    /// Validates <paramref name="image"/>, <paramref name="baseline"/>, and <paramref name="referenceDimension"/>,
    /// and tunes size-only <typeparamref name="TOptions"/> from <paramref name="baseline"/>.
    /// </summary>
    public static TOptions TuneForSizeImage<TOptions, TPixel>(IReadOnlyMatrix<TPixel> image, TOptions baseline, double referenceDimension)
        where TOptions : class, ISizeTunableOptions<TOptions>
        where TPixel : struct
    {
        ValidateImageAndReference(image, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();

        return TOptions.ForSize(image.Size, baseline, referenceDimension);
    }

    /// <summary>
    /// Validates inputs and computes aspect-neutral linear and area scale factors relative to <paramref name="referenceDimension"/>.
    /// </summary>
    public static (double LinearScale, double AreaScale, long TotalPixels) ComputeScales(Size size, double referenceDimension)
    {
        ValidateSizeAndReference(size, referenceDimension);

        long totalPixels = size.Area;
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

    /// <summary>
    /// Computes the geometric-mean linear dimension <c>sqrt(Width * Height)</c> of <paramref name="size"/>.
    /// </summary>
    public static double GeometricMeanDimension(Size size) => size.LinearDimension;
}
