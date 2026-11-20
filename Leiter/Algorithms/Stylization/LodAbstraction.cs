namespace Leiter.Algorithms.Stylization;

using System;
using System.Threading;
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
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the stylized LOD abstracted image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="regionGraph"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="regionGraph"/>, <paramref name="distanceField"/>, or <paramref name="salientMask"/> do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        IReadOnlyMatrix<bool>? salientMask = null,
        LodAbstractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(regionGraph);
        Guard.ThrowIfSizeMismatch(image.Size, regionGraph.Size, nameof(regionGraph));
        Guard.ThrowIfSizeMismatch(image, distanceField);
        Guard.ThrowIfSizeMismatchIfNotNull(image, salientMask);

        LodAbstractionOptions resolvedOptions = OptionsResolver.Resolve(options);
        return ApplyCore(image, regionGraph, distanceField, salientMask, precomputedSalientFractions: null, resolvedOptions, cancellationToken);
    }

    internal static Matrix<Rgb8> ApplyCore(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        IReadOnlyMatrix<bool>? salientMask,
        double[]? precomputedSalientFractions,
        LodAbstractionOptions resolvedOptions,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0)
            return new SequentialMatrix<Rgb8>(image.Size);

        Size size = image.Size;
        int width = size.Width;
        int height = size.Height;

        Rgb64[] normalizedRgb = image.MapToArray(static pixel => pixel.ToRgb64());
        cancellationToken.ThrowIfCancellationRequested();

        bool[] isSalientRegion = precomputedSalientFractions is not null
            ? RegionGraph.ComputeRegionFlags(precomputedSalientFractions, resolvedOptions.SalientOverlapThreshold)
            : regionGraph.ComputeRegionFlags(
                salientMask ?? SaliencyDistanceField.ExtractSalientMask(distanceField),
                resolvedOptions.SalientOverlapThreshold);
        cancellationToken.ThrowIfCancellationRequested();

        var result = new SequentialMatrix<Rgb8>(size);

        void ProcessRow(int y)
        {
            int rowOffset = size.IndexOf(0, y);
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
                    double d = ScalarMath.ClampUnitOrDefault(distanceField[x, y].Value);
                    double rawSize = Math.Round(resolvedOptions.DistanceKernelScale * (d + resolvedOptions.DistanceKernelBias));
                    kernelSize = (int)Math.Clamp(rawSize, resolvedOptions.MinNonSalientKernelSize, resolvedOptions.MaxNonSalientKernelSize);
                    double colorThreshold = resolvedOptions.CrossSegmentColorScale * d;
                    colorThresholdSq = colorThreshold * colorThreshold;
                }

                int radius = kernelSize / 2;
                bool hasHalfBorder = (kernelSize & 1) == 0 && radius > 0;

                var (minX, maxX, minY, maxY) = size.ClampWindow(x, y, radius);

                double sumR = 0.0;
                double sumG = 0.0;
                double sumB = 0.0;
                double weightSum = 0.0;

                for (int qy = minY; qy <= maxY; qy++)
                {
                    double wy = hasHalfBorder && Math.Abs(qy - y) == radius ? 0.5 : 1.0;
                    int qRowOffset = size.IndexOf(0, qy);

                    for (int qx = minX; qx <= maxX; qx++)
                    {
                        int qIndex = qRowOffset + qx;
                        int qLabel = regionGraph.Labels[qx, qy];

                        bool contributes = qLabel == centerLabel;
                        if (!contributes && !isSalient && !isSalientRegion[qLabel] && colorThresholdSq > 0.0 && regionGraph.AreNeighbors(centerLabel, qLabel))
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
        }

        if (cancellationToken.CanBeCanceled)
        {
            Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, ProcessRow);
        }
        else
        {
            Parallel.For(0, height, ProcessRow);
        }

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
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the stylized LOD abstracted image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="segmentation"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="segmentation"/>, <paramref name="distanceField"/>, or <paramref name="salientMask"/> do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        IDisjointSet segmentation,
        IReadOnlyMatrix<DoublePixel> distanceField,
        IReadOnlyMatrix<bool>? salientMask = null,
        LodAbstractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.ThrowIfSizeMismatch(image, segmentation);
        Guard.ThrowIfSizeMismatch(image, distanceField);
        Guard.ThrowIfSizeMismatchIfNotNull(image, salientMask);

        LodAbstractionOptions resolvedOptions = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();
        RegionGraph regionGraph = segmentation.ToRegionGraph();
        return ApplyCore(image, regionGraph, distanceField, salientMask, precomputedSalientFractions: null, resolvedOptions, cancellationToken);
    }
}
