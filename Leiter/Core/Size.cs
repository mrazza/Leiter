namespace Leiter.Core;

/// <summary>
/// Represents the two-dimensional size dimensions (width and height) of a matrix or image.
/// </summary>
/// <param name="Width">The horizontal dimension or width in elements/pixels.</param>
/// <param name="Height">The vertical dimension or height in elements/pixels.</param>
public readonly record struct Size(int Width, int Height)
{
    /// <summary>
    /// Gets the total number of elements (<c>Width * Height</c>) in a matrix of this size.
    /// </summary>
    public int Count => Width * Height;

    /// <summary>
    /// Gets the total element area (<c>(long)Width * Height</c>) without 32-bit overflow.
    /// </summary>
    public long Area => (long)Width * Height;

    /// <summary>
    /// Gets the geometric-mean linear dimension (<c>sqrt(Width * Height)</c>) of this size.
    /// </summary>
    public double LinearDimension => Width <= 0 || Height <= 0 ? 0.0 : Math.Sqrt((double)Width * Height);

    /// <summary>
    /// Gets a value indicating whether either dimension is less than or equal to zero.
    /// </summary>
    public bool IsEmpty => Width <= 0 || Height <= 0;

    /// <summary>
    /// Computes the row-major linear index corresponding to the specified coordinates.
    /// </summary>
    /// <param name="x">The zero-based horizontal X-coordinate.</param>
    /// <param name="y">The zero-based vertical Y-coordinate.</param>
    /// <returns>The zero-based row-major linear index.</returns>
    public int IndexOf(int x, int y) => (y * Width) + x;

    /// <summary>
    /// Computes the row-major linear index corresponding to <paramref name="coord"/>.
    /// </summary>
    /// <param name="coord">The two-dimensional coordinate.</param>
    /// <returns>The zero-based row-major linear index.</returns>
    public int IndexOf(Coord coord) => (coord.Y * Width) + coord.X;

    /// <summary>
    /// Computes the two-dimensional coordinate corresponding to a row-major linear index.
    /// </summary>
    /// <param name="index">The zero-based row-major linear index.</param>
    /// <returns>The <see cref="Coord"/> corresponding to <paramref name="index"/>.</returns>
    public Coord CoordOf(int index) => new(index % Width, index / Width);

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
