namespace Leiter.Platform.Windows;

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.Versioning;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Contains extension methods for interoping between the Windows platform <see cref="Bitmap"/>
/// and Leiter types.
/// </summary>
[SupportedOSPlatform("windows")]
public static class BitmapExtensions
{
    /// <summary>
    /// Converts a <see cref="Bitmap"/> to a <see cref="SequentialMatrix{T}"/>.
    /// </summary>
    /// <param name="bitmap">The bitmap to convert.</param>
    /// <returns>The Leiter matrix with the same pixel data.</returns>
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
    /// Converts a Leiter <see cref="IReadOnlyMatrix{T}"/> to a <see cref="Bitmap"/>.
    /// </summary>
    /// <param name="matrix">The Leiter matrix to convert to a <see cref="Bitmap"/>.</param>
    /// <returns>The bitmap based on the specified matrix.</returns>
    public static unsafe Bitmap ToBitmap(this IReadOnlyMatrix<Rgb8> matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        int width = matrix.Width;
        int height = matrix.Height;
        var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        if (width == 0 || height == 0)
            return bitmap;

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
}
