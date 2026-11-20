namespace Leiter.Core;

using System.Collections;

/// <summary>
/// A zero-allocation, read-only view into a sub-region or border-extended region of an underlying matrix.
/// </summary>
/// <typeparam name="T">The value type stored in the underlying matrix.</typeparam>
public readonly struct MatrixView<T> : IReadOnlyMatrix<T>
    where T : struct
{
    /// <summary>
    /// Initializes a new <see cref="MatrixView{T}"/> over <paramref name="matrix"/>.
    /// </summary>
    /// <param name="matrix">The underlying matrix to wrap.</param>
    /// <param name="offsetX">The starting X offset in <paramref name="matrix"/>.</param>
    /// <param name="offsetY">The starting Y offset in <paramref name="matrix"/>.</param>
    /// <param name="width">The width of the view.</param>
    /// <param name="height">The height of the view.</param>
    /// <param name="edgeHandling">The border handling strategy when coordinates fall outside <paramref name="matrix"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="matrix"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> or <paramref name="height"/> is negative, or <paramref name="edgeHandling"/> is not a defined <see cref="Core.EdgeHandling"/> value.</exception>
    public MatrixView(IReadOnlyMatrix<T> matrix, int offsetX, int offsetY, int width, int height, EdgeHandling edgeHandling)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        Guard.ThrowIfNegativeDimensions(width, height);
        Guard.ThrowIfUndefinedEnum(edgeHandling);
        Matrix = matrix;
        Width = width;
        Height = height;
        OffsetX = offsetX;
        OffsetY = offsetY;
        EdgeHandling = edgeHandling;
    }

    /// <summary>
    /// Gets the element at the specified linear index within the view.
    /// </summary>
    /// <param name="index">The zero-based linear index of the element.</param>
    /// <returns>The element at the specified linear index.</returns>
    public T this[int index] => GetElement(index);

    /// <summary>
    /// Gets the element at the specified column and row coordinates within the view.
    /// </summary>
    /// <param name="x">The zero-based column coordinate.</param>
    /// <param name="y">The zero-based row coordinate.</param>
    /// <returns>The element at the specified coordinates.</returns>
    public T this[int x, int y] => GetElement(x, y);

    /// <summary>
    /// Gets the element at the specified coordinate within the view.
    /// </summary>
    /// <param name="coord">The coordinate of the element.</param>
    /// <returns>The element at the specified coordinate.</returns>
    public T this[Coord coord] => GetElement(coord.X, coord.Y);

    /// <summary>
    /// Gets the underlying matrix referenced by this view.
    /// </summary>
    public IReadOnlyMatrix<T> Matrix { get; }

    /// <summary>
    /// Gets the horizontal starting offset within the underlying matrix.
    /// </summary>
    public int OffsetX { get; }

    /// <summary>
    /// Gets the vertical starting offset within the underlying matrix.
    /// </summary>
    public int OffsetY { get; }

    /// <summary>
    /// Gets the <see cref="Core.EdgeHandling"/> strategy used when coordinates fall outside the underlying matrix bounds.
    /// </summary>
    public EdgeHandling EdgeHandling { get; }

    /// <summary>
    /// Gets the number of rows in the view.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the number of columns in the view.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the dimensions of the view as a <see cref="Core.Size"/>.
    /// </summary>
    public Size Size => new(Width, Height);

    /// <summary>
    /// Gets the total number of elements in the view.
    /// </summary>
    /// <exception cref="OverflowException">Thrown when <c>Width * Height</c> overflows <see cref="int"/>.</exception>
    public int Count => checked(Height * Width);

    /// <summary>
    /// Gets the element at the specified linear index within the view.
    /// </summary>
    /// <param name="index">The zero-based linear index of the element.</param>
    /// <returns>The element at the specified linear index.</returns>
    /// <exception cref="IndexOutOfRangeException"><paramref name="index"/> is negative or greater than or equal to <see cref="Count"/>.</exception>
    public T GetElement(int index)
    {
        if (index < 0 || index >= Count)
            throw new IndexOutOfRangeException($"Index must be between 0 and matrix size of {Count} (exclusive) but was {index}.");

        return GetElement(index % Width, index / Width);
    }

    /// <summary>
    /// Gets the element at the specified column and row coordinates within the view.
    /// </summary>
    /// <param name="x">The zero-based column coordinate.</param>
    /// <param name="y">The zero-based row coordinate.</param>
    /// <returns>The element at the specified coordinates.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The underlying matrix has non-positive dimensions or <see cref="EdgeHandling"/> is unsupported.</exception>
    public T GetElement(int x, int y)
    {
        var underlyingX = x + OffsetX;
        var underlyingY = y + OffsetY;

        return Matrix[
            EdgeHandling.ResolveIndex(underlyingX, Matrix.Width),
            EdgeHandling.ResolveIndex(underlyingY, Matrix.Height)];
    }

    /// <summary>
    /// Returns a string representation of the view.
    /// </summary>
    /// <returns>A string representation of the view contents.</returns>
    public override string? ToString() => IReadOnlyMatrix<T>.ToString(this);

    /// <summary>
    /// Returns an enumerator that iterates through the elements of the view.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the view elements.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return GetElement(x, y);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}