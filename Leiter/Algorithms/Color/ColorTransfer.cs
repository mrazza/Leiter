namespace Leiter.Algorithms.Color;

using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Implements Reinhard et al. (2001) linear CIELAB color transfer and watercolor palette matching
/// (Wang et al., Sec. 3.1).
/// </summary>
public static class ColorTransfer
{
    /// <summary>
    /// Computes the per-channel mean and population standard deviation of a <see cref="Lab32"/> image.
    /// </summary>
    /// <param name="labImage">The CIELAB image to analyze.</param>
    /// <returns>The 6D <see cref="LabColorStatistics"/> feature vector, or all zeros when empty.</returns>
    public static LabColorStatistics ComputeStatistics(IReadOnlyMatrix<Lab32> labImage)
    {
        ArgumentNullException.ThrowIfNull(labImage);

        int width = labImage.Width;
        int height = labImage.Height;
        int count = labImage.Count;
        if (count == 0)
            return default;

        double sumL = 0.0;
        double sumA = 0.0;
        double sumB = 0.0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Lab32 pixel = labImage[x, y];
                sumL += pixel.L;
                sumA += pixel.A;
                sumB += pixel.B;
            }
        }

        double meanL = sumL / count;
        double meanA = sumA / count;
        double meanB = sumB / count;

        double sqL = 0.0;
        double sqA = 0.0;
        double sqB = 0.0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Lab32 pixel = labImage[x, y];
                double dL = pixel.L - meanL;
                double dA = pixel.A - meanA;
                double dB = pixel.B - meanB;
                sqL += dL * dL;
                sqA += dA * dA;
                sqB += dB * dB;
            }
        }

        return new LabColorStatistics(
            meanL,
            meanA,
            meanB,
            Math.Sqrt(Math.Max(0.0, sqL / count)),
            Math.Sqrt(Math.Max(0.0, sqA / count)),
            Math.Sqrt(Math.Max(0.0, sqB / count)));
    }

    /// <summary>
    /// Computes CIELAB color statistics for an <see cref="Rgb8"/> image under the specified color space and illuminant.
    /// </summary>
    /// <param name="image">The RGB image to analyze.</param>
    /// <param name="colorSpace">The RGB color space of <paramref name="image"/>.</param>
    /// <param name="illuminant">The reference white illuminant for CIELAB conversion (defaults to <see cref="Illuminant.E"/>).</param>
    /// <returns>The 6D <see cref="LabColorStatistics"/> feature vector.</returns>
    public static LabColorStatistics ComputeStatistics(
        IReadOnlyMatrix<Rgb8> image,
        RgbColorSpace colorSpace = RgbColorSpace.sRGB,
        Illuminant illuminant = Illuminant.E)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.Count == 0)
            return default;

        SequentialMatrix<Lab32> labImage = ConvertToLab32Matrix(image, colorSpace, illuminant);
        return ComputeStatistics((IReadOnlyMatrix<Lab32>)labImage);
    }

    /// <summary>
    /// Selects the palette whose CIELAB feature vector minimizes weighted Euclidean distance to <paramref name="sourceStatistics"/>.
    /// </summary>
    /// <param name="sourceStatistics">The CIELAB statistics of the input image.</param>
    /// <param name="palettes">Optional candidate palettes (defaults to <see cref="WatercolorPalettes.Defaults"/>).</param>
    /// <param name="featureWeights">Optional weights for comparing the 6D feature components (defaults to unit weights).</param>
    /// <returns>The closest matching <see cref="WatercolorPalette"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="palettes"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any weight in <paramref name="featureWeights"/> is negative.</exception>
    public static WatercolorPalette SelectClosestPalette(
        LabColorStatistics sourceStatistics,
        IReadOnlyList<WatercolorPalette>? palettes = null,
        LabColorStatistics? featureWeights = null)
    {
        IReadOnlyList<WatercolorPalette> candidates = palettes ?? WatercolorPalettes.Defaults;
        if (candidates.Count == 0)
            throw new ArgumentException("At least one candidate palette must be provided.", nameof(palettes));

        LabColorStatistics weights = featureWeights ?? LabColorStatistics.UnitWeights;
        WatercolorPalette best = candidates[0] ?? throw new ArgumentNullException(nameof(palettes), "Palette entries cannot be null.");
        double bestDistance = sourceStatistics.FeatureDistance(best.Statistics, weights);

        for (int i = 1; i < candidates.Count; i++)
        {
            WatercolorPalette candidate = candidates[i] ?? throw new ArgumentNullException(nameof(palettes), "Palette entries cannot be null.");
            double distance = sourceStatistics.FeatureDistance(candidate.Statistics, weights);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Applies Reinhard et al. (2001) linear channel transfer in CIELAB space to match <paramref name="targetStatistics"/>.
    /// </summary>
    /// <param name="source">The source CIELAB image.</param>
    /// <param name="targetStatistics">The target CIELAB mean and standard deviation statistics.</param>
    /// <param name="strength">Blending strength in <c>[0, 1]</c> (<c>0</c> returns an unchanged copy, <c>1</c> applies full transfer).</param>
    /// <returns>A new <see cref="Matrix{Lab32}"/> with transferred colors.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="strength"/> is outside <c>[0, 1]</c>.</exception>
    public static Matrix<Lab32> Transfer(
        Matrix<Lab32> source,
        LabColorStatistics targetStatistics,
        double strength = 1.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateStrength(strength);

        if (source.Count == 0 || strength == 0.0)
            return source.Clone();

        LabColorStatistics srcStats = ComputeStatistics(source);
        double scaleL = srcStats.StdDevL > 1e-6 ? targetStatistics.StdDevL / srcStats.StdDevL : 0.0;
        double scaleA = srcStats.StdDevA > 1e-6 ? targetStatistics.StdDevA / srcStats.StdDevA : 0.0;
        double scaleB = srcStats.StdDevB > 1e-6 ? targetStatistics.StdDevB / srcStats.StdDevB : 0.0;

        return source.Map(pixel =>
        {
            double transferredL = ((pixel.L - srcStats.MeanL) * scaleL) + targetStatistics.MeanL;
            double transferredA = ((pixel.A - srcStats.MeanA) * scaleA) + targetStatistics.MeanA;
            double transferredB = ((pixel.B - srcStats.MeanB) * scaleB) + targetStatistics.MeanB;

            return new Lab32(
                (float)(pixel.L + (strength * (transferredL - pixel.L))),
                (float)(pixel.A + (strength * (transferredA - pixel.A))),
                (float)(pixel.B + (strength * (transferredB - pixel.B))));
        });
    }

    /// <summary>
    /// Transfers colors from a <paramref name="reference"/> <see cref="Rgb8"/> image to <paramref name="source"/> via CIELAB.
    /// </summary>
    /// <param name="source">The source RGB image to recolor.</param>
    /// <param name="reference">The reference RGB image supplying target color statistics.</param>
    /// <param name="colorSpace">The RGB color space of the input and output images.</param>
    /// <param name="illuminant">The reference white illuminant for CIELAB conversion (defaults to <see cref="Illuminant.E"/>).</param>
    /// <param name="strength">Blending strength in <c>[0, 1]</c>.</param>
    /// <returns>A recolored <see cref="Matrix{Rgb8}"/> image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="strength"/> is outside <c>[0, 1]</c>.</exception>
    public static Matrix<Rgb8> Transfer(
        Matrix<Rgb8> source,
        IReadOnlyMatrix<Rgb8> reference,
        RgbColorSpace colorSpace = RgbColorSpace.sRGB,
        Illuminant illuminant = Illuminant.E,
        double strength = 1.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reference);
        ValidateStrength(strength);

        if (source.Count == 0 || strength == 0.0)
            return source.Clone();

        LabColorStatistics targetStats = ComputeStatistics(reference, colorSpace, illuminant);
        Matrix<Lab32> sourceLab = source.Map(pixel => pixel.ToLab32(colorSpace, illuminant));
        return TransferLabToRgb8(sourceLab, targetStats, colorSpace, illuminant, strength);
    }

    /// <summary>
    /// Transfers colors from a curated <see cref="WatercolorPalette"/> to <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The source RGB image to recolor.</param>
    /// <param name="palette">The target watercolor palette.</param>
    /// <param name="colorSpace">The RGB color space of the input and output images.</param>
    /// <param name="strength">Blending strength in <c>[0, 1]</c>.</param>
    /// <returns>A recolored <see cref="Matrix{Rgb8}"/> image.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="strength"/> is outside <c>[0, 1]</c>.</exception>
    public static Matrix<Rgb8> Transfer(
        Matrix<Rgb8> source,
        WatercolorPalette palette,
        RgbColorSpace colorSpace = RgbColorSpace.sRGB,
        double strength = 1.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(palette);
        ValidateStrength(strength);

        if (source.Count == 0 || strength == 0.0)
            return source.Clone();

        Matrix<Lab32> sourceLab = source.Map(pixel => pixel.ToLab32(colorSpace, palette.Illuminant));
        return TransferLabToRgb8(sourceLab, palette.Statistics, colorSpace, palette.Illuminant, strength);
    }

    /// <summary>
    /// Automatically selects the closest watercolor palette under <see cref="Illuminant.E"/> and applies color transfer to <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The source RGB image to recolor.</param>
    /// <param name="palettes">Optional candidate palettes (defaults to <see cref="WatercolorPalettes.Defaults"/>).</param>
    /// <param name="colorSpace">The RGB color space of the input and output images.</param>
    /// <param name="featureWeights">Optional weights for comparing the 6D feature components (defaults to unit weights).</param>
    /// <param name="strength">Blending strength in <c>[0, 1]</c>.</param>
    /// <returns>The recolored <see cref="Matrix{Rgb8}"/> image and the selected <see cref="WatercolorPalette"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="palettes"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="strength"/> is outside <c>[0, 1]</c> or any feature weight is negative.</exception>
    public static (Matrix<Rgb8> Result, WatercolorPalette SelectedPalette) TransferToClosestPalette(
        Matrix<Rgb8> source,
        IReadOnlyList<WatercolorPalette>? palettes = null,
        RgbColorSpace colorSpace = RgbColorSpace.sRGB,
        LabColorStatistics? featureWeights = null,
        double strength = 1.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateStrength(strength);

        Matrix<Lab32> sourceLabE = source.Map(pixel => pixel.ToLab32(colorSpace, Illuminant.E));
        LabColorStatistics sourceStats = ComputeStatistics(sourceLabE);
        WatercolorPalette selected = SelectClosestPalette(sourceStats, palettes, featureWeights);

        if (source.Count == 0 || strength == 0.0)
            return (source.Clone(), selected);

        Matrix<Rgb8> result = selected.Illuminant == Illuminant.E
            ? TransferLabToRgb8(sourceLabE, selected.Statistics, colorSpace, Illuminant.E, strength)
            : Transfer(source, selected, colorSpace, strength);

        return (result, selected);
    }

    private static Matrix<Rgb8> TransferLabToRgb8(
        Matrix<Lab32> sourceLab,
        LabColorStatistics targetStatistics,
        RgbColorSpace colorSpace,
        Illuminant illuminant,
        double strength) =>
        Transfer(sourceLab, targetStatistics, strength).Map(pixel => pixel.ToRgb8(colorSpace, illuminant));

    private static SequentialMatrix<Lab32> ConvertToLab32Matrix(
        IReadOnlyMatrix<Rgb8> image,
        RgbColorSpace colorSpace,
        Illuminant illuminant)
    {
        int width = image.Width;
        int height = image.Height;
        var labMatrix = new SequentialMatrix<Lab32>(width, height);

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
                labMatrix[x, y] = image[x, y].ToLab32(colorSpace, illuminant);
        });

        return labMatrix;
    }

    private static void ValidateStrength(double strength)
    {
        if (double.IsNaN(strength) || strength < 0.0 || strength > 1.0)
            throw new ArgumentOutOfRangeException(nameof(strength), "Color transfer strength must be between 0 and 1.");
    }
}
