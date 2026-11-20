namespace Leiter.Tests.Platform.Windows;

using System;
using System.Collections;
using System.Collections.Generic;
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

        string prefix = $"leiter_win_{Guid.NewGuid():N}";
        string pngPath = Path.Combine(Path.GetTempPath(), $"{prefix}.png");
        string bmpPath = Path.Combine(Path.GetTempPath(), $"{prefix}.bmp");
        string jpgPath = Path.Combine(Path.GetTempPath(), $"{prefix}.jpg");
        string webpPath = Path.Combine(Path.GetTempPath(), $"{prefix}.webp");
        string noExtPath = Path.Combine(Path.GetTempPath(), prefix);

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

            Assert.Throws<NotSupportedException>(() => matrix.SaveBitmap(webpPath));
            Assert.Throws<NotSupportedException>(() => matrix.SaveBitmap(noExtPath));
        }
        finally
        {
            foreach (string path in new[] { pngPath, bmpPath, jpgPath, webpPath, noExtPath })
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

    private sealed class ThrowingReadOnlyMatrix(int width, int height) : IReadOnlyMatrix<Rgb8>
    {
        public int Width => width;
        public int Height => height;
        public Size Size => new(Width, Height);
        public int Count => Width * Height;
        public Rgb8 this[int index] => throw new InvalidOperationException("Simulated read failure.");
        public Rgb8 this[int x, int y] => (x == Width - 1 && y == Height - 1)
            ? throw new InvalidOperationException("Simulated read failure.")
            : new Rgb8(10, 20, 30);
        public Rgb8 this[Coord coord] => this[coord.X, coord.Y];
        public Rgb8 GetElement(int index) => this[index];
        public Rgb8 GetElement(int x, int y) => this[x, y];
        public IEnumerator<Rgb8> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
