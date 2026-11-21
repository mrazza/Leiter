namespace Leiter.Algorithms.Color;

using System;
using System.Threading;
using Leiter.Algorithms.Internal;
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

        int count = labImage.Count;
        if (count == 0)
            return default;

        if (labImage is SequentialMatrix<Lab32> sequential && count >= ParallelRunner.DefaultMinParallelCount)
            return ComputeParallelArrayStatistics(sequential.RawData);

        if (labImage.TryGetReadOnlySpan(out ReadOnlySpan<Lab32> span))
            return ComputeSpanStatistics(span);

        return ComputeIndexedStatistics(labImage);
    }

    private static LabColorStatistics ComputeParallelArrayStatistics(Lab32[] data)
    {
        int count = data.Length;
        Lab64[] chunkSums = ParallelRunner.MapChunks(
            count,
            CancellationToken.None,
            (start, end) =>
            {
                double localL = 0.0;
                double localA = 0.0;
                double localB = 0.0;
                for (int i = start; i < end; i++)
                {
                    Lab32 pixel = data[i];
                    localL += pixel.L;
                    localA += pixel.A;
                    localB += pixel.B;
                }

                return new Lab64(localL, localA, localB);
            });

        Lab64 mean = SumLab64(chunkSums) / count;

        Lab64[] chunkSq = ParallelRunner.MapChunks(
            count,
            CancellationToken.None,
            (start, end) =>
            {
                double localSqL = 0.0;
                double localSqA = 0.0;
                double localSqB = 0.0;
                for (int i = start; i < end; i++)
                {
                    Lab32 pixel = data[i];
                    double dL = pixel.L - mean.L;
                    double dA = pixel.A - mean.A;
                    double dB = pixel.B - mean.B;
                    localSqL += dL * dL;
                    localSqA += dA * dA;
                    localSqB += dB * dB;
                }

                return new Lab64(localSqL, localSqA, localSqB);
            });

        Lab64 sqSum = SumLab64(chunkSq);
        return FinalizeStatistics(count, mean, sqSum);
    }

    private static LabColorStatistics ComputeSpanStatistics(ReadOnlySpan<Lab32> span)
    {
        int count = span.Length;
        double sumL = 0.0;
        double sumA = 0.0;
        double sumB = 0.0;
        for (int i = 0; i < count; i++)
        {
            Lab32 pixel = span[i];
            sumL += pixel.L;
            sumA += pixel.A;
            sumB += pixel.B;
        }

        var mean = new Lab64(sumL / count, sumA / count, sumB / count);

        double sqL = 0.0;
        double sqA = 0.0;
        double sqB = 0.0;
        for (int i = 0; i < count; i++)
        {
            Lab32 pixel = span[i];
            double dL = pixel.L - mean.L;
            double dA = pixel.A - mean.A;
            double dB = pixel.B - mean.B;
            sqL += dL * dL;
            sqA += dA * dA;
            sqB += dB * dB;
        }

        return FinalizeStatistics(count, mean, new Lab64(sqL, sqA, sqB));
    }

    private static LabColorStatistics ComputeIndexedStatistics(IReadOnlyMatrix<Lab32> labImage)
    {
        int width = labImage.Width;
        int count = labImage.Count;

        Lab64[] chunkSums = ParallelRunner.MapRows(
            labImage.Size,
            CancellationToken.None,
            (startRow, endRow) =>
            {
                double localL = 0.0;
                double localA = 0.0;
                double localB = 0.0;
                for (int y = startRow; y < endRow; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Lab32 pixel = labImage[x, y];
                        localL += pixel.L;
                        localA += pixel.A;
                        localB += pixel.B;
                    }
                }

                return new Lab64(localL, localA, localB);
            });

        Lab64 mean = SumLab64(chunkSums) / count;

        Lab64[] chunkSq = ParallelRunner.MapRows(
            labImage.Size,
            CancellationToken.None,
            (startRow, endRow) =>
            {
                double localSqL = 0.0;
                double localSqA = 0.0;
                double localSqB = 0.0;
                for (int y = startRow; y < endRow; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Lab32 pixel = labImage[x, y];
                        double dL = pixel.L - mean.L;
                        double dA = pixel.A - mean.A;
                        double dB = pixel.B - mean.B;
                        localSqL += dL * dL;
                        localSqA += dA * dA;
                        localSqB += dB * dB;
                    }
                }

                return new Lab64(localSqL, localSqA, localSqB);
            });

        Lab64 sqSum = SumLab64(chunkSq);
        return FinalizeStatistics(count, mean, sqSum);
    }

    private static Lab64 SumLab64(Lab64[] chunks)
    {
        double sumL = 0.0;
        double sumA = 0.0;
        double sumB = 0.0;
        for (int i = 0; i < chunks.Length; i++)
        {
            sumL += chunks[i].L;
            sumA += chunks[i].A;
            sumB += chunks[i].B;
        }

        return new Lab64(sumL, sumA, sumB);
    }

    private static LabColorStatistics FinalizeStatistics(int count, Lab64 mean, Lab64 sqSum) =>
        new(
            mean.L,
            mean.A,
            mean.B,
            Math.Sqrt(Math.Max(0.0, sqSum.L / count)),
            Math.Sqrt(Math.Max(0.0, sqSum.A / count)),
            Math.Sqrt(Math.Max(0.0, sqSum.B / count)));

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

        Matrix<Lab32> labImage = image.ToLab32(colorSpace, illuminant);
        return ComputeStatistics(labImage);
    }

    /// <summary>
    /// Selects the profile whose CIELAB feature vector minimizes weighted Euclidean distance to <paramref name="sourceStatistics"/>.
    /// </summary>
    /// <param name="sourceStatistics">The CIELAB statistics of the input image.</param>
    /// <param name="profiles">The candidate color transfer profiles to evaluate.</param>
    /// <param name="featureWeights">Optional weights for comparing the 6D feature components (defaults to unit weights).</param>
    /// <returns>The closest matching <see cref="ColorTransferProfile"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profiles"/>, any entry in <paramref name="profiles"/>, or any profile's <see cref="ColorTransferProfile.Name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="profiles"/> is empty or contains a profile with an empty or whitespace name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any profile contains an undefined illuminant or invalid CIELAB statistics, or when any weight in <paramref name="featureWeights"/> is negative or non-finite.</exception>
    public static ColorTransferProfile SelectClosestProfile(
        LabColorStatistics sourceStatistics,
        IReadOnlyList<ColorTransferProfile> profiles,
        LabColorStatistics? featureWeights = null)
    {
        ValidateProfiles(profiles);
        LabColorStatistics weights = featureWeights ?? LabColorStatistics.UnitWeights;
        ValidateFeatureWeights(weights);

        return SelectClosestProfileUnchecked(sourceStatistics, profiles, weights);
    }

    private static ColorTransferProfile SelectClosestProfileUnchecked(
        LabColorStatistics sourceStatistics,
        IReadOnlyList<ColorTransferProfile> profiles,
        LabColorStatistics weights)
    {
        ColorTransferProfile best = profiles[0];
        double bestDistance = sourceStatistics.FeatureDistance(best.Statistics, weights);

        for (int i = 1; i < profiles.Count; i++)
        {
            ColorTransferProfile candidate = profiles[i];
            double distance = sourceStatistics.FeatureDistance(candidate.Statistics, weights);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        return best;
    }

    private static void ValidateProfiles(IReadOnlyList<ColorTransferProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        if (profiles.Count == 0)
            throw new ArgumentException("At least one candidate profile must be provided.", nameof(profiles));

        for (int i = 0; i < profiles.Count; i++)
        {
            ColorTransferProfile profile = profiles[i]
                ?? throw new ArgumentNullException(nameof(profiles), "Profile entries cannot be null.");
            profile.Validate(nameof(profiles));
        }
    }

    private static void ValidateFeatureWeights(LabColorStatistics featureWeights)
    {
        Guard.ThrowIfNotFiniteNonNegative(featureWeights.MeanL, nameof(featureWeights));
        Guard.ThrowIfNotFiniteNonNegative(featureWeights.MeanA, nameof(featureWeights));
        Guard.ThrowIfNotFiniteNonNegative(featureWeights.MeanB, nameof(featureWeights));
        Guard.ThrowIfNotFiniteNonNegative(featureWeights.StdDevL, nameof(featureWeights));
        Guard.ThrowIfNotFiniteNonNegative(featureWeights.StdDevA, nameof(featureWeights));
        Guard.ThrowIfNotFiniteNonNegative(featureWeights.StdDevB, nameof(featureWeights));
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
        Matrix<Lab32> sourceLab = source.ToLab32(colorSpace, illuminant);
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
    /// <exception cref="ArgumentException">Thrown when <paramref name="profile"/> has an empty or whitespace name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> is undefined, <paramref name="profile"/> contains an undefined illuminant or invalid CIELAB statistics, or <paramref name="strength"/> is outside <c>[0, 1]</c>.</exception>
    public static Matrix<Rgb8> Transfer(
        IReadOnlyMatrix<Rgb8> source,
        ColorTransferProfile profile,
        RgbColorSpace colorSpace = RgbColorSpace.SRgb,
        double strength = 1.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        profile.Validate(nameof(profile));
        Guard.ThrowIfNotFiniteInRange(strength, 0.0, 1.0);

        if (source.Count == 0 || strength == 0.0)
            return source.Clone();

        Matrix<Lab32> sourceLab = source.ToLab32(colorSpace, profile.Illuminant);
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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/>, <paramref name="profiles"/>, any profile entry, or any profile's <see cref="ColorTransferProfile.Name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="profiles"/> is empty or contains a profile with an empty or whitespace name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="colorSpace"/> is undefined, any profile contains an undefined illuminant or invalid CIELAB statistics, <paramref name="strength"/> is outside <c>[0, 1]</c>, or any feature weight is negative or non-finite.</exception>
    public static (Matrix<Rgb8> Result, ColorTransferProfile SelectedProfile) TransferToClosestProfile(
        IReadOnlyMatrix<Rgb8> source,
        IReadOnlyList<ColorTransferProfile> profiles,
        RgbColorSpace colorSpace = RgbColorSpace.SRgb,
        LabColorStatistics? featureWeights = null,
        double strength = 1.0)
    {
        ArgumentNullException.ThrowIfNull(source);
        ValidateProfiles(profiles);
        Guard.ThrowIfUndefinedEnum(colorSpace);
        LabColorStatistics weights = featureWeights ?? LabColorStatistics.UnitWeights;
        ValidateFeatureWeights(weights);
        Guard.ThrowIfNotFiniteInRange(strength, 0.0, 1.0);

        if (source.Count == 0)
        {
            ColorTransferProfile emptySelected = SelectClosestProfileUnchecked(default, profiles, weights);
            return (source.Clone(), emptySelected);
        }

        if (strength == 0.0 && profiles.Count == 1)
        {
            return (source.Clone(), profiles[0]);
        }

        Matrix<Lab32> sourceLabE = source.ToLab32(colorSpace, Illuminant.E);
        LabColorStatistics sourceStats = ComputeStatistics(sourceLabE);
        ColorTransferProfile selected = SelectClosestProfileUnchecked(sourceStats, profiles, weights);

        if (strength == 0.0)
        {
            return (source.Clone(), selected);
        }

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
        Transfer(sourceLab, targetStatistics, strength).ToRgb8(colorSpace, illuminant);
}
