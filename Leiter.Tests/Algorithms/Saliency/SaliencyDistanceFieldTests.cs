namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms.Saliency;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyDistanceField"/>.
/// </summary>
public class SaliencyDistanceFieldTests
{
    [Fact]
    public void ComputeNormalizedField_KnownGeometry_ShouldMatchExpectedNormalizedDistances()
    {
        var mask = new MaskMatrix(31, 31);
        for (int y = 10; y <= 20; y++)
        {
            for (int x = 10; x <= 20; x++)
            {
                mask[x, y] = true;
            }
        }

        Matrix<DoublePixel> rawField = SaliencyDistanceField.ComputeNormalizedField(mask);
        Assert.Equal(0.0, rawField[15, 15].Value, 9);
        Assert.Equal(0.0, rawField[10, 10].Value, 9);
        Assert.Equal(0.5, rawField[25, 15].Value, 9);
        Assert.Equal(0.5, rawField[5, 15].Value, 9);
        Assert.Equal(0.5, rawField[15, 25].Value, 9);
        Assert.Equal(0.5, rawField[15, 5].Value, 9);
        Assert.Equal(1.0, rawField[30, 15].Value, 9);
        Assert.Equal(1.0, rawField[0, 15].Value, 9);
        Assert.Equal(1.0, rawField[0, 0].Value, 9);
        Assert.Equal(0.5, rawField[25, 25].Value, 9);
    }

    [Fact]
    public void Compute_SmoothingVariations_ShouldProduceSmoothedDistances()
    {
        var mask = new MaskMatrix(31, 31);
        for (int y = 10; y <= 20; y++)
        {
            for (int x = 10; x <= 20; x++)
            {
                mask[x, y] = true;
            }
        }

        Matrix<DoublePixel> unblurred = SaliencyDistanceField.Compute(mask, new SaliencyDistanceFieldOptions(SmoothingSigma: 0.0));
        Assert.Equal(0.5, unblurred[25, 15].Value, 9);

        Matrix<DoublePixel> smoothed = SaliencyDistanceField.Compute(mask);
        Assert.InRange(smoothed[10, 15].Value, 0.0, 0.4);
        Assert.True(smoothed[30, 15].Value > smoothed[15, 15].Value);
        for (int i = 0; i < smoothed.Count; i++)
        {
            Assert.InRange(smoothed[i].Value, 0.0, 1.0);
        }
    }

    [Fact]
    public void Compute_EmptyAndUniformMasks_ShouldReturnExpectedEdgeFields()
    {
        var empty = new MaskMatrix(0, 0);
        Assert.Empty(SaliencyDistanceField.ComputeNormalizedField(empty));
        Assert.Empty(SaliencyDistanceField.Compute(empty));

        var allBg = new MaskMatrix(4, 4);
        Matrix<DoublePixel> allBgField = SaliencyDistanceField.ComputeNormalizedField(allBg);
        for (int i = 0; i < allBgField.Count; i++)
        {
            Assert.Equal(1.0, allBgField[i].Value, 9);
        }

        var allFg = new MaskMatrix(4, 4);
        allFg.SetAll(true);
        Matrix<DoublePixel> allFgField = SaliencyDistanceField.ComputeNormalizedField(allFg);
        for (int i = 0; i < allFgField.Count; i++)
        {
            Assert.Equal(0.0, allFgField[i].Value, 9);
        }
    }

