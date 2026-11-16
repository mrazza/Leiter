namespace Leiter.Core;

/// <summary>
/// Represents the two-dimensional size dimensions (width and height) of a matrix or image.
/// </summary>
/// <param name="Width">The horizontal dimension or width in elements/pixels.</param>
/// <param name="Height">The vertical dimension or height in elements/pixels.</param>
public readonly record struct Size(int Width, int Height)
{
    /// <summary>
    /// Determines whether the specified integer coordinates lie within the bounds <c>[0, Width)</c> and <c>[0, Height)</c>.
    /// </summary>
    /// <param name="x">The zero-based horizontal X-coordinate.</param>
    /// <param name="y">The zero-based vertical Y-coordinate.</param>
    /// <returns><see langword="true"/> if the coordinates lie within the bounds; otherwise, <see langword="false"/>.</returns>
    public bool Contains(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>
    /// Determines whether the specified coordinate lies within the bounds <c>[0, Width)</c> and <c>[0, Height)</c>.
    /// </summary>
    /// <param name="coord">The two-dimensional coordinate to test.</param>
    /// <returns><see langword="true"/> if the coordinate lies within the bounds; otherwise, <see langword="false"/>.</returns>
    public bool Contains(Coord coord) => Contains(coord.X, coord.Y);
}
