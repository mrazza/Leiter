namespace Leiter.Algorithms;

using System;
using System.Threading.Tasks;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Provides spatial gradient computation operations using convolution kernels such as Sobel and Scharr.
/// </summary>
public static class SpatialGradients
{
    /// <summary>
    /// Computes spatial gradient vectors for a scalar image using the specified gradient operator.
    /// </summary>
    /// <param name="source">The source scalar image matrix.</param>
    /// <param name="gradientOperator">The gradient operator kernel to use.</param>
    /// <returns>A new <see cref="Matrix{Vector2D}"/> containing horizontal and vertical derivative components per pixel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="gradientOperator"/> is not a defined <see cref="GradientOperator"/> value.</exception>
    public static Matrix<Vector2D> Compute(
        IReadOnlyMatrix<DoublePixel> source,
        GradientOperator gradientOperator = GradientOperator.Sobel)
    {
        ArgumentNullException.ThrowIfNull(source);
        var (w0, w1, w2, scale) = GetKernelWeights(gradientOperator);

        int width = source.Width;
        int height = source.Height;
        var result = new SequentialMatrix<Vector2D>(width, height);
        if (width == 0 || height == 0)
            return result;

        Parallel.For(0, height, y =>
        {
            int ym1 = Math.Clamp(y - 1, 0, height - 1);
            int y0 = y;
            int yp1 = Math.Clamp(y + 1, 0, height - 1);

            for (int x = 0; x < width; x++)
            {
                int xm1 = Math.Clamp(x - 1, 0, width - 1);
                int xp1 = Math.Clamp(x + 1, 0, width - 1);

                var (gx, gy) = ComputeGradientAt(
                    source[xm1, ym1].Value, source[x, ym1].Value, source[xp1, ym1].Value,
                    source[xm1, y0].Value,                        source[xp1, y0].Value,
                    source[xm1, yp1].Value, source[x, yp1].Value, source[xp1, yp1].Value,
                    w0, w1, w2, scale);

                result[x, y] = new Vector2D(gx, gy);
            }
        });

        return result;
    }

    /// <summary>
    /// Computes spatial gradient vectors from the Rec. 601 normalized luminance of an RGB image.
    /// </summary>
    /// <param name="image">The source RGB image.</param>
    /// <param name="gradientOperator">The gradient operator kernel to use.</param>
    /// <returns>A new <see cref="Matrix{Vector2D}"/> containing luminance gradient vectors per pixel.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="gradientOperator"/> is not a defined <see cref="GradientOperator"/> value.</exception>
    public static Matrix<Vector2D> ComputeLuminanceGradients(
        IReadOnlyMatrix<Rgb8> image,
        GradientOperator gradientOperator = GradientOperator.Sobel)
    {
        ArgumentNullException.ThrowIfNull(image);
        Guard.ThrowIfUndefinedEnum(gradientOperator);

        int width = image.Width;
        int height = image.Height;
        if (width == 0 || height == 0)
            return new SequentialMatrix<Vector2D>(image.Size);

        Matrix<DoublePixel> luma = image.Map(static p => new DoublePixel(p.GetLuma()));
        return Compute(luma, gradientOperator);
    }

