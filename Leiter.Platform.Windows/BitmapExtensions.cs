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
            int rowByteCount = width * 3;
            Span<Rgb8> destSpan = matrix.AsSpan();

            for (int y = 0; y < height; y++)
            {
                ReadOnlySpan<byte> srcRow = new(scan0 + ((nint)y * stride), rowByteCount);
                Span<Rgb8> destRow = destSpan.Slice(y * width, width);
                for (int x = 0; x < width; x++)
                {
                    int offset = x * 3;
                    destRow[x] = new Rgb8(srcRow[offset + 2], srcRow[offset + 1], srcRow[offset]);
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
        try
        {
            var rect = new Rectangle(0, 0, width, height);
            BitmapData bitmapData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                byte* scan0 = (byte*)bitmapData.Scan0;
                int stride = bitmapData.Stride;
                int rowByteCount = width * 3;

                if (matrix.TryGetReadOnlySpan(out ReadOnlySpan<Rgb8> srcSpan))
                {
                    for (int y = 0; y < height; y++)
                    {
                        ReadOnlySpan<Rgb8> srcRow = srcSpan.Slice(y * width, width);
                        Span<byte> destRow = new(scan0 + ((nint)y * stride), rowByteCount);
                        for (int x = 0; x < width; x++)
                        {
                            Rgb8 pixel = srcRow[x];
                            int offset = x * 3;
                            destRow[offset] = pixel.B;
                            destRow[offset + 1] = pixel.G;
                            destRow[offset + 2] = pixel.R;
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        Span<byte> destRow = new(scan0 + ((nint)y * stride), rowByteCount);
                        for (int x = 0; x < width; x++)
                        {
                            Rgb8 pixel = matrix[x, y];
                            int offset = x * 3;
                            destRow[offset] = pixel.B;
                            destRow[offset + 1] = pixel.G;
                            destRow[offset + 2] = pixel.R;
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
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
    /// <remarks>
    /// Supported file extensions (case-insensitive) are <c>.png</c>, <c>.jpg</c>, <c>.jpeg</c>, <c>.bmp</c>, <c>.gif</c>, <c>.tif</c>, and <c>.tiff</c>.
    /// </remarks>
    /// <param name="matrix">The image matrix to save.</param>
    /// <param name="path">The destination file path.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="matrix"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty or contains only white-space characters.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="matrix"/> has a width or height less than or equal to zero.</exception>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="path"/> does not have a supported image file extension.</exception>
    public static void SaveBitmap(this IReadOnlyMatrix<Rgb8> matrix, string path)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(matrix.Width, 0, nameof(matrix));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(matrix.Height, 0, nameof(matrix));

        string extension = Path.GetExtension(path);
        ImageFormat format = extension.ToLowerInvariant() switch
        {
            ".png" => ImageFormat.Png,
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".bmp" => ImageFormat.Bmp,
            ".gif" => ImageFormat.Gif,
            ".tif" or ".tiff" => ImageFormat.Tiff,
            _ => throw new NotSupportedException(
                $"Unsupported image file extension '{extension}'. Supported extensions: .png, .jpg, .jpeg, .bmp, .gif, .tif, .tiff.")
        };

        using Bitmap bitmap = matrix.ToBitmap();
        bitmap.Save(path, format);
    }
}
