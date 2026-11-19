namespace Leiter.Tests.Platform.ImageSharp;

using Leiter.Platform.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Leiter.Platform.ImageSharp.ColorExtensions" />.
/// </summary>
public class ColorExtensionsTests
{
    [Fact]
    public void ColorExtensions_ShouldConvertCorrectly()
    {
        var original = new Rgb24(10, 20, 30);
        var leiterPixel = original.ToRgb8();
        Assert.Equal(10, leiterPixel.R);
        Assert.Equal(20, leiterPixel.G);
        Assert.Equal(30, leiterPixel.B);

        var convertedBack = leiterPixel.ToRgb24();
        Assert.Equal(original, convertedBack);
    }
}
