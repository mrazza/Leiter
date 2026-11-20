namespace Leiter.Tests.Platform.ImageSharp;

using System;
using System.IO;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Platform.ImageSharp;
using Leiter.Tests.TestUtils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Leiter.Platform.ImageSharp.ImageExtensions" />.
/// </summary>
public class ImageExtensionsTests
{
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

    [Fact]
    public void ImageExtensions_NullOrEmptyDimensions_ShouldThrowExpectedExceptions()
    {
        Assert.Throws<ArgumentNullException>(() => ((Image<Rgb24>)null!).ToSequentialMatrix());
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Rgb8>)null!).ToImage());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(0, 5).ToImage());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(5, 0).ToImage());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(0, 0).ToImage());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(0, 5).SaveImage("out.png"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(5, 0).SaveImage("out.png"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Rgb8>(0, 0).SaveImage("out.png"));
    }

    [Fact]
    public void LoadRgb8_And_SaveImage_ShouldRoundTripAndValidateArguments()
    {
        var matrix = new SequentialMatrix<Rgb8>(3, 2);
        matrix[0, 0] = new Rgb8(10, 20, 30);
        matrix[2, 1] = new Rgb8(200, 150, 100);

        string tempPath = Path.Combine(Path.GetTempPath(), $"leiter_imgsharp_{Guid.NewGuid():N}.png");
        try
        {
            matrix.SaveImage(tempPath);
            SequentialMatrix<Rgb8> loaded = Leiter.Platform.ImageSharp.ImageExtensions.LoadRgb8(tempPath);
            MatrixAssert.Equal(matrix, loaded);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        Assert.Throws<ArgumentNullException>(() => Leiter.Platform.ImageSharp.ImageExtensions.LoadRgb8(null!));
        Assert.Throws<ArgumentException>(() => Leiter.Platform.ImageSharp.ImageExtensions.LoadRgb8(string.Empty));
        Assert.Throws<ArgumentException>(() => Leiter.Platform.ImageSharp.ImageExtensions.LoadRgb8("   "));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Rgb8>)null!).SaveImage("out.png"));
        Assert.Throws<ArgumentNullException>(() => matrix.SaveImage(null!));
        Assert.Throws<ArgumentException>(() => Leiter.Platform.ImageSharp.ImageExtensions.SaveImage(matrix, string.Empty));
        Assert.Throws<ArgumentException>(() => Leiter.Platform.ImageSharp.ImageExtensions.SaveImage(matrix, " "));
    }

    [Fact]
    public void ToImage_WhenMatrixIndexerThrows_ShouldDisposeAndRethrow()
    {
        var throwing = new ThrowingReadOnlyMatrix(2, 2);
        Assert.Throws<InvalidOperationException>(() => throwing.ToImage());
    }

    [Fact]
    public void ToImage_NonSequentialMatrixView_AndChannelOrder_ShouldPreserveExactPixels()
    {
        var source = new SequentialMatrix<Rgb8>(4, 3);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                source[x, y] = new Rgb8((byte)(10 + (x * 40)), (byte)(80 + (y * 30)), (byte)(210 - (x * 20)));
            }
        }

        using Image<Rgb24> seqImage = source.ToImage();
        MatrixAssert.Equal(source, seqImage.ToSequentialMatrix());

        var view = new MatrixView<Rgb8>(source, 1, 1, 2, 2, EdgeHandling.Extend);
        using Image<Rgb24> viewImage = view.ToImage();
        MatrixAssert.Equal(view.ToSequentialMatrix(), viewImage.ToSequentialMatrix());
    }

    [Theory]
    [InlineData("out.unknown")]
    [InlineData("out.xyz")]
    [InlineData("out")]
    [InlineData("out.")]
    public void SaveImage_UnsupportedOrMissingExtension_ShouldThrowNotSupportedBeforeAllocation(string fileName)
    {
        string path = Path.Combine(Path.GetTempPath(), $"leiter_imgsharp_{Guid.NewGuid():N}_{fileName}");
        var throwing = new ThrowingReadOnlyMatrix(2, 2);

        try
        {
            // ThrowingReadOnlyMatrix throws InvalidOperationException if ToImage() reads pixels,
            // so NotSupportedException proves the extension is rejected before allocating an image.
            Assert.Throws<NotSupportedException>(() => throwing.SaveImage(path));
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

    [Fact]
    public void SaveImage_UnsupportedExtensionWithExistingFile_ShouldThrowAndNotCorruptFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"leiter_imgsharp_{Guid.NewGuid():N}.unknown");
        byte[] sentinel = [1, 2, 3, 4];
        File.WriteAllBytes(path, sentinel);
        var matrix = new SequentialMatrix<Rgb8>(2, 2);

        try
        {
            Assert.Throws<NotSupportedException>(() => matrix.SaveImage(path));
            Assert.Throws<NotSupportedException>(() => matrix.SaveImage("dir.png/no_extension"));
            Assert.Equal(sentinel, File.ReadAllBytes(path));
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
