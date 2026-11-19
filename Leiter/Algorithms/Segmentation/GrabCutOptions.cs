namespace Leiter.Algorithms.Segmentation;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures parameters for <see cref="GrabCut"/> foreground/background segmentation.
/// </summary>
/// <param name="Iterations">The maximum number of alternating GMM-update and graph-cut iterations (<c>≥ 1</c>).</param>
/// <param name="MixtureComponentCount">The number of Gaussian components in each foreground/background GMM (<c>≥ 1</c>).</param>
/// <param name="SmoothnessWeight">The contrast-sensitive pairwise boundary smoothness weight γ (<c>≥ 0</c>).</param>
public sealed record GrabCutOptions(
    int Iterations = 5,
    int MixtureComponentCount = 5,
    double SmoothnessWeight = 50.0)
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default smoothness weights apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="GrabCutOptions"/> instance.
    /// </summary>
    public static GrabCutOptions Default { get; } = new();

    /// <summary>
    /// Creates <see cref="GrabCutOptions"/> with <see cref="SmoothnessWeight"/> scaled linearly to <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="GrabCutOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static GrabCutOptions ForSize(Size size, double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        GrabCutOptions defaults = Default;
        if (totalPixels == 0)
        {
            return defaults;
        }

        return defaults with
        {
            SmoothnessWeight = defaults.SmoothnessWeight * s
        };
    }

    /// <summary>
    /// Creates <see cref="GrabCutOptions"/> tuned for the dimensions and color complexity of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The input RGB image to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="GrabCutOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static GrabCutOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        ImageTuningProfile profile = ImageTuningProfile.Analyze(image);
        return ForProfile(in profile, referenceDimension);
    }

    internal static GrabCutOptions ForProfile(in ImageTuningProfile profile, double referenceDimension)
    {
        GrabCutOptions scaled = ForSize(profile.ImageSize, referenceDimension);
        if (profile.IsEmpty)
        {
            return scaled;
        }

        int components = profile.LabDispersion < 14.0 ? 4 : profile.LabDispersion > 30.0 ? 6 : 5;
        double smoothness = scaled.SmoothnessWeight / Math.Sqrt(Math.Clamp(profile.ColorSpreadFactor, 0.80, 1.25));

        return scaled with
        {
            MixtureComponentCount = components,
            SmoothnessWeight = smoothness
        };
    }

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is outside its valid range.</exception>
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Iterations, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MixtureComponentCount, 1);
        Guard.ThrowIfNotFiniteNonNegative(SmoothnessWeight);
    }
}
