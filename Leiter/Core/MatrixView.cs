namespace Leiter.Core;

using System.Collections;
using System.Diagnostics;

public class MatrixView<T> : IReadOnlyMatrix<T>
    where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
{
    public MatrixView(IReadOnlyMatrix<T> matrix, int offsetX, int offsetY, int width, int height, EdgeHandling edgeHandling)
    {
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

    public IReadOnlyMatrix<T> Matrix { get; private init; }

    public int OffsetX { get; private init; }

    public int OffsetY { get; private init; }

    public EdgeHandling EdgeHandling { get; private init; }

    public int Height { get; private init; }

    public int Width { get; private init; }

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

    public Coord CoordFromIndex(int index) => IReadOnlyMatrix<T>.CoordFromIndex(this, index);

    public int IndexFromCoord(Coord coord) => IReadOnlyMatrix<T>.IndexFromCoord(this, coord);

    public override string? ToString() => IReadOnlyMatrix<T>.ToString(this);

    public IEnumerator<T> GetEnumerator()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return GetElement(x, y);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}