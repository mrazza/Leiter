namespace Leiter.Platform.ImageSharp;

using Leiter.Core;
using Leiter.Pixels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Contains cross-platform extension methods for interoperating between <c>SixLabors.ImageSharp</c> <see cref="Image{TPixel}"/>
/// and Leiter types.
/// </summary>
public static class ImageExtensions
{
    /// <summary>
    /// Converts a <see cref="Image{Rgb24}"/> to a <see cref="SequentialMatrix{Rgb8}"/>.
    /// </summary>
    /// <param name="image">The image to convert.</param>
    /// <returns>The Leiter matrix with the same data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    public static SequentialMatrix<Rgb8> ToSequentialMatrix(this Image<Rgb24> image)
    {
        ArgumentNullException.ThrowIfNull(image);

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
    /// Converts a Leiter <see cref="IReadOnlyMatrix{Rgb8}"/> to a <see cref="Image{Rgb24}"/>.
    /// </summary>
    /// <param name="matrix">The Leiter matrix to convert.</param>
    /// <returns>The image based on the specified matrix.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="matrix"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="matrix"/> has a width or height less than or equal to zero.</exception>
    public static Image<Rgb24> ToImage(this IReadOnlyMatrix<Rgb8> matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(matrix.Width, 0, nameof(matrix));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(matrix.Height, 0, nameof(matrix));

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
