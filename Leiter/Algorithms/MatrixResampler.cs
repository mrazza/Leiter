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
        where T : struct, IPixel<T>
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

    /// <summary>
    /// Samples a numeric pixel from the matrix at the specified coordinates using bilinear interpolation with edge clamping.
    /// </summary>
    /// <typeparam name="T">The numeric pixel type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to sample.</param>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <param name="edgeHandling">The edge handling mode for out-of-bounds coordinates.</param>
    /// <returns>The bilinearly interpolated pixel value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="edgeHandling"/> is invalid, or <paramref name="x"/> or <paramref name="y"/> is not finite.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty.</exception>
    public static T SampleBilinear<T>(
        this IReadOnlyMatrix<T> source,
        double x,
        double y,
        EdgeHandling edgeHandling = EdgeHandling.EXTEND)
        where T : struct, IPixel<T>
    {
        ValidateSampleInputs(source, x, y, edgeHandling);

        double cx = Math.Clamp(x, 0.0, source.Width - 1.0);
        double cy = Math.Clamp(y, 0.0, source.Height - 1.0);

        int x0 = (int)Math.Floor(cx);
        int x1 = Math.Min(source.Width - 1, x0 + 1);
        double wx = cx - x0;

        int y0 = (int)Math.Floor(cy);
        int y1 = Math.Min(source.Height - 1, y0 + 1);
        double wy = cy - y0;

        double w00 = (1.0 - wx) * (1.0 - wy);
        double w10 = wx * (1.0 - wy);
        double w01 = (1.0 - wx) * wy;
        double w11 = wx * wy;

        Vector<double> interpolatedPixel =
            (source[x0, y0].ToDoubleVector() * w00) +
            (source[x1, y0].ToDoubleVector() * w10) +
            (source[x0, y1].ToDoubleVector() * w01) +
            (source[x1, y1].ToDoubleVector() * w11);

        return T.FromDoubleVector(interpolatedPixel);
    }

    /// <summary>
    /// Samples a numeric pixel from the matrix at the specified position using bilinear interpolation with edge clamping.
    /// </summary>
    /// <typeparam name="T">The numeric pixel type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to sample.</param>
    /// <param name="position">The 2D coordinate position.</param>
    /// <param name="edgeHandling">The edge handling mode for out-of-bounds coordinates.</param>
    /// <returns>The bilinearly interpolated pixel value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="edgeHandling"/> is invalid, or <paramref name="position"/> has non-finite components.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty.</exception>
    public static T SampleBilinear<T>(
        this IReadOnlyMatrix<T> source,
        Vector2D position,
        EdgeHandling edgeHandling = EdgeHandling.EXTEND)
        where T : struct, IPixel<T> =>
        source.SampleBilinear(position.X, position.Y, edgeHandling);

    /// <summary>
    /// Samples an element from the matrix at the specified coordinates using nearest-neighbor sampling with edge clamping.
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to sample.</param>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <param name="edgeHandling">The edge handling mode for out-of-bounds coordinates.</param>
    /// <returns>The nearest element in the matrix.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="edgeHandling"/> is invalid, or <paramref name="x"/> or <paramref name="y"/> is not finite.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty.</exception>
    public static T SampleNearest<T>(
        this IReadOnlyMatrix<T> source,
        double x,
        double y,
        EdgeHandling edgeHandling = EdgeHandling.EXTEND)
        where T : struct
    {
        ValidateSampleInputs(source, x, y, edgeHandling);

        int ix = Math.Clamp((int)Math.Round(x, MidpointRounding.AwayFromZero), 0, source.Width - 1);
        int iy = Math.Clamp((int)Math.Round(y, MidpointRounding.AwayFromZero), 0, source.Height - 1);

        return source[ix, iy];
    }

    /// <summary>
    /// Samples an element from the matrix at the specified position using nearest-neighbor sampling with edge clamping.
    /// </summary>
    /// <typeparam name="T">The element type stored in the matrix.</typeparam>
    /// <param name="source">The source matrix to sample.</param>
    /// <param name="position">The 2D coordinate position.</param>
    /// <param name="edgeHandling">The edge handling mode for out-of-bounds coordinates.</param>
    /// <returns>The nearest element in the matrix.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="edgeHandling"/> is invalid, or <paramref name="position"/> has non-finite components.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="source"/> is empty.</exception>
    public static T SampleNearest<T>(
        this IReadOnlyMatrix<T> source,
        Vector2D position,
        EdgeHandling edgeHandling = EdgeHandling.EXTEND)
        where T : struct =>
        source.SampleNearest(position.X, position.Y, edgeHandling);

    private static void ValidateSampleInputs(
        IUntypedMatrix source,
        double x,
        double y,
        EdgeHandling edgeHandling)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!Enum.IsDefined(edgeHandling))
            throw new ArgumentOutOfRangeException(nameof(edgeHandling), edgeHandling, "Unsupported edge handling mode.");

        if (source.Count == 0)
            throw new ArgumentException("Cannot sample from an empty matrix.", nameof(source));

        if (!double.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x), x, "Coordinate X must be finite.");

        if (!double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(y), y, "Coordinate Y must be finite.");
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
