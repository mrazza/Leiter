namespace Leiter.Algorithms.Segmentation;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Specifies configuration parameters for Mean-Shift filtering and segmentation.
/// </summary>
/// <param name="SpatialRadius">The radius of the spatial search window in pixels. Must be greater than or equal to 1.</param>
/// <param name="RangeRadius">The radius of the CIELAB color range search window. Must be a finite positive number.</param>
/// <param name="MinRegionSize">The minimum pixel count required for a segmented region; smaller regions are pruned and merged into adjacent regions. Set to <c>0</c> (or <c>1</c>) to disable small-region merging. Must be non-negative.</param>
/// <param name="MaxIterations">The maximum number of Mean-Shift mode-seeking iterations per pixel (default 20). Must be greater than or equal to 1.</param>
/// <param name="ConvergenceEpsilon">The convergence distance threshold in joint 5D spatial-range feature space below which iteration stops. Must be a finite positive number.</param>
/// <param name="Connectivity">The grid connectivity used to construct adjacent neighbor edges for mode grouping and region pruning.</param>
public sealed record MeanShiftOptions(
    int SpatialRadius = 8,
    double RangeRadius = 5.5,
    int MinRegionSize = 50,
    int MaxIterations = 20,
    double ConvergenceEpsilon = 0.05,
    GridConnectivity Connectivity = GridConnectivity.EightConnected) : ISizeTunableOptions<MeanShiftOptions>, IProfileTunableOptions<MeanShiftOptions>
{
    private const int MaxMinRegionSizeDivisor = 32;
    private const double MinConvergenceEpsilonFloor = 0.01;
    private const double MaxConvergenceEpsilonCeiling = 0.5;

    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default spatial parameters apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="MeanShiftOptions"/> instance.
    /// </summary>
    public static MeanShiftOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="MeanShiftOptions"/> with <see cref="SpatialRadius"/>, <see cref="MinRegionSize"/>, and <see cref="ConvergenceEpsilon"/> scaled geometrically for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="MeanShiftOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static MeanShiftOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="MeanShiftOptions"/> by scaling <paramref name="baseline"/> geometrically for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline Mean-Shift configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="MeanShiftOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static MeanShiftOptions ForSize(
        Size size,
        MeanShiftOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateSizeTuning(size, baseline, referenceDimension);
        return ScaleCore(size, baseline, referenceDimension, colorSpreadFactor: 1.0, clutterFactor: 1.0);
    }

    /// <summary>
    /// Creates <see cref="MeanShiftOptions"/> tuned for both the dimensions and image statistics of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="MeanShiftOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static MeanShiftOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage<MeanShiftOptions>(image, referenceDimension);

    /// <summary>
    /// Creates <see cref="MeanShiftOptions"/> tuned from <paramref name="baseline"/> for the dimensions and image statistics of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="baseline">The baseline Mean-Shift configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="MeanShiftOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static MeanShiftOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        MeanShiftOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForImage(image, baseline, referenceDimension);

    /// <summary>
    /// Creates <see cref="MeanShiftOptions"/> tuned for both the dimensions and CIELAB statistics of <paramref name="labImage"/>.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="MeanShiftOptions"/> instance tuned for <paramref name="labImage"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="labImage"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static MeanShiftOptions ForImage(
        IReadOnlyMatrix<Lab32> labImage,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForLabImage<MeanShiftOptions>(labImage, referenceDimension);

    /// <summary>
    /// Creates <see cref="MeanShiftOptions"/> tuned from <paramref name="baseline"/> for the dimensions and CIELAB statistics of <paramref name="labImage"/>.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to analyze.</param>
    /// <param name="baseline">The baseline Mean-Shift configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="MeanShiftOptions"/> instance tuned for <paramref name="labImage"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static MeanShiftOptions ForImage(
        IReadOnlyMatrix<Lab32> labImage,
        MeanShiftOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForLabImage(labImage, baseline, referenceDimension);

    /// <summary>
    /// Creates <see cref="MeanShiftOptions"/> tuned from default Mean-Shift parameters using pre-computed <paramref name="analysis"/>.
    /// </summary>
    /// <param name="analysis">The pre-computed image analysis profile.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="MeanShiftOptions"/> instance tuned for <paramref name="analysis"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static MeanShiftOptions ForAnalysis(
        ImageAnalysis analysis,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis<MeanShiftOptions>(analysis, referenceDimension);

    /// <summary>
    /// Creates <see cref="MeanShiftOptions"/> tuned from <paramref name="baseline"/> using pre-computed <paramref name="analysis"/>.
    /// </summary>
    /// <param name="analysis">The pre-computed image analysis profile.</param>
    /// <param name="baseline">The baseline Mean-Shift configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="MeanShiftOptions"/> instance tuned for <paramref name="analysis"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/> or <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/> or <paramref name="referenceDimension"/> is invalid.</exception>
    public static MeanShiftOptions ForAnalysis(
        ImageAnalysis analysis,
        MeanShiftOptions baseline,
        double referenceDimension = DefaultReferenceDimension) =>
        OptionTuning.TuneForAnalysis(analysis, baseline, referenceDimension);

    static MeanShiftOptions IProfileTunableOptions<MeanShiftOptions>.ForProfile(
        in ImageTuningProfile profile,
        MeanShiftOptions baseline,
        double referenceDimension) =>
        ForProfile(in profile, baseline, referenceDimension);

    internal static MeanShiftOptions ForProfile(
        in ImageTuningProfile profile,
        MeanShiftOptions baseline,
        double referenceDimension) =>
        ScaleCore(
            profile.ImageSize,
            baseline,
            referenceDimension,
            profile.IsEmpty ? 1.0 : profile.ColorSpreadFactor,
            profile.IsEmpty ? 1.0 : profile.ClutterFactor);

    private static MeanShiftOptions ScaleCore(
        Size size,
        MeanShiftOptions baseline,
        double referenceDimension,
        double colorSpreadFactor,
        double clutterFactor)
    {
        var (linearScale, areaScale, totalPixels) = OptionTuning.ComputeScalesUnchecked(size, referenceDimension);
        if (totalPixels == 0)
        {
            return baseline;
        }

        double minConvergence = Math.Min(MinConvergenceEpsilonFloor, baseline.ConvergenceEpsilon);
        double maxConvergence = Math.Max(MaxConvergenceEpsilonCeiling, baseline.ConvergenceEpsilon);
        double rangeRadius = OptionTuning.ScaleFinitePositive(baseline.RangeRadius * colorSpreadFactor, double.Epsilon);
        int minRegionSize = baseline.MinRegionSize == 0
            ? 0
            : OptionTuning.ClampRegionSize(baseline.MinRegionSize * areaScale * clutterFactor, totalPixels, MaxMinRegionSizeDivisor);
        double convergenceEpsilon = OptionTuning.ScaleFinitePositive(
            baseline.ConvergenceEpsilon * linearScale,
            minConvergence,
            maxConvergence);

        return baseline with
        {
            SpatialRadius = OptionTuning.RoundPositiveInt(baseline.SpatialRadius * linearScale),
            RangeRadius = rangeRadius,
            MinRegionSize = minRegionSize,
            ConvergenceEpsilon = convergenceEpsilon
        };
    }

    /// <summary>
    /// Validates the options, throwing <see cref="ArgumentOutOfRangeException"/> if any parameter is outside its valid range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any configuration property is invalid.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(SpatialRadius, 1);
        Guard.ThrowIfNotFinitePositive(RangeRadius);
        ArgumentOutOfRangeException.ThrowIfNegative(MinRegionSize);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxIterations, 1);
        Guard.ThrowIfNotFinitePositive(ConvergenceEpsilon);
        Guard.ThrowIfUndefinedEnum(Connectivity);
    }
}
