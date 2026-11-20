namespace Leiter.Algorithms.Segmentation;

using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Provides region-coloring visualization helpers for segmented images and region partitions.
/// </summary>
public static class SegmentationVisualizer
{
    /// <summary>
    /// Delegate type for a method that determines the color (or pixel value) for a given region.
    /// </summary>
    /// <typeparam name="T">The pixel value type.</typeparam>
    /// <param name="image">The original source image associated with the segmentation.</param>
    /// <param name="segment">The segment for which a color is computed.</param>
    /// <returns>The rendered color value for <paramref name="segment"/>.</returns>
    public delegate T ColorAssigner<T>(IReadOnlyMatrix<T> image, Region<Coord> segment)
        where T : struct, IPixel<T>;

    /// <summary>
    /// Returns a <see cref="ColorAssigner{T}"/> that assigns a random 8-bit RGB color to each segment.
    /// </summary>
    /// <param name="seed">An optional seed for the pseudo-random number generator.</param>
    /// <returns>A <see cref="ColorAssigner{Rgb8}"/> for use with <see cref="ColorRegions{T}(IReadOnlyMatrix{T}, IEnumerable{Region{Coord}}, ColorAssigner{T})"/>.</returns>
    public static ColorAssigner<Rgb8> RandomColorAssigner(int? seed = null)
    {
        ulong state = seed is int s ? unchecked((ulong)s) : unchecked((ulong)Random.Shared.NextInt64());
        return (_, _) =>
        {
            ulong bits = SplitMix64.Next(ref state);
            return new Rgb8((byte)bits, (byte)(bits >> 8), (byte)(bits >> 16));
        };
    }

    /// <summary>
    /// Returns a <see cref="ColorAssigner{T}"/> that assigns each segment the arithmetic mean RGB color
    /// of the pixels contained in that segment.
    /// </summary>
    /// <returns>A <see cref="ColorAssigner{Rgb8}"/> for use with <see cref="ColorRegions{T}(IReadOnlyMatrix{T}, IEnumerable{Region{Coord}}, ColorAssigner{T})"/>.</returns>
    public static ColorAssigner<Rgb8> AverageColorAssigner() =>
        static (image, region) =>
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(region);

            int count = region.Pixels.Count;
            if (count <= 0)
                throw new ArgumentException("All regions of the segmentation must have at least one pixel.", nameof(region));

            double sumR = 0.0;
            double sumG = 0.0;
            double sumB = 0.0;
            foreach (Coord coord in region.Pixels)
            {
                Rgb8 pixel = image[coord];
                sumR += pixel.R;
                sumG += pixel.G;
                sumB += pixel.B;
            }

