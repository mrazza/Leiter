namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="EdgeDarkening"/>.
/// </summary>
public class EdgeDarkeningTests
{
    /// <summary>
    /// Verifies that passing a null image to Apply throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Apply_NullImage_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => EdgeDarkening.Apply(null!));
    }

    /// <summary>
    /// Verifies that passing invalid options to Apply throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Apply_InvalidOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var invalidOptions = new EdgeDarkeningOptions(Intensity: -1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => EdgeDarkening.Apply(image, invalidOptions));
    }

    /// <summary>
    /// Verifies that empty images of various zero dimensions return an empty matrix.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void Apply_EmptyImage_ShouldReturnEmptyMatrix(int width, int height)
    {
        var empty = new SequentialMatrix<Rgb8>(width, height);
        Matrix<Rgb8> result = EdgeDarkening.Apply(empty);

        Assert.Empty(result);
        Assert.Equal(width, result.Width);
        Assert.Equal(height, result.Height);
    }

    /// <summary>
    /// Verifies that a single-pixel image remains unchanged.
    /// </summary>
    [Fact]
    public void Apply_SinglePixelImage_ShouldRemainUnchanged()
    {
        var image = new SequentialMatrix<Rgb8>(1, 1);
        image[0, 0] = new Rgb8(120, 140, 160);

        Matrix<Rgb8> result = EdgeDarkening.Apply(image);

        Assert.Equal(image[0, 0], result[0, 0]);
    }

    /// <summary>
    /// Verifies that specifying an intensity of 0.0 returns an exact copy without modification.
    /// </summary>
    [Fact]
    public void Apply_ZeroIntensity_ShouldReturnExactClone()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Create(4, 4, (x, y) => new Rgb8((byte)(x * 40), (byte)(y * 50), 100));

        var options = new EdgeDarkeningOptions(Intensity: 0.0);
        Matrix<Rgb8> result = EdgeDarkening.Apply(image, options);

        MatrixAssert.Equal(image, result);
    }

    /// <summary>
    /// Verifies that specifying an intensity of 0.0 with a non-matrix read-only source returns an exact copy.
    /// </summary>
    [Fact]
    public void Apply_ZeroIntensity_CustomReadOnlyMatrix_ShouldReturnExactCopy()
    {
        var custom = new DummyReadOnlyMatrix<Rgb8>(3, 3, new Rgb8(150, 160, 170));
        var options = new EdgeDarkeningOptions(Intensity: 0.0);

        Matrix<Rgb8> result = EdgeDarkening.Apply(custom, options);

        MatrixAssert.Equal(custom, result);
    }

    /// <summary>
    /// Verifies that a flat uniform image has zero gradients everywhere and remains completely unchanged.
    /// </summary>
    [Fact]
    public void Apply_FlatImage_ShouldRemainUnchanged()
    {
        var color = new Rgb8(140, 160, 180);
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(8, 8, color);

        Matrix<Rgb8> result = EdgeDarkening.Apply(image);

        MatrixAssert.Equal(image, result);
    }

    /// <summary>
    /// Verifies that pixels near step-edges darken strictly relative to interior pixels.
    /// </summary>
    [Fact]
    public void Apply_StepEdge_EdgePixelsDarkenStrictlyRelativeToInteriorPixels()
    {
        var leftColor = new Rgb8(200, 200, 200);
        var rightColor = new Rgb8(60, 60, 60);
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(32, 16, leftColor, rightColor, splitX: 16);

        var options = new EdgeDarkeningOptions(Intensity: 0.7, BlurSigma: 1.0);
        Matrix<Rgb8> result = EdgeDarkening.Apply(image, options);

        // Interior pixel far from the edge should remain untouched.
        Assert.Equal(leftColor, result[0, 8]);

        // Boundary pixel should be strictly darker than the original color.
        Rgb8 edgePixel = result[15, 8];
        Assert.True(edgePixel.R < leftColor.R);
        Assert.True(edgePixel.G < leftColor.G);
        Assert.True(edgePixel.B < leftColor.B);
    }

    /// <summary>
    /// Verifies that a higher Intensity setting produces strictly stronger edge darkening.
    /// </summary>
    [Fact]
    public void Apply_HigherIntensity_ProducesStrongerDarkening()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(16, 16, new Rgb8(220, 220, 220), new Rgb8(50, 50, 50), splitX: 8);

        var optionsLow = new EdgeDarkeningOptions(Intensity: 0.25, BlurSigma: 0.5);
        var optionsHigh = new EdgeDarkeningOptions(Intensity: 0.85, BlurSigma: 0.5);

        Matrix<Rgb8> resultLow = EdgeDarkening.Apply(image, optionsLow);
        Matrix<Rgb8> resultHigh = EdgeDarkening.Apply(image, optionsHigh);

        Assert.True(resultHigh[7, 8].R < resultLow[7, 8].R);
    }

    /// <summary>
    /// Verifies that a positive BlurSigma spreads edge darkening to neighboring pixels compared to BlurSigma zero.
    /// </summary>
    [Fact]
    public void Apply_BlurSigmaZero_VsPositiveBlurSigma_SpreadsDarkening()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(20, 16, new Rgb8(210, 210, 210), new Rgb8(40, 40, 40), splitX: 10);

        var optionsNoBlur = new EdgeDarkeningOptions(Intensity: 0.7, BlurSigma: 0.0);
        var optionsBlurred = new EdgeDarkeningOptions(Intensity: 0.7, BlurSigma: 2.0);

        Matrix<Rgb8> resultNoBlur = EdgeDarkening.Apply(image, optionsNoBlur);
        Matrix<Rgb8> resultBlurred = EdgeDarkening.Apply(image, optionsBlurred);

        // At distance 3 from boundary (x = 7), unblurred has negligible gradient response, but blurred diffuses outward.
        Assert.True(resultBlurred[7, 8].R < resultNoBlur[7, 8].R);
    }

    /// <summary>
    /// Verifies that the Scharr gradient operator successfully detects edges and produces darkening.
    /// </summary>
    [Fact]
    public void Apply_ScharrOperator_ShouldDarkenEdges()
    {
        var leftColor = new Rgb8(190, 190, 190);
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(16, 16, leftColor, new Rgb8(40, 40, 40), splitX: 8);

        var options = new EdgeDarkeningOptions(Intensity: 0.6, GradientOperator: GradientOperator.Scharr);
        Matrix<Rgb8> result = EdgeDarkening.Apply(image, options);

        Assert.True(result[7, 8].R < leftColor.R);
    }

    /// <summary>
    /// Verifies that a custom read-only matrix implementation processes properly.
    /// </summary>
    [Fact]
    public void Apply_CustomReadOnlyMatrix_ShouldWork()
    {
        var custom = new DummyReadOnlyMatrix<Rgb8>(8, 8, new Rgb8(150, 150, 150));
        Matrix<Rgb8> result = EdgeDarkening.Apply(custom);

        Assert.Equal(custom.Size, result.Size);
        Assert.Equal(new Rgb8(150, 150, 150), result[4, 4]);
    }
}
