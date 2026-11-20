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
/// <param name="MinRegionSize">The minimum number of pixels required for a segmented region. Smaller regions are pruned and merged into adjacent regions. Must be greater than or equal to 1.</param>
/// <param name="MaxIterations">The maximum number of Mean-Shift mode-seeking iterations per pixel (default 20). Must be greater than or equal to 1.</param>
/// <param name="ConvergenceEpsilon">The convergence distance threshold in joint 5D spatial-range feature space below which iteration stops. Must be a finite positive number.</param>
/// <param name="Connectivity">The grid connectivity used to construct adjacent neighbor edges for mode grouping and region pruning.</param>
public sealed record MeanShiftOptions(
    int SpatialRadius = 8,
    double RangeRadius = 5.5,
    int MinRegionSize = 50,
    int MaxIterations = 20,
    double ConvergenceEpsilon = 0.05,
    GridConnectivity Connectivity = GridConnectivity.EightConnected) : IValidatableOptions
{
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
    public static MeanShiftOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension)
    {
        var (s, a, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        MeanShiftOptions defaults = Default;
        if (totalPixels == 0)
        {
            return defaults;
        }

        return defaults with
        {
            SpatialRadius = OptionTuning.RoundPositiveInt(defaults.SpatialRadius * s),
            MinRegionSize = OptionTuning.ClampRegionSize(defaults.MinRegionSize * a, totalPixels, maxFractionDivisor: 32),
            ConvergenceEpsilon = Math.Clamp(defaults.ConvergenceEpsilon * s, defaults.ConvergenceEpsilon, 0.5)
        };
    }

    /// <summary>
    /// Creates <see cref="MeanShiftOptions"/> tuned for both the dimensions and image statistics of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="MeanShiftOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static MeanShiftOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        ImageTuningProfile profile = ImageTuningProfile.Analyze(image);
        return ForProfile(in profile, referenceDimension);
    }

    /// <summary>
    /// Creates <see cref="MeanShiftOptions"/> tuned for both the dimensions and CIELAB statistics of <paramref name="labImage"/>.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="MeanShiftOptions"/> instance tuned for <paramref name="labImage"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static MeanShiftOptions ForImage(
        IReadOnlyMatrix<Lab32> labImage,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(labImage, referenceDimension);
        ImageTuningProfile profile = ImageTuningProfile.Analyze(labImage);
        return ForProfile(in profile, referenceDimension);
    }

    internal static MeanShiftOptions ForProfile(in ImageTuningProfile profile, double referenceDimension)
    {
        MeanShiftOptions scaled = ForSize(profile.ImageSize, referenceDimension);
        if (profile.IsEmpty)
        {
            return scaled;
        }

        long totalPixels = (long)profile.ImageSize.Width * profile.ImageSize.Height;
        int minRegionSize = OptionTuning.ClampRegionSize(scaled.MinRegionSize * profile.ClutterFactor, totalPixels, maxFractionDivisor: 32);

        return scaled with
        {
            RangeRadius = scaled.RangeRadius * profile.ColorSpreadFactor,
            MinRegionSize = minRegionSize
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
        ArgumentOutOfRangeException.ThrowIfLessThan(MinRegionSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxIterations, 1);
        Guard.ThrowIfNotFinitePositive(ConvergenceEpsilon);
        Guard.ThrowIfUndefinedEnum(Connectivity);
    }
}
