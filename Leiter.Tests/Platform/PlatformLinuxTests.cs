
using Xunit;
using Leiter.Platform.Linux;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Leiter.Core;
using Leiter.Pixels;
using System.IO;
using System.Runtime.Versioning;

namespace Leiter.Tests.Platform;

/// <summary>
/// Provides unit tests for <see cref="PlatformLinuxTests" />.
/// </summary>
[SupportedOSPlatform("linux")]
public class PlatformLinuxTests
{
    /// <summary>
    /// Verifies that the color extensions should convert correctly behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the image extensions should convert correctly behaves correctly.
    /// </summary>
    [Fact]
    public void ImageExtensions_ShouldConvertCorrectly()
    {
        using var img = new Image<Rgb24>(5, 5);
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    row[x] = new Rgb24((byte)(x * 10), (byte)(y * 10), 0);
                }
            }
        });

        // Convert to SequentialMatrix
        var matrix = img.ToSequentialMatrix();
        Assert.Equal(5, matrix.Width);
        Assert.Equal(5, matrix.Height);
        Assert.Equal(20, matrix[2, 0].R);
        Assert.Equal(30, matrix[0, 3].G);

        // Convert back to Image
        using var convertedImg = matrix.ToImage();
        Assert.Equal(img.Width, convertedImg.Width);
        Assert.Equal(img.Height, convertedImg.Height);

        convertedImg.ProcessPixelRows(accessor =>
        {
            var row = accessor.GetRowSpan(3);
            Assert.Equal(new Rgb24(0, 30, 0), row[0]);
        });
    }
}
