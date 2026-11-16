namespace Leiter.Core;

/// <summary>
/// Represents a two-dimensional matrix stored as a contiguous, row-major flat array in memory.
/// </summary>
/// <typeparam name="T">The type of elements stored in the matrix.</typeparam>
public class SequentialMatrix<T> : Matrix<T>
    where T : struct
{
    private readonly T[] data;

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialMatrix{T}"/> class with the specified width and height.
    /// </summary>
    /// <param name="width">The number of columns in the matrix.</param>
    /// <param name="height">The number of rows in the matrix.</param>
    public SequentialMatrix(int width, int height)
        : this(width, height, new T[width * height]) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialMatrix{T}"/> class with the specified size dimensions.
    /// </summary>
    /// <param name="size">The dimensions of the matrix.</param>
    public SequentialMatrix(Size size)
        : this(size.Width, size.Height) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialMatrix{T}"/> class from a two-dimensional array indexed by <c>[row, column]</c>.
    /// </summary>
    /// <param name="values">The two-dimensional array indexed by <c>[row, column]</c> to copy elements from.</param>
    public SequentialMatrix(T[,] values)
        : this(values.GetLength(1), values.GetLength(0), values.Cast<T>().ToArray()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialMatrix{T}"/> class with the specified size dimensions and an array of elements.
    /// </summary>
    /// <param name="size">The dimensions of the matrix.</param>
    /// <param name="values">The array of elements to copy into the matrix.</param>
    /// <exception cref="ArgumentException">The length of <paramref name="values"/> does not equal the total number of elements specified by <paramref name="size"/>.</exception>
    public SequentialMatrix(Size size, T[] values)
        : this(size.Width, size.Height, values.ToArray()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialMatrix{T}"/> class, taking direct ownership of the provided data array without copying.
    /// </summary>
    /// <param name="width">The number of columns in the matrix.</param>
    /// <param name="height">The number of rows in the matrix.</param>
    /// <param name="data">The contiguous row-major data array to take direct ownership of.</param>
    /// <exception cref="ArgumentException">The length of <paramref name="data"/> does not match <paramref name="width"/> * <paramref name="height"/>.</exception>
    private protected SequentialMatrix(int width, int height, T[] data)
        : base(width, height)
    {
        if (data.Length != width * height)
            throw new ArgumentException("Data length does not match matrix size.");

        this.data = data;
    }

    /// <summary>
    /// Creates a shallow copy of the underlying contiguous element array.
    /// </summary>
    /// <returns>A new array containing the copied matrix elements.</returns>
    private protected T[] CloneData()
    {
        var clonedData = new T[data.Length];
        Array.Copy(data, clonedData, data.Length);
        return clonedData;
    }

    /// <inheritdoc/>
    public override T GetElement(int index)
    {
        if (index < 0 || index >= data.Length)
            throw new IndexOutOfRangeException($"Index must be between 0 and matrix size of {Count} (exclusive) but was {index}.");

        return data[index];
    }

    /// <inheritdoc/>
    public override T GetElement(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            throw new IndexOutOfRangeException($"Element index must be between {{0, 0}} (inclusive) and {{{Width}, {Height}}} (exclusive) but was {{{x}, {y}}}.");

        return data[x + (y * Width)];
    }

    /// <inheritdoc/>
    public override void SetElement(int index, T value)
    {
        if (index < 0 || index >= data.Length)
            throw new IndexOutOfRangeException($"{index} exceeds Matrix size of {Count}");

        data[index] = value;
    }

    /// <inheritdoc/>
    public override void SetElement(int x, int y, T value)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            throw new IndexOutOfRangeException($"Element index must be between {{0, 0}} (inclusive) and {{{Width}, {Height}}} (exclusive) but was {{{x}, {y}}}.");

        data[x + (y * Width)] = value;
    }

    /// <inheritdoc/>
    public override void SetAll(T value)
    {
        Array.Fill(data, value);
    }

    /// <inheritdoc/>
    public override SequentialMatrix<T> Clone() =>
        new(Width, Height, CloneData());

    /// <inheritdoc/>
    public override IEnumerator<T> GetEnumerator() =>
        data.AsEnumerable().GetEnumerator();

    /// <summary>
    /// Applies a transformation function to each element of the matrix in parallel, producing a new matrix of the mapped type.
    /// </summary>
    /// <typeparam name="R">The type of elements in the resulting matrix.</typeparam>
    /// <param name="func">A function to apply to each element.</param>
    /// <returns>A new <see cref="Matrix{R}"/> containing the mapped elements.</returns>
    public override Matrix<R> Map<R>(Func<T, R> func)
    {
        var mappedData = new R[data.Length];
        Parallel.For(0, data.Length, index => mappedData[index] = func(data[index]));
        return new SequentialMatrix<R>(Width, Height, mappedData);
    }
}
