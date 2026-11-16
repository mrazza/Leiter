namespace Leiter.Core;

/// <summary>
/// Defines non-generic dimensions and structural properties for a two-dimensional matrix.
/// </summary>
public interface IUntypedMatrix
{
    /// <summary>
    /// Gets the number of columns in the matrix.
    /// </summary>
    int Width { get; }

    /// <summary>
    /// Gets the number of rows in the matrix.
    /// </summary>
    int Height { get; }

    /// <summary>
    /// Gets the dimensions of the matrix as a <see cref="Core.Size"/>.
    /// </summary>
    Size Size { get; }

    /// <summary>
    /// Gets the total number of elements in the matrix.
    /// </summary>
    int Count { get; }
}
