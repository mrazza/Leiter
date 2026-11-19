namespace Leiter.Algorithms.Saliency;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Morphology;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for Cheng et al.'s iterative <see cref="SaliencyCut"/> salient object segmentation.
/// </summary>
/// <param name="InitialThreshold">Initial fixed threshold in [0, 1] used on the saliency map (default <c>70 / 255</c> per Cheng et al. Sec. 5.1).</param>
/// <param name="MaxOuterIterations">Maximum number of trimap dilation/erosion refinement iterations (<c>≥ 1</c>).</param>
/// <param name="InnerGrabCutIterations">Number of GMM/min-cut iterations executed per outer trimap step (<c>≥ 1</c>).</param>
/// <param name="TrimapBandRadius">Morphological dilation/erosion radius in pixels used to construct the unknown boundary band (<c>≥ 1</c>).</param>
/// <param name="MixtureComponentCount">Number of Gaussian components per foreground/background GMM (<c>≥ 1</c>).</param>
/// <param name="SmoothnessWeight">Contrast-sensitive pairwise boundary smoothness weight γ (<c>≥ 0</c>).</param>
/// <param name="RetentionPolicy">Connected-component cleanup policy applied to initial and refined masks.</param>
/// <param name="MinAreaFraction">Minimum component area fraction in [0, 1] when <paramref name="RetentionPolicy"/> is <see cref="ComponentRetentionPolicy.MinimumAreaFraction"/>.</param>
/// <param name="MaxWorkingDimension">Optional maximum width or height for working resolution; <see langword="null"/> runs at full native resolution.</param>
public sealed record SaliencyCutOptions(
    double InitialThreshold = 70.0 / 255.0,
    int MaxOuterIterations = 4,
    int InnerGrabCutIterations = 2,
    int TrimapBandRadius = 10,
    int MixtureComponentCount = 5,
    double SmoothnessWeight = 50.0,
    ComponentRetentionPolicy RetentionPolicy = ComponentRetentionPolicy.MinimumAreaFraction,
    double MinAreaFraction = 0.01,
    int? MaxWorkingDimension = null)
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default band radii and smoothness weights apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Creates <see cref="SaliencyCutOptions"/> with trimap band radius and pairwise smoothness weight scaled linearly for full-resolution execution on <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="SaliencyCutOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static SaliencyCutOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        var defaults = new SaliencyCutOptions();
        if (totalPixels == 0)
        {
            return defaults;
        }

        int maxBand = Math.Max(1, Math.Min(size.Width, size.Height) / 3);
        int bandRadius = Math.Clamp(OptionTuning.RoundPositiveInt(defaults.TrimapBandRadius * s), 1, maxBand);

        return defaults with
        {
            TrimapBandRadius = bandRadius,
            SmoothnessWeight = defaults.SmoothnessWeight * s,
            MaxWorkingDimension = null
        };
    }

    /// <summary>
    /// Creates <see cref="SaliencyCutOptions"/> tuned for the dimensions and color complexity of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="SaliencyCutOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static SaliencyCutOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        ImageTuningProfile profile = ImageTuningProfile.Analyze(image);
        return ForProfile(in profile, referenceDimension);
    }

    internal static SaliencyCutOptions ForProfile(in ImageTuningProfile profile, double referenceDimension)
    {
        SaliencyCutOptions scaled = ForSize(profile.ImageSize, referenceDimension);
        if (profile.IsEmpty)
        {
            return scaled;
        }

        int components = profile.LabDispersion < 14.0 ? 4 : profile.LabDispersion > 30.0 ? 6 : 5;
        return scaled with
        {
            MixtureComponentCount = components
        };
    }

    internal void Validate()
    {
        if (!double.IsFinite(InitialThreshold) || InitialThreshold < 0.0 || InitialThreshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(InitialThreshold), InitialThreshold, "InitialThreshold must be finite and within [0, 1].");
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxOuterIterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(InnerGrabCutIterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(TrimapBandRadius, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MixtureComponentCount, 1);
        if (!double.IsFinite(SmoothnessWeight) || SmoothnessWeight < 0.0)
            throw new ArgumentOutOfRangeException(nameof(SmoothnessWeight), SmoothnessWeight, "SmoothnessWeight must be finite and non-negative.");
        if (!Enum.IsDefined(RetentionPolicy))
            throw new ArgumentOutOfRangeException(nameof(RetentionPolicy), RetentionPolicy, "Unsupported component retention policy.");
        if (!double.IsFinite(MinAreaFraction) || MinAreaFraction < 0.0 || MinAreaFraction > 1.0)
            throw new ArgumentOutOfRangeException(nameof(MinAreaFraction), MinAreaFraction, "MinAreaFraction must be finite and within [0, 1].");
        if (MaxWorkingDimension is int maxDim)
            ArgumentOutOfRangeException.ThrowIfLessThan(maxDim, 2, nameof(MaxWorkingDimension));
    }
}
