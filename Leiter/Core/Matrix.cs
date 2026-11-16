namespace Leiter.Core;

using System.Collections;

/// <summary>
/// Provides an abstract base class for a two-dimensional matrix of elements.
/// </summary>
/// <typeparam name="T">The type of elements stored in the matrix.</typeparam>
public abstract class Matrix<T> : IReadOnlyMatrix<T>
    where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
{
    /// <summary>
    /// Gets the number of columns in the matrix.
    /// </summary>
    public int Width { get; private init; }

    /// <summary>
    /// Gets the number of rows in the matrix.
    /// </summary>
    public int Height { get; private init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Matrix{T}"/> class with the specified width and height.
    /// </summary>
    /// <param name="width">The number of columns in the matrix.</param>
    /// <param name="height">The number of rows in the matrix.</param>
    protected Matrix(int width, int height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Gets or sets the element at the specified linear index.
    /// </summary>
    /// <param name="index">The zero-based linear index of the element.</param>
    /// <returns>The element at the specified linear index.</returns>
    public T this[int index]
    {
        get => GetElement(index);
        set => SetElement(index, value);
    }

    /// <summary>
    /// Gets or sets the element at the specified column and row coordinates.
    /// </summary>
    /// <param name="x">The zero-based column coordinate.</param>
    /// <param name="y">The zero-based row coordinate.</param>
    /// <returns>The element at the specified coordinates.</returns>
    public T this[int x, int y]
    {
        get => GetElement(x, y);
        set => SetElement(x, y, value);
    }

    /// <summary>
    /// Gets or sets the element at the specified coordinate.
    /// </summary>
    /// <param name="coord">The coordinate of the element.</param>
    /// <returns>The element at the specified coordinate.</returns>
    public T this[Coord coord]
    {
        get => GetElement(coord.X, coord.Y);
        set => SetElement(coord.X, coord.Y, value);
    }

    /// <summary>
    /// Gets the total number of elements in the matrix.
    /// </summary>
    public int Count => Width * Height;

    /// <summary>
    /// Gets the dimensions of the matrix as a <see cref="Core.Size"/>.
    /// </summary>
    public Size Size => new(Width, Height);

    /// <summary>
    /// Gets the element at the specified linear index.
    /// </summary>
    /// <param name="index">The zero-based linear index of the element.</param>
    /// <returns>The element at the specified linear index.</returns>
    public abstract T GetElement(int index);

    /// <summary>
    /// Gets the element at the specified column and row coordinates.
    /// </summary>
    /// <param name="x">The zero-based column coordinate.</param>
    /// <param name="y">The zero-based row coordinate.</param>
    /// <returns>The element at the specified coordinates.</returns>
    public abstract T GetElement(int x, int y);

    /// <summary>
    /// Sets the element at the specified linear index.
    /// </summary>
    /// <param name="index">The zero-based linear index of the element.</param>
    /// <param name="value">The value to set.</param>
    public abstract void SetElement(int index, T value);

    /// <summary>
    /// Sets the element at the specified column and row coordinates.
    /// </summary>
    /// <param name="x">The zero-based column coordinate.</param>
    /// <param name="y">The zero-based row coordinate.</param>
    /// <param name="value">The value to set.</param>
    public abstract void SetElement(int x, int y, T value);

    /// <summary>
    /// Sets all elements in the matrix to the specified value.
    /// </summary>
    /// <param name="value">The value to fill the matrix with.</param>
    public abstract void SetAll(T value);

    /// <summary>
    /// Multiplies this matrix by another matrix using matrix multiplication.
    /// </summary>
    /// <param name="right">The matrix to multiply with.</param>
    /// <returns>A new <see cref="Matrix{T}"/> resulting from the multiplication.</returns>
    /// <exception cref="ArgumentException">The number of columns in this matrix does not equal the number of rows in <paramref name="right"/>.</exception>
    public abstract Matrix<T> Multiply(IReadOnlyMatrix<T> right);

    /// <summary>
    /// Applies a transformation function to each element of the matrix, producing a new matrix of the mapped type.
    /// </summary>
    /// <typeparam name="R">The type of elements in the resulting matrix.</typeparam>
    /// <param name="func">A function to apply to each element.</param>
    /// <returns>A new <see cref="Matrix{R}"/> containing the mapped elements.</returns>
    public abstract Matrix<R> Map<R>(Func<T, R> func)
        where R : struct, ISelfOperable<R>, INumericOperable<R>, IScalarOperable<R>;

    /// <summary>
    /// Creates a deep copy of this matrix.
    /// </summary>
    /// <returns>A new <see cref="Matrix{T}"/> that is a copy of this instance.</returns>
    public abstract Matrix<T> Clone();

    /// <summary>
    /// Returns a string that represents the current matrix.
    /// </summary>
    /// <returns>A string representation of the matrix contents.</returns>
    public override string? ToString() => IReadOnlyMatrix<T>.ToString(this);

    /// <summary>
    /// Returns an enumerator that iterates through the matrix elements.
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the matrix elements.</returns>
    public virtual IEnumerator<T> GetEnumerator()
    {
        for (int index = 0; index < Count; index++)
            yield return this[index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}