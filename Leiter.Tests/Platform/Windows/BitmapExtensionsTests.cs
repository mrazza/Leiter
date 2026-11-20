namespace Leiter.Tests.Platform.Windows;

using System;
using System.IO;
using System.Runtime.Versioning;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.Windows;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Leiter.Platform.Windows.BitmapExtensions" />.
/// </summary>
[SupportedOSPlatform("windows")]
public class BitmapExtensionsTests
{
    [WindowsFact]
    public void BitmapExtensions_ShouldConvertCorrectly()
    {
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

        string tempPath = Path.Combine(Path.GetTempPath(), $"leiter_win_{Guid.NewGuid():N}.png");
        try
        {
            matrix.Save(tempPath);
            SequentialMatrix<Rgb8> loaded = BitmapExtensions.LoadRgb8(tempPath);
            MatrixAssert.Equal(matrix, loaded);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact]
    public void BitmapExtensions_NullOrEmptyDimensions_ShouldThrowExpectedExceptions()
    {
        Assert.Throws<ArgumentNullException>(() => ((System.Drawing.Bitmap)null!).ToSequentialMatrix());
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Rgb8>)null!).ToBitmap());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(0, 5).ToBitmap());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(5, 0).ToBitmap());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(0, 0).ToBitmap());
        Assert.Throws<ArgumentNullException>(() => BitmapExtensions.LoadRgb8(null!));
        Assert.Throws<ArgumentException>(() => BitmapExtensions.LoadRgb8("   "));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Rgb8>)null!).Save("out.png"));
        Assert.Throws<ArgumentException>(() => new SequentialMatrix<Rgb8>(2, 2).Save(" "));
    }
}
