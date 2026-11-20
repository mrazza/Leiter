namespace Leiter.Tests.Algorithms.Saliency;

using System;
using System.Linq;
using Leiter.Algorithms.Saliency;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GlobalContrastSaliency"/>.
/// </summary>
public class GlobalContrastSaliencyTests
{
    [Fact]
    public void ComputeSaliency_MultiColorImage_ShouldComputeNormalizedSaliency()
    {
        // 20x20 image with 25+ unique quantized colors to satisfy the smoothing logic
        SequentialMatrix<Rgb8> img = TestMatrices.Create(
            20,
            20,
            (x, y) => new Rgb8((byte)(x * 12), (byte)(y * 12), (byte)((x + y) * 6)));

        // Compute saliency (both with and without color smoothing)
        Matrix<DoublePixel> saliencyWithSmoothing = GlobalContrastSaliency.ComputeSaliency(img, new GlobalContrastSaliencyOptions(EnableSmoothing: true));
        Matrix<DoublePixel> saliencyWithoutSmoothing = GlobalContrastSaliency.ComputeSaliency(img, new GlobalContrastSaliencyOptions(EnableSmoothing: false));

        Assert.Equal(20, saliencyWithSmoothing.Width);
        Assert.Equal(20, saliencyWithSmoothing.Height);

        // Min and max should be normalized to 0.0 and 1.0
        double minS = saliencyWithSmoothing.Min(p => p.Value);
        double maxS = saliencyWithSmoothing.Max(p => p.Value);
        Assert.Equal(0.0, minS, 4);
        Assert.Equal(1.0, maxS, 4);

        // Verify that different areas have different saliency (i.e. we are not getting flat zeros)
        Assert.Contains(saliencyWithSmoothing, p => p.Value > 0.0 && p.Value < 1.0);
        Assert.Contains(saliencyWithoutSmoothing, p => p.Value > 0.0 && p.Value < 1.0);
    }

    /// <summary>
    /// Verifies that global contrast saliency of a contrasting object is correctly computed.
    /// </summary>
    [Fact]
    public void ComputeSaliency_ContrastingObject_ShouldBeMostSalient()
    {
        // 10x10 image with a 4x4 red square in the center (16 pixels, 16%) and black background
        SequentialMatrix<Rgb8> img = TestMatrices.CenteredBox(
            10,
            10,
            3,
            3,
            6,
            6,
            new Rgb8(255, 0, 0),
            new Rgb8(0, 0, 0));

        Matrix<DoublePixel> saliency = GlobalContrastSaliency.ComputeSaliency(img, new GlobalContrastSaliencyOptions(EnableSmoothing: false));

        // The contrasting red region should have highest saliency (1.0)
        Assert.Equal(1.0, saliency[5, 5].Value, 4);

        // The black background should have the lowest saliency (0.0)
        Assert.Equal(0.0, saliency[0, 0].Value, 4);
    }

    /// <summary>
    /// Verifies that global contrast saliency of a solid-color image returns all zeros without NaN.
    /// </summary>
    [Fact]
    public void ComputeSaliency_SolidColorImage_ShouldReturnZerosWithoutNaN()
    {
        SequentialMatrix<Rgb8> img = TestMatrices.Constant(10, 10, new Rgb8(100, 150, 200));

        Matrix<DoublePixel> withSmoothing = GlobalContrastSaliency.ComputeSaliency(img, new GlobalContrastSaliencyOptions(EnableSmoothing: true));
        Matrix<DoublePixel> withoutSmoothing = GlobalContrastSaliency.ComputeSaliency(img, new GlobalContrastSaliencyOptions(EnableSmoothing: false));

        SequentialMatrix<DoublePixel> expectedZeros = TestMatrices.Constant<DoublePixel>(10, 10, 0.0);
        MatrixAssert.ApproxEqual(expectedZeros, withSmoothing, 1e-4);
        MatrixAssert.ApproxEqual(expectedZeros, withoutSmoothing, 1e-4);
    }

    /// <summary>
    /// Verifies that global contrast saliency on a 2-color image with smoothing enabled does not produce NaN.
    /// </summary>
    [Fact]
    public void ComputeSaliency_ContrastingObjectWithSmoothing_ShouldNotProduceNaN()
    {
        SequentialMatrix<Rgb8> img = TestMatrices.CenteredBox(
            10,
            10,
            3,
            3,
            6,
            6,
            new Rgb8(255, 0, 0),
            new Rgb8(0, 0, 0));

        Matrix<DoublePixel> saliency = GlobalContrastSaliency.ComputeSaliency(img, new GlobalContrastSaliencyOptions(EnableSmoothing: true));

        MatrixAssert.AllInRange(saliency, 0.0, 1.0);
        Assert.Equal(1.0, saliency[5, 5].Value, 4);
        Assert.Equal(0.0, saliency[0, 0].Value, 4);
    }

    [Fact]
    public void ComputeSaliency_EmptyAndMatrixView_ShouldWork()
    {
        var emptyImg = new SequentialMatrix<Rgb8>(0, 0);
        Matrix<DoublePixel> globalEmpty = GlobalContrastSaliency.ComputeSaliency(emptyImg);
        Assert.Empty(globalEmpty);
        Assert.Equal(0, globalEmpty.Width);
        Assert.Equal(0, globalEmpty.Height);

        Assert.Throws<ArgumentNullException>(() => GlobalContrastSaliency.ComputeSaliency(null!));

        var source = new SequentialMatrix<Rgb8>(4, 4);
        source[1, 1] = new Rgb8(255, 0, 0);
        var view = new MatrixView<Rgb8>(source, 0, 0, 4, 4, EdgeHandling.Extend);
        Matrix<DoublePixel> viewSaliency = GlobalContrastSaliency.ComputeSaliency(view, new GlobalContrastSaliencyOptions(EnableSmoothing: false));
        Assert.Equal(1.0, viewSaliency[1, 1].Value, 4);
    }

    [Fact]
    public void ComputeSaliency_DefaultAndExplicitOptions_ShouldResolveNullToDefault()
    {
        SequentialMatrix<Rgb8> img = TestMatrices.Create(
            20,
            20,
            (x, y) => new Rgb8((byte)(x * 12), (byte)(y * 12), (byte)((x + y) * 6)));

        Matrix<DoublePixel> expectedDefault = GlobalContrastSaliency.ComputeSaliency(img);
        Matrix<DoublePixel> fromNullOptions = GlobalContrastSaliency.ComputeSaliency(img, null);
        Matrix<DoublePixel> fromTrueOptions = GlobalContrastSaliency.ComputeSaliency(img, new GlobalContrastSaliencyOptions(EnableSmoothing: true));
        MatrixAssert.Equal(expectedDefault, fromNullOptions);
        MatrixAssert.Equal(expectedDefault, fromTrueOptions);

        Matrix<DoublePixel> fromFalseOptions = GlobalContrastSaliency.ComputeSaliency(img, new GlobalContrastSaliencyOptions(EnableSmoothing: false));
        MatrixAssert.NotEqual(expectedDefault, fromFalseOptions);

        var emptyImg = new SequentialMatrix<Rgb8>(0, 0);
        Matrix<DoublePixel> emptyResult = GlobalContrastSaliency.ComputeSaliency(emptyImg, GlobalContrastSaliencyOptions.Default);
        Assert.Empty(emptyResult);

        Assert.Throws<ArgumentNullException>(() => GlobalContrastSaliency.ComputeSaliency(null!, GlobalContrastSaliencyOptions.Default));
    }
}
