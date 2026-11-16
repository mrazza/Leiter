namespace Leiter.Tests.Algorithms.Saliency;

using System.Linq;
using Leiter.Algorithms.Saliency;
using Leiter.Core;
using Leiter.Pixels;
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
        var img = new SequentialMatrix<Rgb8>(20, 20);
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                // Each grid cell has a different color to ensure we have many unique colors
                byte r = (byte)(x * 12);
                byte g = (byte)(y * 12);
                byte b = (byte)((x + y) * 6);
                img[x, y] = new Rgb8(r, g, b);
            }
        }

        // Compute saliency (both with and without color smoothing)
        var saliencyWithSmoothing = GlobalContrastSaliency.ComputeSaliency(img, enableColorSpaceSmoothing: true);
        var saliencyWithoutSmoothing = GlobalContrastSaliency.ComputeSaliency(img, enableColorSpaceSmoothing: false);

        Assert.Equal(20, saliencyWithSmoothing.Width);
        Assert.Equal(20, saliencyWithSmoothing.Height);

        // Min and max should be normalized to 0.0 and 1.0
        var minS = saliencyWithSmoothing.Min(p => p.Value);
        var maxS = saliencyWithSmoothing.Max(p => p.Value);
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
        var img = new SequentialMatrix<Rgb8>(10, 10);
        img.SetAll(new Rgb8(0, 0, 0));
        for (int y = 3; y <= 6; y++)
        {
            for (int x = 3; x <= 6; x++)
            {
                img[x, y] = new Rgb8(255, 0, 0);
            }
        }

        var saliency = GlobalContrastSaliency.ComputeSaliency(img, enableColorSpaceSmoothing: false);

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
        var img = new SequentialMatrix<Rgb8>(10, 10);
        img.SetAll(new Rgb8(100, 150, 200));

        var withSmoothing = GlobalContrastSaliency.ComputeSaliency(img, enableColorSpaceSmoothing: true);
        var withoutSmoothing = GlobalContrastSaliency.ComputeSaliency(img, enableColorSpaceSmoothing: false);

        Assert.Equal(10, withSmoothing.Width);
        Assert.Equal(10, withSmoothing.Height);
        Assert.All(withSmoothing, p =>
        {
            Assert.False(double.IsNaN(p.Value));
            Assert.Equal(0.0, p.Value, 4);
        });
        Assert.All(withoutSmoothing, p =>
        {
            Assert.False(double.IsNaN(p.Value));
            Assert.Equal(0.0, p.Value, 4);
        });
    }

    /// <summary>
    /// Verifies that global contrast saliency on a 2-color image with smoothing enabled does not produce NaN.
    /// </summary>
    [Fact]
    public void ComputeSaliency_ContrastingObjectWithSmoothing_ShouldNotProduceNaN()
    {
        var img = new SequentialMatrix<Rgb8>(10, 10);
        img.SetAll(new Rgb8(0, 0, 0));
        for (int y = 3; y <= 6; y++)
        {
            for (int x = 3; x <= 6; x++)
            {
                img[x, y] = new Rgb8(255, 0, 0);
            }
        }

        var saliency = GlobalContrastSaliency.ComputeSaliency(img, enableColorSpaceSmoothing: true);

        Assert.All(saliency, p => Assert.False(double.IsNaN(p.Value)));
        Assert.Equal(1.0, saliency[5, 5].Value, 4);
        Assert.Equal(0.0, saliency[0, 0].Value, 4);
    }

    [Fact]
    public void ComputeSaliency_EmptyImage_ShouldReturnEmptyMatrix()
    {
        var emptyImg = new SequentialMatrix<Rgb8>(0, 0);
        Matrix<DoublePixel> globalEmpty = GlobalContrastSaliency.ComputeSaliency(emptyImg);
        Assert.Empty(globalEmpty);
        Assert.Equal(0, globalEmpty.Width);
        Assert.Equal(0, globalEmpty.Height);
    }
}
