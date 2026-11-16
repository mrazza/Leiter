namespace Leiter.Core;

using System.Text;

/// <summary>
/// Represents a strongly typed, read-only two-dimensional matrix of elements.
/// </summary>
/// <typeparam name="T">The type of elements stored in the matrix.</typeparam>
public interface IReadOnlyMatrix<T> : IUntypedMatrix, IReadOnlyCollection<T>, IReadOnlyList<T>, IEnumerable<T>
    where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
{
    /// <summary>
    /// Gets the element at the specified zero-based column and row coordinates.
    /// </summary>
    /// <param name="x">The zero-based column coordinate.</param>
    /// <param name="y">The zero-based row coordinate.</param>
    /// <returns>The element at the specified coordinates.</returns>
    T this[int x, int y]
    {
        get;
    }

    /// <summary>
    /// Gets the element at the specified coordinate.
    /// </summary>
    /// <param name="coord">The coordinate of the element to get.</param>
    /// <returns>The element at the specified coordinate.</returns>
    T this[Coord coord]
    {
        get;
    }

    /// <summary>
    /// Gets the element at the specified linear index.
    /// </summary>
    /// <param name="index">The zero-based linear index of the element.</param>
    /// <returns>The element at the specified linear index.</returns>
    T GetElement(int index);

    /// <summary>
    /// Gets the element at the specified coordinate components.
    /// </summary>
    /// <param name="width">The zero-based column (X) coordinate of the element.</param>
    /// <param name="height">The zero-based row (Y) coordinate of the element.</param>
    /// <returns>The element at the specified coordinates.</returns>
    T GetElement(int width, int height);

    int IUntypedMatrix.Count => Count;

    int IReadOnlyCollection<T>.Count => Count;

    /// <summary>
    /// Gets the total number of elements in the matrix.
    /// </summary>
    new int Count { get; }

    /// <summary>
    /// Generates a string representation of the specified matrix.
    /// </summary>
    /// <param name="matrix">The matrix to convert to a string.</param>
    /// <returns>A string representation of the matrix contents.</returns>
    protected internal static string ToString(IReadOnlyMatrix<T> matrix)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(matrix.GetType());
        builder.AppendLine(" {");

        for (int y = 0; y < matrix.Height; y++)
        {
            builder.Append('\t');

            for (int x = 0; x < matrix.Width; x++)
            {
                if (x > 0)
                    builder.Append(", ");
                builder.Append(matrix[x, y].ToString());
            }
            builder.AppendLine();
        }

        builder.Append('}');
        return builder.ToString();
    }
}