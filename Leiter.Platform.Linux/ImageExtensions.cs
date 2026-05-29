namespace Leiter.Platform.Linux;

using Leiter.Core;
using Leiter.Pixels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Runtime.Versioning;

/// <summary>
/// Contains extension methods for interoping between the Linux platform (via ImageSharp) <see cref="Image{TPixel}"/>
/// and Leiter types.
/// </summary>
[SupportedOSPlatform("linux")]
public static class ImageExtensions
{
    /// <summary>
    /// Converts a <see cref="Image{Rgb24}"/> to a <see cref="SequentialMatrix{T}"/>.
    /// </summary>
    /// <param name="image">The image to convert.</param>
    /// <returns>The Leiter matrix with the same data.</returns>
    public static SequentialMatrix<Rgb8> ToSequentialMatrix(this Image<Rgb24> image)
    {
        var matrix = new SequentialMatrix<Rgb8>(image.Width, image.Height);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    matrix[x, y] = pixelRow[x].ToRgb8();
                }
            }
        });
        return matrix;
    }

    /// <summary>
    /// Converts a Leiter <see cref="IReadOnlyMatrix{T}"/> to a <see cref="Image{Rgb24}"/>.
    /// </summary>
    /// <param name="matrix">The Leiter matrix to a <see cref="Image{Rgb24}"/>.</param>
    /// <returns>The Image based on the specified matrix.</returns>
    public static Image<Rgb24> ToImage(this IReadOnlyMatrix<Rgb8> matrix)
    {
        var image = new Image<Rgb24>(matrix.Width, matrix.Height);
        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var pixelRow = accessor.GetRowSpan(y);
                for (int x = 0; x < pixelRow.Length; x++)
                {
                    pixelRow[x] = matrix[x, y].ToRgb24();
                }
            }
        });
        return image;
    }
}
