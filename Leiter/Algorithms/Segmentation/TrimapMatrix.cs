namespace Leiter.Algorithms.Segmentation;

using Leiter.Core;

/// <summary>
/// Represents a two-dimensional pixelwise <see cref="TrimapLabel"/> matrix backed by a contiguous row-major array.
/// </summary>
public class TrimapMatrix : SequentialMatrix<TrimapLabel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TrimapMatrix"/> class with the specified width and height.
    /// All elements default to <see cref="TrimapLabel.Background"/>.
    /// </summary>
    /// <param name="width">The number of columns in the trimap.</param>
    /// <param name="height">The number of rows in the trimap.</param>
    public TrimapMatrix(int width, int height)
        : base(width, height) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrimapMatrix"/> class with the specified size dimensions.
    /// All elements default to <see cref="TrimapLabel.Background"/>.
    /// </summary>
    /// <param name="size">The dimensions of the trimap.</param>
    public TrimapMatrix(Size size)
        : base(size) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrimapMatrix"/> class from a two-dimensional array indexed by <c>[row, column]</c>.
    /// </summary>
    /// <param name="values">The two-dimensional <see cref="TrimapLabel"/> array indexed by <c>[row, column]</c> to copy elements from.</param>
    public TrimapMatrix(TrimapLabel[,] values)
        : base(values) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrimapMatrix"/> class with the specified size dimensions and array of elements.
    /// </summary>
    /// <param name="size">The dimensions of the trimap.</param>
    /// <param name="values">The row-major <see cref="TrimapLabel"/> array to copy elements from.</param>
    /// <exception cref="ArgumentException">The length of <paramref name="values"/> does not equal the total number of elements specified by <paramref name="size"/>.</exception>
    public TrimapMatrix(Size size, TrimapLabel[] values)
        : base(size, values) { }

    internal TrimapMatrix(int width, int height, TrimapLabel[] data)
        : base(width, height, data) { }

    /// <summary>
    /// Creates a deep copy of this trimap matrix.
    /// </summary>
    /// <returns>A new <see cref="TrimapMatrix"/> that is a copy of this instance.</returns>
    public override TrimapMatrix Clone() =>
        new(Width, Height, CloneData());

    /// <summary>
    /// Materializes any <see cref="IReadOnlyMatrix{T}"/> of <see cref="TrimapLabel"/>
    /// (such as a <see cref="MatrixView{T}"/> or <see cref="Matrix{T}"/>) into a standalone <see cref="TrimapMatrix"/>.
    /// </summary>
    /// <param name="source">The source trimap matrix to copy.</param>
    /// <returns>A new <see cref="TrimapMatrix"/> containing the copied <see cref="TrimapLabel"/> values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static TrimapMatrix From(IReadOnlyMatrix<TrimapLabel> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source is TrimapMatrix existing)
            return existing.Clone();

        int width = source.Width;
        int height = source.Height;
        var data = new TrimapLabel[source.Count];

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
                data[rowBase + x] = source[x, y];
        }

        return new TrimapMatrix(width, height, data);
    }

    /// <summary>
    /// Constructs a trimap for an image of size <paramref name="imageSize"/> where pixels inside the clipped rectangle
    /// are marked <see cref="TrimapLabel.ProbableForeground"/> and all pixels outside are marked <see cref="TrimapLabel.Background"/>.
    /// </summary>
    /// <param name="imageSize">The target image dimensions.</param>
    /// <param name="rectangleTopLeft">The top-left coordinate of the bounding rectangle.</param>
    /// <param name="rectangleSize">The width and height of the bounding rectangle.</param>
    /// <returns>A <see cref="TrimapMatrix"/> matching <paramref name="imageSize"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="imageSize"/> has negative dimensions or <paramref name="rectangleSize"/> dimensions are not positive.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the rectangle does not overlap <paramref name="imageSize"/> or leaves zero background pixels outside it.
    /// </exception>
    public static TrimapMatrix FromBoundingBox(Size imageSize, Coord rectangleTopLeft, Size rectangleSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(imageSize.Width, nameof(imageSize));
        ArgumentOutOfRangeException.ThrowIfNegative(imageSize.Height, nameof(imageSize));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rectangleSize.Width, 0, nameof(rectangleSize));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rectangleSize.Height, 0, nameof(rectangleSize));

        if (imageSize.Width == 0 || imageSize.Height == 0)
            return new TrimapMatrix(imageSize);

        int left = Math.Max(0, rectangleTopLeft.X);
        int top = Math.Max(0, rectangleTopLeft.Y);
        int right = (int)Math.Min((long)imageSize.Width, (long)rectangleTopLeft.X + rectangleSize.Width);
        int bottom = (int)Math.Min((long)imageSize.Height, (long)rectangleTopLeft.Y + rectangleSize.Height);

        if (left >= right || top >= bottom)
            throw new ArgumentException("Foreground rectangle does not overlap the image bounds.", nameof(rectangleTopLeft));

        long overlapArea = (long)(right - left) * (bottom - top);
        long totalArea = (long)imageSize.Width * imageSize.Height;
        if (overlapArea == totalArea)
            throw new ArgumentException("Foreground rectangle must leave at least one background pixel outside it.", nameof(rectangleSize));

        var trimap = new TrimapMatrix(imageSize);
        for (int y = top; y < bottom; y++)
        {
            for (int x = left; x < right; x++)
                trimap[x, y] = TrimapLabel.ProbableForeground;
        }

        return trimap;
    }
}
