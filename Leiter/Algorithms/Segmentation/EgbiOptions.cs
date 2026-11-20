namespace Leiter.Algorithms.Segmentation;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Specifies configuration parameters for Felzenszwalb–Huttenlocher Efficient Graph-Based Image Segmentation (EGBI).
/// </summary>
/// <param name="KFactor">The scale parameter controlling preference for larger segments. Must be a finite non-negative number.</param>
/// <param name="Epsilon">The minimum color distance threshold below which adjacent pixels are always merged into the same segment. Must be a finite non-negative number.</param>
/// <param name="MinRegionSize">The minimum pixel count required per region during post-processing; set to <c>0</c> to disable small-region merging. Must be non-negative.</param>
/// <param name="Connectivity">The grid neighborhood connectivity used to construct adjacent pixel graph edges.</param>
public sealed record EgbiOptions(
    double KFactor = 100.0,
    double Epsilon = 1.0,
    int MinRegionSize = 100,
    GridConnectivity Connectivity = GridConnectivity.EightConnected) : ISizeTunableOptions<EgbiOptions>, IProfileTunableOptions<EgbiOptions>
{
    private const double MinEpsilonFloor = 0.15;
    private const int MaxMinRegionSizeDivisor = 16;

    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which baseline parameters apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="EgbiOptions"/> instance.
    /// </summary>
    public static EgbiOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="EgbiOptions"/> scaled geometrically for <paramref name="size"/> from default EGBI parameters.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EgbiOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static EgbiOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="EgbiOptions"/> scaled geometrically for <paramref name="size"/> from <paramref name="baseline"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline EGBI parameters calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EgbiOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static EgbiOptions ForSize(
        Size size,
        EgbiOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateSizeTuning(size, baseline, referenceDimension);
        return ScaleCore(size, baseline, referenceDimension, colorSpreadFactor: 1.0, clutterFactor: 1.0);
    }

    /// <summary>
    /// Creates <see cref="EgbiOptions"/> tuned for both the dimensions and image statistics of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EgbiOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static EgbiOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage<EgbiOptions>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="EgbiOptions"/> tuned from <paramref name="baseline"/> for the dimensions and statistics of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="baseline">The baseline EGBI parameters calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EgbiOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static EgbiOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        EgbiOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage(image, baseline, referenceDimension);

    /// <summary>
    /// Creates <see cref="EgbiOptions"/> tuned for both the dimensions and CIELAB statistics of <paramref name="labImage"/>.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EgbiOptions"/> instance tuned for <paramref name="labImage"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="labImage"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static EgbiOptions ForImage(
        IReadOnlyMatrix<Lab32> labImage,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForLabImage<EgbiOptions>(labImage, referenceDimension);

    /// <summary>
    /// Creates <see cref="EgbiOptions"/> tuned from <paramref name="baseline"/> for the dimensions and CIELAB statistics of <paramref name="labImage"/>.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to analyze.</param>
    /// <param name="baseline">The baseline EGBI parameters calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EgbiOptions"/> instance tuned for <paramref name="labImage"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static EgbiOptions ForImage(
        IReadOnlyMatrix<Lab32> labImage,
        EgbiOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForLabImage(labImage, baseline, referenceDimension);

    /// <summary>
    /// Creates <see cref="EgbiOptions"/> tuned from default EGBI parameters using pre-computed <paramref name="analysis"/>.
    /// </summary>
    /// <param name="analysis">The pre-computed image analysis profile.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EgbiOptions"/> instance tuned for <paramref name="analysis"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static EgbiOptions ForAnalysis(
        ImageAnalysis analysis,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis<EgbiOptions>(analysis, referenceDimension);

    /// <summary>
    /// Creates <see cref="EgbiOptions"/> tuned from <paramref name="baseline"/> using pre-computed <paramref name="analysis"/>.
    /// </summary>
    /// <param name="analysis">The pre-computed image analysis profile.</param>
    /// <param name="baseline">The baseline EGBI parameters calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EgbiOptions"/> instance tuned for <paramref name="analysis"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static EgbiOptions ForAnalysis(
        ImageAnalysis analysis,
        EgbiOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis(analysis, baseline, referenceDimension);

    static EgbiOptions IProfileTunableOptions<EgbiOptions>.ForProfile(
        in ImageTuningProfile profile,
        EgbiOptions baseline,
        double referenceDimension) =>
        ForProfile(in profile, baseline, referenceDimension);

    internal static EgbiOptions ForProfile(
        in ImageTuningProfile profile,
        EgbiOptions baseline,
        double referenceDimension) =>
        ScaleCore(
            profile.ImageSize,
            baseline,
            referenceDimension,
            profile.IsEmpty ? 1.0 : profile.ColorSpreadFactor,
            profile.IsEmpty ? 1.0 : profile.ClutterFactor);

    private static EgbiOptions ScaleCore(
        Size size,
        EgbiOptions baseline,
        double referenceDimension,
        double colorSpreadFactor,
        double clutterFactor)
    {
        var (linearScale, areaScale, totalPixels) = OptionTuning.ComputeScalesUnchecked(size, referenceDimension);
        if (totalPixels == 0)
        {
            return baseline;
        }

        double edgeAttenuation = OptionTuning.ComputeBlurredEdgeAttenuation(linearScale);
        double kFactor = OptionTuning.ScaleFiniteNonNegative(baseline.KFactor * areaScale * edgeAttenuation * colorSpreadFactor);
        double epsilonFloor = Math.Min(MinEpsilonFloor, baseline.Epsilon);
        double epsilon = baseline.Epsilon == 0.0
            ? 0.0
            : OptionTuning.ScaleFinitePositive(baseline.Epsilon * edgeAttenuation, epsilonFloor);
        int minRegionSize = baseline.MinRegionSize == 0
            ? 0
            : OptionTuning.ClampRegionSize(baseline.MinRegionSize * areaScale * clutterFactor, totalPixels, MaxMinRegionSizeDivisor);

        return baseline with
        {
            KFactor = kFactor,
            Epsilon = epsilon,
            MinRegionSize = minRegionSize
        };
    }

    /// <summary>
    /// Validates the options, throwing <see cref="ArgumentOutOfRangeException"/> if any parameter is outside its valid range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any configuration property is invalid.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFiniteNonNegative(KFactor);
        Guard.ThrowIfNotFiniteNonNegative(Epsilon);
        ArgumentOutOfRangeException.ThrowIfNegative(MinRegionSize);
        Guard.ThrowIfUndefinedEnum(Connectivity);
    }
}
