
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Algorithms;
using System;

namespace Leiter.Tests.Algorithms;

/// <summary>
/// Provides unit tests for <see cref="MatrixMath" /> and <see cref="Blur" />.
/// </summary>
public class MatrixAlgorithmTests
{
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

    [Fact]
    public void HadamardProduct_DimensionMismatch_ShouldThrow()
    {
        var m1 = new SequentialMatrix<DoublePixel>(2, 2);
        var m2 = new SequentialMatrix<DoublePixel>(3, 3);

        Assert.Throws<ArgumentException>(() => m1.HadamardProduct<DoublePixel>(m2));
        Assert.Throws<ArgumentException>(() => m1.HadamardProduct<DoublePixel, DoublePixel>(m2));
    }

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

    [Fact]
    public void FrobeniusProduct_DimensionMismatch_ShouldThrow()
    {
        var m1 = new SequentialMatrix<DoublePixel>(2, 2);
        var m2 = new SequentialMatrix<DoublePixel>(3, 3);

        Assert.Throws<ArgumentException>(() => m1.FrobeniusProduct<DoublePixel>(m2));
        Assert.Throws<ArgumentException>(() => m1.FrobeniusProduct<DoublePixel, DoublePixel>(m2));
    }

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

    [Fact]
    public void ConvolveSeparable_ShouldMatch2DConvolution()
    {
        var img = new SequentialMatrix<DoublePixel>(7, 6);
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
                img[x, y] = x * 1.5 + y * y + ((x + y) % 3) * 2.0;

        double[] hKernel = [0.2, 0.5, 0.3];
        double[] vKernel = [0.1, 0.3, 0.4, 0.2];
        var kernel2D = new SequentialMatrix<DoublePixel>(hKernel.Length, vKernel.Length);
        for (int ky = 0; ky < vKernel.Length; ky++)
            for (int kx = 0; kx < hKernel.Length; kx++)
                kernel2D[kx, ky] = hKernel[kx] * vKernel[ky];

        var expected = img.Convolve(kernel2D);
        var actual = img.ConvolveSeparable(hKernel, vKernel);

        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
                Assert.Equal(expected[x, y].Value, actual[x, y].Value, 10);
    }

    [Fact]
    public void BoxBlur_Separable_ShouldMatchExplicit2DConvolution()
    {
        var img = new SequentialMatrix<DoublePixel>(7, 5);
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
                img[x, y] = (x + 1) * 3.0 + (y * 5.0);

        const int radius = 2;
        int dim = radius * 2 + 1;
        var kernel2D = new SequentialMatrix<DoublePixel>(dim, dim);
        kernel2D.SetAll(1.0 / (dim * dim));

        var expected = img.Convolve(kernel2D);
        var actual = Blur.BoxBlur(img, radius);

        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
                Assert.Equal(expected[x, y].Value, actual[x, y].Value, 10);

        // Verify small dimensions smaller than kernel width
        var small = new SequentialMatrix<DoublePixel>(3, 2);
        small[0, 0] = 1.0; small[1, 0] = 4.0; small[2, 0] = 9.0;
        small[0, 1] = 2.0; small[1, 1] = 5.0; small[2, 1] = 8.0;
        var expectedSmall = small.Convolve(kernel2D);
        var actualSmall = Blur.BoxBlur(small, radius);

        for (int y = 0; y < small.Height; y++)
            for (int x = 0; x < small.Width; x++)
                Assert.Equal(expectedSmall[x, y].Value, actualSmall[x, y].Value, 10);
    }