    [Fact]
    public void Compute_ImageAndSaliencyOverload_ShouldExtractMaskAndComputeDistanceField()
    {
        var image = new SequentialMatrix<Rgb8>(20, 20);
        var saliency = new SequentialMatrix<DoublePixel>(20, 20);
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                bool fg = x >= 6 && x <= 13 && y >= 6 && y <= 13;
                image[x, y] = fg ? new Rgb8(220, 30, 30) : new Rgb8(20, 180, 40);
                saliency[x, y] = fg ? 0.9 : 0.05;
            }
        }

        (MaskMatrix mask, Matrix<DoublePixel> field) = SaliencyDistanceField.Compute(image, saliency);
        Assert.True(mask[10, 10]);
        Assert.False(mask[0, 0]);
        Assert.True(field[0, 0].Value > field[10, 10].Value);
    }

    [Fact]
    public void Validation_InvalidArguments_ShouldThrowExpectedExceptions()
    {
        var allBg = new MaskMatrix(4, 4);

        Assert.Throws<ArgumentNullException>(() => SaliencyDistanceField.ComputeNormalizedField(null!));
        Assert.Throws<ArgumentNullException>(() => SaliencyDistanceField.Compute(null!, new SaliencyDistanceFieldOptions(SmoothingSigma: 1.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyDistanceField.Compute(allBg, new SaliencyDistanceFieldOptions(DefaultSigmaScale: -0.1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyDistanceField.Compute(allBg, new SaliencyDistanceFieldOptions(SmoothingSigma: -1.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyDistanceField.Compute(allBg, new SaliencyDistanceFieldOptions(SmoothingSigma: double.NaN)));

        var image = new SequentialMatrix<Rgb8>(4, 4);
        var saliency = new SequentialMatrix<DoublePixel>(4, 4);
        var mismatchedSaliency = new SequentialMatrix<DoublePixel>(2, 2);

        ArgumentNullException nullImageEx = Assert.Throws<ArgumentNullException>(() =>
            SaliencyDistanceField.Compute(null!, saliency, SaliencyCutOptions.Default, SaliencyDistanceFieldOptions.Default));
        Assert.Equal("image", nullImageEx.ParamName);

        ArgumentNullException nullSaliencyEx = Assert.Throws<ArgumentNullException>(() =>
            SaliencyDistanceField.Compute(image, (IReadOnlyMatrix<DoublePixel>)null!, SaliencyCutOptions.Default, SaliencyDistanceFieldOptions.Default));
        Assert.Equal("saliencyMap", nullSaliencyEx.ParamName);

        ArgumentException mismatchEx = Assert.Throws<ArgumentException>(() =>
            SaliencyDistanceField.Compute(image, mismatchedSaliency, SaliencyCutOptions.Default, SaliencyDistanceFieldOptions.Default));
        Assert.Equal("saliencyMap", mismatchEx.ParamName);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SaliencyDistanceField.Compute(image, saliency, SaliencyCutOptions.Default, new SaliencyDistanceFieldOptions(SmoothingSigma: -1.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SaliencyDistanceField.Compute(image, saliency, SaliencyCutOptions.Default, new SaliencyDistanceFieldOptions(DefaultSigmaScale: -0.1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SaliencyDistanceField.Compute(image, saliency, SaliencyCutOptions.Default, new SaliencyDistanceFieldOptions(SmoothingSigma: double.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SaliencyDistanceField.Compute(image, saliency, new SaliencyCutOptions(MaxOuterIterations: 0), SaliencyDistanceFieldOptions.Default));
    }

    [Fact]
    public void ExtractSalientMask_ShouldThresholdFiniteValuesAndValidate()
    {
        var field = new SequentialMatrix<DoublePixel>(5, 1);
        field[0, 0] = 0.0;
        field[1, 0] = 1e-6;
        field[2, 0] = 0.25;
        field[3, 0] = double.NaN;
        field[4, 0] = double.PositiveInfinity;

        MaskMatrix defaultMask = SaliencyDistanceField.ExtractSalientMask(field);
        Assert.True(defaultMask[0, 0]);
        Assert.True(defaultMask[1, 0]);
        Assert.False(defaultMask[2, 0]);
        Assert.False(defaultMask[3, 0]);
        Assert.False(defaultMask[4, 0]);

        MaskMatrix customMask = SaliencyDistanceField.ExtractSalientMask(field, threshold: 0.3);
        Assert.True(customMask[2, 0]);
        Assert.False(customMask[3, 0]);

        Assert.Throws<ArgumentNullException>(() => SaliencyDistanceField.ExtractSalientMask(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyDistanceField.ExtractSalientMask(field, threshold: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyDistanceField.ExtractSalientMask(field, threshold: double.NaN));
    }

    [Fact]
    public void Compute_LargeSmoothingSigma_ShouldPreserveExactZeroInSalientMaskAndRecoverViaExtractSalientMask()
    {
        var mask = new MaskMatrix(64, 64);
        for (int y = 20; y <= 43; y++)
        {
            for (int x = 20; x <= 43; x++)
            {
                mask[x, y] = true;
            }
        }

        Matrix<DoublePixel> smoothed = SaliencyDistanceField.Compute(mask, new SaliencyDistanceFieldOptions(SmoothingSigma: 18.0));

        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                if (mask[x, y])
                {
                    Assert.Equal(0.0, smoothed[x, y].Value, 12);
                }
                else
                {
                    Assert.True(smoothed[x, y].Value > 1e-6);
                }
            }
        }

        MaskMatrix recovered = SaliencyDistanceField.ExtractSalientMask(smoothed);
        MatrixAssert.Equal(mask, recovered);
    }
}
