namespace Leiter.Tests.Core;

using System;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MaskExtensions"/>.
/// </summary>
public class MaskExtensionsTests
{
    [Fact]
    public void BooleanMaskOperations_ShouldComputeExpectedResults()
    {
        var left = new MaskMatrix(new[,] { { true, true }, { false, false } });
        var right = new MaskMatrix(new[,] { { false, true }, { true, false } });

        Assert.Equal(2, left.CountTrue());
        Assert.Equal(2, left.CountFalse());

        MatrixAssert.Equal(new MaskMatrix(new[,] { { false, false }, { true, true } }), left.Invert());
        MatrixAssert.Equal(new MaskMatrix(new[,] { { false, true }, { false, false } }), left.And(right));
        MatrixAssert.Equal(new MaskMatrix(new[,] { { true, true }, { true, false } }), left.Or(right));
        MatrixAssert.Equal(new MaskMatrix(new[,] { { true, false }, { false, false } }), left.Except(right));
    }

    [Fact]
    public void ApplyMask_ShouldRetainMaskedPixelsAndReplaceUnmasked()
    {
        var image = new SequentialMatrix<Rgb8>(new Rgb8[,]
        {
            { new(10, 20, 30), new(40, 50, 60) },
            { new(70, 80, 90), new(100, 110, 120) }
        });

        var mask = new MaskMatrix(new[,]
        {
            { true, false },
            { false, true }
        });

        Matrix<Rgb8> defaultMasked = image.ApplyMask(mask);
        MatrixAssert.Equal(
            new SequentialMatrix<Rgb8>(new Rgb8[,]
            {
                { new(10, 20, 30), Rgb8.Zero },
                { Rgb8.Zero, new(100, 110, 120) }
            }),
            defaultMasked);

        var fallback = new Rgb8(255, 255, 255);
        Matrix<Rgb8> customMasked = image.ApplyMask(mask, fallback);
        MatrixAssert.Equal(
            new SequentialMatrix<Rgb8>(new Rgb8[,]
            {
                { new(10, 20, 30), fallback },
                { fallback, new(100, 110, 120) }
            }),
            customMasked);
    }

    [Fact]
    public void ToMaskMatrix_FromReadOnlyMatrix_ShouldMaterializeMaskMatrix()
    {
        var original = new MaskMatrix(new[,] { { true, false }, { false, true } });

        var view = new MatrixView<bool>(original, 0, 0, 2, 1, EdgeHandling.Extend);
        MaskMatrix viaExtension = view.ToMaskMatrix();
        MatrixAssert.Equal(new MaskMatrix(new[,] { { true, false } }), viaExtension);

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<bool>)null!).ToMaskMatrix());
    }

    [Fact]
    public void ToMaskMatrix_WithPredicate_ShouldProjectInParallelAndValidateArgs()
    {
        var scores = new SequentialMatrix<DoublePixel>(new DoublePixel[,]
        {
            { 0.2, 0.8 },
            { 0.5, 0.9 }
        });

        MaskMatrix thresholded = scores.ToMaskMatrix(static p => p.Value >= 0.8);
        MatrixAssert.Equal(new MaskMatrix(new[,] { { false, true }, { false, true } }), thresholded);

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).ToMaskMatrix(static _ => true));
        Assert.Throws<ArgumentNullException>(() => scores.ToMaskMatrix(null!));
    }

    [Fact]
    public void MaskExtensions_NullAndDimensionMismatch_ShouldThrow()
    {
        var small = new MaskMatrix(2, 2);
        var differentSize = new MaskMatrix(3, 2);
        var image = new SequentialMatrix<Rgb8>(2, 2);

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<bool>)null!).CountTrue());
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<bool>)null!).CountFalse());
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<bool>)null!).Invert());
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<bool>)null!).And(small));
        Assert.Throws<ArgumentNullException>(() => small.And(null!));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Rgb8>)null!).ApplyMask(small));
        Assert.Throws<ArgumentNullException>(() => image.ApplyMask(null!));

        Assert.Throws<ArgumentException>(() => small.And(differentSize));
        Assert.Throws<ArgumentException>(() => small.Or(differentSize));
        Assert.Throws<ArgumentException>(() => small.Except(differentSize));
        Assert.Throws<ArgumentException>(() => image.ApplyMask(differentSize));
    }

    [Fact]
    public void NonSequentialMatrixViews_ShouldMatchMaskOperationsAndApplyMask()
    {
        var leftMask = new MaskMatrix(3, 3);
        leftMask[1, 1] = true;
        leftMask[2, 1] = true;

        var rightMask = new MaskMatrix(3, 3);
        rightMask[2, 1] = true;
        rightMask[1, 2] = true;

        var leftView = new MatrixView<bool>(leftMask, 1, 1, 2, 2, EdgeHandling.Extend);
        var rightView = new MatrixView<bool>(rightMask, 1, 1, 2, 2, EdgeHandling.Extend);
        MaskMatrix leftMat = leftView.ToMaskMatrix();
        MaskMatrix rightMat = rightView.ToMaskMatrix();

        Assert.Equal(leftMat.CountTrue(), leftView.CountTrue());
        Assert.Equal(leftMat.CountFalse(), leftView.CountFalse());
        MatrixAssert.Equal(leftMat.Invert(), leftView.Invert());
        MatrixAssert.Equal(leftMat.And(rightMat), leftView.And(rightView));
        MatrixAssert.Equal(leftMat.Or(rightMat), leftView.Or(rightMat));
        MatrixAssert.Equal(leftMat.Except(rightMat), leftMat.Except(rightView));

        var image = new SequentialMatrix<Rgb8>(3, 3);
        image[1, 1] = new Rgb8(10, 20, 30);
        image[2, 1] = new Rgb8(40, 50, 60);
        image[1, 2] = new Rgb8(70, 80, 90);
        image[2, 2] = new Rgb8(100, 110, 120);

        var imageView = new MatrixView<Rgb8>(image, 1, 1, 2, 2, EdgeHandling.Extend);
        var fallback = new Rgb8(5, 5, 5);
        Matrix<Rgb8> expected = imageView.ToSequentialMatrix().ApplyMask(leftMat, fallback);
        Matrix<Rgb8> actual = imageView.ApplyMask(leftView, fallback);
        MatrixAssert.Equal(expected, actual);
    }
}
