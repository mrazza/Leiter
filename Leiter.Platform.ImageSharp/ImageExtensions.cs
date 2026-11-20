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

    /// <summary>
    /// Loads an image file from <paramref name="path"/> into a <see cref="SequentialMatrix{Rgb8}"/>.
    /// </summary>
    /// <param name="path">The file path of the image to load.</param>
    /// <returns>The loaded image as a <see cref="SequentialMatrix{Rgb8}"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or contains only white-space characters.</exception>
    public static SequentialMatrix<Rgb8> LoadRgb8(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using Image<Rgb24> image = Image.Load<Rgb24>(path);
        return image.ToSequentialMatrix();
    }

    /// <summary>
    /// Saves an <see cref="IReadOnlyMatrix{Rgb8}"/> to disk at <paramref name="path"/>, inferring the image encoder from the file extension.
    /// </summary>
    /// <param name="matrix">The image matrix to save.</param>
    /// <param name="path">The destination file path.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or contains only white-space characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="matrix"/> has a width or height less than or equal to zero.</exception>
    public static void Save(this IReadOnlyMatrix<Rgb8> matrix, string path)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using Image<Rgb24> image = matrix.ToImage();
        image.Save(path);
    }
}
