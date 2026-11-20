namespace Leiter.Algorithms.Stylization;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for Wang et al.'s saliency-guided level-of-detail (LOD) abstraction (Sec. 3.3).
/// </summary>
/// <param name="SalientKernelSize">The fixed kernel size applied within salient regions (default 5).</param>
/// <param name="MinNonSalientKernelSize">The minimum kernel size applied in non-salient regions near salient boundaries (default 4).</param>
/// <param name="MaxNonSalientKernelSize">The maximum kernel size applied in non-salient regions near image borders (default 9).</param>
/// <param name="DistanceKernelScale">The scaling multiplier mapping normalized distance to kernel size (default 10.0).</param>
/// <param name="DistanceKernelBias">The additive bias offset for distance-based kernel sizing (default 0.3).</param>
/// <param name="CrossSegmentColorScale">The color difference threshold scale factor relative to distance d for cross-segment filtering (default 0.3).</param>
/// <param name="SalientOverlapThreshold">The minimum fraction of salient pixels required for a segmented region to be classified as salient (default 0.5).</param>
public sealed record LodAbstractionOptions(
    int SalientKernelSize = 5,
    int MinNonSalientKernelSize = 4,
    int MaxNonSalientKernelSize = 9,
    double DistanceKernelScale = 10.0,
    double DistanceKernelBias = 0.3,
    double CrossSegmentColorScale = 0.3,
    double SalientOverlapThreshold = 0.5) : IValidatableOptions
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default kernel widths apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="LodAbstractionOptions"/> instance.
    /// </summary>
    public static LodAbstractionOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="LodAbstractionOptions"/> with kernel sizes and <see cref="DistanceKernelScale"/> scaled linearly to <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="LodAbstractionOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static LodAbstractionOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        LodAbstractionOptions defaults = Default;
        if (totalPixels == 0)
        {
            return defaults;
        }

        int salientKernel = OptionTuning.RoundPositiveInt(defaults.SalientKernelSize * s);
        int minNonSalient = OptionTuning.RoundPositiveInt(defaults.MinNonSalientKernelSize * s);
        int maxNonSalient = Math.Max(minNonSalient, OptionTuning.RoundPositiveInt(defaults.MaxNonSalientKernelSize * s));

        return defaults with
        {
            SalientKernelSize = salientKernel,
            MinNonSalientKernelSize = minNonSalient,
            MaxNonSalientKernelSize = maxNonSalient,
            DistanceKernelScale = defaults.DistanceKernelScale * s
        };
    }

    /// <summary>
    /// Creates <see cref="LodAbstractionOptions"/> tuned for both the dimensions and color spread of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="LodAbstractionOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static LodAbstractionOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        ImageTuningProfile profile = ImageTuningProfile.Analyze(image);
        return ForProfile(in profile, referenceDimension);
    }

    internal static LodAbstractionOptions ForProfile(in ImageTuningProfile profile, double referenceDimension)
    {
        LodAbstractionOptions scaled = ForSize(profile.ImageSize, referenceDimension);
        if (profile.IsEmpty)
        {
            return scaled;
        }

        return scaled with
        {
            CrossSegmentColorScale = Math.Clamp(scaled.CrossSegmentColorScale * profile.ColorSpreadFactor, 0.15, 0.50)
        };
    }

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is negative, non-finite, or outside its valid range.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(SalientKernelSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MinNonSalientKernelSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxNonSalientKernelSize, MinNonSalientKernelSize);
        Guard.ThrowIfNotFiniteNonNegative(DistanceKernelScale);
        Guard.ThrowIfNotFinite(DistanceKernelBias);
        Guard.ThrowIfNotFiniteNonNegative(CrossSegmentColorScale);
        Guard.ThrowIfNotFiniteInRange(SalientOverlapThreshold, 0.0, 1.0);
    }
}
