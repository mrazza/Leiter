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
    }

    [Fact]
    public void LoadRgb8_And_Save_ShouldRoundTripAndValidateArguments()
    {
        var matrix = new SequentialMatrix<Rgb8>(3, 2);
        matrix[0, 0] = new Rgb8(10, 20, 30);
        matrix[2, 1] = new Rgb8(200, 150, 100);

        string tempPath = Path.Combine(Path.GetTempPath(), $"leiter_imgsharp_{Guid.NewGuid():N}.png");
        try
        {
            matrix.Save(tempPath);
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
        Assert.Throws<ArgumentException>(() => Leiter.Platform.ImageSharp.ImageExtensions.LoadRgb8("   "));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Rgb8>)null!).Save("out.png"));
        Assert.Throws<ArgumentException>(() => Leiter.Platform.ImageSharp.ImageExtensions.Save(matrix, " "));
    }
}
