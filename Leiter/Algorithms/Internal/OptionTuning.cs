namespace Leiter.Algorithms.Internal;

using System;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

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
    /// The default baseline Gaussian pre-blur standard deviation (<c>σ_0 = 0.8</c>) calibrated at <see cref="DefaultReferenceDimension"/>.
    /// </summary>
    public const double DefaultBaseBlurSigma = 0.8;

    /// <summary>
    /// The mean 3D CIELAB color dispersion across <see cref="Stylization.Watercolor.WatercolorPalettes.Defaults"/> used to normalize color spread adaptation.
    /// </summary>
    public const double ReferenceLabDispersion = 25.14;

    /// <summary>
    /// The exact mean CIELAB lightness standard deviation (<c>StdDevL</c>) across <see cref="Stylization.Watercolor.WatercolorPalettes.Defaults"/> under <see cref="Illuminant.E"/>.
    /// </summary>
    public const double ReferenceStdDevL = 17.696;

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
    /// Validates that <paramref name="size"/>, <paramref name="baseline"/>, and <paramref name="referenceDimension"/> are valid for size-based tuning.
    /// </summary>
    public static void ValidateSizeTuning<TOptions>(Size size, TOptions baseline, double referenceDimension)
        where TOptions : class, IValidatableOptions
    {
        ValidateSizeAndReference(size, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
    }

    /// <summary>
    /// Validates that <paramref name="baseline"/> is non-null and valid alongside a positive finite <paramref name="referenceDimension"/>.
    /// </summary>
    public static void ValidateBaselineAndReference<TOptions>(TOptions baseline, double referenceDimension)
        where TOptions : class, IValidatableOptions
    {
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
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
        ValidateImageTuning(image, baseline, referenceDimension);

        ImageTuningProfile profile = ImageTuningProfile.AnalyzeUnchecked(image);
        return TOptions.ForProfile(in profile, baseline, referenceDimension);
    }

    /// <summary>
    /// Validates <paramref name="labImage"/> and <paramref name="referenceDimension"/>, analyzes canonical CIELAB statistics,
    /// and tunes <typeparamref name="TOptions"/> using its default baseline.
    /// </summary>
    public static TOptions TuneForLabImage<TOptions>(
        IReadOnlyMatrix<Lab32> labImage,
        double referenceDimension,
        Illuminant illuminant = Illuminant.D65)
        where TOptions : class, IProfileTunableOptions<TOptions> =>
        TuneForLabImage(labImage, TOptions.Default, referenceDimension, illuminant);

    /// <summary>
    /// Validates <paramref name="labImage"/>, <paramref name="baseline"/>, <paramref name="referenceDimension"/>, and <paramref name="illuminant"/>,
    /// analyzes canonical CIELAB statistics, and tunes <typeparamref name="TOptions"/> from <paramref name="baseline"/>.
    /// </summary>
    public static TOptions TuneForLabImage<TOptions>(
        IReadOnlyMatrix<Lab32> labImage,
        TOptions baseline,
        double referenceDimension,
        Illuminant illuminant = Illuminant.D65)
        where TOptions : class, IProfileTunableOptions<TOptions>
    {
        ValidateImageTuning(labImage, baseline, referenceDimension);
        Guard.ThrowIfUndefinedEnum(illuminant);

        ImageTuningProfile profile = ImageTuningProfile.AnalyzeUnchecked(labImage, illuminant);
        return TOptions.ForProfile(in profile, baseline, referenceDimension);
    }

    /// <summary>
    /// Validates <paramref name="analysis"/> and <paramref name="referenceDimension"/>, and tunes <typeparamref name="TOptions"/>
    /// from its default baseline using the pre-computed profile.
    /// </summary>
    public static TOptions TuneForAnalysis<TOptions>(ImageAnalysis analysis, double referenceDimension)
        where TOptions : class, IProfileTunableOptions<TOptions> =>
        TuneForAnalysis(analysis, TOptions.Default, referenceDimension);

    /// <summary>
    /// Validates <paramref name="analysis"/>, <paramref name="baseline"/>, and <paramref name="referenceDimension"/>,
    /// and tunes <typeparamref name="TOptions"/> from <paramref name="baseline"/> using the pre-computed profile.
    /// </summary>
    public static TOptions TuneForAnalysis<TOptions>(ImageAnalysis analysis, TOptions baseline, double referenceDimension)
        where TOptions : class, IProfileTunableOptions<TOptions>
    {
        ValidateAnalysisTuning(analysis, baseline, referenceDimension);

        ImageTuningProfile profile = analysis.Profile;
        return TOptions.ForProfile(in profile, baseline, referenceDimension);
    }

    /// <summary>
    /// Validates <paramref name="analysis"/> and <paramref name="referenceDimension"/>, and tunes size-only <typeparamref name="TOptions"/>
    /// from its default baseline.
    /// </summary>
    public static TOptions TuneForSizeAnalysis<TOptions>(ImageAnalysis analysis, double referenceDimension)
        where TOptions : class, ISizeTunableOptions<TOptions> =>
        TuneForSizeAnalysis(analysis, TOptions.Default, referenceDimension);

    /// <summary>
    /// Validates <paramref name="analysis"/>, <paramref name="baseline"/>, and <paramref name="referenceDimension"/>,
    /// and tunes size-only <typeparamref name="TOptions"/> from <paramref name="baseline"/>.
    /// </summary>
    public static TOptions TuneForSizeAnalysis<TOptions>(ImageAnalysis analysis, TOptions baseline, double referenceDimension)
        where TOptions : class, ISizeTunableOptions<TOptions>
    {
        ValidateAnalysisTuning(analysis, baseline, referenceDimension);

        return TOptions.ForSize(analysis.Size, baseline, referenceDimension);
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
        ValidateImageTuning(image, baseline, referenceDimension);

        return TOptions.ForSize(image.Size, baseline, referenceDimension);
    }

    private static void ValidateImageTuning<T, TOptions>(IReadOnlyMatrix<T> image, TOptions baseline, double referenceDimension)
        where T : struct
        where TOptions : class, IValidatableOptions
    {
        ValidateImageAndReference(image, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
    }

    private static void ValidateAnalysisTuning<TOptions>(ImageAnalysis analysis, TOptions baseline, double referenceDimension)
        where TOptions : class, IValidatableOptions
    {
        ArgumentNullException.ThrowIfNull(analysis);
        Guard.ThrowIfNotFinitePositive(referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
    }

    /// <summary>
    /// Validates inputs and computes aspect-neutral linear and area scale factors relative to <paramref name="referenceDimension"/>.
    /// </summary>
    public static (double LinearScale, double AreaScale, long TotalPixels) ComputeScales(Size size, double referenceDimension)
    {
        ValidateSizeAndReference(size, referenceDimension);
        return ComputeScalesUnchecked(size, referenceDimension);
    }

    /// <summary>
    /// Computes aspect-neutral linear and area scale factors relative to <paramref name="referenceDimension"/>
    /// without re-validating pre-validated arguments.
    /// </summary>
    public static (double LinearScale, double AreaScale, long TotalPixels) ComputeScalesUnchecked(Size size, double referenceDimension)
    {
        long totalPixels = size.Area;
        if (totalPixels == 0)
        {
            return (1.0, 1.0, 0L);
        }

        double referenceArea = referenceDimension * referenceDimension;
        if (referenceArea > 0.0 && double.IsFinite(referenceArea))
        {
            double areaScale = totalPixels / referenceArea;
            if (double.IsFinite(areaScale))
            {
                return (Math.Sqrt(areaScale), areaScale, totalPixels);
            }
        }

        double linearScale = Math.Sqrt(totalPixels) / referenceDimension;
        double safeAreaScale = linearScale * linearScale;
        return (
            double.IsFinite(linearScale) ? linearScale : double.MaxValue,
            double.IsFinite(safeAreaScale) ? safeAreaScale : double.MaxValue,
            totalPixels);
    }

    /// <summary>
    /// Computes the adjacent-pixel edge difference attenuation factor under Gaussian pre-smoothing scaled by <paramref name="linearScale"/>.
    /// </summary>
    public static double ComputeBlurredEdgeAttenuation(double linearScale, double baseSigma = DefaultBaseBlurSigma)
    {
        if (baseSigma == 0.0)
        {
            return 1.0;
        }

        double twoPiSigmaSq = 2.0 * Math.PI * baseSigma * baseSigma;
        double scaledTerm = twoPiSigmaSq * linearScale * linearScale;
        if (double.IsFinite(scaledTerm))
        {
            return Math.Sqrt((twoPiSigmaSq + 1.0) / (scaledTerm + 1.0));
        }

        double denomScale = Math.Sqrt(2.0 * Math.PI) * Math.Abs(baseSigma) * linearScale;
        return double.IsFinite(denomScale)
            ? Math.Sqrt(twoPiSigmaSq + 1.0) / double.Hypot(denomScale, 1.0)
            : 0.0;
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
        return (int)Math.Min(rounded, maxAllowed);
    }

    /// <summary>
    /// Clamps a scaled spatial frequency to <c>[MinFrequency, MaxNyquistFrequency]</c>.
    /// </summary>
    public static double ClampFrequency(double scaledFrequency) =>
        double.IsNaN(scaledFrequency)
            ? MinFrequency
            : Math.Clamp(scaledFrequency, MinFrequency, MaxNyquistFrequency);

    /// <summary>
    /// Clamps <paramref name="value"/> to the finite interval <c>[minValue, maxValue]</c>,
    /// substituting <paramref name="minValue"/> when <paramref name="value"/> is <see cref="double.NaN"/>.
    /// </summary>
    public static double ClampFinite(double value, double minValue, double maxValue = double.MaxValue)
    {
        Guard.ThrowIfNotFinite(minValue);
        Guard.ThrowIfNotFinite(maxValue);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxValue, minValue);

        return double.IsNaN(value) ? minValue : Math.Clamp(value, minValue, maxValue);
    }

    /// <summary>
    /// Clamps a scaled scalar value to the finite positive interval <c>[minValue, maxValue]</c>,
    /// preventing overflow to <see cref="double.PositiveInfinity"/>.
    /// </summary>
    public static double ScaleFinitePositive(double value, double minValue, double maxValue = double.MaxValue)
    {
        Guard.ThrowIfNotFinitePositive(minValue);
        return ClampFinite(value, minValue, maxValue);
    }

    /// <summary>
    /// Clamps a scaled scalar value to the finite non-negative interval <c>[0.0, maxValue]</c>,
    /// preventing overflow to <see cref="double.PositiveInfinity"/>.
    /// </summary>
    public static double ScaleFiniteNonNegative(double value, double maxValue = double.MaxValue)
    {
        Guard.ThrowIfNotFiniteNonNegative(maxValue);
        return ClampFinite(value, 0.0, maxValue);
    }

    /// <summary>
    /// Rounds a scaled scalar value away from zero and clamps it in <see langword="double"/> precision
    /// to <c>[minValue, int.MaxValue]</c> before casting to <see langword="int"/>.
    /// </summary>
    public static int RoundPositiveInt(double value, int minValue = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minValue, 1);

        if (double.IsNaN(value))
        {
            return minValue;
        }

        double clamped = Math.Clamp(value, (double)minValue, (double)int.MaxValue);
        return (int)Math.Round(clamped, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Computes the geometric-mean linear dimension <c>sqrt(Width * Height)</c> of <paramref name="size"/>.
    /// </summary>
    public static double GeometricMeanDimension(Size size) => size.LinearDimension;
}
