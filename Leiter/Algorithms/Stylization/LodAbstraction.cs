namespace Leiter.Algorithms.Stylization;

using System;
using System.Threading.Tasks;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Saliency;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Implements Wang et al.'s saliency-guided level-of-detail (LOD) abstraction (Sec. 3.3 of "Towards Photo Watercolorization").
/// </summary>
public static class LodAbstraction
{
    /// <summary>
    /// Applies Wang et al. Sec. 3.3 saliency-guided level-of-detail abstraction to an image using a precomputed <see cref="RegionGraph"/>.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="regionGraph">The region adjacency graph representing the image segmentation.</param>
    /// <param name="distanceField">The normalized [0, 1] saliency distance field.</param>
    /// <param name="salientMask">Optional boolean salient mask; if <see langword="null"/>, regions with distance ≤ 1e-6 are considered salient.</param>
    /// <param name="options">Optional LOD abstraction configuration parameters.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the stylized LOD abstracted image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="regionGraph"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="regionGraph"/>, <paramref name="distanceField"/>, or <paramref name="salientMask"/> do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        IReadOnlyMatrix<bool>? salientMask = null,
        LodAbstractionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(regionGraph);
        ArgumentNullException.ThrowIfNull(distanceField);

        if (image.Size != regionGraph.Size)
            throw new ArgumentException("The region graph size must match the image size.", nameof(regionGraph));

        if (image.Size != distanceField.Size)
            throw new ArgumentException("The distance field size must match the image size.", nameof(distanceField));

        if (salientMask is not null && salientMask.Size != image.Size)
            throw new ArgumentException("The salient mask size must match the image size.", nameof(salientMask));

        LodAbstractionOptions resolvedOptions = options ?? new LodAbstractionOptions();
        resolvedOptions.Validate();

        if (image.Count == 0)
            return new SequentialMatrix<Rgb8>(image.Size);

        int width = image.Width;
        int height = image.Height;

        Rgb64[] normalizedRgb = image.MapToArray(static pixel => pixel.ToRgb64());

        IReadOnlyMatrix<bool> effectiveSalientMask = salientMask ?? SaliencyDistanceField.ExtractSalientMask(distanceField);
        double[] salientFractions = regionGraph.ComputeRegionFractions(effectiveSalientMask);
        bool[] isSalientRegion = new bool[salientFractions.Length];
        for (int i = 0; i < salientFractions.Length; i++)
            isSalientRegion[i] = salientFractions[i] >= resolvedOptions.SalientOverlapThreshold;

        var result = new SequentialMatrix<Rgb8>(width, height);

        Parallel.For(0, height, y =>
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                int centerIndex = rowOffset + x;
                int centerLabel = regionGraph.Labels[x, y];
                Rgb64 centerColor = normalizedRgb[centerIndex];
                bool isSalient = isSalientRegion[centerLabel];

                int kernelSize;
                double colorThresholdSq;

                if (isSalient)
                {
                    kernelSize = resolvedOptions.SalientKernelSize;
                    colorThresholdSq = -1.0;
                }
                else
                {
                    double rawD = distanceField[x, y].Value;
                    double d = double.IsFinite(rawD) ? Math.Clamp(rawD, 0.0, 1.0) : 0.0;
                    int computedSize = (int)Math.Round(resolvedOptions.DistanceKernelScale * (d + resolvedOptions.DistanceKernelBias));
                    kernelSize = Math.Clamp(computedSize, resolvedOptions.MinNonSalientKernelSize, resolvedOptions.MaxNonSalientKernelSize);
                    double colorThreshold = resolvedOptions.CrossSegmentColorScale * d;
                    colorThresholdSq = colorThreshold * colorThreshold;
                }

                int radius = kernelSize / 2;
                bool hasHalfBorder = (kernelSize & 1) == 0 && radius > 0;

                int minY = Math.Max(0, y - radius);
                int maxY = Math.Min(height - 1, y + radius);
                int minX = Math.Max(0, x - radius);
                int maxX = Math.Min(width - 1, x + radius);

                double sumR = 0.0;
                double sumG = 0.0;
                double sumB = 0.0;
                double weightSum = 0.0;

                for (int qy = minY; qy <= maxY; qy++)
                {
                    double wy = hasHalfBorder && Math.Abs(qy - y) == radius ? 0.5 : 1.0;
                    int qRowOffset = qy * width;

                    for (int qx = minX; qx <= maxX; qx++)
                    {
                        int qIndex = qRowOffset + qx;
                        int qLabel = regionGraph.Labels[qx, qy];

                        bool contributes = qLabel == centerLabel;
                        if (!contributes && !isSalient && colorThresholdSq > 0.0 && regionGraph.AreNeighbors(centerLabel, qLabel))
                        {
                            if (centerColor.DistanceSquared(normalizedRgb[qIndex]) < colorThresholdSq)
                            {
                                contributes = true;
                            }
                        }

                        if (contributes)
                        {
                            double wx = hasHalfBorder && Math.Abs(qx - x) == radius ? 0.5 : 1.0;
                            double w = wx * wy;

                            Rgb64 qColor = normalizedRgb[qIndex];
                            sumR += qColor.R * w;
                            sumG += qColor.G * w;
                            sumB += qColor.B * w;
                            weightSum += w;
                        }
                    }
                }

                result[x, y] = new Rgb64(sumR / weightSum, sumG / weightSum, sumB / weightSum).ToRgb8();
            }
        });

        return result;
    }

    /// <summary>
    /// Applies Wang et al. Sec. 3.3 saliency-guided level-of-detail abstraction to an image using a disjoint-set pixel partition.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="segmentation">The segmented pixel partition.</param>
    /// <param name="distanceField">The normalized [0, 1] saliency distance field.</param>
    /// <param name="salientMask">Optional boolean salient mask; if <see langword="null"/>, regions with distance ≤ 1e-6 are considered salient.</param>
    /// <param name="options">Optional LOD abstraction configuration parameters.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the stylized LOD abstracted image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="segmentation"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="segmentation"/>, <paramref name="distanceField"/>, or <paramref name="salientMask"/> do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        IDisjointSet segmentation,
        IReadOnlyMatrix<DoublePixel> distanceField,
        IReadOnlyMatrix<bool>? salientMask = null,
        LodAbstractionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(segmentation);
        ArgumentNullException.ThrowIfNull(distanceField);

        if (image.Size != segmentation.Size)
            throw new ArgumentException("The segmentation size must match the image size.", nameof(segmentation));

        RegionGraph regionGraph = RegionGraph.FromDisjointSet(segmentation);
        return Apply(image, regionGraph, distanceField, salientMask, options);
    }
}
