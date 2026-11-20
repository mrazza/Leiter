namespace Leiter.Algorithms;

using System;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Captures a cached, scale-invariant image analysis profile so multiple algorithm option presets
/// can be tuned for the same image without repeating thumbnail analysis.
/// </summary>
public sealed class ImageAnalysis
{
    private readonly ImageTuningProfile _profile;

    private ImageAnalysis(in ImageTuningProfile profile)
    {
        _profile = profile;
    }

    /// <summary>
    /// Gets the dimensions of the analyzed source image.
    /// </summary>
    public Size Size => _profile.ImageSize;

    /// <summary>
    /// Gets the canonical internal tuning profile extracted from the analyzed image.
    /// </summary>
    internal ImageTuningProfile Profile => _profile;

    /// <summary>
    /// Analyzes an <see cref="Rgb8"/> image once to derive scale-invariant color dispersion and scene clutter statistics.
    /// </summary>
    /// <param name="image">The source sRGB image to analyze.</param>
    /// <returns>An <see cref="ImageAnalysis"/> instance representing <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has negative dimensions.</exception>
    public static ImageAnalysis Analyze(IReadOnlyMatrix<Rgb8> image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Guard.ThrowIfNegative(image.Size, nameof(image));

        return new ImageAnalysis(ImageTuningProfile.AnalyzeUnchecked(image));
    }

    /// <summary>
    /// Analyzes a <see cref="Lab32"/> image once under <paramref name="illuminant"/> to derive scale-invariant color dispersion and scene clutter statistics.
    /// </summary>
    /// <param name="labImage">The source CIELAB image to analyze.</param>
    /// <param name="illuminant">The CIE reference white illuminant in which <paramref name="labImage"/> is expressed (defaults to <see cref="Illuminant.D65"/>).</param>
    /// <returns>An <see cref="ImageAnalysis"/> instance representing <paramref name="labImage"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="labImage"/> has negative dimensions or <paramref name="illuminant"/> is undefined.</exception>
    public static ImageAnalysis Analyze(IReadOnlyMatrix<Lab32> labImage, Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(labImage);
        Guard.ThrowIfNegative(labImage.Size, nameof(labImage));
        Guard.ThrowIfUndefinedEnum(illuminant);

        return new ImageAnalysis(ImageTuningProfile.AnalyzeUnchecked(labImage, illuminant));
    }
}