    /// <summary>
    /// Computes spatial color gradients and Frobenius gradient magnitude for an RGB image.
    /// </summary>
    /// <remarks>
    /// <c>GradientX</c> and <c>GradientY</c> store signed per-channel spatial derivatives <c>(∂R, ∂G, ∂B)</c> of normalized <c>[0, 1]</c>
    /// RGB channels in <see cref="Rgb64"/> (channel components may be negative).
    /// </remarks>
    /// <param name="image">The source RGB image.</param>
    /// <param name="gradientOperator">The gradient operator kernel to use.</param>
    /// <returns>A tuple containing the horizontal RGB derivative matrix, vertical RGB derivative matrix, and Frobenius magnitude matrix.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="gradientOperator"/> is not a defined <see cref="GradientOperator"/> value.</exception>
    public static (Matrix<Rgb64> GradientX, Matrix<Rgb64> GradientY, Matrix<DoublePixel> Magnitude) ComputeColorGradients(
        IReadOnlyMatrix<Rgb8> image,
        GradientOperator gradientOperator = GradientOperator.Sobel)
    {
        ArgumentNullException.ThrowIfNull(image);
        var (w0, w1, w2, scale) = GetKernelWeights(gradientOperator);

        int width = image.Width;
        int height = image.Height;
        var gradX = new SequentialMatrix<Rgb64>(width, height);
        var gradY = new SequentialMatrix<Rgb64>(width, height);
        var magnitude = new SequentialMatrix<DoublePixel>(width, height);

        if (width == 0 || height == 0)
            return (gradX, gradY, magnitude);

        double normalizedScale = scale / 255.0;
        Parallel.For(0, height, y =>
        {
            int ym1 = Math.Clamp(y - 1, 0, height - 1);
            int y0 = y;
            int yp1 = Math.Clamp(y + 1, 0, height - 1);

            for (int x = 0; x < width; x++)
            {
                int xm1 = Math.Clamp(x - 1, 0, width - 1);
                int xp1 = Math.Clamp(x + 1, 0, width - 1);

                var (gx, gy) = ComputeRgb8GradientAt(
                    image[xm1, ym1], image[x, ym1], image[xp1, ym1],
                    image[xm1, y0],                 image[xp1, y0],
                    image[xm1, yp1], image[x, yp1], image[xp1, yp1],
                    w0, w1, w2, normalizedScale);

                gradX[x, y] = gx;
                gradY[x, y] = gy;

                magnitude[x, y] = Math.Sqrt(gx.NormSquared() + gy.NormSquared());
            }
        });

        return (gradX, gradY, magnitude);
    }

    internal static (Rgb64 Gx, Rgb64 Gy) ComputeRgb8GradientAt(
        Rgb8 vM1M1, Rgb8 v0M1, Rgb8 vP1M1,
        Rgb8 vM10,            Rgb8 vP10,
        Rgb8 vM1P1, Rgb8 v0P1, Rgb8 vP1P1,
        double w0, double w1, double w2, double normalizedScale)
    {
        var (gxR, gyR) = ComputeGradientAt(
            vM1M1.R, v0M1.R, vP1M1.R,
            vM10.R,          vP10.R,
            vM1P1.R, v0P1.R, vP1P1.R,
            w0, w1, w2, normalizedScale);

        var (gxG, gyG) = ComputeGradientAt(
            vM1M1.G, v0M1.G, vP1M1.G,
            vM10.G,          vP10.G,
            vM1P1.G, v0P1.G, vP1P1.G,
            w0, w1, w2, normalizedScale);

        var (gxB, gyB) = ComputeGradientAt(
            vM1M1.B, v0M1.B, vP1M1.B,
            vM10.B,          vP10.B,
            vM1P1.B, v0P1.B, vP1P1.B,
            w0, w1, w2, normalizedScale);

        return (new Rgb64(gxR, gxG, gxB), new Rgb64(gyR, gyG, gyB));
    }

    /// <summary>
    /// Computes the Euclidean norm (length) of an RGB derivative vector across color channels.
    /// </summary>
    /// <param name="derivative">The RGB derivative value.</param>
    /// <returns>The Euclidean norm of the color channels.</returns>
    public static double ChannelNorm(Rgb64 derivative) =>
        derivative.Norm();

    internal static (double W0, double W1, double W2, double Scale) GetKernelWeights(GradientOperator gradientOperator) =>
        gradientOperator switch
        {
            GradientOperator.Sobel => (1.0, 2.0, 1.0, 1.0 / 8.0),
            GradientOperator.Scharr => (3.0, 10.0, 3.0, 1.0 / 32.0),
            _ => throw new ArgumentOutOfRangeException(nameof(gradientOperator), gradientOperator, "Unsupported gradient operator.")
        };

    private static (double Gx, double Gy) ComputeGradientAt(
        double vM1M1, double v0M1, double vP1M1,
        double vM10,              double vP10,
        double vM1P1, double v0P1, double vP1P1,
        double w0, double w1, double w2, double scale)
    {
        double gx = ((w0 * (vP1M1 - vM1M1)) + (w1 * (vP10 - vM10)) + (w2 * (vP1P1 - vM1P1))) * scale;
        double gy = ((w0 * (vM1P1 - vM1M1)) + (w1 * (v0P1 - v0M1)) + (w2 * (vP1P1 - vP1M1))) * scale;
        return (gx, gy);
    }
}
