namespace Leiter.Tests.Algorithms.Quantization;

using System.Collections.Generic;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Quantization;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="QuantizedPalette{TPixel, TMetric}"/>.
/// </summary>
public class QuantizedPaletteTests
{
    [Fact]
    public void Constructor_PropertyAccessors_ShouldAssignAndReturnProperties()
    {
        var image = new SequentialMatrix<Rgb8>(3, 3);
        image.SetAll(new Rgb8(200, 100, 50));
        var histogram = new BucketingHistogram<Rgb8>(static p => p);
        histogram.Increment(new Rgb8(200, 100, 50), 9);
        var metricLookup = new Dictionary<Rgb8, Lab32>
        {
            [new Rgb8(200, 100, 50)] = new Lab32(50f, 30f, 40f)
        };

        var palette = new QuantizedPalette<Rgb8, Lab32>(image, histogram, metricLookup);

        Assert.Same(image, palette.Image);
        Assert.Same(histogram, palette.Histogram);
        Assert.Same(metricLookup, palette.MetricLookup);
    }

    [Fact]
    public void Equality_RecordStructSemantics_ShouldBehaveCorrectly()
    {
        var image1 = new SequentialMatrix<Rgb8>(2, 2);
        var image2 = new SequentialMatrix<Rgb8>(2, 2);
        var histogram = new BucketingHistogram<Rgb8>(static p => p);
        var metricLookup = new Dictionary<Rgb8, Lab32>();

        var a = new QuantizedPalette<Rgb8, Lab32>(image1, histogram, metricLookup);
        var b = new QuantizedPalette<Rgb8, Lab32>(image1, histogram, metricLookup);
        var c = new QuantizedPalette<Rgb8, Lab32>(image2, histogram, metricLookup);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.NotEqual(a, c);
        Assert.False(a == c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void PaletteQuantizer_QuantizeAndTruncate_ProducesConsistentQuantizedPalette()
    {
        var img = new SequentialMatrix<Rgb8>(2, 2);
        img.SetAll(new Rgb8(120, 60, 30));

        QuantizedPalette<Rgb8, Lab32> result = PaletteQuantizer.QuantizeAndTruncate(
            img,
            static p => p,
            static p => p.ToLab32(RgbColorSpace.SRgb));

        Assert.NotNull(result.Image);
        Assert.NotNull(result.Histogram);
        Assert.NotNull(result.MetricLookup);
        Assert.Equal(2, result.Image.Width);
        Assert.Equal(2, result.Image.Height);
        Assert.Equal(1, result.Histogram.BucketCount);
        Assert.Equal(4, result.Histogram.Total);
        Assert.Single(result.MetricLookup);
    }
}
