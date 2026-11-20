namespace Leiter.Tests.Algorithms.Quantization;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Quantization;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="PaletteQuantizer" />.
/// </summary>
public class PaletteQuantizerTests
{
    /// <summary>
    /// Verifies that an empty image produces an empty quantized palette without throwing.
    /// </summary>
    [Fact]
    public void QuantizeAndTruncate_EmptyImage_ShouldReturnEmptyPalette()
    {
        var empty = new SequentialMatrix<Rgb8>(0, 5);

        var result = PaletteQuantizer.QuantizeAndTruncate(
            empty,
            static pixel => UniformQuantizer.QuantizeToMidpoint(pixel, 12),
            static pixel => pixel.ToLab32(RgbColorSpace.SRgb));

        Assert.Equal(0, result.Image.Width);
        Assert.Equal(5, result.Image.Height);
        Assert.Empty(result.Image);
        Assert.Equal(0, result.Histogram.BucketCount);
        Assert.Equal(0, result.Histogram.Total);
        Assert.Empty(result.MetricLookup);
    }

    /// <summary>
    /// Verifies that rare colors outside the coverage fraction are remapped to the nearest retained color in metric space.
    /// </summary>
    [Fact]
    public void QuantizeAndTruncate_RareColors_ShouldTruncateToCoverageAndRemapInMetricSpace()
    {
        var img = new SequentialMatrix<Rgb8>(10, 10);
        var dark = new Rgb8(0, 0, 0);
        var light = new Rgb8(255, 255, 255);
        var rareNearDark = new Rgb8(8, 8, 8);
        var rareNearLight = new Rgb8(248, 248, 248);

        for (int i = 0; i < 60; i++)
        {
            img[i] = dark;
        }

        for (int i = 60; i < 95; i++)
        {
            img[i] = light;
        }

        for (int i = 95; i < 98; i++)
        {
            img[i] = rareNearDark;
        }

        for (int i = 98; i < 100; i++)
        {
            img[i] = rareNearLight;
        }

        var result = PaletteQuantizer.QuantizeAndTruncate(
            img,
            static pixel => pixel,
            static pixel => pixel.ToLab32(RgbColorSpace.SRgb),
            coverageFraction: 0.95);

        Assert.Equal(2, result.Histogram.BucketCount);
        Assert.Equal(2, result.MetricLookup.Count);
        Assert.Equal(100, result.Histogram.Total);
        Assert.Equal(63, result.Histogram[dark]);
        Assert.Equal(37, result.Histogram[light]);
        Assert.Equal(dark, result.Image[95]);
        Assert.Equal(dark, result.Image[97]);
        Assert.Equal(light, result.Image[98]);
        Assert.Equal(light, result.Image[99]);
    }

    /// <summary>
    /// Verifies that the single-type overload works on non-Rgb8 pixel types and breaks ties deterministically by first-seen order.
    /// </summary>
    [Fact]
    public void QuantizeAndTruncate_SingleTypeOverload_ShouldSupportGenericPixelsAndDeterministicTieBreaking()
    {
        var labImg = new SequentialMatrix<Lab32>(2, 2);
        var first = new Lab32(10f, 0f, 0f);
        var second = new Lab32(90f, 0f, 0f);
        var third = new Lab32(12f, 0f, 0f);
        var fourth = new Lab32(88f, 0f, 0f);
        labImg[0] = first;
        labImg[1] = second;
        labImg[2] = third;
        labImg[3] = fourth;

        var labResult = PaletteQuantizer.QuantizeAndTruncate(
            labImg,
            static pixel => pixel,
            coverageFraction: 0.5);

        Assert.Equal(2, labResult.Histogram.BucketCount);
        Assert.Equal(2, labResult.Histogram[first]);
        Assert.Equal(2, labResult.Histogram[second]);
        Assert.Equal(first, labResult.Image[2]);
        Assert.Equal(second, labResult.Image[3]);

        // Equidistant bucket tie (distance 5 from both 0.0 and 10.0) should pick 0.0 because it was seen first.
        var doubleImg = new SequentialMatrix<DoublePixel>(new Size(5, 1), [0.0, 0.0, 10.0, 10.0, 5.0]);
        var doubleResult = PaletteQuantizer.QuantizeAndTruncate(
            doubleImg,
            static pixel => pixel,
            coverageFraction: 0.8);

        Assert.Equal(2, doubleResult.Histogram.BucketCount);
        Assert.Equal(3, doubleResult.Histogram[new DoublePixel(0.0)]);
        Assert.Equal(2, doubleResult.Histogram[new DoublePixel(10.0)]);
        Assert.Equal(new DoublePixel(0.0), doubleResult.Image[4]);
    }

    /// <summary>
    /// Verifies that full coverage (1.0) retains all distinct buckets including rare outliers without remapping.
    /// </summary>
    [Fact]
    public void QuantizeAndTruncate_FullCoverage_ShouldRetainAllBuckets()
    {
        SequentialMatrix<Rgb8> img = TestMatrices.Constant(10, 10, new Rgb8(0, 0, 0));
        img[0, 0] = new Rgb8(255, 0, 0);

        var result = PaletteQuantizer.QuantizeAndTruncate(
            img,
            static pixel => pixel,
            static pixel => pixel.ToLab32(RgbColorSpace.SRgb),
            coverageFraction: 1.0);

        Assert.Equal(2, result.Histogram.BucketCount);
        Assert.Equal(99, result.Histogram[new Rgb8(0, 0, 0)]);
        Assert.Equal(1, result.Histogram[new Rgb8(255, 0, 0)]);
        Assert.Equal(new Rgb8(255, 0, 0), result.Image[0, 0]);
    }

    /// <summary>
    /// Verifies that invalid arguments throw the expected exceptions.
    /// </summary>
    [Fact]
    public void QuantizeAndTruncate_InvalidArguments_ShouldThrow()
    {
        var img = new SequentialMatrix<Rgb8>(2, 2);

        Assert.Throws<ArgumentNullException>(() =>
            PaletteQuantizer.QuantizeAndTruncate<Rgb8, Lab32>(null!, static p => p, static p => p.ToLab32(RgbColorSpace.SRgb)));
        Assert.Throws<ArgumentNullException>(() =>
            PaletteQuantizer.QuantizeAndTruncate<Rgb8, Lab32>(img, null!, static p => p.ToLab32(RgbColorSpace.SRgb)));
        Assert.Throws<ArgumentNullException>(() =>
            PaletteQuantizer.QuantizeAndTruncate<Rgb8, Lab32>(img, static p => p, null!));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaletteQuantizer.QuantizeAndTruncate(img, static p => p, coverageFraction: 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaletteQuantizer.QuantizeAndTruncate(img, static p => p, coverageFraction: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaletteQuantizer.QuantizeAndTruncate(img, static p => p, coverageFraction: 1.01));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaletteQuantizer.QuantizeAndTruncate(img, static p => p, coverageFraction: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaletteQuantizer.QuantizeAndTruncate(img, static p => p, coverageFraction: double.PositiveInfinity));

        var view = new MatrixView<Rgb8>(img, 0, 0, 2, 2, EdgeHandling.Extend);
        var viewResult = PaletteQuantizer.QuantizeAndTruncate(view, static p => p);
        Assert.Equal(view.Size, viewResult.Image.Size);
    }
}
