
using Xunit;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Leiter.Algorithms;
using System;

namespace Leiter.Tests.Pixels;

/// <summary>
/// Provides unit tests or helpers for <see cref="ColorSpaceTests" />.
/// </summary>
public class ColorSpaceTests
{
    /// <summary>
    /// Verifies that the rgb to linear rgb and back should be close to identity behaves correctly.
    /// </summary>
    [Fact]
    public void SRgbToLinearRgb_AndBack_ShouldBeCloseToIdentity()
    {
        var original = new Rgb64(0.5, 0.2, 0.8);
        var linear = RgbColorSpaceConversions.SRgbToLinearRgb(original);
        var srgb = RgbColorSpaceConversions.LinearRgbToSRgb(linear);

        Assert.Equal(original.R, srgb.R, 5);
        Assert.Equal(original.G, srgb.G, 5);
        Assert.Equal(original.B, srgb.B, 5);

        // Test small values (<= 0.04045 and <= 0.0031308)
        var smallOriginal = new Rgb64(0.02, 0.01, 0.03);
        var smallLinear = RgbColorSpaceConversions.SRgbToLinearRgb(smallOriginal);
        var smallSrgb = RgbColorSpaceConversions.LinearRgbToSRgb(smallLinear);

        Assert.Equal(smallOriginal.R, smallSrgb.R, 5);
        Assert.Equal(smallOriginal.G, smallSrgb.G, 5);
        Assert.Equal(smallOriginal.B, smallSrgb.B, 5);
    }

    /// <summary>
    /// Verifies that the pixel conversion rgb8 to rgb64 to rgb8 should work behaves correctly.
    /// </summary>
    [Fact]
    public void PixelConversion_Rgb8_ToRgb64_ToRgb8_ShouldWork()
    {
        var rgb8 = new Rgb8(100, 150, 200);
        var rgb64 = rgb8.ToRgb64();

        Assert.Equal(100 / 255.0, rgb64.R, 5);
        Assert.Equal(150 / 255.0, rgb64.G, 5);
        Assert.Equal(200 / 255.0, rgb64.B, 5);

        var rgb8Back = rgb64.ToRgb8();
        Assert.Equal(rgb8.R, rgb8Back.R);
        Assert.Equal(rgb8.G, rgb8Back.G);
        Assert.Equal(rgb8.B, rgb8Back.B);
    }

    /// <summary>
    /// Executes the pixel conversion to xyz32 and rgb8 rgb64 operation.
    /// </summary>
    [Fact]
    public void PixelConversion_ToXyz32_AndRgb8Rgb64()
    {
        var rgb8 = new Rgb8(255, 0, 0); // Red
        var xyzFromRgb8 = rgb8.ToXyz32(RgbColorSpace.sRGB);
        var xyzFromRgb64 = rgb8.ToRgb64().ToXyz32(RgbColorSpace.sRGB);

        Assert.Equal(xyzFromRgb8.X, xyzFromRgb64.X, 4);
        Assert.Equal(xyzFromRgb8.Y, xyzFromRgb64.Y, 4);
        Assert.Equal(xyzFromRgb8.Z, xyzFromRgb64.Z, 4);

        // Convert back to Rgb64/8
        var rgb64Back = xyzFromRgb8.ToRgb64(RgbColorSpace.sRGB);
        Assert.Equal(1.0, rgb64Back.R, 4);
        Assert.Equal(0.0, rgb64Back.G, 4);
        Assert.Equal(0.0, rgb64Back.B, 4);

        var rgb8Back = xyzFromRgb8.ToRgb8(RgbColorSpace.sRGB);
        // Allow tiny delta due to precision rounding (e.g. 254 instead of 255)
        Assert.True(Math.Abs(255 - rgb8Back.R) <= 1);
        Assert.True(rgb8Back.G <= 1);
        Assert.True(rgb8Back.B <= 1);
    }

    /// <summary>
    /// Verifies that the pixel conversion to lab32 should work behaves correctly.
    /// </summary>
    [Fact]
    public void PixelConversion_ToLab32_ShouldWork()
    {
        var rgb8 = new Rgb8(128, 128, 128); // Grey
        var labFromRgb8 = rgb8.ToLab32(RgbColorSpace.sRGB);
        var labFromRgb64 = rgb8.ToRgb64().ToLab32(RgbColorSpace.sRGB);

        Assert.Equal(labFromRgb8.L, labFromRgb64.L, 4);
        Assert.Equal(labFromRgb8.A, labFromRgb64.A, 4);
        Assert.Equal(labFromRgb8.B, labFromRgb64.B, 4);
    }

    /// <summary>
    /// Verifies that the pixel conversion linear rgb xyz should work behaves correctly.
    /// </summary>
    [Fact]
    public void PixelConversion_LinearRgbXyz_ShouldWork()
    {
        var rgb64 = new Rgb64(0.5, 0.5, 0.5);
        var xyz = rgb64.ToXyz32(RgbColorSpace.LinearRGB);
        var rgb64Back = xyz.ToRgb64(RgbColorSpace.LinearRGB);

        Assert.Equal(0.5, rgb64Back.R, 4);
        Assert.Equal(0.5, rgb64Back.G, 4);
        Assert.Equal(0.5, rgb64Back.B, 4);
    }

    /// <summary>
    /// Verifies that the pixel conversion invalid color space should throw behaves correctly.
    /// </summary>
    [Fact]
    public void PixelConversion_InvalidColorSpace_ShouldThrow()
    {
        var rgb64 = new Rgb64(0.5, 0.5, 0.5);
        var xyz = new Xyz32(0.5f, 0.5f, 0.5f);
        Assert.Throws<NotImplementedException>(() => rgb64.ToXyz32((RgbColorSpace)999));
        Assert.Throws<NotImplementedException>(() => xyz.ToRgb64((RgbColorSpace)999));
    }
}
