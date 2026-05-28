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
    /// <summary>
    /// Gets the width property.
    /// </summary>
    public int Width => 2;
    /// <summary>
    /// Gets the height property.
    /// </summary>
    public int Height => 2;
    /// <summary>
    /// Gets the size property.
    /// </summary>
    public Size Size => new(2, 2);
    /// <summary>
    /// Gets the count property.
    /// </summary>
    public int Count => 4;

    public DoublePixel this[int x, int y] => new(x + y);
    public DoublePixel this[Coord coord] => this[coord.X, coord.Y];
    public DoublePixel this[int index] => new(index);

    /// <summary>
    /// Executes the get element operation.
    /// </summary>
    public DoublePixel GetElement(int index) => new(index);
    /// <summary>
    /// Executes the get element operation.
    /// </summary>
    public DoublePixel GetElement(int width, int height) => new(width * height);

    /// <summary>
    /// Executes the get enumerator operation.
    /// </summary>
    public IEnumerator<DoublePixel> GetEnumerator() => Enumerable.Range(0, 4).Select(i => new DoublePixel(i)).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
