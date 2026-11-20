namespace Leiter.Algorithms.Color;

using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Implements Reinhard et al. (2001) linear CIELAB color transfer and color profile matching.
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is undefined.</exception>
    public static LabColorStatistics ComputeStatistics(
        IReadOnlyMatrix<Rgb8> image,
        RgbColorSpace colorSpace = RgbColorSpace.SRgb,
        Illuminant illuminant = Illuminant.E)
    {
        ArgumentNullException.ThrowIfNull(image);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Guard.ThrowIfUndefinedEnum(illuminant);

        if (image.Count == 0)
            return default;

        Matrix<Lab32> labImage = image.Map(pixel => pixel.ToLab32(colorSpace, illuminant));
        return ComputeStatistics(labImage);
    }

    /// <summary>
    /// Selects the profile whose CIELAB feature vector minimizes weighted Euclidean distance to <paramref name="sourceStatistics"/>.
    /// </summary>
    /// <param name="sourceStatistics">The CIELAB statistics of the input image.</param>
    /// <param name="profiles">The candidate color transfer profiles to evaluate.</param>
    /// <param name="featureWeights">Optional weights for comparing the 6D feature components (defaults to unit weights).</param>
    /// <returns>The closest matching <see cref="ColorTransferProfile"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profiles"/> or any entry in <paramref name="profiles"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="profiles"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any weight in <paramref name="featureWeights"/> is negative.</exception>
    public static ColorTransferProfile SelectClosestProfile(
        LabColorStatistics sourceStatistics,
        IReadOnlyList<ColorTransferProfile> profiles,
        LabColorStatistics? featureWeights = null)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (profiles.Count == 0)
            throw new ArgumentException("At least one candidate profile must be provided.", nameof(profiles));

        LabColorStatistics weights = featureWeights ?? LabColorStatistics.UnitWeights;
        ColorTransferProfile best = profiles[0] ?? throw new ArgumentNullException(nameof(profiles), "Profile entries cannot be null.");
        double bestDistance = sourceStatistics.FeatureDistance(best.Statistics, weights);

        for (int i = 1; i < profiles.Count; i++)
        {
            ColorTransferProfile candidate = profiles[i] ?? throw new ArgumentNullException(nameof(profiles), "Profile entries cannot be null.");
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
        IReadOnlyMatrix<Lab32> source,
        LabColorStatistics targetStatistics,
        double strength = 1.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        Guard.ThrowIfNotFiniteInRange(strength, 0.0, 1.0);

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
                (float)ScalarMath.Lerp(pixel.L, transferredL, strength),
                (float)ScalarMath.Lerp(pixel.A, transferredA, strength),
                (float)ScalarMath.Lerp(pixel.B, transferredB, strength));
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
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="illuminant"/> is undefined, or <paramref name="strength"/> is outside <c>[0, 1]</c>.</exception>
    public static Matrix<Rgb8> Transfer(
        IReadOnlyMatrix<Rgb8> source,
        IReadOnlyMatrix<Rgb8> reference,
        RgbColorSpace colorSpace = RgbColorSpace.SRgb,
        Illuminant illuminant = Illuminant.E,
        double strength = 1.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(reference);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Guard.ThrowIfUndefinedEnum(illuminant);
        Guard.ThrowIfNotFiniteInRange(strength, 0.0, 1.0);

        if (source.Count == 0 || strength == 0.0)
            return source.Clone();

        LabColorStatistics targetStats = ComputeStatistics(reference, colorSpace, illuminant);
        Matrix<Lab32> sourceLab = source.Map(pixel => pixel.ToLab32(colorSpace, illuminant));
        return TransferLabToRgb8(sourceLab, targetStats, colorSpace, illuminant, strength);
    }

    /// <summary>
    /// Transfers colors from a <see cref="ColorTransferProfile"/> to <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The source RGB image to recolor.</param>
    /// <param name="profile">The target color transfer profile.</param>
    /// <param name="colorSpace">The RGB color space of the input and output images.</param>
    /// <param name="strength">Blending strength in <c>[0, 1]</c>.</param>
    /// <returns>A recolored <see cref="Matrix{Rgb8}"/> image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="profile"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or <paramref name="profile"/>'s illuminant is undefined, or <paramref name="strength"/> is outside <c>[0, 1]</c>.</exception>
    public static Matrix<Rgb8> Transfer(
        IReadOnlyMatrix<Rgb8> source,
        ColorTransferProfile profile,
        RgbColorSpace colorSpace = RgbColorSpace.SRgb,
        double strength = 1.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Guard.ThrowIfUndefinedEnum(profile.Illuminant, nameof(profile));
        Guard.ThrowIfNotFiniteInRange(strength, 0.0, 1.0);

        if (source.Count == 0 || strength == 0.0)
            return source.Clone();

        Matrix<Lab32> sourceLab = source.Map(pixel => pixel.ToLab32(colorSpace, profile.Illuminant));
        return TransferLabToRgb8(sourceLab, profile.Statistics, colorSpace, profile.Illuminant, strength);
    }

    /// <summary>
    /// Automatically selects the closest matching profile under <see cref="Illuminant.E"/> and applies color transfer to <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The source RGB image to recolor.</param>
    /// <param name="profiles">The candidate color transfer profiles to evaluate.</param>
    /// <param name="colorSpace">The RGB color space of the input and output images.</param>
    /// <param name="featureWeights">Optional weights for comparing the 6D feature components (defaults to unit weights).</param>
    /// <param name="strength">Blending strength in <c>[0, 1]</c>.</param>
    /// <returns>The recolored <see cref="Matrix{Rgb8}"/> image and the selected <see cref="ColorTransferProfile"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/>, <paramref name="profiles"/>, or any profile entry is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="profiles"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> or any profile's illuminant is undefined, <paramref name="strength"/> is outside <c>[0, 1]</c>, or any feature weight is negative.</exception>
    public static (Matrix<Rgb8> Result, ColorTransferProfile SelectedProfile) TransferToClosestProfile(
        IReadOnlyMatrix<Rgb8> source,
        IReadOnlyList<ColorTransferProfile> profiles,
        RgbColorSpace colorSpace = RgbColorSpace.SRgb,
        LabColorStatistics? featureWeights = null,
        double strength = 1.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profiles);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        Guard.ThrowIfNotFiniteInRange(strength, 0.0, 1.0);

        Matrix<Lab32> sourceLabE = source.Map(pixel => pixel.ToLab32(colorSpace, Illuminant.E));
        LabColorStatistics sourceStats = ComputeStatistics(sourceLabE);
        ColorTransferProfile selected = SelectClosestProfile(sourceStats, profiles, featureWeights);

        for (int i = 0; i < profiles.Count; i++)
            Guard.ThrowIfUndefinedEnum(profiles[i].Illuminant, nameof(profiles));

        if (source.Count == 0 || strength == 0.0)
            return (source.Clone(), selected);

        Matrix<Rgb8> result = selected.Illuminant == Illuminant.E
            ? TransferLabToRgb8(sourceLabE, selected.Statistics, colorSpace, Illuminant.E, strength)
            : Transfer(source, selected, colorSpace, strength);

        return (result, selected);
    }

    private static Matrix<Rgb8> TransferLabToRgb8(
        IReadOnlyMatrix<Lab32> sourceLab,
        LabColorStatistics targetStatistics,
        RgbColorSpace colorSpace,
        Illuminant illuminant,
        double strength) =>
        Transfer(sourceLab, targetStatistics, strength).Map(pixel => pixel.ToRgb8(colorSpace, illuminant));
}
