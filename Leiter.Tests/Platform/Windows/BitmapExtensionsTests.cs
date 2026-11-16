namespace Leiter.Tests.Platform.Windows;

using System;
using System.Runtime.Versioning;
using Leiter.Platform.Windows;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Leiter.Platform.Windows.BitmapExtensions" />.
/// </summary>
[SupportedOSPlatform("windows")]
public class BitmapExtensionsTests
{
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
