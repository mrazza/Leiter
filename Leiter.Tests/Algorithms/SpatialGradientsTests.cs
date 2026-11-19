namespace Leiter.Tests.Algorithms;

using System;
using Leiter.Algorithms;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SpatialGradients"/> and <see cref="GradientOperator"/>.
/// </summary>
public class SpatialGradientsTests
{
    /// <summary>
    /// Verifies that both Sobel and Scharr operators recover the exact analytical derivative of a linear ramp.
    /// </summary>
    [Fact]
    public void Compute_LinearRamp_SobelAndScharr_ShouldProduceExactGradients()
    {
        const double a = 3.5;
        const double b = -2.0;

        SequentialMatrix<DoublePixel> source = TestMatrices.Create(5, 5, (x, y) => new DoublePixel((a * x) + (b * y)));

        Matrix<Vector2D> sobel = SpatialGradients.Compute(source, GradientOperator.Sobel);
        Matrix<Vector2D> scharr = SpatialGradients.Compute(source, GradientOperator.Scharr);

        Assert.Equal(a, sobel[2, 2].X, 9);
        Assert.Equal(b, sobel[2, 2].Y, 9);

        Assert.Equal(a, scharr[2, 2].X, 9);
        Assert.Equal(b, scharr[2, 2].Y, 9);
    }

