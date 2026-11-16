namespace Leiter.Tests.TestUtils;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// A reusable, mock implementation of <see cref="IReadOnlyMatrix{T}"/> for double pixels.
/// </summary>
public class DummyReadOnlyMatrix : IReadOnlyMatrix<DoublePixel>
{
    /// <inheritdoc/>
    public int Width => 2;

    /// <inheritdoc/>
    public int Height => 2;

    /// <inheritdoc/>
    public Size Size => new(2, 2);

    /// <inheritdoc/>
    public int Count => 4;

    /// <inheritdoc/>
    public DoublePixel this[int x, int y] => new(x + y);

    /// <inheritdoc/>
    public DoublePixel this[Coord coord] => this[coord.X, coord.Y];

    /// <inheritdoc/>
    public DoublePixel this[int index] => new(index);

    /// <inheritdoc/>
    public DoublePixel GetElement(int index) => new(index);

    /// <inheritdoc/>
    public DoublePixel GetElement(int width, int height) => new(width * height);

    /// <inheritdoc/>
    public IEnumerator<DoublePixel> GetEnumerator() => Enumerable.Range(0, 4).Select(i => new DoublePixel(i)).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
