namespace Leiter.Tests.TestUtils;

using System;
using System.Collections;
using System.Collections.Generic;
using Leiter.Core;

/// <summary>
/// A reusable non-<see cref="Matrix{T}"/> implementation of <see cref="IReadOnlyMatrix{T}"/> for testing read-only matrix paths.
/// </summary>
/// <typeparam name="T">The unmanaged element type stored in the matrix.</typeparam>
public sealed class DummyReadOnlyMatrix<T> : IReadOnlyMatrix<T>
    where T : struct
{
    private readonly T[] data;

    /// <summary>
    /// Initializes a new instance filled with a uniform value.
    /// </summary>
    /// <param name="width">The matrix width in elements.</param>
    /// <param name="height">The matrix height in elements.</param>
    /// <param name="fillValue">The constant value stored at every coordinate.</param>
    public DummyReadOnlyMatrix(int width, int height, T fillValue = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);

        Width = width;
        Height = height;
        data = new T[width * height];
        Array.Fill(data, fillValue);
    }

    /// <summary>
    /// Initializes a new instance populated from a coordinate generator delegate.
    /// </summary>
    /// <param name="width">The matrix width in elements.</param>
    /// <param name="height">The matrix height in elements.</param>
    /// <param name="generator">Delegate invoked as <c>(x, y) =&gt; value</c>.</param>
    public DummyReadOnlyMatrix(int width, int height, Func<int, int, T> generator)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        ArgumentNullException.ThrowIfNull(generator);

        Width = width;
        Height = height;
        data = new T[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                data[(y * width) + x] = generator(x, y);
            }
        }
    }

    /// <summary>
    /// Initializes a new instance by copying elements from an existing read-only matrix.
    /// </summary>
    /// <param name="source">The source matrix to copy.</param>
    public DummyReadOnlyMatrix(IReadOnlyMatrix<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Width = source.Width;
        Height = source.Height;
        data = new T[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                data[(y * Width) + x] = source[x, y];
            }
        }
    }

    /// <inheritdoc/>
    public int Width { get; }

    /// <inheritdoc/>
    public int Height { get; }

    /// <inheritdoc/>
    public Size Size => new(Width, Height);

    /// <inheritdoc/>
    public int Count => data.Length;

    /// <inheritdoc/>
    public T this[int x, int y] => data[(y * Width) + x];

    /// <inheritdoc/>
    public T this[Coord coord] => this[coord.X, coord.Y];

    /// <inheritdoc/>
    public T this[int index] => data[index];

    /// <inheritdoc/>
    public T GetElement(int index) => data[index];

    /// <inheritdoc/>
    public T GetElement(int width, int height) => this[width, height];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)data).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
