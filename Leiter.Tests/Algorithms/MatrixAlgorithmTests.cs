
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Algorithms;
using System;

namespace Leiter.Tests.Algorithms;

/// <summary>
/// Provides unit tests for <see cref="MatrixAlgorithmTests" />.
/// </summary>
public class MatrixAlgorithmTests
{
    /// <summary>
    /// Verifies that the hadamard product same dimensions should multiply element wise behaves correctly.
    /// </summary>
    [Fact]
    public void HadamardProduct_SameDimensions_ShouldMultiplyElementWise()
    {
        var m1 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 2.0, 3.0, 4.0, 5.0 });
        var m2 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 3.0, 4.0, 5.0, 6.0 });

        // Force first overload by providing only 1 generic argument
        var result = m1.HadamardProduct<DoublePixel>(m2);
        Assert.Equal(6.0, result[0].Value);
        Assert.Equal(12.0, result[1].Value);
        Assert.Equal(20.0, result[2].Value);
        Assert.Equal(30.0, result[3].Value);
    }

    /// <summary>
    /// Verifies that the hadamard product with scalar matrix should multiply element wise behaves correctly.
    /// </summary>
    [Fact]
    public void HadamardProduct_WithScalarMatrix_ShouldMultiplyElementWise()
    {
        var m1 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 2.0, 3.0, 4.0, 5.0 });
        var m2 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 3.0, 4.0, 5.0, 6.0 });

        // Force second overload by providing both generic arguments
        var result = m1.HadamardProduct<DoublePixel, DoublePixel>(m2);
        Assert.Equal(6.0, result[0].Value);
        Assert.Equal(12.0, result[1].Value);
    }

    /// <summary>
    /// Verifies that the hadamard product dimension mismatch should throw behaves correctly.
    /// </summary>
    [Fact]
    public void HadamardProduct_DimensionMismatch_ShouldThrow()
    {
        var m1 = new SequentialMatrix<DoublePixel>(2, 2);
        var m2 = new SequentialMatrix<DoublePixel>(3, 3);

        Assert.Throws<ArgumentException>(() => m1.HadamardProduct<DoublePixel>(m2));
        Assert.Throws<ArgumentException>(() => m1.HadamardProduct<DoublePixel, DoublePixel>(m2));
    }

    /// <summary>
    /// Verifies that the frobenius product should return inner product behaves correctly.
    /// </summary>
    [Fact]
    public void FrobeniusProduct_ShouldReturnInnerProduct()
    {
        var m1 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 1.0, 2.0, 3.0, 4.0 });
        var m2 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 2.0, 3.0, 4.0, 5.0 });

        // Force first overload
        var result = m1.FrobeniusProduct<DoublePixel>(m2);
        Assert.Equal(40.0, result.Value);

        // Force second overload
        var result2 = m1.FrobeniusProduct<DoublePixel, DoublePixel>(m2);
        Assert.Equal(40.0, result2.Value);
    }

    /// <summary>
    /// Verifies that the frobenius product dimension mismatch should throw behaves correctly.
    /// </summary>
    [Fact]
    public void FrobeniusProduct_DimensionMismatch_ShouldThrow()
    {
        var m1 = new SequentialMatrix<DoublePixel>(2, 2);
        var m2 = new SequentialMatrix<DoublePixel>(3, 3);

        Assert.Throws<ArgumentException>(() => m1.FrobeniusProduct<DoublePixel>(m2));
        Assert.Throws<ArgumentException>(() => m1.FrobeniusProduct<DoublePixel, DoublePixel>(m2));
    }

    /// <summary>
    /// Verifies that the convolve should perform convolution behaves correctly.
    /// </summary>
    [Fact]
    public void Convolve_ShouldPerformConvolution()
    {
        // 3x3 image
        var img = new SequentialMatrix<DoublePixel>(new Size(3, 3), new DoublePixel[] {
            1.0, 2.0, 3.0,
            4.0, 5.0, 6.0,
            7.0, 8.0, 9.0
        });

        // 3x3 kernel
        var kernel = new SequentialMatrix<DoublePixel>(new Size(3, 3), new DoublePixel[] {
            0.0, 0.0, 0.0,
            0.0, 1.0, 0.0,
            0.0, 0.0, 0.0
        });

        var result = img.Convolve(kernel);
        // Using identity kernel should keep image identical
        Assert.Equal(1.0, result[0, 0].Value);
        Assert.Equal(5.0, result[1, 1].Value);
        Assert.Equal(9.0, result[2, 2].Value);
    }

    /// <summary>
    /// Verifies that the blur gaussian blur and box blur should work behaves correctly.
    /// </summary>
    [Fact]
    public void Blur_GaussianBlur_AndBoxBlur_ShouldWork()
    {
        var img = new SequentialMatrix<DoublePixel>(5, 5);
        img.SetAll(10.0);

        var blurredG = Blur.GaussianBlur(img, 1.0, 1);
        Assert.Equal(5, blurredG.Width);
        Assert.Equal(5, blurredG.Height);
        // Uniform image blurred should remain close to original value
        Assert.Equal(10.0, blurredG[2, 2].Value, 4);

        var blurredB = Blur.BoxBlur(img, 1);
        Assert.Equal(5, blurredB.Width);
        Assert.Equal(5, blurredB.Height);
        Assert.Equal(10.0, blurredB[2, 2].Value, 4);
    }
}