    [Fact]
    public void GaussianBlur_Separable_ShouldMatchExplicit2DGaussian()
    {
        var img = new SequentialMatrix<DoublePixel>(9, 7);
        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
                img[x, y] = Math.Sin(x * 0.7 + y * 0.4) * 50.0 + 100.0;

        const double sigma = 1.4;
        const int radius = 2;
        int dim = radius * 2 + 1;
        var kernel2D = new SequentialMatrix<DoublePixel>(dim, dim);
        double sum = 0.0;
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                double val = Math.Exp(-(x * x + y * y) / (2.0 * sigma * sigma));
                kernel2D[x + radius, y + radius] = val;
                sum += val;
            }
        }
        for (int y = 0; y < dim; y++)
            for (int x = 0; x < dim; x++)
                kernel2D[x, y] /= sum;

        var expected = img.Convolve(kernel2D);
        var actual = Blur.GaussianBlur(img, sigma, radius);

        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
                Assert.Equal(expected[x, y].Value, actual[x, y].Value, 10);
    }

    [Fact]
    public void GaussianBlurRecursive_ShouldPreserveConstantsSymmetryAndApproximateGaussian()
    {
        var constantImg = new SequentialMatrix<DoublePixel>(16, 14);
        constantImg.SetAll(42.5);
        var blurredConst = Blur.GaussianBlurRecursive(constantImg, 4.0);
        for (int y = 0; y < blurredConst.Height; y++)
            for (int x = 0; x < blurredConst.Width; x++)
                Assert.Equal(42.5, blurredConst[x, y].Value, 10);

        var symImg = new SequentialMatrix<DoublePixel>(21, 15);
        for (int y = 0; y < symImg.Height; y++)
        {
            for (int x = 0; x < symImg.Width; x++)
            {
                int dx = x - 10;
                int dy = y - 7;
                symImg[x, y] = dx * dx + dy * dy + (dx == 0 && dy == 0 ? 50.0 : 0.0);
            }
        }

        var blurredSym = Blur.GaussianBlurRecursive(symImg, 3.5);
        for (int y = 0; y < symImg.Height; y++)
        {
            for (int x = 0; x < symImg.Width; x++)
            {
                Assert.Equal(blurredSym[20 - x, y].Value, blurredSym[x, y].Value, 1e-10);
                Assert.Equal(blurredSym[x, 14 - y].Value, blurredSym[x, y].Value, 1e-10);
            }
        }

        var impulse = new SequentialMatrix<DoublePixel>(81, 81);
        impulse.SetAll(0.0);
        impulse[40, 40] = 1000.0;

        var recursive = Blur.GaussianBlurRecursive(impulse, 4.0);
        var separable = Blur.GaussianBlur(impulse, 4.0, 16);

        double totalMass = 0.0;
        for (int i = 0; i < recursive.Count; i++)
            totalMass += recursive[i].Value;

        Assert.Equal(1000.0, totalMass, 2);
        Assert.Equal(separable[40, 40].Value, recursive[40, 40].Value, 0);
        Assert.True(recursive[40, 40].Value > recursive[41, 40].Value);
        Assert.True(recursive[41, 40].Value > recursive[43, 40].Value);
        Assert.True(recursive[43, 40].Value > recursive[46, 40].Value);
    }

    [Fact]
    public void FrobeniusProduct_MatrixViewOverloads_ShouldMatchAndThrowOnMismatch()
    {
        var m1 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 1.0, 2.0, 3.0, 4.0 });
        var m2 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 2.0, 3.0, 4.0, 5.0 });
        var view = new MatrixView<DoublePixel>(m1, 0, 0, 2, 2, EdgeHandling.EXTEND);

        var sameTypeResult = view.FrobeniusProduct<DoublePixel>(m2);
        Assert.Equal(40.0, sameTypeResult.Value);

        var scalarExplicitResult = view.FrobeniusProduct<DoublePixel, DoublePixel>(m2);
        Assert.Equal(40.0, scalarExplicitResult.Value);

        var rgbImg = new SequentialMatrix<Rgb8>(3, 3);
        rgbImg[1, 1] = new Rgb8(255, 0, 0);
        rgbImg[2, 2] = new Rgb8(0, 0, 255);
        var rgbView = new MatrixView<Rgb8>(rgbImg, 1, 1, 2, 2, EdgeHandling.EXTEND);
        var scalarKernel = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 1.0, 0.0, 0.0, 1.0 });

        Rgb8 inferredResult = rgbView.FrobeniusProduct(scalarKernel);
        Assert.Equal(new Rgb8(255, 0, 255), inferredResult);

        var m3 = new SequentialMatrix<DoublePixel>(3, 3);
        Assert.Throws<ArgumentException>(() => view.FrobeniusProduct<DoublePixel>(m3));
        Assert.Throws<ArgumentException>(() => view.FrobeniusProduct<DoublePixel, DoublePixel>(m3));
        Assert.Throws<ArgumentException>(() => rgbView.FrobeniusProduct(m3));
    }

    [Fact]
    public void Convolve_And_ConvolveSeparable_EdgeCases_ShouldValidateAndHandleEmptyAndLargeKernels()
    {
        var img = new SequentialMatrix<DoublePixel>(2, 2);
        img.SetAll(5.0);

        Assert.Throws<ArgumentException>(() => img.Convolve(new SequentialMatrix<DoublePixel>(0, 3)));
        Assert.Throws<ArgumentException>(() => img.Convolve(new SequentialMatrix<DoublePixel>(3, 0)));

        var validKernel = new SequentialMatrix<DoublePixel>(1, 1);
        validKernel[0, 0] = 1.0;

        var emptyW = new SequentialMatrix<DoublePixel>(0, 3);
        var convolvedEmptyW = emptyW.Convolve(validKernel);
        Assert.Equal(0, convolvedEmptyW.Width);
        Assert.Equal(3, convolvedEmptyW.Height);

        var emptyH = new SequentialMatrix<DoublePixel>(3, 0);
        var convolvedEmptyH = emptyH.Convolve(validKernel);
        Assert.Equal(3, convolvedEmptyH.Width);
        Assert.Equal(0, convolvedEmptyH.Height);

        Assert.Throws<ArgumentException>(() => img.ConvolveSeparable(ReadOnlySpan<double>.Empty, [1.0]));
        Assert.Throws<ArgumentException>(() => img.ConvolveSeparable([1.0], ReadOnlySpan<double>.Empty));

        var sepEmptyW = emptyW.ConvolveSeparable([1.0], [1.0]);
        Assert.Equal(0, sepEmptyW.Width);
        Assert.Equal(3, sepEmptyW.Height);

        var sepEmptyH = emptyH.ConvolveSeparable([1.0], [1.0]);
        Assert.Equal(3, sepEmptyH.Width);
        Assert.Equal(0, sepEmptyH.Height);

        double[] largeVertical = new double[257];
        Array.Fill(largeVertical, 1.0 / largeVertical.Length);
        var convolvedLargeV = img.ConvolveSeparable([1.0], largeVertical);
        for (int i = 0; i < convolvedLargeV.Count; i++)
            Assert.Equal(5.0, convolvedLargeV[i].Value, 10);
    }

    [Fact]
    public void Blur_EdgeCases_AndSmallMediumSigmaRecursive_ShouldWork()
    {
        var uniform = new SequentialMatrix<DoublePixel>(6, 6);
        uniform.SetAll(12.5);

        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlur(uniform, 1.0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlur(uniform, 0.0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlur(uniform, -1.0, 1));

        var emptyW = new SequentialMatrix<DoublePixel>(0, 4);
        var emptyH = new SequentialMatrix<DoublePixel>(4, 0);

        Assert.Equal(0, Blur.GaussianBlur(emptyW, 1.0, 1).Width);
        Assert.Equal(0, Blur.GaussianBlur(emptyH, 1.0, 1).Height);
        var gaussZeroRadius = Blur.GaussianBlur(uniform, 1.0, 0);
        Assert.Equal(12.5, gaussZeroRadius[2, 2].Value, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.BoxBlur(uniform, -1));
        Assert.Equal(0, Blur.BoxBlur(emptyW, 1).Width);
        Assert.Equal(0, Blur.BoxBlur(emptyH, 1).Height);
        var boxZeroRadius = Blur.BoxBlur(uniform, 0);
        Assert.Equal(12.5, boxZeroRadius[2, 2].Value, 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlurRecursive(uniform, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlurRecursive(uniform, -1.0));
        Assert.Equal(0, Blur.GaussianBlurRecursive(emptyW, 1.0).Width);
        Assert.Equal(0, Blur.GaussianBlurRecursive(emptyH, 1.0).Height);

        // Exercise both small-sigma (< 0.5 FIR fallback) and medium-sigma (0.5 <= sigma < 2.5) branches.
        var smallSigma = Blur.GaussianBlurRecursive(uniform, 0.3);
        var mediumSigma = Blur.GaussianBlurRecursive(uniform, 1.2);
        for (int i = 0; i < uniform.Count; i++)
        {
            Assert.Equal(12.5, smallSigma[i].Value, 10);
            Assert.Equal(12.5, mediumSigma[i].Value, 10);
        }
    }
}
