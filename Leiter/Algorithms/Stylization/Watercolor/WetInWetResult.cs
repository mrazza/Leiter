namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Encapsulates the output of a wet-in-wet edge bleeding filter application.
/// </summary>
public sealed record WetInWetResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WetInWetResult"/> record.
    /// </summary>
    /// <param name="image">The filtered RGB image.</param>
    /// <param name="wetAreaMask">The binary mask indicating pixels within the smoothed wet-in-wet area.</param>
    /// <param name="qualifyingBoundaries">The boolean qualification status for each boundary in the region graph.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="wetAreaMask"/>, or <paramref name="qualifyingBoundaries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="image"/> and <paramref name="wetAreaMask"/> dimensions do not match.</exception>
    public WetInWetResult(
        Matrix<Rgb8> image,
        MaskMatrix wetAreaMask,
        IReadOnlyList<bool> qualifyingBoundaries)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(wetAreaMask);
        ArgumentNullException.ThrowIfNull(qualifyingBoundaries);
        Guard.ThrowIfSizeMismatch(image, wetAreaMask);

        Image = image;
        WetAreaMask = wetAreaMask;
        QualifyingBoundaries = qualifyingBoundaries;
    }

    /// <summary>
    /// Gets the filtered RGB image.
    /// </summary>
    public Matrix<Rgb8> Image { get; }

    /// <summary>
    /// Gets the binary mask indicating pixels within the smoothed wet-in-wet area.
    /// </summary>
    public MaskMatrix WetAreaMask { get; }

    /// <summary>
    /// Gets the boolean qualification status for each boundary in the region graph.
    /// </summary>
    public IReadOnlyList<bool> QualifyingBoundaries { get; }

    /// <summary>
    /// Deconstructs this result into its constituent parts.
    /// </summary>
    /// <param name="image">The filtered RGB image.</param>
    /// <param name="wetAreaMask">The binary mask indicating pixels within the smoothed wet-in-wet area.</param>
    /// <param name="qualifyingBoundaries">The boolean qualification status for each boundary in the region graph.</param>
    public void Deconstruct(
        out Matrix<Rgb8> image,
        out MaskMatrix wetAreaMask,
        out IReadOnlyList<bool> qualifyingBoundaries)
    {
        image = Image;
        wetAreaMask = WetAreaMask;
        qualifyingBoundaries = QualifyingBoundaries;
    }
}
