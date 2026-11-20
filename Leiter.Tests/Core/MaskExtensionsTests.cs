namespace Leiter.Tests.Core;

using System;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MaskExtensions"/>.
/// </summary>
public class MaskExtensionsTests
{
    [Fact]
    public void BooleanMaskOperations_ShouldComputeExpectedResults()
    {
        var left = new MaskMatrix(2, 2);
        left[0, 0] = true;
        left[1, 0] = true;

        var right = new MaskMatrix(2, 2);
        right[1, 0] = true;
        right[0, 1] = true;

        Assert.Equal(2, left.CountTrue());
        Assert.Equal(2, left.CountFalse());

        MaskMatrix inverted = left.Invert();
        Assert.False(inverted[0, 0]);
        Assert.False(inverted[1, 0]);
        Assert.True(inverted[0, 1]);
        Assert.True(inverted[1, 1]);

        MaskMatrix intersected = left.And(right);
        Assert.False(intersected[0, 0]);
        Assert.True(intersected[1, 0]);
        Assert.False(intersected[0, 1]);
        Assert.False(intersected[1, 1]);

        MaskMatrix unioned = left.Or(right);
        Assert.True(unioned[0, 0]);
        Assert.True(unioned[1, 0]);
        Assert.True(unioned[0, 1]);
        Assert.False(unioned[1, 1]);

        MaskMatrix subtracted = left.Except(right);
        Assert.True(subtracted[0, 0]);
        Assert.False(subtracted[1, 0]);
        Assert.False(subtracted[0, 1]);
        Assert.False(subtracted[1, 1]);
    }

    [Fact]
    public void ApplyMask_ShouldRetainMaskedPixelsAndReplaceUnmasked()
    {
        var image = new SequentialMatrix<Rgb8>(2, 2);
        image[0, 0] = new Rgb8(10, 20, 30);
        image[1, 0] = new Rgb8(40, 50, 60);
        image[0, 1] = new Rgb8(70, 80, 90);
        image[1, 1] = new Rgb8(100, 110, 120);

        var mask = new MaskMatrix(2, 2);
        mask[0, 0] = true;
        mask[1, 1] = true;

        Matrix<Rgb8> defaultMasked = image.ApplyMask(mask);
        Assert.Equal(new Rgb8(10, 20, 30), defaultMasked[0, 0]);
        Assert.Equal(Rgb8.Zero, defaultMasked[1, 0]);
        Assert.Equal(Rgb8.Zero, defaultMasked[0, 1]);
        Assert.Equal(new Rgb8(100, 110, 120), defaultMasked[1, 1]);

        var fallback = new Rgb8(255, 255, 255);
        Matrix<Rgb8> customMasked = image.ApplyMask(mask, fallback);
        Assert.Equal(fallback, customMasked[1, 0]);
        Assert.Equal(new Rgb8(10, 20, 30), customMasked[0, 0]);
    }

    [Fact]
    public void ToMaskMatrix_FromReadOnlyMatrix_ShouldMaterializeMaskMatrix()
    {
        var original = new MaskMatrix(2, 2);
        original[0, 0] = true;
        original[1, 1] = true;

        var view = new MatrixView<bool>(original, 0, 0, 2, 1, EdgeHandling.Extend);
        MaskMatrix viaExtension = view.ToMaskMatrix();
        Assert.Equal(new Size(2, 1), viaExtension.Size);
        Assert.True(viaExtension[0, 0]);
        Assert.False(viaExtension[1, 0]);

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<bool>)null!).ToMaskMatrix());
    }

    [Fact]
    public void ToMaskMatrix_WithPredicate_ShouldProjectInParallelAndValidateArgs()
    {
        var scores = new SequentialMatrix<DoublePixel>(2, 2);
        scores[0, 0] = 0.2;
        scores[1, 0] = 0.8;
        scores[0, 1] = 0.5;
        scores[1, 1] = 0.9;

        MaskMatrix thresholded = scores.ToMaskMatrix(static p => p.Value >= 0.8);
        Assert.False(thresholded[0, 0]);
        Assert.True(thresholded[1, 0]);
        Assert.False(thresholded[0, 1]);
        Assert.True(thresholded[1, 1]);

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
        Assert.True(leftMat.Invert().ContentEquals(leftView.Invert()));
        Assert.True(leftMat.And(rightMat).ContentEquals(leftView.And(rightView)));
        Assert.True(leftMat.Or(rightMat).ContentEquals(leftView.Or(rightMat)));
        Assert.True(leftMat.Except(rightMat).ContentEquals(leftMat.Except(rightView)));

        var image = new SequentialMatrix<Rgb8>(3, 3);
        image[1, 1] = new Rgb8(10, 20, 30);
        image[2, 1] = new Rgb8(40, 50, 60);
        image[1, 2] = new Rgb8(70, 80, 90);
        image[2, 2] = new Rgb8(100, 110, 120);

        var imageView = new MatrixView<Rgb8>(image, 1, 1, 2, 2, EdgeHandling.Extend);
        var fallback = new Rgb8(5, 5, 5);
        Matrix<Rgb8> expected = imageView.ToSequentialMatrix().ApplyMask(leftMat, fallback);
        Matrix<Rgb8> actual = imageView.ApplyMask(leftView, fallback);
        Assert.True(expected.ContentEquals(actual));
    }
}
