namespace Leiter.Platform.Windows;

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Contains extension methods for interoperating between the Windows platform <see cref="Bitmap"/>
/// and Leiter types.
/// </summary>
[SupportedOSPlatform("windows")]
public static class BitmapExtensions
{
    /// <summary>
    /// Converts a <see cref="Bitmap"/> to a <see cref="SequentialMatrix{Rgb8}"/>.
    /// </summary>
    /// <param name="bitmap">The bitmap to convert.</param>
    /// <returns>The Leiter matrix with the same pixel data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="bitmap"/> is <see langword="null"/>.</exception>
    public static unsafe SequentialMatrix<Rgb8> ToSequentialMatrix(this Bitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        int width = bitmap.Width;
        int height = bitmap.Height;
        var matrix = new SequentialMatrix<Rgb8>(width, height);
        if (width == 0 || height == 0)
            return matrix;

        var rect = new Rectangle(0, 0, width, height);
        BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            byte* scan0 = (byte*)bitmapData.Scan0;
            int stride = bitmapData.Stride;

            for (int y = 0; y < height; y++)
            {
                byte* row = scan0 + ((nint)y * stride);
                for (int x = 0; x < width; x++)
                {
                    int offset = x * 3;
                    matrix[x, y] = new Rgb8(row[offset + 2], row[offset + 1], row[offset]);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return matrix;
    }

    /// <summary>
    /// Converts a Leiter <see cref="IReadOnlyMatrix{Rgb8}"/> to a <see cref="Bitmap"/>.
    /// </summary>
    /// <param name="matrix">The Leiter matrix to convert to a <see cref="Bitmap"/>.</param>
    /// <returns>The bitmap based on the specified matrix.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="matrix"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="matrix"/> has a width or height less than or equal to zero.</exception>
    public static unsafe Bitmap ToBitmap(this IReadOnlyMatrix<Rgb8> matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        int width = matrix.Width;
        int height = matrix.Height;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 0, nameof(matrix));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(height, 0, nameof(matrix));

        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);

        var rect = new Rectangle(0, 0, width, height);
        BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            byte* scan0 = (byte*)bitmapData.Scan0;
            int stride = bitmapData.Stride;

            for (int y = 0; y < height; y++)
            {
                byte* row = scan0 + ((nint)y * stride);
                for (int x = 0; x < width; x++)
                {
                    Rgb8 pixel = matrix[x, y];
                    int offset = x * 3;
                    row[offset] = pixel.B;
                    row[offset + 1] = pixel.G;
                    row[offset + 2] = pixel.R;
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        return bitmap;
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

        using var bitmap = new Bitmap(path);
        return bitmap.ToSequentialMatrix();
    }

    /// <summary>
    /// Saves an <see cref="IReadOnlyMatrix{Rgb8}"/> to disk at <paramref name="path"/>, determining the <see cref="ImageFormat"/> from the file extension.
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

        using Bitmap bitmap = matrix.ToBitmap();
        ImageFormat format = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".bmp" => ImageFormat.Bmp,
            ".gif" => ImageFormat.Gif,
            ".tif" or ".tiff" => ImageFormat.Tiff,
            _ => ImageFormat.Png
        };
        bitmap.Save(path, format);
    }
}
