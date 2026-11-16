namespace Leiter.Core;

using System.Collections;
using System.Diagnostics;

/// <summary>
/// A zero-allocation, read-only view into a sub-region or border-extended region of an underlying matrix.
/// </summary>
/// <typeparam name="T">The pixel type stored in the underlying matrix.</typeparam>
public readonly struct MatrixView<T> : IReadOnlyMatrix<T>
    where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
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
    public MatrixView(IReadOnlyMatrix<T> matrix, int offsetX, int offsetY, int width, int height, EdgeHandling edgeHandling)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        Matrix = matrix;
        Width = width;
        Height = height;
        OffsetX = offsetX;
        OffsetY = offsetY;
        EdgeHandling = edgeHandling;
    }

    public T this[int index] => GetElement(index);

    public T this[int x, int y] => GetElement(x, y);

    public T this[Coord coord] => GetElement(coord.X, coord.Y);

    public IReadOnlyMatrix<T> Matrix { get; }

    public int OffsetX { get; }

    public int OffsetY { get; }

    public EdgeHandling EdgeHandling { get; }

    public int Height { get; }

    public int Width { get; }

    public Size Size => new(Width, Height);

    public int Count => Height * Width;

    public T GetElement(int index)
    {
        if (index < 0 || index >= Count)
            throw new IndexOutOfRangeException($"Index must be between 0 and matrix size of {Count} (exclusive) but was {index}.");

        return GetElement(index % Width, index / Width);
    }

    public T GetElement(int x, int y)
    {
        var underLyingX = x + OffsetX;
        var underlyingY = y + OffsetY;

        return EdgeHandling switch
        {
            EdgeHandling.EXTEND => Matrix[Math.Clamp(underLyingX, 0, Matrix.Width - 1), Math.Clamp(underlyingY, 0, Matrix.Height - 1)],
            _ => throw new UnreachableException("Code should be unreachable, switch statement exhaustive."),
        };
    }

    public override string? ToString() => IReadOnlyMatrix<T>.ToString(this);

    public IEnumerator<T> GetEnumerator()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return GetElement(x, y);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}