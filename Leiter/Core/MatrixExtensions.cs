namespace Leiter.Core;

using System.Threading;
using Leiter.Algorithms.Internal;

/// <summary>
/// Provides extension methods for matrices and sequences converting to or indexing into matrices.
/// </summary>
public static class MatrixExtensions
{
    /// <summary>
    /// Creates a new <see cref="SequentialMatrix{T}"/> of the specified dimensions populated from an enumerable sequence.
    /// </summary>
    /// <typeparam name="T">The type of elements in the matrix.</typeparam>
    /// <param name="values">The sequence of elements to populate the matrix with.</param>
    /// <param name="width">The number of columns in the resulting matrix.</param>
    /// <param name="height">The number of rows in the resulting matrix.</param>
    /// <returns>A new <see cref="SequentialMatrix{T}"/> containing the elements from <paramref name="values"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="values"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The sequence in <paramref name="values"/> contains fewer elements than <paramref name="width"/> * <paramref name="height"/>.</exception>
    public static SequentialMatrix<T> ToSequentialMatrix<T>(this IEnumerable<T> values, int width, int height)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(values);

        var result = new SequentialMatrix<T>(width, height);
        using var enumerator = values.GetEnumerator();

        for (int index = 0; index < result.Count; index++)
        {
            if (!enumerator.MoveNext())
            {
                throw new ArgumentException($"IEnumerable of type {values.GetType()} is smaller than matrix of size {{{width}, {height}}}.", nameof(values));
            }
            result[index] = enumerator.Current;
        }

