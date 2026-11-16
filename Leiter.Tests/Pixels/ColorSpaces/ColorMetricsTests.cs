namespace Leiter.Tests.Pixels.ColorSpaces;

using System;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="ColorMetrics"/>.
/// </summary>
public class ColorMetricsTests
{
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
        Assert.Equal(0.0, new Rgb64(1.0, 0.0, 1e-17).GetHueDegrees(), 6);

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