            double invCount = 1.0 / count;
            return new Rgb8(
                Rgb8.ClampRoundedByte(sumR * invCount),
                Rgb8.ClampRoundedByte(sumG * invCount),
                Rgb8.ClampRoundedByte(sumB * invCount));
        };

    /// <summary>
    /// Returns a <see cref="ColorAssigner{T}"/> that assigns each segment the arithmetic mean pixel value
    /// computed in double-precision SIMD vector space, or <see langword="default"/> when a segment has no pixels.
    /// </summary>
    /// <typeparam name="T">The pixel value type.</typeparam>
    /// <returns>A <see cref="ColorAssigner{T}"/> for use with <see cref="ColorRegions{T}(IReadOnlyMatrix{T}, IEnumerable{Region{Coord}}, ColorAssigner{T})"/>.</returns>
    public static ColorAssigner<T> AverageColorAssigner<T>()
        where T : struct, IPixel<T> =>
        static (image, region) =>
        {
            ArgumentNullException.ThrowIfNull(image);
            ArgumentNullException.ThrowIfNull(region);

            int count = region.Pixels.Count;
            if (count <= 0)
                return default;

            Vector256<double> sum = Vector256<double>.Zero;
            foreach (Coord coord in region.Pixels)
                sum += image[coord].ToDoubleVector();

            return T.FromDoubleVector(sum / (double)count);
        };

    /// <summary>
    /// Returns a <see cref="ColorAssigner{T}"/> that assigns each segment a sequential <see cref="LongPixel"/> identifier.
    /// </summary>
    /// <returns>A <see cref="ColorAssigner{LongPixel}"/> for use with <see cref="ColorRegions{T}(IReadOnlyMatrix{T}, IEnumerable{Region{Coord}}, ColorAssigner{T})"/>.</returns>
    public static ColorAssigner<LongPixel> IntegerColorAssigner()
    {
        long index = 0;
        return (_, _) => index++;
    }

    /// <summary>
    /// Renders a new <see cref="Matrix{Rgb8}"/> where every region in <paramref name="regions"/> is filled with a random color.
    /// </summary>
    /// <param name="image">The source image providing output dimensions and fallback values for uncovered pixels.</param>
    /// <param name="regions">The segmentation regions to color.</param>
    /// <param name="seed">An optional seed for deterministic random colors.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> with colored regions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="regions"/>, or any region element is <see langword="null"/>.</exception>
    public static Matrix<Rgb8> ColorRegions(
        IReadOnlyMatrix<Rgb8> image,
        IEnumerable<Region<Coord>> regions,
        int? seed = null) =>
        ColorRegions(image, regions, RandomColorAssigner(seed));

    /// <summary>
    /// Renders a new <see cref="Matrix{T}"/> where every region in <paramref name="regions"/> is filled
    /// using <paramref name="colorAssigner"/> without mutating <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type for the image matrix.</typeparam>
    /// <param name="image">The source image providing pixel colors and output dimensions.</param>
    /// <param name="regions">The segmentation regions to color.</param>
    /// <param name="colorAssigner">The delegate selecting a color for each region.</param>
    /// <returns>A new <see cref="Matrix{T}"/> containing the colored segmentation visualization.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="image"/>, <paramref name="regions"/>, <paramref name="colorAssigner"/>,
    /// or any element in <paramref name="regions"/> is <see langword="null"/>.
    /// </exception>
    public static Matrix<T> ColorRegions<T>(
        IReadOnlyMatrix<T> image,
        IEnumerable<Region<Coord>> regions,
        ColorAssigner<T> colorAssigner)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(colorAssigner);

        Matrix<T> result = image.Clone();
        foreach (Region<Coord> region in regions)
        {
            ArgumentNullException.ThrowIfNull(region, nameof(regions));

            T color = colorAssigner(image, region);
            foreach (Coord pixel in region.Pixels)
            {
                result[pixel.X, pixel.Y] = color;
            }
        }

        return result;
    }

    /// <summary>
    /// Renders a new <see cref="Matrix{Rgb8}"/> where every region in <paramref name="regionGraph"/> is filled with a random color.
    /// </summary>
    /// <param name="image">The source image providing output dimensions.</param>
    /// <param name="regionGraph">The region graph defining regions to color.</param>
    /// <param name="seed">An optional seed for deterministic random colors.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> with colored regions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="regionGraph"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="regionGraph"/> dimensions do not match <paramref name="image"/>.</exception>
    public static Matrix<Rgb8> ColorRegions(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        int? seed = null) =>
        ColorRegions(image, regionGraph, RandomColorAssigner(seed));

    /// <summary>
    /// Renders a new <see cref="Matrix{T}"/> where every region in <paramref name="regionGraph"/> is filled
    /// using <paramref name="colorAssigner"/> without mutating <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type for the image matrix.</typeparam>
    /// <param name="image">The source image providing pixel colors and output dimensions.</param>
    /// <param name="regionGraph">The region graph defining regions to color.</param>
    /// <param name="colorAssigner">The delegate selecting a color for each region.</param>
    /// <returns>A new <see cref="Matrix{T}"/> containing the colored segmentation visualization.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="regionGraph"/>, or <paramref name="colorAssigner"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="regionGraph"/> dimensions do not match <paramref name="image"/>.</exception>
    public static Matrix<T> ColorRegions<T>(
        IReadOnlyMatrix<T> image,
        RegionGraph regionGraph,
        ColorAssigner<T> colorAssigner)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(regionGraph);
        ArgumentNullException.ThrowIfNull(colorAssigner);
        Guard.ThrowIfSizeMismatch(image.Size, regionGraph.Size, nameof(regionGraph));

        return ColorRegions(image, regionGraph.Regions, colorAssigner);
    }

    /// <summary>
    /// Renders a new <see cref="Matrix{Rgb8}"/> where every partition in <paramref name="segmentation"/> is filled with a random color.
    /// </summary>
    /// <param name="image">The source image providing output dimensions.</param>
    /// <param name="segmentation">The disjoint-set segmentation defining regions to color.</param>
    /// <param name="seed">An optional seed for deterministic random colors.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> with colored regions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="segmentation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="segmentation"/> dimensions do not match <paramref name="image"/>.</exception>
    public static Matrix<Rgb8> ColorRegions(
        IReadOnlyMatrix<Rgb8> image,
        IDisjointSet segmentation,
        int? seed = null) =>
        ColorRegions(image, segmentation, RandomColorAssigner(seed));

    /// <summary>
    /// Renders a new <see cref="Matrix{T}"/> where every partition in <paramref name="segmentation"/> is filled
    /// using <paramref name="colorAssigner"/> without mutating <paramref name="image"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type for the image matrix.</typeparam>
    /// <param name="image">The source image providing pixel colors and output dimensions.</param>
    /// <param name="segmentation">The disjoint-set segmentation defining regions to color.</param>
    /// <param name="colorAssigner">The delegate selecting a color for each region.</param>
    /// <returns>A new <see cref="Matrix{T}"/> containing the colored segmentation visualization.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="segmentation"/>, or <paramref name="colorAssigner"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="segmentation"/> dimensions do not match <paramref name="image"/>.</exception>
    public static Matrix<T> ColorRegions<T>(
        IReadOnlyMatrix<T> image,
        IDisjointSet segmentation,
        ColorAssigner<T> colorAssigner)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(segmentation);
        ArgumentNullException.ThrowIfNull(colorAssigner);
        Guard.ThrowIfSizeMismatch(image, segmentation);

        int width = segmentation.Width;
        int height = segmentation.Height;
        var regionsByRoot = new Dictionary<long, Region<Coord>>();
        var orderedRegions = new List<Region<Coord>>();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                long root = segmentation[x, y].Value;
                if (!regionsByRoot.TryGetValue(root, out Region<Coord>? region))
                {
                    region = new Region<Coord> { Id = unchecked((int)root) };
                    regionsByRoot[root] = region;
                    orderedRegions.Add(region);
                }

                region.Pixels.Add(new Coord(x, y));
            }
        }

        return ColorRegions(image, orderedRegions, colorAssigner);
    }
}
