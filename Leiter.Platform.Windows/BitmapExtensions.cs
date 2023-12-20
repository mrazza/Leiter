namespace Leiter.Platform.Windows;

using System.Drawing;
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
    /// Converts a <see cref="Bitmap"/> to a <see cref="SequentialMatrix"/>.
    /// </summary>
    /// <param name="bitmap">The bitmap to convert.</param>
    /// <returns>The Leiter matrix with the same data.</returns>
    public static SequentialMatrix<Rgb8> ToSequentialMatrix(this Bitmap bitmap)
    {
        var matrix = new SequentialMatrix<Rgb8>(bitmap.Width, bitmap.Height);
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                matrix[x, y] = bitmap.GetPixel(x, y).ToRgb8();
            }
        }
        return matrix;
    }

    /// <summary>
    /// Converts a Leiter <see cref="IReadOnlyMatrix"/> to a <see cref="Bitmap"/>.
    /// </summary>
    /// <param name="matrix">The Leiter matrix to a <see cref="Bitmap"/>.</param>
    /// <returns>The Bitmap based on the specified matrix.</returns>
    public static Bitmap ToBitmap(this IReadOnlyMatrix<Rgb8> matrix)
    {
        var bitmap = new Bitmap(matrix.Width, matrix.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        for (int y = 0; y < matrix.Height; y++)
        {
            for (int x = 0; x < matrix.Width; x++)
            {
                bitmap.SetPixel(x, y, matrix[x, y].ToColor());
            }
        }
        return bitmap;
    }
}