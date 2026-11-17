namespace Leiter.Core;

/// <summary>
/// Represents a two-dimensional boolean foreground/background mask backed by a contiguous row-major array.
/// </summary>
public class MaskMatrix : SequentialMatrix<bool>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MaskMatrix"/> class with the specified width and height.
    /// </summary>
    /// <param name="width">The number of columns in the mask.</param>
    /// <param name="height">The number of rows in the mask.</param>
    public MaskMatrix(int width, int height)
        : base(width, height) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MaskMatrix"/> class with the specified size dimensions.
    /// </summary>
    /// <param name="size">The dimensions of the mask.</param>
    public MaskMatrix(Size size)
        : base(size) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MaskMatrix"/> class from a two-dimensional array indexed by <c>[row, column]</c>.
    /// </summary>
    /// <param name="values">The two-dimensional boolean array indexed by <c>[row, column]</c> to copy elements from.</param>
    public MaskMatrix(bool[,] values)
        : base(values) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MaskMatrix"/> class with the specified size dimensions and array of elements.
    /// </summary>
    /// <param name="size">The dimensions of the mask.</param>
    /// <param name="values">The row-major boolean array to copy elements from.</param>
    /// <exception cref="ArgumentException">The length of <paramref name="values"/> does not equal the total number of elements specified by <paramref name="size"/>.</exception>
    public MaskMatrix(Size size, bool[] values)
        : base(size, values) { }

    internal MaskMatrix(int width, int height, bool[] data)
        : base(width, height, data) { }

    /// <summary>
    /// Creates a deep copy of this mask matrix.
    /// </summary>
    /// <returns>A new <see cref="MaskMatrix"/> that is a copy of this instance.</returns>
    public override MaskMatrix Clone() =>
        new(Width, Height, CloneData());

    /// <summary>
    /// Materializes any <see cref="IReadOnlyMatrix{T}"/> of <see cref="bool"/> (such as a <see cref="MatrixView{T}"/> or <see cref="Matrix{T}"/>)
    /// into a standalone <see cref="MaskMatrix"/>.
    /// </summary>
    /// <param name="source">The source boolean matrix to copy.</param>
    /// <returns>A new <see cref="MaskMatrix"/> containing the copied boolean values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static new MaskMatrix From(IReadOnlyMatrix<bool> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source.Width, source.Height, CopyElements(source));
    }

    /// <summary>
    /// Computes the logical complement (<c>!value</c> per element) of a mask matrix.
    /// </summary>
    /// <param name="mask">The mask matrix to invert.</param>
    /// <returns>A new <see cref="MaskMatrix"/> with inverted boolean entries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    public static MaskMatrix operator !(MaskMatrix mask) => mask.Invert();

    /// <summary>
    /// Computes the element-wise logical intersection (<paramref name="left"/> AND <paramref name="right"/>) of two mask matrices.
    /// </summary>
    /// <param name="left">The left mask operand.</param>
    /// <param name="right">The right mask operand.</param>
    /// <returns>A new <see cref="MaskMatrix"/> containing the element-wise logical AND.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either mask is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when mask dimensions do not match.</exception>
    public static MaskMatrix operator &(MaskMatrix left, MaskMatrix right) => left.And(right);

    /// <summary>
    /// Computes the element-wise logical union (<paramref name="left"/> OR <paramref name="right"/>) of two mask matrices.
    /// </summary>
    /// <param name="left">The left mask operand.</param>
    /// <param name="right">The right mask operand.</param>
    /// <returns>A new <see cref="MaskMatrix"/> containing the element-wise logical OR.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either mask is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when mask dimensions do not match.</exception>
    public static MaskMatrix operator |(MaskMatrix left, MaskMatrix right) => left.Or(right);
}
