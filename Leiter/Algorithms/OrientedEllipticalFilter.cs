namespace Leiter.Algorithms;

using System;
using System.Threading.Tasks;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Implements an oriented elliptical Gaussian smoothing filter that adaptively blurs along local tangent vector orientations.
/// </summary>
public static class OrientedEllipticalFilter
{
    /// <summary>
    /// Applies an oriented elliptical Gaussian smoothing filter to an RGB image guided by a vector orientation field.
    /// </summary>
    /// <param name="image">The input RGB image to filter.</param>
    /// <param name="orientations">The vector field specifying the tangent orientation at each pixel.</param>
    /// <param name="options">Optional configuration parameters controlling filter radii and Gaussian standard deviation.</param>
    /// <param name="activeMask">Optional boolean mask; when provided, pixels where the mask is <see langword="false"/> are not filtered.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the filtered RGB image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="orientations"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="orientations"/> or <paramref name="activeMask"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<Vector2D> orientations,
        OrientedEllipticalFilterOptions? options = null,
        IReadOnlyMatrix<bool>? activeMask = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        Guard.ThrowIfSizeMismatch(image, orientations);
        Guard.ThrowIfSizeMismatchIfNotNull(image, activeMask);

        OrientedEllipticalFilterOptions opts = options ?? OrientedEllipticalFilterOptions.Default;
        opts.Validate();

        if (image.Count == 0)
            return new SequentialMatrix<Rgb8>(image.Size);

        int width = image.Width;
        int height = image.Height;

        Rgb64[] normalizedRgb = image.MapToArray(static pixel => pixel.ToRgb64());

        double a = opts.MajorRadius;
        double b = opts.MinorRadius;
        double sigmaU = a * opts.SigmaScale;
        double sigmaV = b * opts.SigmaScale;
        double invTwoSigmaUSq = 0.5 / (sigmaU * sigmaU);
        double invTwoSigmaVSq = 0.5 / (sigmaV * sigmaV);
        double invASq = 1.0 / (a * a);
        double invBSq = 1.0 / (b * b);
        int maxR = (int)Math.Ceiling(Math.Max(a, b));

        var result = new SequentialMatrix<Rgb8>(image.Size);

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                if (activeMask is not null && !activeMask[x, y])
                {
                    result[x, y] = image[x, y];
                    continue;
                }

                Vector2D axis = orientations[x, y].Normalize();
                if (axis == Vector2D.Zero)
                {
                    result[x, y] = image[x, y];
                    continue;
                }

                Vector2D vAxis = axis.Perpendicular;

                int minY = Math.Max(0, y - maxR);
                int maxY = Math.Min(height - 1, y + maxR);
                int minX = Math.Max(0, x - maxR);
                int maxX = Math.Min(width - 1, x + maxR);

                double sumR = 0.0;
                double sumG = 0.0;
                double sumB = 0.0;
                double totalWeight = 0.0;

                for (int sampleY = minY; sampleY <= maxY; sampleY++)
                {
                    double dy = sampleY - y;
                    int sampleRowOffset = sampleY * width;

                    for (int sampleX = minX; sampleX <= maxX; sampleX++)
                    {
                        double dx = sampleX - x;

                        double u = (dx * axis.X) + (dy * axis.Y);
                        double v = (dx * vAxis.X) + (dy * vAxis.Y);

                        if (((u * u) * invASq) + ((v * v) * invBSq) > 1.0)
                            continue;

                        double weight = Math.Exp(-(((u * u) * invTwoSigmaUSq) + ((v * v) * invTwoSigmaVSq)));
                        Rgb64 samplePixel = normalizedRgb[sampleRowOffset + sampleX];

                        sumR += samplePixel.R * weight;
                        sumG += samplePixel.G * weight;
                        sumB += samplePixel.B * weight;
                        totalWeight += weight;
                    }
                }

                result[x, y] = new Rgb64(sumR / totalWeight, sumG / totalWeight, sumB / totalWeight).ToRgb8();
            }
        });

        return result;
    }
}
