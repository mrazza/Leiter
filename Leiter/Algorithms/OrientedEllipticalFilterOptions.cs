namespace Leiter.Algorithms;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Core;

/// <summary>
/// Configures parameters for the oriented elliptical Gaussian smoothing filter.
/// </summary>
/// <param name="MajorRadius">The semi-major radius along the local tangent orientation (default 6.0; must be finite and greater than 0.0).</param>
/// <param name="MinorRadius">The semi-minor radius perpendicular to the local tangent orientation (default 1.5; must be finite and greater than 0.0).</param>
/// <param name="SigmaScale">The Gaussian standard deviation scaling factor relative to the radii (default 0.5; must be finite and greater than 0.0).</param>
public sealed record OrientedEllipticalFilterOptions(
    double MajorRadius = 6.0,
    double MinorRadius = 1.5,
    double SigmaScale = 0.5)
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which default radii apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Creates <see cref="OrientedEllipticalFilterOptions"/> with semi-axes scaled linearly to <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="OrientedEllipticalFilterOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static OrientedEllipticalFilterOptions ForSize(
        Size size,
        double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        var defaults = new OrientedEllipticalFilterOptions();
        if (totalPixels == 0)
        {
            return defaults;
        }

        return defaults with
        {
            MajorRadius = Math.Max(0.25, defaults.MajorRadius * s),
            MinorRadius = Math.Max(0.25, defaults.MinorRadius * s)
        };
    }

    /// <summary>
    /// Creates <see cref="OrientedEllipticalFilterOptions"/> scaled for the dimensions of <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <param name="image">The target image matrix whose dimensions guide scaling.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="OrientedEllipticalFilterOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static OrientedEllipticalFilterOptions ForImage<T>(
        IReadOnlyMatrix<T> image,
        double referenceDimension = DefaultReferenceDimension)
        where T : struct
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        return ForSize(image.Size, referenceDimension);
    }

    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is non-finite or less than or equal to 0.0.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(MajorRadius) || MajorRadius <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(MajorRadius), MajorRadius, "MajorRadius must be finite and greater than 0.0.");

        if (!double.IsFinite(MinorRadius) || MinorRadius <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(MinorRadius), MinorRadius, "MinorRadius must be finite and greater than 0.0.");

        if (!double.IsFinite(SigmaScale) || SigmaScale <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(SigmaScale), SigmaScale, "SigmaScale must be finite and greater than 0.0.");
    }
}
