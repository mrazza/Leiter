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

        string prefix = $"leiter_win_{Guid.NewGuid():N}";
        string pngPath = Path.Combine(Path.GetTempPath(), $"{prefix}.png");
        string bmpPath = Path.Combine(Path.GetTempPath(), $"{prefix}.bmp");
        string jpgPath = Path.Combine(Path.GetTempPath(), $"{prefix}.jpg");

        try
        {
            matrix.SaveBitmap(pngPath);
            SequentialMatrix<Rgb8> loadedPng = BitmapExtensions.LoadRgb8(pngPath);
            MatrixAssert.Equal(matrix, loadedPng);

            matrix.SaveBitmap(bmpPath);
            SequentialMatrix<Rgb8> loadedBmp = BitmapExtensions.LoadRgb8(bmpPath);
            MatrixAssert.Equal(matrix, loadedBmp);

            matrix.SaveBitmap(jpgPath);
            Assert.True(File.Exists(jpgPath));
            SequentialMatrix<Rgb8> loadedJpg = BitmapExtensions.LoadRgb8(jpgPath);
            Assert.Equal(matrix.Size, loadedJpg.Size);
        }
        finally
        {
            foreach (string path in new[] { pngPath, bmpPath, jpgPath })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    [WindowsFact]
    public void ToBitmap_WhenMatrixIndexerThrows_ShouldDisposeAndRethrow()
    {
        var throwing = new ThrowingReadOnlyMatrix(2, 2);
        Assert.Throws<InvalidOperationException>(() => throwing.ToBitmap());
    }

    [WindowsFact]
    public void ToBitmap_NonSequentialMatrixView_AndDistinctChannels_ShouldPreserveExactBgrOrder()
    {
        var source = new SequentialMatrix<Rgb8>(4, 3);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                source[x, y] = new Rgb8((byte)(15 + (x * 40)), (byte)(90 + (y * 30)), (byte)(225 - (x * 25)));
            }
        }

        using System.Drawing.Bitmap seqBmp = source.ToBitmap();
        MatrixAssert.Equal(source, seqBmp.ToSequentialMatrix());

        var view = new MatrixView<Rgb8>(source, 1, 1, 2, 2, EdgeHandling.Extend);
        using System.Drawing.Bitmap viewBmp = view.ToBitmap();
        MatrixAssert.Equal(view.ToSequentialMatrix(), viewBmp.ToSequentialMatrix());
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
        Assert.Throws<ArgumentException>(() => BitmapExtensions.LoadRgb8(string.Empty));
        Assert.Throws<ArgumentException>(() => BitmapExtensions.LoadRgb8("   "));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Rgb8>)null!).SaveBitmap("out.png"));
        Assert.Throws<ArgumentNullException>(() => new SequentialMatrix<Rgb8>(2, 2).SaveBitmap(null!));
        Assert.Throws<ArgumentException>(() => new SequentialMatrix<Rgb8>(2, 2).SaveBitmap(string.Empty));
        Assert.Throws<ArgumentException>(() => new SequentialMatrix<Rgb8>(2, 2).SaveBitmap(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(0, 5).SaveBitmap("out.png"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(5, 0).SaveBitmap("out.png"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(0, 0).SaveBitmap("out.png"));
    }

    [Theory]
    [InlineData("out.webp")]
    [InlineData("out.unknown")]
    [InlineData("out")]
    [InlineData("out.")]
    public void SaveBitmap_UnsupportedOrMissingExtension_ShouldThrowNotSupportedBeforeAllocation(string fileName)
    {
        string path = Path.Combine(Path.GetTempPath(), $"leiter_win_{Guid.NewGuid():N}_{fileName}");
        var throwing = new ThrowingReadOnlyMatrix(2, 2);

        try
        {
            Assert.Throws<NotSupportedException>(() => throwing.SaveBitmap(path));
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