    /// <summary>
    /// Verifies that computing gradients on a constant image yields zero vectors everywhere.
    /// </summary>
    [Fact]
    public void Compute_ConstantImage_ShouldProduceZeroGradients()
    {
        SequentialMatrix<DoublePixel> source = TestMatrices.Constant(4, 4, new DoublePixel(42.0));

        Matrix<Vector2D> sobel = SpatialGradients.Compute(source, GradientOperator.Sobel);
        Matrix<Vector2D> scharr = SpatialGradients.Compute(source, GradientOperator.Scharr);

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                Assert.Equal(0.0, sobel[x, y].X, 9);
                Assert.Equal(0.0, sobel[x, y].Y, 9);
                Assert.Equal(0.0, scharr[x, y].X, 9);
                Assert.Equal(0.0, scharr[x, y].Y, 9);
            }
        }
    }

    /// <summary>
    /// Verifies that empty matrices return empty gradient matrices matching the source dimensions.
    /// </summary>
    [Fact]
    public void Compute_EmptyMatrix_ShouldReturnEmptyMatrix()
    {
        var empty00 = new SequentialMatrix<DoublePixel>(0, 0);
        var empty03 = new SequentialMatrix<DoublePixel>(0, 3);
        var empty30 = new SequentialMatrix<DoublePixel>(3, 0);

        Matrix<Vector2D> res00 = SpatialGradients.Compute(empty00);
        Matrix<Vector2D> res03 = SpatialGradients.Compute(empty03);
        Matrix<Vector2D> res30 = SpatialGradients.Compute(empty30);

        Assert.Empty(res00);
        Assert.Equal(new Size(0, 0), res00.Size);

        Assert.Empty(res03);
        Assert.Equal(new Size(0, 3), res03.Size);

        Assert.Empty(res30);
        Assert.Equal(new Size(3, 0), res30.Size);
    }

    /// <summary>
    /// Verifies that computing gradients on a single-pixel matrix yields a zero vector.
    /// </summary>
    [Fact]
    public void Compute_SinglePixel_ShouldProduceZeroGradient()
    {
        var single = new SequentialMatrix<DoublePixel>(1, 1);
        single[0, 0] = 12.34;

        Matrix<Vector2D> grad = SpatialGradients.Compute(single);

        Assert.Single(grad);
        Assert.Equal(0.0, grad[0, 0].X, 9);
        Assert.Equal(0.0, grad[0, 0].Y, 9);
    }

    /// <summary>
    /// Verifies that <see cref="SpatialGradients.Compute"/> validates arguments correctly.
    /// </summary>
    [Fact]
    public void Compute_Validation_ShouldThrowCorrectExceptions()
    {
        var valid = new SequentialMatrix<DoublePixel>(2, 2);

        Assert.Throws<ArgumentNullException>(() => SpatialGradients.Compute(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpatialGradients.Compute(valid, (GradientOperator)999));
    }

    /// <summary>
    /// Verifies that <see cref="SpatialGradients.ComputeLuminanceGradients"/> accurately computes normalized Rec. 601 luma gradients for a step-edge.
    /// </summary>
    [Fact]
    public void ComputeLuminanceGradients_StepEdge_ShouldCalculateAccurately()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        for (int y = 0; y < 4; y++)
        {
            image[0, y] = new Rgb8(0, 0, 0);
            image[1, y] = new Rgb8(0, 0, 0);
            image[2, y] = new Rgb8(255, 0, 0);
            image[3, y] = new Rgb8(255, 0, 0);
        }

        Matrix<Vector2D> sobel = SpatialGradients.ComputeLuminanceGradients(image, GradientOperator.Sobel);
        Matrix<Vector2D> scharr = SpatialGradients.ComputeLuminanceGradients(image, GradientOperator.Scharr);

        // Rec. 601 normalized luma for Rgb8(255, 0, 0) is 0.299.
        // For Sobel at (1, 1): gx = 4 * 0.299 / 8 = 0.1495, gy = 0.
        Assert.Equal(0.1495, sobel[1, 1].X, 9);
        Assert.Equal(0.0, sobel[1, 1].Y, 9);

        // For Scharr at (1, 1): gx = 16 * 0.299 / 32 = 0.1495, gy = 0.
        Assert.Equal(0.1495, scharr[1, 1].X, 9);
        Assert.Equal(0.0, scharr[1, 1].Y, 9);
    }

    /// <summary>
    /// Verifies that <see cref="SpatialGradients.ComputeLuminanceGradients"/> handles empty and single-pixel matrices.
    /// </summary>
    [Fact]
    public void ComputeLuminanceGradients_EmptyAndSinglePixel_ShouldHandleCorrectly()
    {
        var empty = new SequentialMatrix<Rgb8>(0, 0);
        Matrix<Vector2D> resEmpty = SpatialGradients.ComputeLuminanceGradients(empty);
        Assert.Empty(resEmpty);

        var single = new SequentialMatrix<Rgb8>(1, 1);
        single[0, 0] = new Rgb8(100, 150, 200);
        Matrix<Vector2D> resSingle = SpatialGradients.ComputeLuminanceGradients(single);
        Assert.Single(resSingle);
        Assert.Equal(0.0, resSingle[0, 0].X, 9);
        Assert.Equal(0.0, resSingle[0, 0].Y, 9);
    }

    /// <summary>
    /// Verifies that <see cref="SpatialGradients.ComputeLuminanceGradients"/> validates input arguments.
    /// </summary>
    [Fact]
    public void ComputeLuminanceGradients_Validation_ShouldThrowCorrectExceptions()
    {
        var valid = new SequentialMatrix<Rgb8>(2, 2);

        Assert.Throws<ArgumentNullException>(() => SpatialGradients.ComputeLuminanceGradients(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpatialGradients.ComputeLuminanceGradients(valid, (GradientOperator)999));
    }

    /// <summary>
    /// Verifies that <see cref="SpatialGradients.ComputeColorGradients"/> computes exact normalized derivatives and Frobenius magnitude on a step-edge.
    /// </summary>
    [Fact]
    public void ComputeColorGradients_StepEdgeAndChannelNorm_ShouldMatchExpected()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        for (int y = 0; y < 4; y++)
        {
            image[0, y] = new Rgb8(0, 0, 0);
            image[1, y] = new Rgb8(0, 0, 0);
            image[2, y] = new Rgb8(255, 0, 0);
            image[3, y] = new Rgb8(255, 0, 0);
        }

        var (sobelGx, sobelGy, sobelMag) = SpatialGradients.ComputeColorGradients(image, GradientOperator.Sobel);
        var (scharrGx, scharrGy, scharrMag) = SpatialGradients.ComputeColorGradients(image, GradientOperator.Scharr);

        // Step-edge normalized to 1.0: gx = 4 * 1.0 / 8 = 0.5.
        Assert.Equal(0.5, sobelGx[1, 1].R, 9);
        Assert.Equal(0.0, sobelGx[1, 1].G, 9);
        Assert.Equal(0.0, sobelGx[1, 1].B, 9);

        Assert.Equal(0.0, sobelGy[1, 1].R, 9);
        Assert.Equal(0.0, sobelGy[1, 1].G, 9);
        Assert.Equal(0.0, sobelGy[1, 1].B, 9);

        Assert.Equal(0.5, SpatialGradients.ChannelNorm(sobelGx[1, 1]), 9);
        Assert.Equal(0.0, SpatialGradients.ChannelNorm(sobelGy[1, 1]), 9);
        Assert.Equal(0.5, sobelMag[1, 1].Value, 9);

        // Scharr at (1, 1): gx = 16 * 1.0 / 32 = 0.5.
        Assert.Equal(0.5, scharrGx[1, 1].R, 9);
        Assert.Equal(0.0, scharrGx[1, 1].G, 9);
        Assert.Equal(0.0, scharrGx[1, 1].B, 9);

        Assert.Equal(0.0, scharrGy[1, 1].R, 9);
        Assert.Equal(0.5, SpatialGradients.ChannelNorm(scharrGx[1, 1]), 9);
        Assert.Equal(0.5, scharrMag[1, 1].Value, 9);
    }

    /// <summary>
    /// Verifies that <see cref="SpatialGradients.ComputeColorGradients"/> handles horizontal and vertical gradients across multiple channels simultaneously.
    /// </summary>
    [Fact]
    public void ComputeColorGradients_MultiChannelDerivatives_ShouldComputeCorrectly()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                // R steps horizontally, G steps vertically, B remains constant
                byte r = (byte)(x >= 2 ? 255 : 0);
                byte g = (byte)(y >= 2 ? 255 : 0);
                byte b = 128;
                image[x, y] = new Rgb8(r, g, b);
            }
        }

        var (gx, gy, mag) = SpatialGradients.ComputeColorGradients(image, GradientOperator.Sobel);

        Assert.Equal(0.5, gx[1, 1].R, 9);
        Assert.Equal(0.0, gx[1, 1].G, 9);
        Assert.Equal(0.0, gx[1, 1].B, 9);

        Assert.Equal(0.0, gy[1, 1].R, 9);
        Assert.Equal(0.5, gy[1, 1].G, 9);
        Assert.Equal(0.0, gy[1, 1].B, 9);

        double expectedNorm = Math.Sqrt((0.5 * 0.5) + (0.5 * 0.5));
        Assert.Equal(expectedNorm, mag[1, 1].Value, 9);
    }

    /// <summary>
    /// Verifies that <see cref="SpatialGradients.ComputeColorGradients"/> handles empty and single-pixel matrices.
    /// </summary>
    [Fact]
    public void ComputeColorGradients_EmptyAndSinglePixel_ShouldHandleCorrectly()
    {
        var empty = new SequentialMatrix<Rgb8>(0, 0);
        var (emptyGx, emptyGy, emptyMag) = SpatialGradients.ComputeColorGradients(empty);

        Assert.Empty(emptyGx);
        Assert.Empty(emptyGy);
        Assert.Empty(emptyMag);

        var single = new SequentialMatrix<Rgb8>(1, 1);
        single[0, 0] = new Rgb8(200, 100, 50);
        var (singleGx, singleGy, singleMag) = SpatialGradients.ComputeColorGradients(single);

        Assert.Single(singleGx);
        Assert.Single(singleGy);
        Assert.Single(singleMag);

        Assert.Equal(Rgb64.Zero, singleGx[0, 0]);
        Assert.Equal(Rgb64.Zero, singleGy[0, 0]);
        Assert.Equal(0.0, singleMag[0, 0].Value, 9);
    }

    /// <summary>
    /// Verifies that <see cref="SpatialGradients.ComputeColorGradients"/> validates input arguments.
    /// </summary>
    [Fact]
    public void ComputeColorGradients_Validation_ShouldThrowCorrectExceptions()
    {
        var valid = new SequentialMatrix<Rgb8>(2, 2);

        Assert.Throws<ArgumentNullException>(() => SpatialGradients.ComputeColorGradients(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpatialGradients.ComputeColorGradients(valid, (GradientOperator)999));
    }

    /// <summary>
    /// Verifies that <see cref="SpatialGradients.ChannelNorm"/> computes the Euclidean length of RGB derivative channels.
    /// </summary>
    [Fact]
    public void ChannelNorm_KnownVectors_ShouldComputeEuclideanNorm()
    {
        var vector = new Rgb64(2.0, 3.0, 6.0);
        Assert.Equal(7.0, SpatialGradients.ChannelNorm(vector), 9);

        var negative = new Rgb64(-2.0, -3.0, -6.0);
        Assert.Equal(7.0, SpatialGradients.ChannelNorm(negative), 9);

        Assert.Equal(0.0, SpatialGradients.ChannelNorm(Rgb64.Zero), 9);
    }
}
