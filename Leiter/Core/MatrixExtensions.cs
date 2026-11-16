namespace Leiter.Core;

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
    /// <exception cref="ArgumentException">The sequence in <paramref name="values"/> contains fewer elements than <paramref name="width"/> * <paramref name="height"/>.</exception>
    public static SequentialMatrix<T> ToSequentialMatrix<T>(this IEnumerable<T> values, int width, int height)
        where T : struct
    {
        var result = new SequentialMatrix<T>(width, height);
        var enumerator = values.GetEnumerator();

        for (int index = 0; index < result.Count; index++)
        {
            if (!enumerator.MoveNext())
            {
                throw new ArgumentException($"IEnumerable of type {values.GetType()} is smaller than matrix of size {{{width}, {height}}}.");
            }
            result[index] = enumerator.Current;
        }

        return result;
    }

    /// <summary>
    /// Applies a transformation function to each element of a read-only matrix, returning a new matrix of the mapped type.
    /// </summary>
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

        int width = matrix.Width;
        int height = matrix.Height;
        var result = new SequentialMatrix<R>(width, height);

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
                result[x, y] = selector(matrix[x, y]);
        });

        return result;
    }

    /// <summary>
    /// Computes the 2D coordinate corresponding to a linear index in the matrix.
    /// </summary>
    /// <param name="matrix">The matrix whose dimensions determine the coordinate mapping.</param>
    /// <param name="index">The zero-based linear index.</param>
    /// <returns>The <see cref="Coord"/> corresponding to the linear index.</returns>
    public static Coord CoordFromIndex(this IUntypedMatrix matrix, int index) => new(index % matrix.Width, index / matrix.Width);

    /// <summary>
    /// Computes the linear index corresponding to a 2D coordinate in the matrix.
    /// </summary>
    /// <param name="matrix">The matrix whose dimensions determine the coordinate mapping.</param>
    /// <param name="coord">The 2D coordinate.</param>
    /// <returns>The zero-based linear index corresponding to the coordinate.</returns>
    public static int IndexFromCoord(this IUntypedMatrix matrix, Coord coord) => coord.X + coord.Y * matrix.Width;

    /// <summary>
    /// Normalizes a 2D coordinate relative to the dimensions of the matrix into the [0, 1) range.
    /// </summary>
    /// <param name="matrix">The matrix whose dimensions are used for normalization.</param>
    /// <param name="coord">The integer coordinate to normalize.</param>
    /// <returns>A <see cref="NormalizedCoord"/> with components in the normalized range.</returns>
    public static NormalizedCoord NormalizeCoord(this IUntypedMatrix matrix, Coord coord) => new(coord.X / (double)matrix.Width, coord.Y / (double)matrix.Height);

    /// <summary>
    /// Denormalizes a normalized coordinate back to integer pixel coordinates based on the matrix dimensions.
    /// </summary>
    /// <param name="matrix">The matrix whose dimensions are used for denormalization.</param>
    /// <param name="normalizedCoord">The normalized coordinate to denormalize.</param>
    /// <returns>A <see cref="Coord"/> in integer matrix coordinates.</returns>
    public static Coord DenormalizeCoord(this IUntypedMatrix matrix, NormalizedCoord normalizedCoord) => new((int)(normalizedCoord.X * matrix.Width), (int)(normalizedCoord.Y * matrix.Height));

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
}
