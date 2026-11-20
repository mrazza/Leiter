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
    /// <exception cref="OverflowException">Thrown when <c>Width * Height</c> overflows <see cref="int"/>.</exception>
    public int Count => checked(Width * Height);

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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="Width"/> is less than or equal to zero.</exception>
    public Coord CoordOf(int index)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Width, 0);
        return new(index % Width, index / Width);
    }

    /// <summary>
    /// Clamps a square window centered at (<paramref name="centerX"/>, <paramref name="centerY"/>) with half-extent <paramref name="radius"/>
    /// to the inclusive coordinate bounds <c>[0, Width - 1]</c> and <c>[0, Height - 1]</c>.
    /// </summary>
    /// <param name="centerX">The window center X-coordinate.</param>
    /// <param name="centerY">The window center Y-coordinate.</param>
    /// <param name="radius">The non-negative window radius in elements.</param>
    /// <returns>The inclusive clamped window bounds <c>(MinX, MaxX, MinY, MaxY)</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="radius"/> is negative, or when <see cref="Width"/> or <see cref="Height"/> is less than or equal to zero.
    /// </exception>
    public (int MinX, int MaxX, int MinY, int MaxY) ClampWindow(int centerX, int centerY, int radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(Height, 0);

        int minX = (int)Math.Clamp((long)centerX - radius, 0L, Width - 1);
        int maxX = (int)Math.Clamp((long)centerX + radius, 0L, Width - 1);
        int minY = (int)Math.Clamp((long)centerY - radius, 0L, Height - 1);
        int maxY = (int)Math.Clamp((long)centerY + radius, 0L, Height - 1);
        return (minX, maxX, minY, maxY);
    }

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
