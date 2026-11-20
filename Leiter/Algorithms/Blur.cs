namespace Leiter.Algorithms;

using System.Numerics;
using Leiter.Algorithms.Internal;
using Leiter.Core;

/// <summary>
/// Provides algorithms to blur an input image.
/// </summary>
public static class Blur
{
    /// <summary>
    /// Represents a method that applies a one-dimensional filter to a line of SIMD vector samples in place.
    /// </summary>
    /// <param name="line">The line of SIMD vectors to filter in place.</param>
    private delegate void LineFilter1D(Span<Vector<double>> line);

    /// <summary>
    /// Performs a Gaussian blur of the specified image using a single standard deviation <paramref name="sigma"/>,
    /// returning an unchanged clone when <paramref name="sigma"/> is <c>0</c>, an exact separable kernel blur when <paramref name="sigma"/> &lt;= <c>2.0</c>,
    /// and a constant-time Young–van Vliet recursive blur for larger <paramref name="sigma"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input and output matrices.</typeparam>
    /// <param name="image">The image to blur.</param>
    /// <param name="sigma">The non-negative Gaussian standard deviation.</param>
    /// <returns>A new matrix containing the smoothed image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sigma"/> is negative or non-finite.</exception>
    public static Matrix<T> GaussianBlur<T>(IReadOnlyMatrix<T> image, double sigma)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(image);
        Guard.ThrowIfNotFiniteNonNegative(sigma);

        if (image.Width == 0 || image.Height == 0 || sigma == 0.0)
            return image.Clone();

        return sigma <= 2.0
            ? GaussianBlur(image, sigma, Math.Max(1, (int)Math.Ceiling(2.5 * sigma)))
            : GaussianBlurRecursive(image, sigma);
    }

    /// <summary>
    /// Performs a Gaussian blur of the specified image using a separable kernel with a given sigma value and radius.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input and output matrices.</typeparam>
    /// <param name="image">The image to blur.</param>
    /// <param name="sigma">The standard deviation of the Gaussian distribution. This value controls the amount of blur applied by the filter.</param>
    /// <param name="radius">The radius of the Gaussian blur kernel. The kernel size is <c>2 * radius + 1</c>.</param>
    /// <returns>A new matrix containing the result of the Gaussian blur.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is negative or <paramref name="sigma"/> is non-finite or not positive.</exception>
    public static Matrix<T> GaussianBlur<T>(IReadOnlyMatrix<T> image, double sigma, int radius)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        Guard.ThrowIfNotFinitePositive(sigma);

        if (image.Width == 0 || image.Height == 0 || radius == 0)
            return image.Clone();

        double[] kernel = CreateGaussianKernel1D(sigma, radius);
        return image.ConvolveSeparable(kernel, kernel);
    }

    /// <summary>
    /// Performs a separable 1D Box Blur of the specified image using a kernel with a configurable radius.
    /// </summary>
    /// <remarks>
    /// Performs a separable horizontal and vertical uniform convolution with clamp-to-edge border handling.
    /// The effective 2D kernel size is <c>(radius * 2 + 1) x (radius * 2 + 1)</c>.
    /// </remarks>
    /// <typeparam name="T">The pixel type of the input and output matrices.</typeparam>
    /// <param name="image">The image to blur.</param>
    /// <param name="radius">The radius of the box blur kernel. This value controls the area from where blur samples are taken.</param>
    /// <returns>A new matrix containing the result of the box blur.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is negative.</exception>
    public static Matrix<T> BoxBlur<T>(IReadOnlyMatrix<T> image, int radius)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);

        if (image.Width == 0 || image.Height == 0 || radius == 0)
            return image.Clone();

        int kernelDimension = radius * 2 + 1;
        var kernel = new double[kernelDimension];
        Array.Fill(kernel, 1.0 / kernelDimension);
        return image.ConvolveSeparable(kernel, kernel);
    }

    /// <summary>
    /// Performs a constant-time O(W · H) recursive IIR Gaussian blur using the Young–van Vliet filter
    /// with exact Triggs–Sdika boundary conditions.
    /// </summary>
    /// <remarks>
    /// Complexity is independent of <paramref name="sigma"/>, making this well suited for large-sigma smoothing
    /// (for <paramref name="sigma"/> &lt; 0.5, falls back to exact separable finite-impulse-response Gaussian blur).
    /// </remarks>
    /// <typeparam name="T">The pixel type of the input and output matrices.</typeparam>
    /// <param name="image">The image to blur.</param>
    /// <param name="sigma">The standard deviation of the Gaussian distribution.</param>
    /// <returns>A new matrix containing the smoothed image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sigma"/> is non-finite or not positive.</exception>
    public static Matrix<T> GaussianBlurRecursive<T>(IReadOnlyMatrix<T> image, double sigma)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(image);
        Guard.ThrowIfNotFinitePositive(sigma);

        if (image.Width == 0 || image.Height == 0)
            return image.Clone();

        if (sigma < 0.5)
            return GaussianBlur(image, sigma, Math.Max(1, (int)Math.Ceiling(3.0 * sigma)));

        var filter = YoungVanVlietFilter.Create(sigma);
        return ApplySeparable1D(image, filter.FilterLine);
    }

    /// <summary>
    /// Applies a separable 1D line filter across an image using a parallel two-pass (row-then-column) approach.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input and output matrices.</typeparam>
    /// <param name="image">The image to filter.</param>
    /// <param name="filterLine">The 1D line filter delegate applied to each row and column.</param>
    /// <returns>A new matrix containing the filtered result.</returns>
    private static Matrix<T> ApplySeparable1D<T>(IReadOnlyMatrix<T> image, LineFilter1D filterLine)
        where T : struct, IPixel<T>
    {
        int width = image.Width;
        int height = image.Height;
        Vector<double>[] buffer = MatrixMath.VectorizeMatrix(image);

        Parallel.For(0, height, y =>
        {
            filterLine(buffer.AsSpan(y * width, width));
        });

        var result = new SequentialMatrix<T>(width, height);

        Parallel.For(
            0,
            width,
            () => new Vector<double>[height],
            (x, _, col) =>
            {
                for (int y = 0; y < height; y++)
                    col[y] = buffer[y * width + x];

                filterLine(col);

                for (int y = 0; y < height; y++)
                    result[x, y] = T.FromDoubleVector(col[y]);

                return col;
            },
            static _ => { });

        return result;
    }

    private static double[] CreateGaussianKernel1D(double sigma, int radius)
    {
        int kernelDimension = radius * 2 + 1;
        var kernel = new double[kernelDimension];
        double twoSigmaSq = 2.0 * sigma * sigma;
        double sum = 0.0;

        for (int i = -radius; i <= radius; i++)
        {
            double value = Math.Exp(-(i * i) / twoSigmaSq);
            kernel[i + radius] = value;
            sum += value;
        }

        for (int i = 0; i < kernelDimension; i++)
            kernel[i] /= sum;

        return kernel;
    }
}