        return result;
    }

    /// <summary>
    /// Creates an independent <see cref="Matrix{T}"/> copy of a read-only matrix, preserving concrete <see cref="Matrix{T}"/> subtypes when possible.
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="matrix">The source matrix to copy.</param>
    /// <returns>
    /// The result of <see cref="Matrix{T}.Clone"/> when <paramref name="matrix"/> is a <see cref="Matrix{T}"/>;
    /// otherwise, a materialized <see cref="SequentialMatrix{T}"/> containing the elements of <paramref name="matrix"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <see langword="null"/>.</exception>
    public static Matrix<T> Clone<T>(this IReadOnlyMatrix<T> matrix)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return matrix is Matrix<T> concreteMatrix
            ? concreteMatrix.Clone()
            : SequentialMatrix<T>.From(matrix);
    }

    /// <summary>
    /// Materializes a read-only matrix into a standalone, contiguous row-major <see cref="SequentialMatrix{T}"/> (for example, when contiguous span or array access is needed).
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to copy.</param>
    /// <returns>A new <see cref="SequentialMatrix{T}"/> containing the elements of <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static SequentialMatrix<T> ToSequentialMatrix<T>(this IReadOnlyMatrix<T> source)
        where T : struct =>
        SequentialMatrix<T>.From(source);

    /// <summary>
    /// Attempts to obtain a zero-copy read-only span over the matrix's contiguous row-major storage.
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="matrix">The source matrix to inspect.</param>
    /// <param name="span">When this method returns <see langword="true"/>, receives a contiguous row-major <see cref="ReadOnlySpan{T}"/>; otherwise, <see langword="default"/>.</param>
    /// <returns><see langword="true"/> if <paramref name="matrix"/> exposes contiguous row-major storage; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <see langword="null"/>.</exception>
    public static bool TryGetReadOnlySpan<T>(this IReadOnlyMatrix<T> matrix, out ReadOnlySpan<T> span)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (matrix is SequentialMatrix<T> sequential)
        {
            span = sequential.AsReadOnlySpan();
            return true;
        }

        span = default;
        return false;
    }

    /// <summary>
    /// Copies the elements of a read-only matrix into a flat row-major array.
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to copy.</param>
    /// <returns>A new row-major array containing the elements of <paramref name="source"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static T[] ToArray<T>(this IReadOnlyMatrix<T> source)
        where T : struct =>
        SequentialMatrix<T>.CopyElements(source);

    /// <summary>
    /// Applies a transformation function to each element of a read-only matrix in parallel, returning a flat row-major array.
    /// </summary>
    /// <remarks>
    /// <paramref name="selector"/> is invoked concurrently from multiple threads and must be thread-safe.
    /// </remarks>
    /// <typeparam name="T">The element type of the source matrix.</typeparam>
    /// <typeparam name="R">The element type of the resulting array.</typeparam>
    /// <param name="matrix">The source matrix to transform.</param>
    /// <param name="selector">The transformation function applied to each element.</param>
    /// <returns>A row-major array containing the mapped elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> or <paramref name="selector"/> is <see langword="null"/>.</exception>
    public static R[] MapToArray<T, R>(this IReadOnlyMatrix<T> matrix, Func<T, R> selector)
        where T : struct
        where R : struct
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(selector);

        int count = matrix.Count;
        if (count == 0)
            return [];

        var mappedData = new R[count];
        if (matrix is SequentialMatrix<T> sequential)
        {
            T[] raw = sequential.RawData;
            ParallelRunner.ForChunks(
                raw.Length,
                CancellationToken.None,
                (start, end) =>
                {
                    for (int index = start; index < end; index++)
                        mappedData[index] = selector(raw[index]);
                });
            return mappedData;
        }

        int width = matrix.Width;
        int height = matrix.Height;
        int minRowParallel = count >= ParallelRunner.DefaultMinParallelCount && height >= 4 ? 2 : height + 1;
        ParallelRunner.ForChunks(
            height,
            CancellationToken.None,
            (startRow, endRow) =>
            {
                for (int y = startRow; y < endRow; y++)
                {
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                        mappedData[rowBase + x] = selector(matrix[x, y]);
                }
            },
            minParallelCount: minRowParallel);

        return mappedData;
    }

    /// <summary>
    /// Applies a transformation function to each element of a read-only matrix, returning a new matrix of the mapped type.
    /// </summary>
    /// <remarks>
    /// <paramref name="selector"/> is invoked concurrently from multiple threads and must be thread-safe.
    /// </remarks>
    /// <typeparam name="T">The element type of the source matrix.</typeparam>
    /// <typeparam name="R">The element type of the resulting matrix.</typeparam>
    /// <param name="matrix">The source matrix to transform.</param>
    /// <param name="selector">The transformation function applied to each element.</param>
    /// <returns>A new <see cref="Matrix{R}"/> containing the mapped elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> or <paramref name="selector"/> is <see langword="null"/>.</exception>
    public static Matrix<R> Map<T, R>(this IReadOnlyMatrix<T> matrix, Func<T, R> selector)
        where T : struct
        where R : struct
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(selector);

        if (matrix is Matrix<T> concreteMatrix)
            return concreteMatrix.Map(selector);

        return new SequentialMatrix<R>(matrix.Width, matrix.Height, matrix.MapToArray(selector));
    }

    /// <summary>
    /// Applies a coordinate-aware transformation function to each element of a read-only matrix in parallel,
    /// returning a new matrix of the mapped type.
    /// </summary>
    /// <remarks>
    /// <paramref name="selector"/> is invoked concurrently from multiple threads and must be thread-safe.
    /// </remarks>
    /// <typeparam name="T">The element type of the source matrix.</typeparam>
    /// <typeparam name="R">The element type of the resulting matrix.</typeparam>
    /// <param name="matrix">The source matrix to transform.</param>
    /// <param name="selector">The transformation function receiving <c>(x, y, element)</c> for each cell.</param>
    /// <returns>A new <see cref="Matrix{R}"/> containing the mapped elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> or <paramref name="selector"/> is <see langword="null"/>.</exception>
    public static Matrix<R> MapIndexed<T, R>(this IReadOnlyMatrix<T> matrix, Func<int, int, T, R> selector)
        where T : struct
        where R : struct
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentNullException.ThrowIfNull(selector);

        int width = matrix.Width;
        int height = matrix.Height;
        long area = matrix.Size.Area;
        if (area == 0)
            return new SequentialMatrix<R>(width, height);

        if (area > Array.MaxLength)
            throw new ArgumentOutOfRangeException(nameof(matrix), matrix.Size, $"Matrix element count {area} exceeds the maximum supported array length ({Array.MaxLength}).");

        int count = (int)area;
        var mappedData = new R[count];
        int minRowParallel = count >= ParallelRunner.DefaultMinParallelCount && height >= 4 ? 2 : height + 1;
        if (matrix is SequentialMatrix<T> sequential)
        {
            T[] raw = sequential.RawData;
            ParallelRunner.ForChunks(
                height,
                CancellationToken.None,
                (startRow, endRow) =>
                {
                    for (int y = startRow; y < endRow; y++)
                    {
                        int rowBase = y * width;
                        for (int x = 0; x < width; x++)
                            mappedData[rowBase + x] = selector(x, y, raw[rowBase + x]);
                    }
                },
                minParallelCount: minRowParallel);
            return new SequentialMatrix<R>(width, height, mappedData);
        }

        ParallelRunner.ForChunks(
            height,
            CancellationToken.None,
            (startRow, endRow) =>
            {
                for (int y = startRow; y < endRow; y++)
                {
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                        mappedData[rowBase + x] = selector(x, y, matrix[x, y]);
                }
            },
            minParallelCount: minRowParallel);

        return new SequentialMatrix<R>(width, height, mappedData);
    }

    /// <summary>
    /// Combines corresponding elements of two same-sized matrices in parallel, returning a new matrix of results.
    /// </summary>
    /// <remarks>
    /// <paramref name="combiner"/> is invoked concurrently from multiple threads and must be thread-safe.
    /// </remarks>
    /// <typeparam name="TLeft">The element type of the left matrix.</typeparam>
    /// <typeparam name="TRight">The element type of the right matrix.</typeparam>
    /// <typeparam name="TResult">The element type of the resulting matrix.</typeparam>
    /// <param name="left">The left source matrix.</param>
    /// <param name="right">The right source matrix matching <paramref name="left"/> dimensions.</param>
    /// <param name="combiner">The combination function evaluated at each corresponding coordinate.</param>
    /// <returns>A new <see cref="Matrix{TResult}"/> containing the combined elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="left"/>, <paramref name="right"/>, or <paramref name="combiner"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the dimensions of <paramref name="left"/> and <paramref name="right"/> do not match.</exception>
    public static Matrix<TResult> Zip<TLeft, TRight, TResult>(
        this IReadOnlyMatrix<TLeft> left,
        IReadOnlyMatrix<TRight> right,
        Func<TLeft, TRight, TResult> combiner)
        where TLeft : struct
        where TRight : struct
        where TResult : struct
    {
        TResult[] data = left.ZipToArray(right, combiner);
        return new SequentialMatrix<TResult>(left.Width, left.Height, data);
    }

    internal static TResult[] ZipToArray<TLeft, TRight, TResult>(
        this IReadOnlyMatrix<TLeft> left,
        IReadOnlyMatrix<TRight> right,
        Func<TLeft, TRight, TResult> combiner)
        where TLeft : struct
        where TRight : struct
        where TResult : struct
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        ArgumentNullException.ThrowIfNull(combiner);
        Guard.ThrowIfSizeMismatch(left, right);

        int count = left.Count;
        if (count == 0)
            return [];

        var result = new TResult[count];
        if (left is SequentialMatrix<TLeft> leftSeq && right is SequentialMatrix<TRight> rightSeq)
        {
            TLeft[] leftRaw = leftSeq.RawData;
            TRight[] rightRaw = rightSeq.RawData;
            ParallelRunner.ForChunks(
                count,
                CancellationToken.None,
                (start, end) =>
                {
                    for (int i = start; i < end; i++)
                        result[i] = combiner(leftRaw[i], rightRaw[i]);
                });
            return result;
        }

        int width = left.Width;
        int height = left.Height;
        int minRowParallel = count >= ParallelRunner.DefaultMinParallelCount && height >= 4 ? 2 : height + 1;
        if (left is SequentialMatrix<TLeft> leftOnly)
        {
            TLeft[] leftRaw = leftOnly.RawData;
            ParallelRunner.ForChunks(
                height,
                CancellationToken.None,
                (startRow, endRow) =>
                {
                    for (int y = startRow; y < endRow; y++)
                    {
                        int rowBase = y * width;
                        for (int x = 0; x < width; x++)
                            result[rowBase + x] = combiner(leftRaw[rowBase + x], right[x, y]);
                    }
                },
                minParallelCount: minRowParallel);
            return result;
        }

        if (right is SequentialMatrix<TRight> rightOnly)
        {
            TRight[] rightRaw = rightOnly.RawData;
            ParallelRunner.ForChunks(
                height,
                CancellationToken.None,
                (startRow, endRow) =>
                {
                    for (int y = startRow; y < endRow; y++)
                    {
                        int rowBase = y * width;
                        for (int x = 0; x < width; x++)
                            result[rowBase + x] = combiner(left[x, y], rightRaw[rowBase + x]);
                    }
                },
                minParallelCount: minRowParallel);
            return result;
        }

        ParallelRunner.ForChunks(
            height,
            CancellationToken.None,
            (startRow, endRow) =>
            {
                for (int y = startRow; y < endRow; y++)
                {
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                        result[rowBase + x] = combiner(left[x, y], right[x, y]);
                }
            },
            minParallelCount: minRowParallel);
        return result;
    }

    /// <summary>
    /// Computes the 2D coordinate corresponding to a linear index in the matrix.
    /// </summary>
    /// <param name="matrix">The matrix whose dimensions determine the coordinate mapping.</param>
    /// <param name="index">The zero-based linear index.</param>
    /// <returns>The <see cref="Coord"/> corresponding to the linear index.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <see langword="null"/>.</exception>
    public static Coord CoordFromIndex(this IUntypedMatrix matrix, int index)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return matrix.Size.CoordOf(index);
    }

    /// <summary>
    /// Computes the linear index corresponding to a 2D coordinate in the matrix.
    /// </summary>
    /// <param name="matrix">The matrix whose dimensions determine the coordinate mapping.</param>
    /// <param name="coord">The 2D coordinate.</param>
    /// <returns>The zero-based linear index corresponding to the coordinate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <see langword="null"/>.</exception>
    public static int IndexFromCoord(this IUntypedMatrix matrix, Coord coord)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return matrix.Size.IndexOf(coord);
    }

    /// <summary>
    /// Normalizes a 2D coordinate relative to the dimensions of the matrix into the [0, 1) range.
    /// </summary>
    /// <param name="matrix">The matrix whose dimensions are used for normalization.</param>
    /// <param name="coord">The integer coordinate to normalize.</param>
    /// <returns>A <see cref="NormalizedCoord"/> with components in the normalized range.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <see langword="null"/>.</exception>
    public static NormalizedCoord NormalizeCoord(this IUntypedMatrix matrix, Coord coord)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return new(coord.X / (double)matrix.Width, coord.Y / (double)matrix.Height);
    }

    /// <summary>
    /// Denormalizes a normalized coordinate back to integer pixel coordinates based on the matrix dimensions.
    /// </summary>
    /// <param name="matrix">The matrix whose dimensions are used for denormalization.</param>
    /// <param name="normalizedCoord">The normalized coordinate to denormalize.</param>
    /// <returns>A <see cref="Coord"/> in integer matrix coordinates.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <see langword="null"/>.</exception>
    public static Coord DenormalizeCoord(this IUntypedMatrix matrix, NormalizedCoord normalizedCoord)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return new((int)(normalizedCoord.X * matrix.Width), (int)(normalizedCoord.Y * matrix.Height));
    }

    /// <summary>
    /// Determines whether the specified integer coordinates lie within the bounds of the matrix.
    /// </summary>
    /// <param name="matrix">The matrix whose bounds are checked.</param>
    /// <param name="x">The zero-based column (X) coordinate.</param>
    /// <param name="y">The zero-based row (Y) coordinate.</param>
    /// <returns><see langword="true"/> if the coordinates lie within the matrix bounds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <see langword="null"/>.</exception>
    public static bool Contains(this IUntypedMatrix matrix, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return matrix.Size.Contains(x, y);
    }

    /// <summary>
    /// Determines whether the specified coordinate lies within the bounds of the matrix.
    /// </summary>
    /// <param name="matrix">The matrix whose bounds are checked.</param>
    /// <param name="coord">The 2D coordinate to test.</param>
    /// <returns><see langword="true"/> if the coordinate lies within the matrix bounds; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <see langword="null"/>.</exception>
    public static bool Contains(this IUntypedMatrix matrix, Coord coord)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        return matrix.Size.Contains(coord);
    }

    /// <summary>
    /// Determines whether two matrices have identical dimensions and equal element values at every coordinate.
    /// </summary>
    /// <typeparam name="T">The element type stored in both matrices.</typeparam>
    /// <param name="left">The first matrix to compare.</param>
    /// <param name="right">The second matrix to compare.</param>
    /// <returns><see langword="true"/> if both matrices have the same size and equal elements; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="left"/> or <paramref name="right"/> is <see langword="null"/>.</exception>
    public static bool ContentEquals<T>(this IReadOnlyMatrix<T> left, IReadOnlyMatrix<T> right)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Size != right.Size)
            return false;

        if (left.TryGetReadOnlySpan(out ReadOnlySpan<T> leftSpan) && right.TryGetReadOnlySpan(out ReadOnlySpan<T> rightSpan))
            return MemoryExtensions.SequenceEqual(leftSpan, rightSpan, comparer: null);

        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        int width = left.Width;
        int height = left.Height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!comparer.Equals(left[x, y], right[x, y]))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the element at the specified coordinates clamped to the matrix bounds.
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="matrix">The source matrix to read from.</param>
    /// <param name="x">The column (X) coordinate to clamp.</param>
    /// <param name="y">The row (Y) coordinate to clamp.</param>
    /// <returns>The element at the clamped coordinates.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="matrix"/> is empty.</exception>
    public static T GetClamped<T>(this IReadOnlyMatrix<T> matrix, int x, int y)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(matrix);

        if (matrix.Count == 0)
            throw new ArgumentException("Cannot read from an empty matrix.", nameof(matrix));

        return matrix[
            EdgeHandling.Extend.ResolveIndex(x, matrix.Width),
            EdgeHandling.Extend.ResolveIndex(y, matrix.Height)];
    }

    /// <summary>
    /// Gets the element at the specified coordinate clamped to the matrix bounds.
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="matrix">The source matrix to read from.</param>
    /// <param name="coord">The 2D coordinate to clamp.</param>
    /// <returns>The element at the clamped coordinate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="matrix"/> is empty.</exception>
    public static T GetClamped<T>(this IReadOnlyMatrix<T> matrix, Coord coord)
        where T : struct =>
        matrix.GetClamped(coord.X, coord.Y);
}
