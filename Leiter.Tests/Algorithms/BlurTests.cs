namespace Leiter.Tests.Algorithms;

using System;
using Leiter.Algorithms;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Blur"/>.
/// </summary>
public class BlurTests
{
    [Fact]
    public void Blur_GaussianBlur_AndBoxBlur_ShouldWork()
    {
        SequentialMatrix<DoublePixel> img = TestMatrices.Constant(5, 5, new DoublePixel(10.0));

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
    public void BoxBlur_Separable_ShouldMatchExplicit2DConvolution()
    {
        SequentialMatrix<DoublePixel> img = TestMatrices.Create(7, 5, (x, y) => new DoublePixel(((x + 1) * 3.0) + (y * 5.0)));

        const int radius = 2;
        int dim = radius * 2 + 1;
        SequentialMatrix<DoublePixel> kernel2D = TestMatrices.Constant(dim, dim, new DoublePixel(1.0 / (dim * dim)));

        Matrix<DoublePixel> expected = img.Convolve(kernel2D);
        Matrix<DoublePixel> actual = Blur.BoxBlur(img, radius);

        MatrixAssert.ApproxEqual(expected, actual, 1e-10);

        // Verify small dimensions smaller than kernel width
        var small = new SequentialMatrix<DoublePixel>(3, 2);
        small[0, 0] = 1.0; small[1, 0] = 4.0; small[2, 0] = 9.0;
        small[0, 1] = 2.0; small[1, 1] = 5.0; small[2, 1] = 8.0;
        Matrix<DoublePixel> expectedSmall = small.Convolve(kernel2D);
        Matrix<DoublePixel> actualSmall = Blur.BoxBlur(small, radius);

        MatrixAssert.ApproxEqual(expectedSmall, actualSmall, 1e-10);
    }

    [Fact]
    public void GaussianBlur_Separable_ShouldMatchExplicit2DGaussian()
    {
        SequentialMatrix<DoublePixel> img = TestMatrices.Create(9, 7, (x, y) => new DoublePixel((Math.Sin((x * 0.7) + (y * 0.4)) * 50.0) + 100.0));

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

        Matrix<DoublePixel> expected = img.Convolve(kernel2D);
        Matrix<DoublePixel> actual = Blur.GaussianBlur(img, sigma, radius);

        MatrixAssert.ApproxEqual(expected, actual, 1e-10);
    }

    [Fact]
    public void GaussianBlurRecursive_ShouldPreserveConstantsSymmetryAndApproximateGaussian()
    {
        SequentialMatrix<DoublePixel> constantImg = TestMatrices.Constant(16, 14, new DoublePixel(42.5));
        Matrix<DoublePixel> blurredConst = Blur.GaussianBlurRecursive(constantImg, 4.0);
        MatrixAssert.ApproxEqual(constantImg, blurredConst, 1e-10);

        SequentialMatrix<DoublePixel> symImg = TestMatrices.Create(
            21,
            15,
            (x, y) =>
            {
                int dx = x - 10;
                int dy = y - 7;
                return new DoublePixel((dx * dx) + (dy * dy) + (dx == 0 && dy == 0 ? 50.0 : 0.0));
            });

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
    public void Blur_EdgeCases_AndSmallMediumSigmaRecursive_ShouldWork()
    {
        SequentialMatrix<DoublePixel> uniform = TestMatrices.Constant(6, 6, new DoublePixel(12.5));

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
        MatrixAssert.ApproxEqual(uniform, smallSigma, 1e-10);
        MatrixAssert.ApproxEqual(uniform, mediumSigma, 1e-10);

        // Null, non-finite sigma, and non-Matrix IReadOnlyMatrix (MatrixView) inputs.
        Assert.Throws<ArgumentNullException>(() => Blur.GaussianBlur<DoublePixel>(null!, 1.0, 1));
        Assert.Throws<ArgumentNullException>(() => Blur.BoxBlur<DoublePixel>(null!, 1));
        Assert.Throws<ArgumentNullException>(() => Blur.GaussianBlurRecursive<DoublePixel>(null!, 1.0));

        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlur(uniform, double.NaN, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlur(uniform, double.PositiveInfinity, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlur(uniform, double.NegativeInfinity, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlurRecursive(uniform, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlurRecursive(uniform, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlurRecursive(uniform, double.NegativeInfinity));

        var view = new MatrixView<DoublePixel>(uniform, 0, 0, 4, 4, EdgeHandling.Extend);
        Assert.Equal(12.5, Blur.GaussianBlur(view, 1.0, 1)[2, 2].Value, 10);
        Assert.Equal(12.5, Blur.BoxBlur(view, 1)[2, 2].Value, 10);
        Assert.Equal(12.5, Blur.GaussianBlurRecursive(view, 2.0)[2, 2].Value, 10);
        Assert.Equal(12.5, Blur.GaussianBlur(view, 1.0, 0)[2, 2].Value, 10);
    }

    [Fact]
    public void GaussianBlur_AdaptiveSigmaOverload_ShouldHandleZeroSmallAndLargeSigmas()
    {
        SequentialMatrix<DoublePixel> img = TestMatrices.Create(15, 13, (x, y) => new DoublePixel((x * 11.0) + (y * 7.0)));

        Assert.Throws<ArgumentNullException>(() => Blur.GaussianBlur<DoublePixel>(null!, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlur(img, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Blur.GaussianBlur(img, double.NaN));

        Matrix<DoublePixel> zeroBlurred = Blur.GaussianBlur(img, 0.0);
        MatrixAssert.ApproxEqual(img, zeroBlurred, 0.0);

        Matrix<DoublePixel> emptyBlurred = Blur.GaussianBlur(new SequentialMatrix<DoublePixel>(0, 4), 1.5);
        Assert.Equal(0, emptyBlurred.Width);

        Matrix<DoublePixel> firExpected = Blur.GaussianBlur(img, 0.8, 2);
        Matrix<DoublePixel> firAdaptive = Blur.GaussianBlur(img, 0.8);
        MatrixAssert.ApproxEqual(firExpected, firAdaptive, 1e-12);

        Matrix<DoublePixel> recursiveExpected = Blur.GaussianBlurRecursive(img, 3.5);
        Matrix<DoublePixel> recursiveAdaptive = Blur.GaussianBlur(img, 3.5);
        MatrixAssert.ApproxEqual(recursiveExpected, recursiveAdaptive, 1e-12);
    }

    [Theory]
    [InlineData(0.2, 1)]
    [InlineData(0.35, 1)]
    [InlineData(0.45, 2)]
    public void GaussianBlur_And_GaussianBlurRecursive_SmallSigma_ShouldMatch(double sigma, int expectedRadius)
    {
        SequentialMatrix<DoublePixel> img = TestMatrices.Create(15, 13, (x, y) => new DoublePixel(((x * 7.0) + (y * 11.0)) % 29.0));

        Matrix<DoublePixel> expected = Blur.GaussianBlur(img, sigma, expectedRadius);
        Matrix<DoublePixel> adaptive = Blur.GaussianBlur(img, sigma);
        Matrix<DoublePixel> recursive = Blur.GaussianBlurRecursive(img, sigma);

        MatrixAssert.ApproxEqual(expected, adaptive, 1e-12);
        MatrixAssert.ApproxEqual(expected, recursive, 1e-12);
    }
}
