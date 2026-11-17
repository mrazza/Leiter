namespace Leiter.Tests.Algorithms;

using System;
using Leiter.Algorithms;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Blur"/>.
/// </summary>
public class BlurTests
{
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

        var view = new MatrixView<DoublePixel>(uniform, 0, 0, 4, 4, EdgeHandling.EXTEND);
        Assert.Equal(12.5, Blur.GaussianBlur(view, 1.0, 1)[2, 2].Value, 10);
        Assert.Equal(12.5, Blur.BoxBlur(view, 1)[2, 2].Value, 10);
        Assert.Equal(12.5, Blur.GaussianBlurRecursive(view, 2.0)[2, 2].Value, 10);
        Assert.Equal(12.5, Blur.GaussianBlur(view, 1.0, 0)[2, 2].Value, 10);
    }
}
