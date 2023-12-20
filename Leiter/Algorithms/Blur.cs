namespace Leiter.Algorithms;

using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Provides algorithms to blur an input image.
/// </summary>
public static class Blur
{
    /// <summary>
    /// Performs a Gaussian Blur of the specified image using a kernel with a given sigma value and the configured radius.
    /// </summary>
    /// <typeparam name="T">The pixel type of the input and output matrices.</typeparam>
    /// <param name="image">The image to blur.</param>
    /// <param name="sigma">The standard deviation of the Gaussian distribution. This value controls the amount of blur applied by the filter.</param>
    /// <param name="radius">The radius of the box blur kernel. This value controls the area from where blur samples are taken.</param>
    /// <returns>A new matrix containing the result of the gaussian blur.</returns>
    public static Matrix<T> GaussianBlur<T>(Matrix<T> image, double sigma, int radius)
        where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
    {
        int kernelDimension = radius * 2 + 1;
        var kernel = new SequentialMatrix<DoublePixel>(kernelDimension, kernelDimension);
        double sum = 0.0;
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                var value = Math.Exp(-(x * x + y * y) /
                                      (2 * sigma * sigma));
                kernel[x + radius, y + radius] = value;
                sum += value;
            }
        }

        for (int x = 0; x < kernelDimension; ++x)
            for (int y = 0; y < kernelDimension; ++y)
                kernel[x, y] /= sum;

        return image.Convolve<T, DoublePixel, double>(kernel);
    }

    /// <summary>
    /// Performs a Box Blur of the specified image using a kernel with a configurable radius.
    /// </summary>
    /// <remarks>
    /// Performs a convolution of the input matrix with a blox blur kernel of size determined
    /// by the radius argument. The kernel will be (radius * 2 + 1) x (radius * 2 + 1).
    /// </remarks>
    /// <typeparam name="T">The pixel type of the input and output matrices.</typeparam>
    /// <param name="image">The image to blur.</param>
    /// <param name="radius">The radius of the box blur kernel. This value controls the area from where blur samples are taken.</param>
    /// <returns>A new matrix containing the result of the blox blur.</returns>
    public static Matrix<T> BoxBlur<T>(Matrix<T> image, int radius)
        where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
    {
        var kernelDimension = radius * 2 + 1;
        var kernel = new SequentialMatrix<DoublePixel>(kernelDimension, kernelDimension);
        kernel.SetAll(1.0 / (kernelDimension * kernelDimension));
        return image.Convolve<T, DoublePixel, double>(kernel);
    }
}