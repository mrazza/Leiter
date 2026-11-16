namespace Leiter.Algorithms;

using System.Numerics;
using Leiter.Core;

/// <summary>
/// Provides bilinear and nearest-neighbor matrix resizing operations.
/// </summary>
public static class MatrixResampler
{
    private readonly record struct BilinearSampleAxis(
        int LowerIndex,
        int UpperIndex,
        double LowerWeight,
        double UpperWeight);

    /// <summary>
    /// Resizes a numeric pixel matrix to <paramref name="targetSize"/> using pixel-center bilinear interpolation.
    /// </summary>
    /// <typeparam name="T">The numeric pixel type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to resample.</param>
    /// <param name="targetSize">The target matrix dimensions.</param>
    /// <returns>A new <see cref="Matrix{T}"/> with dimensions <paramref name="targetSize"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetSize"/> has a negative width or height.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty while <paramref name="targetSize"/> has positive dimensions.</exception>
    public static Matrix<T> ResizeBilinear<T>(this IReadOnlyMatrix<T> source, Size targetSize)
        where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
    {
        if (!ValidateResampleInputs(source, targetSize))
            return new SequentialMatrix<T>(targetSize);

        var result = new SequentialMatrix<T>(targetSize);

        Parallel.For(0, targetSize.Height, targetY =>
        {
            BilinearSampleAxis vertical = ComputeBilinearSampleAxis(targetY, source.Height, targetSize.Height);
            int topY = vertical.LowerIndex;
            int bottomY = vertical.UpperIndex;

            for (int targetX = 0; targetX < targetSize.Width; targetX++)
            {
                BilinearSampleAxis horizontal = ComputeBilinearSampleAxis(targetX, source.Width, targetSize.Width);
                int leftX = horizontal.LowerIndex;
                int rightX = horizontal.UpperIndex;

                double topLeftWeight = horizontal.LowerWeight * vertical.LowerWeight;
                double topRightWeight = horizontal.UpperWeight * vertical.LowerWeight;
                double bottomLeftWeight = horizontal.LowerWeight * vertical.UpperWeight;
                double bottomRightWeight = horizontal.UpperWeight * vertical.UpperWeight;

                Vector<double> interpolatedPixel =
                    (source[leftX, topY].ToDoubleVector() * topLeftWeight) +
                    (source[rightX, topY].ToDoubleVector() * topRightWeight) +
                    (source[leftX, bottomY].ToDoubleVector() * bottomLeftWeight) +
                    (source[rightX, bottomY].ToDoubleVector() * bottomRightWeight);

                result[targetX, targetY] = T.FromDoubleVector(interpolatedPixel);
            }
        });

        return result;
    }

    /// <summary>
    /// Resizes a matrix to <paramref name="targetSize"/> using nearest-neighbor sampling.
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to resample.</param>
    /// <param name="targetSize">The target matrix dimensions.</param>
    /// <returns>A new <see cref="Matrix{T}"/> with dimensions <paramref name="targetSize"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetSize"/> has a negative width or height.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty while <paramref name="targetSize"/> has positive dimensions.</exception>
    public static Matrix<T> ResizeNearest<T>(this IReadOnlyMatrix<T> source, Size targetSize)
        where T : struct
    {
        if (!ValidateResampleInputs(source, targetSize))
            return new SequentialMatrix<T>(targetSize);

        var result = new SequentialMatrix<T>(targetSize);
        CopyNearest(source, result);
        return result;
    }

    /// <summary>
    /// Resizes a boolean mask matrix to <paramref name="targetSize"/> using nearest-neighbor sampling.
    /// </summary>
    /// <param name="source">The source boolean mask to resample.</param>
    /// <param name="targetSize">The target mask dimensions.</param>
    /// <returns>A new <see cref="MaskMatrix"/> with dimensions <paramref name="targetSize"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="targetSize"/> has a negative width or height.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty while <paramref name="targetSize"/> has positive dimensions.</exception>
    public static MaskMatrix ResizeMaskNearest(this IReadOnlyMatrix<bool> source, Size targetSize)
    {
        if (!ValidateResampleInputs(source, targetSize))
            return new MaskMatrix(targetSize);

        var result = new MaskMatrix(targetSize);
        CopyNearest(source, result);
        return result;
    }

    private static bool ValidateResampleInputs(IUntypedMatrix source, Size targetSize)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(targetSize.Width, nameof(targetSize));
        ArgumentOutOfRangeException.ThrowIfNegative(targetSize.Height, nameof(targetSize));

        if (targetSize.Width == 0 || targetSize.Height == 0)
            return false;

        if (source.Count == 0)
            throw new ArgumentException("Cannot resample an empty source matrix to a non-empty target size.", nameof(source));

        return true;
    }

    private static BilinearSampleAxis ComputeBilinearSampleAxis(
        int targetCoord,
        int sourceLength,
        int targetLength)
    {
        double sourceCoord = (((targetCoord + 0.5) * sourceLength) / targetLength) - 0.5;
        double clampedCoord = Math.Clamp(sourceCoord, 0.0, sourceLength - 1);

        int lowerIndex = (int)Math.Floor(clampedCoord);
        int upperIndex = Math.Min(sourceLength - 1, lowerIndex + 1);
        double upperWeight = clampedCoord - lowerIndex;
        double lowerWeight = 1.0 - upperWeight;

        return new BilinearSampleAxis(lowerIndex, upperIndex, lowerWeight, upperWeight);
    }

    private static int ComputeNearestSourceIndex(int targetCoord, int sourceLength, int targetLength) =>
        Math.Clamp((int)((long)targetCoord * sourceLength / targetLength), 0, sourceLength - 1);

    private static void CopyNearest<T>(IReadOnlyMatrix<T> source, Matrix<T> destination)
        where T : struct
    {
        int sourceWidth = source.Width;
        int sourceHeight = source.Height;
        int targetWidth = destination.Width;
        int targetHeight = destination.Height;

        Parallel.For(0, targetHeight, targetY =>
        {
            int sourceY = ComputeNearestSourceIndex(targetY, sourceHeight, targetHeight);
            for (int targetX = 0; targetX < targetWidth; targetX++)
            {
                int sourceX = ComputeNearestSourceIndex(targetX, sourceWidth, targetWidth);
                destination[targetX, targetY] = source[sourceX, sourceY];
            }
        });
    }
}
