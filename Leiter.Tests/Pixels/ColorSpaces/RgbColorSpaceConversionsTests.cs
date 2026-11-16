namespace Leiter.Tests.Pixels.ColorSpaces;

using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="RgbColorSpaceConversions"/>.
/// </summary>
public class RgbColorSpaceConversionsTests
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
}
