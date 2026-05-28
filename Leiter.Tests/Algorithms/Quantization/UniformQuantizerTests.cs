
using Xunit;
using Leiter.Pixels;
using Leiter.Algorithms.Quantization;
using System;

namespace Leiter.Tests.Algorithms.Quantization;

public class UniformQuantizerTests
{
    [Fact]
    public void QuantizeToMidpoint_ShouldQuantizeCorrectly()
    {
        var pixel = new Rgb8(100, 150, 200);

        // Max levels (256) should return the same pixel
        Assert.Equal(pixel, UniformQuantizer.QuantizeToMidpoint(pixel, 256));

        // Test normal levels
        var quantized = UniformQuantizer.QuantizeToMidpoint(pixel, 4);
        // levelsPerChannel = 4 -> step size is 64. Midpoints are 32, 96, 160, 224
        // R: 100 -> 100 * 4 / 256 = 1. 1 * 64 + 32 = 96
        // G: 150 -> 150 * 4 / 256 = 2. 2 * 64 + 32 = 160
        // B: 200 -> 200 * 4 / 256 = 3. 3 * 64 + 32 = 224
        Assert.Equal(96, quantized.R);
        Assert.Equal(160, quantized.G);
        Assert.Equal(224, quantized.B);
    }

    [Fact]
    public void QuantizeToMidpoint_InvalidLevels_ShouldThrow()
    {
        var pixel = new Rgb8(100, 150, 200);
        Assert.Throws<ArgumentException>(() => UniformQuantizer.QuantizeToMidpoint(pixel, 0));
        Assert.Throws<ArgumentException>(() => UniformQuantizer.QuantizeToMidpoint(pixel, 257));
    }

    [Fact]
    public void QuantizeBitsToMidpoint_ShouldQuantizeCorrectly()
    {
        var pixel = new Rgb8(100, 150, 200);

        // Max bits (8) should return the same pixel
        Assert.Equal(pixel, UniformQuantizer.QuantizeBitsToMidpoint(pixel, 8));

        var quantized = UniformQuantizer.QuantizeBitsToMidpoint(pixel, 2); // 2 bits per channel
        // shift = 6
        // R: 100 -> (100 >> 6) = 1. (1 << 6) = 64. Midpoint addition (1 << 5) = 32. Total 96
        // G: 150 -> (150 >> 6) = 2. (2 << 6) = 128. Midpoint addition (1 << 5) = 32. Total 160
        // B: 200 -> (200 >> 6) = 3. (3 << 6) = 192. Midpoint addition (1 << 5) = 32. Total 224
        Assert.Equal(96, quantized.R);
        Assert.Equal(160, quantized.G);
        Assert.Equal(224, quantized.B);
    }

    [Fact]
    public void QuantizeBitsToMidpoint_InvalidBits_ShouldThrow()
    {
        var pixel = new Rgb8(100, 150, 200);
        Assert.Throws<ArgumentException>(() => UniformQuantizer.QuantizeBitsToMidpoint(pixel, 0));
        Assert.Throws<ArgumentException>(() => UniformQuantizer.QuantizeBitsToMidpoint(pixel, 9));
    }
}
