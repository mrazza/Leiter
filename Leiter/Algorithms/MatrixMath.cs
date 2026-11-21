namespace Leiter.Algorithms;

using System.Runtime.Intrinsics;
using Leiter.Algorithms.Internal;
using Leiter.Core;

/// <summary>
/// Provides element-wise, inner-product, and convolution operations on matrices.
/// </summary>
public static class MatrixMath
{
    private interface IFrobeniusMultiplier<in TRight>
        where TRight : struct
    {
        static abstract Vector256<double> Multiply(Vector256<double> left, TRight right);
    }

    private readonly struct PixelVectorMultiplier<T> : IFrobeniusMultiplier<T>
        where T : struct, IPixel<T>
    {
        public static Vector256<double> Multiply(Vector256<double> left, T right) =>
            left * right.ToDoubleVector();
    }

    private readonly struct ScalarMultiplier<R> : IFrobeniusMultiplier<R>
        where R : struct, IScalar<R>
    {
        public static Vector256<double> Multiply(Vector256<double> left, R right) =>
            left * right.AsDouble();
    }
    /// <summary>
    /// Multiplies two matrices using standard matrix multiplication.
    /// </summary>
    /// <typeparam name="T">The numeric element type of both matrices.</typeparam>
    /// <param name="self">The left-hand matrix.</param>
    /// <param name="right">The right-hand matrix whose height matches <paramref name="self"/>'s width.</param>
    /// <returns>A new <see cref="Matrix{T}"/> containing the matrix product.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="self"/> or <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The number of columns in <paramref name="self"/> does not equal the number of rows in <paramref name="right"/>.</exception>
    public static Matrix<T> Multiply<T>(this IReadOnlyMatrix<T> self, IReadOnlyMatrix<T> right)
        where T : struct, ISelfOperable<T>
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(right);

        if (self.Width != right.Height)
            throw new ArgumentException("Right hand side matrix must have height equal to width of the left hand side matrix.", nameof(right));

        int selfHeight = self.Height;
        int rightWidth = right.Width;
        int innerLength = self.Width;
        var result = new SequentialMatrix<T>(rightWidth, selfHeight);
        ParallelRunner.For(selfHeight, CancellationToken.None, y =>
        {
            for (int x = 0; x < rightWidth; x++)
            {
                T sum = T.Zero;
                for (int k = 0; k < innerLength; k++)
                    sum = sum.Add(self[k, y].Multiply(right[x, k]));
                result[x, y] = sum;
            }
        });

