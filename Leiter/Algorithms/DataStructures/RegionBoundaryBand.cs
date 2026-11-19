namespace Leiter.Algorithms.DataStructures;

using Leiter.Core;

/// <summary>
/// Represents a multi-source geodesic distance band expanded from inter-region boundaries of a <see cref="RegionGraph"/>.
/// Pixels outside the computed band have a distance of <see cref="int.MaxValue"/> and a nearest-boundary/seed index of <c>-1</c>.
/// </summary>
public sealed class RegionBoundaryBand
{
    internal RegionBoundaryBand(
        Size size,
        int maxRadius,
        SequentialMatrix<int> distances,
        SequentialMatrix<int> nearestBoundaryIndices,
        SequentialMatrix<int> nearestSeedIndices,
        Coord[] seedPixels)
    {
        Size = size;
        MaxRadius = maxRadius;
        Distances = distances;
        NearestBoundaryIndices = nearestBoundaryIndices;
        NearestSeedIndices = nearestSeedIndices;
        SeedPixels = seedPixels;
    }

    /// <summary>
    /// Gets the dimensions of the underlying labeled image.
    /// </summary>
    public Size Size { get; }

    /// <summary>
    /// Gets the width of the band matrices in pixels.
    /// </summary>
    public int Width => Size.Width;

    /// <summary>
    /// Gets the height of the band matrices in pixels.
    /// </summary>
    public int Height => Size.Height;

    /// <summary>
    /// Gets the maximum 8-connected Chebyshev step radius expanded from qualifying boundary seed pixels.
    /// </summary>
    public int MaxRadius { get; }

    /// <summary>
    /// Gets the per-pixel 8-connected step distance to the nearest qualifying boundary pixel,
    /// or <see cref="int.MaxValue"/> for pixels outside the band.
    /// </summary>
    public IReadOnlyMatrix<int> Distances { get; }

    /// <summary>
    /// Gets the per-pixel index into <see cref="RegionGraph.Boundaries"/> of the nearest qualifying boundary,
    /// or <c>-1</c> for pixels outside the band.
    /// </summary>
    public IReadOnlyMatrix<int> NearestBoundaryIndices { get; }

    /// <summary>
    /// Gets the per-pixel row-major linear pixel index of the nearest distance-zero boundary seed pixel,
    /// or <c>-1</c> for pixels outside the band.
    /// </summary>
    public IReadOnlyMatrix<int> NearestSeedIndices { get; }

    /// <summary>
    /// Gets the ordered list of distance-zero boundary seed coordinates in the band.
    /// </summary>
    public ReadOnlyMemory<Coord> SeedPixels { get; }
}
