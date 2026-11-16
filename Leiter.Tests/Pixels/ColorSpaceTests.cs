
using Xunit;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Leiter.Algorithms;
using System;

namespace Leiter.Tests.Pixels;

/// <summary>
/// Provides unit tests for <see cref="RgbColorSpaceConversions" /> and <see cref="Leiter.Algorithms.PixelConversion" />.
/// </summary>
public class ColorSpaceTests
{
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

    [Fact]
    public void PixelConversion_InvalidColorSpace_ShouldThrow()
    {
        var rgb64 = new Rgb64(0.5, 0.5, 0.5);
        var xyz = new Xyz32(0.5f, 0.5f, 0.5f);
        Assert.Throws<NotImplementedException>(() => rgb64.ToXyz32((RgbColorSpace)999));
        Assert.Throws<NotImplementedException>(() => xyz.ToRgb64((RgbColorSpace)999));
    }

    /// <summary>
    /// Verifies that converting out-of-gamut <see cref="Rgb64"/> values to <see cref="Rgb8"/> clamps to byte range.
    /// </summary>
    [Fact]
    public void ToRgb8_OutOfGamut_ShouldClampToByteRange()
    {
        var outOfGamut = new Rgb64(-0.5, 1.5, double.NaN);
        Assert.Equal(new Rgb8(0, 255, 0), outOfGamut.ToRgb8());

        var mixed = new Rgb64(-10.0, 2.0, 0.5);
        Assert.Equal(new Rgb8(0, 255, 127), mixed.ToRgb8());
    }

    [Fact]
    public void Lab32_RoundTrip_D65_And_IlluminantE_ShouldBeCloseToIdentity()
    {
        foreach (Illuminant illuminant in new[] { Illuminant.D65, Illuminant.E })
        {
            var bright = new Rgb8(210, 120, 65);
            Lab32 brightLab = bright.ToLab32(RgbColorSpace.sRGB, illuminant);
            Rgb8 brightBack = brightLab.ToRgb8(RgbColorSpace.sRGB, illuminant);
            Assert.True(Math.Abs(bright.R - brightBack.R) <= 1);
            Assert.True(Math.Abs(bright.G - brightBack.G) <= 1);
            Assert.True(Math.Abs(bright.B - brightBack.B) <= 1);

            // Very dark RGB exercises the linear segment below epsilon in LabTransfer/InverseLabTransfer.
            var dark = new Rgb64(3.0 / 255.0, 5.0 / 255.0, 8.0 / 255.0);
            Lab32 darkLab = dark.ToLab32(RgbColorSpace.LinearRGB, illuminant);
            Rgb64 darkBack = darkLab.ToRgb64(RgbColorSpace.LinearRGB, illuminant);
            Assert.Equal(dark.R, darkBack.R, 4);
            Assert.Equal(dark.G, darkBack.G, 4);
            Assert.Equal(dark.B, darkBack.B, 4);

            var xyz = new Xyz32(0.35f, 0.28f, 0.42f);
            Xyz32 xyzBack = xyz.ToLab32(illuminant).ToXyz32(illuminant);
            Assert.Equal(xyz.X, xyzBack.X, 4);
            Assert.Equal(xyz.Y, xyzBack.Y, 4);
            Assert.Equal(xyz.Z, xyzBack.Z, 4);
        }

        // Verify D65 and Illuminant E produce distinct Lab coordinates for the same XYZ color.
        var sampleXyz = new Xyz32(0.5f, 0.5f, 0.5f);
        Lab32 labD65 = sampleXyz.ToLab32(Illuminant.D65);
        Lab32 labE = sampleXyz.ToLab32(Illuminant.E);
        Assert.NotEqual(labD65.A, labE.A);
        Assert.NotEqual(labD65.B, labE.B);

        // Default D65 convenience overloads round-trip cleanly.
        var mid = new Rgb64(0.4, 0.7, 0.25);
        Rgb64 midBack = mid.ToLab32(RgbColorSpace.sRGB).ToRgb64(RgbColorSpace.sRGB);
        Assert.Equal(mid.R, midBack.R, 4);
        Assert.Equal(mid.G, midBack.G, 4);
        Assert.Equal(mid.B, midBack.B, 4);

        Assert.Throws<NotImplementedException>(() => sampleXyz.ToLab32((Illuminant)999));
        Assert.Throws<NotImplementedException>(() => labD65.ToXyz32((Illuminant)999));
    }

    [Fact]
    public void ColorMetrics_HueIntensitySaturationAndChroma_ShouldComputeCorrectly()
    {
        Assert.Equal(0.0, new Rgb8(255, 0, 0).GetHueDegrees(), 6);
        Assert.Equal(60.0, new Rgb8(255, 255, 0).GetHueDegrees(), 6);
        Assert.Equal(120.0, new Rgb8(0, 255, 0).GetHueDegrees(), 6);
        Assert.Equal(180.0, new Rgb8(0, 255, 255).GetHueDegrees(), 6);
        Assert.Equal(240.0, new Rgb8(0, 0, 255).GetHueDegrees(), 6);
        Assert.Equal(300.0, new Rgb8(255, 0, 255).GetHueDegrees(), 6);
        Assert.Equal(0.0, new Rgb8(128, 128, 128).GetHueDegrees(), 6);

        Assert.Equal(1.0 / 3.0, new Rgb8(255, 0, 0).GetIntensity(), 6);
        Assert.Equal(1.0, new Rgb8(255, 0, 0).GetSaturation(), 6);
        Assert.Equal(0.0, new Rgb8(128, 128, 128).GetSaturation(), 6);
        Assert.Equal(0.0, new Rgb64(0.0, 0.0, 0.0).GetSaturation(), 6);

        var warm = new Rgb64(0.6, 0.3, 0.0);
        Assert.Equal(30.0, warm.GetHueDegrees(), 6);
        Assert.Equal(0.3, warm.GetIntensity(), 6);
        Assert.Equal(1.0, warm.GetSaturation(), 6);

        var lab = new Lab32(50f, 3f, 4f);
        Assert.Equal(5.0, lab.GetChroma(), 6);
        Assert.Equal(0.5, lab.GetNormalizedLightness(), 6);
        Assert.Equal(Math.Atan2(4.0, 3.0) * (180.0 / Math.PI), lab.GetHueDegrees(), 6);
        Assert.Equal(135.0, new Lab32(50f, -5f, 5f).GetHueDegrees(), 6);
        Assert.Equal(270.0, new Lab32(50f, 0f, -5f).GetHueDegrees(), 6);
        Assert.Equal(0.0, new Lab32(50f, 0f, 0f).GetHueDegrees(), 6);

        Assert.Equal(15.0, ColorMetrics.HueDifferenceDegrees(355.0, 10.0), 6);
        Assert.Equal(15.0, ColorMetrics.HueDifferenceDegrees(10.0, 355.0), 6);
        Assert.Equal(180.0, ColorMetrics.HueDifferenceDegrees(0.0, 180.0), 6);
    }
}
