namespace Leiter.Algorithms;

using System.Runtime.Intrinsics;
using Leiter.Core;

/// <summary>
/// Provides element-wise, inner-product, and convolution operations on matrices.
/// </summary>
public static class MatrixMath
{
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
        Parallel.For(0, selfHeight, y =>
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
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(self);
        Guard.ThrowIfSizeMismatch(self, right);
        return self.Zip(right, static (leftPixel, rightPixel) => leftPixel.Multiply(rightPixel));
    }

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
        where R : struct, IScalar<R>
    {
        ArgumentNullException.ThrowIfNull(self);
        Guard.ThrowIfSizeMismatch(self, right);
        return self.Zip(right, static (leftPixel, rightScalar) => leftPixel.Multiply(rightScalar.AsDouble()));
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
        FrobeniusCore<IReadOnlyMatrix<T>, T>(self, right);

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
        FrobeniusCore<MatrixView<T>, T>(self, right);

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
        FrobeniusScalarCore<IReadOnlyMatrix<T>, T, R>(self, right);

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
        FrobeniusScalarCore<MatrixView<T>, T, R>(self, right);

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

        Parallel.For(0, height, y =>
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

        if (horizontalKernel.IsEmpty)
            throw new ArgumentException("Horizontal kernel must not be empty.", nameof(horizontalKernel));
        if (verticalKernel.IsEmpty)
            throw new ArgumentException("Vertical kernel must not be empty.", nameof(verticalKernel));

        int width = self.Width;
        int height = self.Height;
        var result = new SequentialMatrix<T>(width, height);
        if (width == 0 || height == 0)
            return result;

        double[] hKernel = horizontalKernel.ToArray();
        double[] vKernel = verticalKernel.ToArray();
        int hLen = hKernel.Length;
        int vLen = vKernel.Length;
        int halfH = hLen / 2;
        int halfV = vLen / 2;

        var sourceVectors = VectorizeMatrix(self);
        var temp = new Vector256<double>[width * height];

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                Vector256<double> sum = Vector256<double>.Zero;
                for (int kx = 0; kx < hLen; kx++)
                    sum += sourceVectors[rowOffset + Math.Clamp(x - halfH + kx, 0, width - 1)] * hKernel[kx];
                temp[rowOffset + x] = sum;
            }
        });

        Parallel.For(0, height, y =>
        {
            Span<int> rowOffsets = vLen <= 256 ? stackalloc int[vLen] : new int[vLen];
            for (int ky = 0; ky < vLen; ky++)
                rowOffsets[ky] = Math.Clamp(y - halfV + ky, 0, height - 1) * width;

            for (int x = 0; x < width; x++)
            {
                Vector256<double> sum = Vector256<double>.Zero;
                for (int ky = 0; ky < vLen; ky++)
                    sum += temp[rowOffsets[ky] + x] * vKernel[ky];
                result[x, y] = T.FromDoubleVector(sum);
            }
        });

        return result;
    }

    /// <summary>
    /// Computes the Frobenius inner product of two matrices of the same pixel type using vectorized accumulation.
    /// </summary>
    /// <typeparam name="TLeft">The matrix type of the left-hand operand.</typeparam>
    /// <typeparam name="T">The pixel type of both matrices.</typeparam>
    /// <param name="self">The left-hand matrix operand.</param>
    /// <param name="right">The right-hand matrix operand.</param>
    /// <returns>The channel-wise sum of element-wise products as <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="self"/> or <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the matrices have different dimensions.</exception>
    private static T FrobeniusCore<TLeft, T>(TLeft self, IReadOnlyMatrix<T> right)
        where TLeft : IReadOnlyMatrix<T>
        where T : struct, IPixel<T>
    {
        if (self is null)
            throw new ArgumentNullException(nameof(self));
        ArgumentNullException.ThrowIfNull(right);
        Guard.ThrowIfSizeMismatch(self.Size, right.Size, nameof(right));

        Vector256<double> sum = Vector256<double>.Zero;
        int width = self.Width;
        int height = self.Height;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                sum += self[x, y].ToDoubleVector() * right[x, y].ToDoubleVector();

        return T.FromDoubleVector(sum);
    }

    /// <summary>
    /// Computes the Frobenius inner product of a pixel matrix and a scalar matrix using vectorized accumulation.
    /// </summary>
    /// <typeparam name="TLeft">The matrix type of the left-hand operand.</typeparam>
    /// <typeparam name="T">The pixel type of the left-hand matrix.</typeparam>
    /// <typeparam name="R">The scalar pixel type of the right-hand matrix.</typeparam>
    /// <param name="self">The left-hand matrix operand.</param>
    /// <param name="right">The right-hand scalar matrix operand.</param>
    /// <returns>The weighted sum of elements as <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="self"/> or <paramref name="right"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the matrices have different dimensions.</exception>
    private static T FrobeniusScalarCore<TLeft, T, R>(TLeft self, IReadOnlyMatrix<R> right)
        where TLeft : IReadOnlyMatrix<T>
        where T : struct, IPixel<T>
        where R : struct, IScalar<R>
    {
        if (self is null)
            throw new ArgumentNullException(nameof(self));
        ArgumentNullException.ThrowIfNull(right);
        Guard.ThrowIfSizeMismatch(self.Size, right.Size, nameof(right));

        Vector256<double> sum = Vector256<double>.Zero;
        int width = self.Width;
        int height = self.Height;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                sum += self[x, y].ToDoubleVector() * right[x, y].AsDouble();

        return T.FromDoubleVector(sum);
    }

    /// <summary>
    /// Converts a matrix of pixels into a flattened array of double-precision SIMD vectors.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input matrix.</typeparam>
    /// <param name="matrix">The matrix to vectorize.</param>
    /// <returns>A flattened array of SIMD vectors corresponding to the matrix pixels.</returns>
    internal static Vector256<double>[] VectorizeMatrix<T>(IReadOnlyMatrix<T> matrix)
        where T : struct, IPixel<T> =>
        matrix.MapToArray(static pixel => pixel.ToDoubleVector());
}
