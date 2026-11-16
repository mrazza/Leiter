namespace Leiter.Algorithms.Morphology;

using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Provides fast binary morphological operations (<see cref="Dilate"/>, <see cref="Erode"/>, <see cref="Open"/>, and <see cref="Close"/>)
/// on mask matrices where zero represents background and any non-zero value represents foreground.
/// </summary>
public static class BinaryMorphology
{
    /// <summary>
    /// Dilates foreground pixels in a binary mask using a structuring element of the specified radius and shape.
    /// </summary>
    /// <param name="mask">The binary mask (<c>0</c> = background, non-zero = foreground).</param>
    /// <param name="radius">The non-negative structuring element radius in pixels.</param>
    /// <param name="shape">The shape of the structuring element.</param>
    /// <returns>A normalized mask containing <c>1</c> for dilated foreground pixels and <c>0</c> for background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is negative or <paramref name="shape"/> is invalid.</exception>
    public static Matrix<LongPixel> Dilate(
        IReadOnlyMatrix<LongPixel> mask,
        int radius,
        StructuringElementShape shape = StructuringElementShape.Disk)
    {
        ValidateInputs(mask, radius, shape);

        if (mask.Count == 0 || radius == 0)
            return NormalizeMask(mask);

        int width = mask.Width;
        int height = mask.Height;
        int rowStride = width + 1;
        int[] rowPrefixSums = BuildRowPrefixSums(mask);
        int[] horizontalHalfWidths = BuildHorizontalHalfWidths(radius, shape);
        var result = new SequentialMatrix<LongPixel>(width, height);

        Parallel.For(0, height, y =>
        {
            int minNeighborY = Math.Max(0, y - radius);
            int maxNeighborY = Math.Min(height - 1, y + radius);

            for (int x = 0; x < width; x++)
            {
                bool hasForegroundNeighbor = false;
                for (int neighborY = minNeighborY; neighborY <= maxNeighborY; neighborY++)
                {
                    int halfWidth = horizontalHalfWidths[Math.Abs(neighborY - y)];
                    int left = Math.Max(0, x - halfWidth);
                    int right = Math.Min(width - 1, x + halfWidth);
                    int rowOffset = neighborY * rowStride;

                    if (rowPrefixSums[rowOffset + right + 1] - rowPrefixSums[rowOffset + left] > 0)
                    {
                        hasForegroundNeighbor = true;
                        break;
                    }
                }

                result[x, y] = hasForegroundNeighbor ? 1L : 0L;
            }
        });

        return result;
    }

    /// <summary>
    /// Erodes foreground pixels in a binary mask using a structuring element of the specified radius and shape.
    /// </summary>
    /// <param name="mask">The binary mask (<c>0</c> = background, non-zero = foreground).</param>
    /// <param name="radius">The non-negative structuring element radius in pixels.</param>
    /// <param name="shape">The shape of the structuring element.</param>
    /// <param name="borderIsBackground">
    /// If <see langword="true"/>, samples outside the image bounds are treated as background (<c>0</c>);
    /// otherwise coordinates are clamped to the nearest image edge.
    /// </param>
    /// <returns>A normalized mask containing <c>1</c> for eroded foreground pixels and <c>0</c> for background.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is negative or <paramref name="shape"/> is invalid.</exception>
    public static Matrix<LongPixel> Erode(
        IReadOnlyMatrix<LongPixel> mask,
        int radius,
        StructuringElementShape shape = StructuringElementShape.Disk,
        bool borderIsBackground = false)
    {
        ValidateInputs(mask, radius, shape);

        if (mask.Count == 0 || radius == 0)
            return NormalizeMask(mask);

        int width = mask.Width;
        int height = mask.Height;
        int rowStride = width + 1;
        int[] rowPrefixSums = BuildRowPrefixSums(mask);
        int[] horizontalHalfWidths = BuildHorizontalHalfWidths(radius, shape);
        var result = new SequentialMatrix<LongPixel>(width, height);

        Parallel.For(0, height, y =>
        {
            if (borderIsBackground && (y - radius < 0 || y + radius >= height))
                return;

            int minNeighborY = Math.Max(0, y - radius);
            int maxNeighborY = Math.Min(height - 1, y + radius);

            for (int x = 0; x < width; x++)
            {
                if (borderIsBackground && (x - radius < 0 || x + radius >= width))
                    continue;

                bool allForeground = true;
                for (int neighborY = minNeighborY; neighborY <= maxNeighborY; neighborY++)
                {
                    int halfWidth = horizontalHalfWidths[Math.Abs(neighborY - y)];
                    int left = Math.Max(0, x - halfWidth);
                    int right = Math.Min(width - 1, x + halfWidth);
                    int rowOffset = neighborY * rowStride;
                    int spanCount = right - left + 1;

                    if (rowPrefixSums[rowOffset + right + 1] - rowPrefixSums[rowOffset + left] < spanCount)
                    {
                        allForeground = false;
                        break;
                    }
                }

                result[x, y] = allForeground ? 1L : 0L;
            }
        });

        return result;
    }

