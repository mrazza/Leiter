
using Xunit;
using Leiter.Platform.Windows;
using Leiter.Core;
using Leiter.Pixels;
using System;
using System.Runtime.Versioning;

namespace Leiter.Tests.Platform;

/// <summary>
/// Provides unit tests or helpers for <see cref="PlatformWindowsTests" />.
/// </summary>
[SupportedOSPlatform("windows")]
public class PlatformWindowsTests
{
    /// <summary>
    /// Verifies that the color extensions should convert correctly behaves correctly.
    /// </summary>
    [Fact]
    public void ColorExtensions_ShouldConvertCorrectly()
    {
        if (!OperatingSystem.IsWindows()) return;

        // We use reflection/dynamic or wrapper to avoid loading types directly, but simple IsWindows check usually works if type load is deferred.
        RunColorExtensionsTest();
    }

    private void RunColorExtensionsTest()
    {
        var original = System.Drawing.Color.FromArgb(10, 20, 30);
        var leiterPixel = original.ToRgb8();
        Assert.Equal(10, leiterPixel.R);
        Assert.Equal(20, leiterPixel.G);
        Assert.Equal(30, leiterPixel.B);

        var convertedBack = leiterPixel.ToColor();
        Assert.Equal(original.R, convertedBack.R);
        Assert.Equal(original.G, convertedBack.G);
        Assert.Equal(original.B, convertedBack.B);
    }

    /// <summary>
    /// Verifies that the bitmap extensions should convert correctly behaves correctly.
    /// </summary>
    [Fact]
    public void BitmapExtensions_ShouldConvertCorrectly()
    {
        if (!OperatingSystem.IsWindows()) return;

        RunBitmapExtensionsTest();
    }

    private void RunBitmapExtensionsTest()
    {
        using var bmp = new System.Drawing.Bitmap(5, 5);
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                bmp.SetPixel(x, y, System.Drawing.Color.FromArgb(x * 10, y * 10, 0));
            }
        }

        // Convert to SequentialMatrix
        var matrix = bmp.ToSequentialMatrix();
        Assert.Equal(5, matrix.Width);
        Assert.Equal(5, matrix.Height);
        Assert.Equal(20, matrix[2, 0].R);
        Assert.Equal(30, matrix[0, 3].G);

        // Convert back to Bitmap
        using var convertedBmp = matrix.ToBitmap();
        Assert.Equal(bmp.Width, convertedBmp.Width);
        Assert.Equal(bmp.Height, convertedBmp.Height);
        Assert.Equal(System.Drawing.Color.FromArgb(255, 0, 30, 0).ToArgb(), convertedBmp.GetPixel(0, 3).ToArgb());
    }
}
