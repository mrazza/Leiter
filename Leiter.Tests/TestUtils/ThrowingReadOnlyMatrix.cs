namespace Leiter.Tests.TestUtils;

using System;
using System.Collections;
using System.Collections.Generic;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// A test double implementing <see cref="IReadOnlyMatrix{T}"/> for <see cref="Rgb8"/> that throws
/// an <see cref="InvalidOperationException"/> when accessed at the final coordinate or via linear indexers.
/// </summary>
/// <param name="width">The matrix width in pixels.</param>
/// <param name="height">The matrix height in pixels.</param>
public sealed class ThrowingReadOnlyMatrix(int width, int height) : IReadOnlyMatrix<Rgb8>
{
    /// <inheritdoc/>
    public int Width => width;

    /// <inheritdoc/>
    public int Height => height;

    /// <inheritdoc/>
    public Size Size => new(Width, Height);

    /// <inheritdoc/>
    public int Count => Width * Height;

    /// <inheritdoc/>
    public Rgb8 this[int index] => throw new InvalidOperationException("Simulated read failure.");

    /// <inheritdoc/>
    public Rgb8 this[int x, int y] => (x == Width - 1 && y == Height - 1)
        ? throw new InvalidOperationException("Simulated read failure.")
        : new Rgb8(10, 20, 30);

    /// <inheritdoc/>
    public Rgb8 this[Coord coord] => this[coord.X, coord.Y];

    /// <inheritdoc/>
    public Rgb8 GetElement(int index) => this[index];

    /// <inheritdoc/>
    public Rgb8 GetElement(int x, int y) => this[x, y];

    /// <inheritdoc/>
    public IEnumerator<Rgb8> GetEnumerator() { yield break; }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
