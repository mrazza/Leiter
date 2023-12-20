namespace Leiter.Platform.Windows;

using System.Drawing;
using System.Runtime.Versioning;
using Leiter.Core;
using Leiter.Pixels;

[SupportedOSPlatform("windows")]
public static class BitmapExtensions
{
    public static SequentialMatrix<Rgb8> ToSequentialMatrix(this Bitmap bitmap)
    {
        SequentialMatrix<Rgb8> matrix = new SequentialMatrix<Rgb8>(bitmap.Width, bitmap.Height);
        for (int y = 0; y < bitmap.Height; y++)
        {
            for (int x = 0; x < bitmap.Width; x++)
            {
                var pixel = bitmap.GetPixel(x, y);
                matrix[x, y] = new Rgb8(pixel.R, pixel.G, pixel.B);
            }
        }
        return matrix;
    }

    public static Bitmap ToBitmap(this Matrix<Rgb8> matrix)
    {
        var bitmap = new Bitmap(matrix.Width, matrix.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        for (int y = 0; y < matrix.Height; y++)
        {
            for (int x = 0; x < matrix.Width; x++)
            {
                var pixel = matrix[x, y];
                bitmap.SetPixel(x, y, Color.FromArgb(pixel.R, pixel.G, pixel.B));
            }
        }
        return bitmap;
    }
}