        return result;
    }

    /// <summary>
    /// Computes the element-wise (Hadamard) product of two matrices of the same pixel type.
    /// </summary>
    /// <typeparam name="T">The pixel type of both matrices.</typeparam>
    /// <param name="self">The left-hand matrix.</param>
    /// <param name="right">The right-hand matrix.</param>
    /// <returns>A new matrix containing the element-wise product.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="self"/> or <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the matrices have different dimensions.</exception>
    public static Matrix<T> HadamardProduct<T>(this IReadOnlyMatrix<T> self, IReadOnlyMatrix<T> right)
        where T : struct, IPixel<T> =>
        self.Zip(right, static (leftPixel, rightPixel) => leftPixel.Multiply(rightPixel));

    /// <summary>
    /// Computes the element-wise (Hadamard) product of a pixel matrix and a scalar matrix.
    /// </summary>
    /// <typeparam name="T">The pixel type of the left-hand matrix.</typeparam>
    /// <typeparam name="R">The scalar pixel type of the right-hand matrix.</typeparam>
    /// <param name="self">The left-hand matrix.</param>
    /// <param name="right">The right-hand scalar matrix.</param>
    /// <returns>A new matrix containing the scaled elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="self"/> or <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the matrices have different dimensions.</exception>
    public static Matrix<T> HadamardProduct<T, R>(this IReadOnlyMatrix<T> self, IReadOnlyMatrix<R> right)
        where T : struct, IPixel<T>
        where R : struct, IScalar<R> =>
        self.Zip(right, static (leftPixel, rightScalar) => leftPixel.Multiply(rightScalar.AsDouble()));

    /// <summary>
    /// Computes the element-wise sum of two matrices of the same type.
    /// </summary>
    /// <typeparam name="T">The element type of both matrices.</typeparam>
    /// <param name="self">The left-hand matrix.</param>
    /// <param name="right">The right-hand matrix.</param>
    /// <returns>A new matrix containing the element-wise sums.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="self"/> or <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the matrices have different dimensions.</exception>
    public static Matrix<T> Add<T>(this IReadOnlyMatrix<T> self, IReadOnlyMatrix<T> right)
        where T : struct, ISelfOperable<T> =>
        self.Zip(right, static (leftPixel, rightPixel) => leftPixel.Add(rightPixel));

    /// <summary>
    /// Multiplies every element of a matrix by a finite scalar factor.
    /// </summary>
    /// <typeparam name="T">The numeric element type of the matrix.</typeparam>
    /// <param name="self">The source matrix.</param>
    /// <param name="factor">The finite scalar multiplier.</param>
    /// <returns>A new matrix containing the scaled elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="self"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="factor"/> is not finite.</exception>
    public static Matrix<T> Scale<T>(this IReadOnlyMatrix<T> self, double factor)
        where T : struct, INumericOperable<T>
    {
        ArgumentNullException.ThrowIfNull(self);
        Guard.ThrowIfNotFinite(factor);
        return self.Map(pixel => pixel.Multiply(factor));
    }

    internal static void AddInPlace<T>(this Matrix<T> self, IReadOnlyMatrix<T> right)
        where T : struct, ISelfOperable<T>
    {
        Guard.ThrowIfSizeMismatch(self, right);

        int count = self.Count;
        if (count == 0)
            return;

        if (self.TryGetRawArray() is T[] leftRaw && right.TryGetRawArray() is T[] rightRaw)
        {
            ParallelRunner.ForChunks(
                count,
                CancellationToken.None,
                (start, end) =>
                {
                    for (int i = start; i < end; i++)
                        leftRaw[i] = leftRaw[i].Add(rightRaw[i]);
                });
            return;
        }

        int width = self.Width;
        ParallelRunner.ForRows(
            self.Size,
            CancellationToken.None,
            (startRow, endRow) =>
            {
                for (int y = startRow; y < endRow; y++)
                {
                    for (int x = 0; x < width; x++)
                        self[x, y] = self[x, y].Add(right[x, y]);
                }
            });
    }

    internal static void ScaleInPlace<T>(this Matrix<T> self, double factor)
        where T : struct, INumericOperable<T>
    {
        ArgumentNullException.ThrowIfNull(self);
        Guard.ThrowIfNotFinite(factor);

        int count = self.Count;
        if (count == 0)
            return;

        if (self.TryGetRawArray() is T[] raw)
        {
            ParallelRunner.ForChunks(
                count,
                CancellationToken.None,
                (start, end) =>
                {
                    for (int i = start; i < end; i++)
                        raw[i] = raw[i].Multiply(factor);
                });
            return;
        }

        int width = self.Width;
        ParallelRunner.ForRows(
            self.Size,
            CancellationToken.None,
            (startRow, endRow) =>
            {
                for (int y = startRow; y < endRow; y++)
                {
                    for (int x = 0; x < width; x++)
                        self[x, y] = self[x, y].Multiply(factor);
                }
            });
    }

    /// <summary>
    /// Computes the Frobenius inner product of two matrices of the same pixel type.
    /// </summary>
    /// <typeparam name="T">The pixel type of both matrices.</typeparam>
    /// <param name="self">The left-hand matrix.</param>
    /// <param name="right">The right-hand matrix.</param>
    /// <returns>The channel-wise sum of element-wise products as <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="self"/> or <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the matrices have different dimensions.</exception>
    public static T FrobeniusProduct<T>(this IReadOnlyMatrix<T> self, IReadOnlyMatrix<T> right)
        where T : struct, IPixel<T> =>
        FrobeniusCore<IReadOnlyMatrix<T>, T, T, PixelVectorMultiplier<T>>(self, right);

    /// <summary>
    /// Computes the Frobenius inner product of a <see cref="MatrixView{T}"/> and a matrix of the same pixel type without boxing the view.
    /// </summary>
    /// <typeparam name="T">The pixel type of both matrices.</typeparam>
    /// <param name="self">The left-hand matrix view.</param>
    /// <param name="right">The right-hand matrix.</param>
    /// <returns>The channel-wise sum of element-wise products as <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the matrices have different dimensions.</exception>
    public static T FrobeniusProduct<T>(this MatrixView<T> self, IReadOnlyMatrix<T> right)
        where T : struct, IPixel<T> =>
        FrobeniusCore<MatrixView<T>, T, T, PixelVectorMultiplier<T>>(self, right);

    /// <summary>
    /// Computes the Frobenius inner product of a pixel matrix and a scalar matrix.
    /// </summary>
    /// <typeparam name="T">The pixel type of the left-hand matrix.</typeparam>
    /// <typeparam name="R">The scalar pixel type of the right-hand matrix.</typeparam>
    /// <param name="self">The left-hand matrix.</param>
    /// <param name="right">The right-hand scalar matrix.</param>
    /// <returns>The weighted sum of elements as <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="self"/> or <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the matrices have different dimensions.</exception>
    public static T FrobeniusProduct<T, R>(this IReadOnlyMatrix<T> self, IReadOnlyMatrix<R> right)
        where T : struct, IPixel<T>
        where R : struct, IScalar<R> =>
        FrobeniusCore<IReadOnlyMatrix<T>, T, R, ScalarMultiplier<R>>(self, right);

    /// <summary>
    /// Computes the Frobenius inner product of a <see cref="MatrixView{T}"/> and a scalar matrix without boxing the view.
    /// </summary>
    /// <typeparam name="T">The pixel type of the left-hand matrix view.</typeparam>
    /// <typeparam name="R">The scalar pixel type of the right-hand matrix.</typeparam>
    /// <param name="self">The left-hand matrix view.</param>
    /// <param name="right">The right-hand scalar matrix.</param>
    /// <returns>The weighted sum of elements as <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the matrices have different dimensions.</exception>
    public static T FrobeniusProduct<T, R>(this MatrixView<T> self, IReadOnlyMatrix<R> right)
        where T : struct, IPixel<T>
        where R : struct, IScalar<R> =>
        FrobeniusCore<MatrixView<T>, T, R, ScalarMultiplier<R>>(self, right);

    /// <summary>
    /// Convolves a matrix with a 2D scalar kernel using clamp-to-edge border handling.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <typeparam name="R">The scalar pixel type of the kernel.</typeparam>
    /// <param name="self">The matrix to convolve.</param>
    /// <param name="kernel">The 2D convolution kernel.</param>
    /// <returns>A new matrix containing the convolved result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="self"/> or <paramref name="kernel"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="kernel"/> has non-positive dimensions.</exception>
    public static Matrix<T> Convolve<T, R>(this IReadOnlyMatrix<T> self, IReadOnlyMatrix<R> kernel)
        where T : struct, IPixel<T>
        where R : struct, IScalar<R>
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(kernel);

        if (kernel.Width <= 0 || kernel.Height <= 0)
            throw new ArgumentException("Kernel dimensions must be positive.", nameof(kernel));

        int width = self.Width;
        int height = self.Height;
        var result = new SequentialMatrix<T>(width, height);
        if (width == 0 || height == 0)
            return result;

        int halfX = kernel.Width / 2;
        int halfY = kernel.Height / 2;

        ParallelRunner.For(height, CancellationToken.None, y =>
        {
            for (int x = 0; x < width; x++)
            {
                var view = new MatrixView<T>(self, x - halfX, y - halfY, kernel.Width, kernel.Height, EdgeHandling.Extend);
                result[x, y] = view.FrobeniusProduct(kernel);
            }
        });

        return result;
    }

    /// <summary>
    /// Convolves a matrix with separable 1D horizontal and vertical kernels using clamp-to-edge border handling.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <param name="self">The matrix to convolve.</param>
    /// <param name="horizontalKernel">The 1D horizontal kernel weights.</param>
    /// <param name="verticalKernel">The 1D vertical kernel weights.</param>
    /// <returns>A new matrix containing the convolved result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="self"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when either kernel is empty.</exception>
    public static Matrix<T> ConvolveSeparable<T>(
        this IReadOnlyMatrix<T> self,
        ReadOnlySpan<double> horizontalKernel,
        ReadOnlySpan<double> verticalKernel)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(self);
        return SeparableProcessor.Convolve(self, horizontalKernel, verticalKernel);
    }

    private static T FrobeniusCore<TLeft, T, TRight, TMultiplier>(TLeft self, IReadOnlyMatrix<TRight> right)
        where TLeft : IReadOnlyMatrix<T>
        where T : struct, IPixel<T>
        where TRight : struct
        where TMultiplier : struct, IFrobeniusMultiplier<TRight>
    {
        if (self is null)
            throw new ArgumentNullException(nameof(self));
        ArgumentNullException.ThrowIfNull(right);
        Guard.ThrowIfSizeMismatch(self.Size, right.Size, nameof(right));

        Vector256<double> sum = Vector256<double>.Zero;
        int width = self.Width;
        int height = self.Height;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                sum += TMultiplier.Multiply(self[x, y].ToDoubleVector(), right[x, y]);
        }

        return T.FromDoubleVector(sum);
    }
}
