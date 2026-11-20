namespace Leiter.Core;

/// <summary>
/// Provides boolean mask operations and masked compositing helpers for <see cref="IReadOnlyMatrix{T}"/> of <see cref="bool"/>.
/// </summary>
public static class MaskExtensions
{
    /// <summary>
    /// Counts the number of <see langword="true"/> elements in a boolean mask matrix.
    /// </summary>
    /// <param name="mask">The boolean mask matrix to count.</param>
    /// <returns>The total number of <see langword="true"/> entries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    public static int CountTrue(this IReadOnlyMatrix<bool> mask)
    {
        ArgumentNullException.ThrowIfNull(mask);

        if (mask.TryGetReadOnlySpan(out ReadOnlySpan<bool> span))
        {
            int spanCount = 0;
            foreach (bool value in span)
            {
                if (value)
                    spanCount++;
            }

            return spanCount;
        }

        int count = 0;
        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                if (mask[x, y])
                    count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Counts the number of <see langword="false"/> elements in a boolean mask matrix.
    /// </summary>
    /// <param name="mask">The boolean mask matrix to count.</param>
    /// <returns>The total number of <see langword="false"/> entries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    public static int CountFalse(this IReadOnlyMatrix<bool> mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        return mask.Count - mask.CountTrue();
    }

    /// <summary>
    /// Materializes a boolean <see cref="IReadOnlyMatrix{T}"/> as a <see cref="MaskMatrix"/>.
    /// </summary>
    /// <param name="mask">The boolean matrix to copy.</param>
    /// <returns>A new <see cref="MaskMatrix"/> containing the copied boolean values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    public static MaskMatrix ToMaskMatrix(this IReadOnlyMatrix<bool> mask) =>
        MaskMatrix.From(mask);

    /// <summary>
    /// Projects elements of a matrix into a <see cref="MaskMatrix"/> in parallel using the specified predicate.
    /// </summary>
    /// <typeparam name="T">The value type stored in the source matrix.</typeparam>
    /// <param name="source">The source matrix.</param>
    /// <param name="predicate">A predicate evaluated for each element of <paramref name="source"/>.</param>
    /// <returns>A new <see cref="MaskMatrix"/> whose entries equal <paramref name="predicate"/> applied to each element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    public static MaskMatrix ToMaskMatrix<T>(this IReadOnlyMatrix<T> source, Func<T, bool> predicate)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        return new MaskMatrix(source.Width, source.Height, source.MapToArray(predicate));
    }

    /// <summary>
    /// Returns the logical complement of a boolean mask matrix (<c>!value</c> per element).
    /// </summary>
    /// <param name="mask">The boolean mask matrix to invert.</param>
    /// <returns>A new <see cref="MaskMatrix"/> with inverted boolean entries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    public static MaskMatrix Invert(this IReadOnlyMatrix<bool> mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        return mask.ToMaskMatrix(static value => !value);
    }

    /// <summary>
    /// Computes the element-wise logical intersection (<paramref name="left"/> AND <paramref name="right"/>) of two masks.
    /// </summary>
    /// <param name="left">The left mask operand.</param>
    /// <param name="right">The right mask operand.</param>
    /// <returns>A new <see cref="MaskMatrix"/> where each cell is <c>left[x, y] &amp;&amp; right[x, y]</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either mask is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when mask dimensions do not match.</exception>
    public static MaskMatrix And(this IReadOnlyMatrix<bool> left, IReadOnlyMatrix<bool> right) =>
        CombineMasks(left, right, static (a, b) => a && b);

    /// <summary>
    /// Computes the element-wise logical union (<paramref name="left"/> OR <paramref name="right"/>) of two masks.
    /// </summary>
    /// <param name="left">The left mask operand.</param>
    /// <param name="right">The right mask operand.</param>
    /// <returns>A new <see cref="MaskMatrix"/> where each cell is <c>left[x, y] || right[x, y]</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either mask is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when mask dimensions do not match.</exception>
    public static MaskMatrix Or(this IReadOnlyMatrix<bool> left, IReadOnlyMatrix<bool> right) =>
        CombineMasks(left, right, static (a, b) => a || b);

    /// <summary>
    /// Computes the element-wise set difference (<paramref name="left"/> AND NOT <paramref name="right"/>) of two masks.
    /// </summary>
    /// <param name="left">The base mask operand.</param>
    /// <param name="right">The mask of elements to exclude.</param>
    /// <returns>A new <see cref="MaskMatrix"/> where each cell is <c>left[x, y] &amp;&amp; !right[x, y]</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either mask is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when mask dimensions do not match.</exception>
    public static MaskMatrix Except(this IReadOnlyMatrix<bool> left, IReadOnlyMatrix<bool> right) =>
        CombineMasks(left, right, static (a, b) => a && !b);

    /// <summary>
    /// Retains elements of <paramref name="source"/> where <paramref name="mask"/> is <see langword="true"/>,
    /// replacing masked-out positions with <paramref name="background"/>.
    /// </summary>
    /// <typeparam name="T">The value type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix.</param>
    /// <param name="mask">The boolean foreground mask matching <paramref name="source"/> dimensions.</param>
    /// <param name="background">The replacement value assigned where <paramref name="mask"/> is <see langword="false"/>.</param>
    /// <returns>A new matrix containing masked elements from <paramref name="source"/> and <paramref name="background"/> elsewhere.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="mask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="source"/> and <paramref name="mask"/> differ.</exception>
    public static Matrix<T> ApplyMask<T>(this IReadOnlyMatrix<T> source, IReadOnlyMatrix<bool> mask, T background = default)
        where T : struct =>
        source.Zip(mask, (value, isForeground) => isForeground ? value : background);

    private static MaskMatrix CombineMasks(
        IReadOnlyMatrix<bool> left,
        IReadOnlyMatrix<bool> right,
        Func<bool, bool, bool> combiner)
    {
        bool[] data = left.ZipToArray(right, combiner);
        return new(left.Width, left.Height, data);
    }
}
