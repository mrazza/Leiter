namespace Leiter.Algorithms.Color;

using System.Runtime.Intrinsics;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Stores per-region mean color, hue, and intensity summaries computed over a <see cref="RegionGraph"/>.
/// </summary>
/// <typeparam name="TColor">The pixel color type used to accumulate and represent the region mean.</typeparam>
/// <param name="MeanColor">The region's mean pixel color in <typeparamref name="TColor"/>.</param>
/// <param name="MeanHueDegrees">The hue angle in degrees <c>[0, 360)</c> derived from <paramref name="MeanColor"/>.</param>
/// <param name="MeanIntensity">The normalized intensity/lightness derived from <paramref name="MeanColor"/>.</param>
public readonly record struct RegionColorStatistics<TColor>(
    TColor MeanColor,
    double MeanHueDegrees,
    double MeanIntensity)
    where TColor : struct, IPixel<TColor>;

/// <summary>
/// Computes per-region color, hue, and intensity summaries across the regions of a <see cref="RegionGraph"/>.
/// </summary>
public static class RegionColorStatistics
{
    /// <summary>
    /// Computes per-region <see cref="Rgb64"/> color, hue, and intensity statistics from an <see cref="Rgb8"/> image.
    /// </summary>
    /// <param name="graph">The region adjacency graph defining each region's pixel coordinates.</param>
    /// <param name="image">The 8-bit RGB image matching <paramref name="graph"/> dimensions.</param>
    /// <returns>An array of <see cref="RegionColorStatistics{Rgb64}"/> indexed by dense region ID in <c>[0, graph.RegionCount)</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="image"/> dimensions do not match <paramref name="graph"/>.</exception>
    public static RegionColorStatistics<Rgb64>[] Compute(
        RegionGraph graph,
        IReadOnlyMatrix<Rgb8> image) =>
        Compute(
            graph,
            image,
            static pixel => pixel.ToRgb64(),
            static color => color.GetHueDegrees(),
            static color => color.GetIntensity());

    /// <summary>
    /// Computes per-region <see cref="Rgb64"/> color, hue, and intensity statistics from an <see cref="Rgb64"/> image.
    /// </summary>
    /// <param name="graph">The region adjacency graph defining each region's pixel coordinates.</param>
    /// <param name="image">The 64-bit floating-point RGB image matching <paramref name="graph"/> dimensions.</param>
    /// <returns>An array of <see cref="RegionColorStatistics{Rgb64}"/> indexed by dense region ID in <c>[0, graph.RegionCount)</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="image"/> dimensions do not match <paramref name="graph"/>.</exception>
    public static RegionColorStatistics<Rgb64>[] Compute(
        RegionGraph graph,
        IReadOnlyMatrix<Rgb64> image) =>
        Compute(
            graph,
            image,
            static pixel => pixel,
            static color => color.GetHueDegrees(),
            static color => color.GetIntensity());

    /// <summary>
    /// Computes per-region CIELAB color, hue, and normalized lightness statistics from a <see cref="Lab32"/> image.
    /// </summary>
    /// <param name="graph">The region adjacency graph defining each region's pixel coordinates.</param>
    /// <param name="image">The CIELAB image matching <paramref name="graph"/> dimensions.</param>
    /// <returns>An array of <see cref="RegionColorStatistics{Lab32}"/> indexed by dense region ID in <c>[0, graph.RegionCount)</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="image"/> dimensions do not match <paramref name="graph"/>.</exception>
    public static RegionColorStatistics<Lab32>[] Compute(
        RegionGraph graph,
        IReadOnlyMatrix<Lab32> image) =>
        Compute(
            graph,
            image,
            static pixel => pixel,
            static color => color.GetHueDegrees(),
            static color => color.GetNormalizedLightness());

    /// <summary>
    /// Converts pixels from an <see cref="Rgb8"/> image into CIELAB and computes per-region color, hue, and lightness statistics.
    /// </summary>
    /// <param name="graph">The region adjacency graph defining each region's pixel coordinates.</param>
    /// <param name="image">The 8-bit RGB image matching <paramref name="graph"/> dimensions.</param>
    /// <param name="colorSpace">The RGB color space interpretation used when converting pixels to CIELAB.</param>
    /// <param name="illuminant">The CIE reference white illuminant used when converting pixels to CIELAB.</param>
    /// <returns>An array of <see cref="RegionColorStatistics{Lab32}"/> indexed by dense region ID in <c>[0, graph.RegionCount)</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="image"/> dimensions do not match <paramref name="graph"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is undefined.</exception>
    public static RegionColorStatistics<Lab32>[] ComputeLab(
        RegionGraph graph,
        IReadOnlyMatrix<Rgb8> image,
        RgbColorSpace colorSpace = RgbColorSpace.SRgb,
        Illuminant illuminant = Illuminant.D65)
    {
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Guard.ThrowIfUndefinedEnum(illuminant);

        return Compute(
            graph,
            image,
            pixel => pixel.ToRgb64().ToLab32(colorSpace, illuminant),
            static color => color.GetHueDegrees(),
            static color => color.GetNormalizedLightness());
    }

    /// <summary>
    /// Computes per-region color statistics in parallel across all regions of <paramref name="graph"/>
    /// using caller-supplied conversion, hue, and intensity delegates.
    /// </summary>
    /// <typeparam name="TSource">The source image element type.</typeparam>
    /// <typeparam name="TColor">The pixel color type used to accumulate and represent region means.</typeparam>
    /// <param name="graph">The region adjacency graph defining each region's pixel coordinates.</param>
    /// <param name="image">The source image matching <paramref name="graph"/> dimensions.</param>
    /// <param name="colorConverter">Converts each source pixel into <typeparamref name="TColor"/> prior to averaging.</param>
    /// <param name="hueSelector">Extracts the hue angle in degrees from the region mean color.</param>
    /// <param name="intensitySelector">Extracts the scalar intensity/lightness from the region mean color.</param>
    /// <returns>An array of <see cref="RegionColorStatistics{TColor}"/> indexed by dense region ID in <c>[0, graph.RegionCount)</c>.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="graph"/>, <paramref name="image"/>, <paramref name="colorConverter"/>,
    /// <paramref name="hueSelector"/>, or <paramref name="intensitySelector"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="image"/> dimensions do not match <paramref name="graph"/>.</exception>
    public static RegionColorStatistics<TColor>[] Compute<TSource, TColor>(
        RegionGraph graph,
        IReadOnlyMatrix<TSource> image,
        Func<TSource, TColor> colorConverter,
        Func<TColor, double> hueSelector,
        Func<TColor, double> intensitySelector)
        where TSource : struct
        where TColor : struct, IPixel<TColor>
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(colorConverter);
        ArgumentNullException.ThrowIfNull(hueSelector);
        ArgumentNullException.ThrowIfNull(intensitySelector);
        Guard.ThrowIfSizeMismatch(graph.Size, image.Size, nameof(image));

        return graph.ComputeRegionStatistics(region =>
        {
            HashSet<Coord> pixels = region.Pixels;
            if (pixels.Count == 0)
                return new RegionColorStatistics<TColor>(default, 0.0, 0.0);

            Vector256<double> sum = Vector256<double>.Zero;
            foreach (Coord coord in pixels)
                sum += colorConverter(image[coord]).ToDoubleVector();

            TColor meanColor = TColor.FromDoubleVector(sum / (double)pixels.Count);
            return new RegionColorStatistics<TColor>(
                meanColor,
                hueSelector(meanColor),
                intensitySelector(meanColor));
        });
    }
}