    /// <summary>
    /// Applies morphological opening (<see cref="Erode"/> followed by <see cref="Dilate"/>) to remove isolated foreground specks.
    /// </summary>
    /// <param name="mask">The binary mask (<c>0</c> = background, non-zero = foreground).</param>
    /// <param name="radius">The non-negative structuring element radius in pixels.</param>
    /// <param name="shape">The shape of the structuring element.</param>
    /// <param name="borderIsBackground">Whether out-of-bounds samples during erosion are treated as background.</param>
    /// <returns>The opened binary mask with values <c>0</c> and <c>1</c>.</returns>
    public static Matrix<LongPixel> Open(
        IReadOnlyMatrix<LongPixel> mask,
        int radius,
        StructuringElementShape shape = StructuringElementShape.Disk,
        bool borderIsBackground = false)
    {
        return Dilate(Erode(mask, radius, shape, borderIsBackground), radius, shape);
    }

    /// <summary>
    /// Applies morphological closing (<see cref="Dilate"/> followed by <see cref="Erode"/>) to fill small background holes.
    /// </summary>
    /// <param name="mask">The binary mask (<c>0</c> = background, non-zero = foreground).</param>
    /// <param name="radius">The non-negative structuring element radius in pixels.</param>
    /// <param name="shape">The shape of the structuring element.</param>
    /// <param name="borderIsBackground">Whether out-of-bounds samples during erosion are treated as background.</param>
    /// <returns>The closed binary mask with values <c>0</c> and <c>1</c>.</returns>
    public static Matrix<LongPixel> Close(
        IReadOnlyMatrix<LongPixel> mask,
        int radius,
        StructuringElementShape shape = StructuringElementShape.Disk,
        bool borderIsBackground = false)
    {
        return Erode(Dilate(mask, radius, shape), radius, shape, borderIsBackground);
    }

    private static void ValidateInputs(IReadOnlyMatrix<LongPixel> mask, int radius, StructuringElementShape shape)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        if (!Enum.IsDefined(shape))
            throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unsupported structuring element shape.");
    }

    private static Matrix<LongPixel> NormalizeMask(IReadOnlyMatrix<LongPixel> mask)
    {
        var copy = new SequentialMatrix<LongPixel>(mask.Size);
        for (int i = 0; i < mask.Count; i++)
            copy[i] = mask.GetElement(i).Value != 0 ? 1L : 0L;
        return copy;
    }

    private static int[] BuildRowPrefixSums(IReadOnlyMatrix<LongPixel> mask)
    {
        int width = mask.Width;
        int height = mask.Height;
        int rowStride = width + 1;
        var rowPrefixSums = new int[height * rowStride];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * rowStride;
            int runningSum = 0;
            for (int x = 0; x < width; x++)
            {
                if (mask[x, y].Value != 0)
                    runningSum++;
                rowPrefixSums[rowOffset + x + 1] = runningSum;
            }
        });

        return rowPrefixSums;
    }

    private static int[] BuildHorizontalHalfWidths(int radius, StructuringElementShape shape)
    {
        var halfWidths = new int[radius + 1];
        long radiusSquared = (long)radius * radius;

        for (int absDy = 0; absDy <= radius; absDy++)
        {
            halfWidths[absDy] = shape == StructuringElementShape.Square
                ? radius
                : (int)Math.Floor(Math.Sqrt(radiusSquared - ((long)absDy * absDy)));
        }

        return halfWidths;
    }
}
