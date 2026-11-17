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
    GridConnectivity Connectivity = GridConnectivity.EightConnected)
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which baseline parameters apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Creates <see cref="EgbiOptions"/> scaled geometrically for <paramref name="size"/> from default EGBI parameters.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EgbiOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static EgbiOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, new EgbiOptions(), referenceDimension);

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
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();

        var (s, a, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        if (totalPixels == 0)
        {
            return baseline;
        }

        double g = OptionTuning.ComputeBlurredEdgeAttenuation(s);
        double kFactor = baseline.KFactor * a * g;
        double epsilon = baseline.Epsilon == 0.0 ? 0.0 : Math.Max(0.15, baseline.Epsilon * g);
        int minRegionSize = baseline.MinRegionSize == 0
            ? 0
            : OptionTuning.ClampRegionSize(baseline.MinRegionSize * a, totalPixels, maxFractionDivisor: 16);

        return baseline with
        {
            KFactor = kFactor,
            Epsilon = epsilon,
            MinRegionSize = minRegionSize
        };
    }

    /// <summary>
    /// Creates <see cref="EgbiOptions"/> tuned for both the dimensions and image statistics of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EgbiOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static EgbiOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension) =>
        ForImage(image, new EgbiOptions(), referenceDimension);

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
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();

        ImageTuningProfile profile = ImageTuningProfile.Analyze(image);
        return ForProfile(in profile, baseline, referenceDimension);
    }

    /// <summary>
    /// Creates <see cref="EgbiOptions"/> tuned for both the dimensions and CIELAB statistics of <paramref name="labImage"/>.
    /// </summary>
    /// <param name="labImage">The input CIELAB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="EgbiOptions"/> instance tuned for <paramref name="labImage"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static EgbiOptions ForImage(
        IReadOnlyMatrix<Lab32> labImage,
        double referenceDimension = DefaultReferenceDimension) =>
        ForImage(labImage, new EgbiOptions(), referenceDimension);

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
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(labImage, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();

        ImageTuningProfile profile = ImageTuningProfile.Analyze(labImage);
        return ForProfile(in profile, baseline, referenceDimension);
    }

    internal static EgbiOptions ForProfile(
        in ImageTuningProfile profile,
        EgbiOptions baseline,
        double referenceDimension)
    {
        EgbiOptions scaled = ForSize(profile.ImageSize, baseline, referenceDimension);
        if (profile.IsEmpty)
        {
            return scaled;
        }

        long totalPixels = (long)profile.ImageSize.Width * profile.ImageSize.Height;
        int minRegionSize = scaled.MinRegionSize == 0
            ? 0
            : OptionTuning.ClampRegionSize(scaled.MinRegionSize * profile.ClutterFactor, totalPixels, maxFractionDivisor: 16);

        return scaled with
        {
            KFactor = scaled.KFactor * profile.ColorSpreadFactor,
            MinRegionSize = minRegionSize
        };
    }

    /// <summary>
    /// Validates the options, throwing <see cref="ArgumentOutOfRangeException"/> if any parameter is outside its valid range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any configuration property is invalid.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(KFactor) || KFactor < 0.0)
            throw new ArgumentOutOfRangeException(nameof(KFactor), KFactor, "KFactor must be a finite non-negative number.");

        if (!double.IsFinite(Epsilon) || Epsilon < 0.0)
            throw new ArgumentOutOfRangeException(nameof(Epsilon), Epsilon, "Epsilon must be a finite non-negative number.");

        ArgumentOutOfRangeException.ThrowIfNegative(MinRegionSize);

        if (!Enum.IsDefined(Connectivity))
            throw new ArgumentOutOfRangeException(nameof(Connectivity), Connectivity, "Connectivity must be a defined GridConnectivity value.");
    }
}
