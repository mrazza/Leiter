namespace Leiter.Algorithms.Color;

using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Stores per-region color, hue, intensity, and saliency-overlap summaries computed over a <see cref="RegionGraph"/>.
/// </summary>
/// <param name="MeanRgb">The region's mean normalized <c>[0, 1]</c> linear/sRGB color.</param>
/// <param name="MeanLab">The region's mean CIELAB color under the configured color space and illuminant.</param>
/// <param name="MeanHueDegrees">The hue angle in degrees <c>[0, 360)</c> derived from <paramref name="MeanRgb"/>.</param>
/// <param name="MeanIntensity">The HSI intensity <c>(R + G + B) / 3</c> derived from <paramref name="MeanRgb"/>.</param>
/// <param name="SalientFraction">The fraction <c>[0, 1]</c> of pixels in the region marked salient, or <c>0</c> if no salient mask was supplied.</param>
public readonly record struct RegionColorStatistics(
    Rgb64 MeanRgb,
    Lab32 MeanLab,
    double MeanHueDegrees,
    double MeanIntensity,
    double SalientFraction)
{
    /// <summary>
    /// Computes per-region color and saliency statistics in parallel across all regions of <paramref name="graph"/>.
    /// </summary>
    /// <param name="graph">The region adjacency graph defining each region's pixel coordinates.</param>
    /// <param name="image">The RGB image matching <paramref name="graph"/> dimensions.</param>
    /// <param name="salientMask">Optional boolean salient foreground mask matching <paramref name="graph"/> dimensions.</param>
    /// <param name="colorSpace">The RGB color space interpretation used when converting pixels to CIELAB.</param>
    /// <param name="illuminant">The CIE reference white illuminant used when converting pixels to CIELAB.</param>
    /// <returns>An array of <see cref="RegionColorStatistics"/> indexed by dense region ID in <c>[0, graph.RegionCount)</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="graph"/> or <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="image"/> or <paramref name="salientMask"/> dimensions do not match <paramref name="graph"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is undefined.</exception>
    public static RegionColorStatistics[] Compute(
        RegionGraph graph,
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<bool>? salientMask = null,
        RgbColorSpace colorSpace = RgbColorSpace.sRGB,
        Illuminant illuminant = Illuminant.D65)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(image);

        if (image.Size != graph.Size)
            throw new ArgumentException("Image dimensions must match RegionGraph dimensions.", nameof(image));

        if (salientMask is not null && salientMask.Size != graph.Size)
            throw new ArgumentException("Salient mask dimensions must match RegionGraph dimensions.", nameof(salientMask));

        if (!Enum.IsDefined(colorSpace))
            throw new ArgumentOutOfRangeException(nameof(colorSpace), colorSpace, "Unsupported RGB color space.");

        if (!Enum.IsDefined(illuminant))
            throw new ArgumentOutOfRangeException(nameof(illuminant), illuminant, "Unsupported CIE illuminant.");

        return graph.ComputeRegionStatistics(region => ComputeSingleRegion(region, image, salientMask, colorSpace, illuminant));
    }

    private static RegionColorStatistics ComputeSingleRegion(
        Region<Coord> region,
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<bool>? salientMask,
        RgbColorSpace colorSpace,
        Illuminant illuminant)
    {
        int count = region.Pixels.Count;
        double sumR = 0.0;
        double sumG = 0.0;
        double sumB = 0.0;
        double sumL = 0.0;
        double sumA = 0.0;
        double sumLabB = 0.0;
        int salientCount = 0;

        foreach (Coord coord in region.Pixels)
        {
            Rgb8 pixel = image[coord];
            Rgb64 rgb64 = pixel.ToRgb64();
            Lab32 lab = rgb64.ToLab32(colorSpace, illuminant);

            sumR += rgb64.R;
            sumG += rgb64.G;
            sumB += rgb64.B;
            sumL += lab.L;
            sumA += lab.A;
            sumLabB += lab.B;

            if (salientMask is not null && salientMask[coord])
                salientCount++;
        }

        double invCount = 1.0 / count;
        var meanRgb = new Rgb64(sumR * invCount, sumG * invCount, sumB * invCount);
        var meanLab = new Lab32((float)(sumL * invCount), (float)(sumA * invCount), (float)(sumLabB * invCount));

        return new RegionColorStatistics(
            meanRgb,
            meanLab,
            meanRgb.GetHueDegrees(),
            meanRgb.GetIntensity(),
            salientMask is null ? 0.0 : salientCount * invCount);
    }
}
