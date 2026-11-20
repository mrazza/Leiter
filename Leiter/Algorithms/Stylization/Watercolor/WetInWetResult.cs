namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Encapsulates the output of a wet-in-wet edge bleeding filter application.
/// </summary>
public sealed record WetInWetResult
{
    private readonly bool[] qualifyingBoundaries;

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
        Guard.ThrowIfSizeMismatch(image, wetAreaMask);
        ArgumentNullException.ThrowIfNull(qualifyingBoundaries);

        Image = image;
        WetAreaMask = wetAreaMask;
        this.qualifyingBoundaries = qualifyingBoundaries.Count == 0 ? [] : [.. qualifyingBoundaries];
        QualifyingBoundaries = this.qualifyingBoundaries.Length == 0
            ? ReadOnlyCollection<bool>.Empty
            : Array.AsReadOnly(this.qualifyingBoundaries);
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

    /// <summary>
    /// Determines whether another <see cref="WetInWetResult"/> equals this instance by comparing image, mask, and qualifying boundary flags.
    /// </summary>
    /// <param name="other">The other <see cref="WetInWetResult"/> to compare.</param>
    /// <returns><see langword="true"/> if both instances have matching artifacts and boundary flags; otherwise <see langword="false"/>.</returns>
    public bool Equals(WetInWetResult? other) =>
        other is not null &&
        EqualityComparer<Matrix<Rgb8>>.Default.Equals(Image, other.Image) &&
        EqualityComparer<MaskMatrix>.Default.Equals(WetAreaMask, other.WetAreaMask) &&
        qualifyingBoundaries.AsSpan().SequenceEqual(other.qualifyingBoundaries);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Image);
        hash.Add(WetAreaMask);
        for (int i = 0; i < qualifyingBoundaries.Length; i++)
        {
            hash.Add(qualifyingBoundaries[i]);
        }

        return hash.ToHashCode();
    }

    internal void ValidateAgainst(RegionGraph regionGraph, string paramName)
    {
        ArgumentNullException.ThrowIfNull(regionGraph);
        Guard.ThrowIfSizeMismatch(regionGraph.Size, Image.Size, paramName);

        if (qualifyingBoundaries.Length != regionGraph.Boundaries.Count)
        {
            throw new ArgumentException(
                $"Qualifying boundary count ({qualifyingBoundaries.Length}) must match region graph boundary count ({regionGraph.Boundaries.Count}).",
                paramName);
        }
    }
}
