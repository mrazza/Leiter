namespace Leiter.Core;

using System.Threading;
using Leiter.Algorithms.Internal;

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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width"/> or <paramref name="height"/> is negative, or the total element count exceeds <see cref="Array.MaxLength"/>.
    /// </exception>
    public SequentialMatrix(int width, int height)
        : this(width, height, AllocateData(width, height)) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialMatrix{T}"/> class with the specified size dimensions.
    /// </summary>
    /// <param name="size">The dimensions of the matrix.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="size"/> has a negative width or height, or its total element count exceeds <see cref="Array.MaxLength"/>.
    /// </exception>
    public SequentialMatrix(Size size)
        : this(size.Width, size.Height) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialMatrix{T}"/> class from a two-dimensional array indexed by <c>[row, column]</c>.
    /// </summary>
    /// <param name="values">The two-dimensional array indexed by <c>[row, column]</c> to copy elements from.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    public SequentialMatrix(T[,] values)
        : this(
            (values ?? throw new ArgumentNullException(nameof(values))).GetLength(1),
            values.GetLength(0),
            values.Cast<T>().ToArray()) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialMatrix{T}"/> class with the specified size dimensions and an array of elements.
    /// </summary>
    /// <param name="size">The dimensions of the matrix.</param>
    /// <param name="values">The array of elements to copy into the matrix.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="size"/> has a negative width or height, or its total element count exceeds <see cref="Array.MaxLength"/>.
    /// </exception>
    /// <exception cref="ArgumentException">The length of <paramref name="values"/> does not equal the total number of elements specified by <paramref name="size"/>.</exception>
    public SequentialMatrix(Size size, T[] values)
        : this(size.Width, size.Height, CopyInputArray(size, values), nameof(values)) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SequentialMatrix{T}"/> class, taking direct ownership of the provided data array without copying.
    /// </summary>
    /// <param name="width">The number of columns in the matrix.</param>
    /// <param name="height">The number of rows in the matrix.</param>
    /// <param name="data">The contiguous row-major data array to take direct ownership of.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width"/> or <paramref name="height"/> is negative, or the total element count exceeds <see cref="Array.MaxLength"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The length of <paramref name="data"/> does not match <paramref name="width"/> * <paramref name="height"/>.</exception>
    internal SequentialMatrix(int width, int height, T[] data)
        : this(width, height, data, nameof(data)) { }

    private SequentialMatrix(int width, int height, T[] data, string dataParamName)
        : base(width, height)
    {
        ArgumentNullException.ThrowIfNull(data, dataParamName);
        long area = new Size(width, height).Area;
        if (area > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(height), area, $"Matrix element count {area} exceeds the maximum supported array length ({Array.MaxLength}).");
        if (data.Length != area)
            throw new ArgumentException("Data length does not match matrix size.", dataParamName);

        this.data = data;
    }

    private static T[] AllocateData(int width, int height)
    {
        Guard.ThrowIfNegativeDimensions(width, height);
        long area = new Size(width, height).Area;
        if (area > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(height), area, $"Matrix element count {area} exceeds the maximum supported array length ({Array.MaxLength}).");

        return new T[(int)area];
    }

    private static T[] CopyInputArray(Size size, T[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Guard.ThrowIfNegativeDimensions(size.Width, size.Height);
        long area = size.Area;
        if (area > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(size), size, $"Matrix element count {area} exceeds the maximum supported array length ({Array.MaxLength}).");
        if (values.Length != area)
            throw new ArgumentException("Data length does not match matrix size.", nameof(values));

        return values.ToArray();
    }

    internal T[] RawData => data;

    /// <summary>
    /// Returns a read-only span over the underlying contiguous row-major element buffer.
    /// </summary>
    /// <returns>A <see cref="ReadOnlySpan{T}"/> viewing the matrix elements in row-major order.</returns>
    public ReadOnlySpan<T> AsReadOnlySpan() => data;

    /// <summary>
    /// Returns a writable span over the underlying contiguous row-major element buffer.
    /// </summary>
    /// <returns>A <see cref="Span{T}"/> viewing the matrix elements in row-major order.</returns>
    public Span<T> AsSpan() => data;

    /// <summary>
    /// Materializes any <see cref="IReadOnlyMatrix{T}"/> into a standalone <see cref="SequentialMatrix{T}"/>.
    /// </summary>
    /// <param name="source">The source matrix to copy.</param>
    /// <returns>A new <see cref="SequentialMatrix{T}"/> containing the copied elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static SequentialMatrix<T> From(IReadOnlyMatrix<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source.Width, source.Height, CopyElements(source));
    }

    internal static T[] CopyElements(IReadOnlyMatrix<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is SequentialMatrix<T> sequential)
            return sequential.CloneData();

        int width = source.Width;
        int height = source.Height;
        var copied = new T[source.Count];

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
                copied[rowBase + x] = source[x, y];
        }

        return copied;
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
    /// <remarks>
    /// <paramref name="func"/> is invoked concurrently from multiple threads and must be thread-safe.
    /// </remarks>
    /// <typeparam name="R">The type of elements in the resulting matrix.</typeparam>
    /// <param name="func">A function to apply to each element.</param>
    /// <returns>A new <see cref="Matrix{R}"/> containing the mapped elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="func"/> is <see langword="null"/>.</exception>
    public override Matrix<R> Map<R>(Func<T, R> func)
    {
        ArgumentNullException.ThrowIfNull(func);
        var mappedData = new R[data.Length];
        ParallelRunner.ForChunks(
            data.Length,
            CancellationToken.None,
            (start, end) =>
            {
                for (int i = start; i < end; i++)
                    mappedData[i] = func(data[i]);
            });
        return new SequentialMatrix<R>(Width, Height, mappedData);
    }
}
