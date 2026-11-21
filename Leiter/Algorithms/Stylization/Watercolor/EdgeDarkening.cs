namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Threading;
using Leiter.Algorithms;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Applies edge darkening to simulate watercolor pigment accumulation at drying boundaries (Bousseau et al. 2006).
/// </summary>
public static class EdgeDarkening
{
    /// <summary>
    /// Computes spatial color gradients, optionally blurs the edge response, and applies density adjustments to darken edges.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="options">Optional edge darkening configuration parameters. If <see langword="null"/>, default options are used.</param>
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the edge-darkened image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> are invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        EdgeDarkeningOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        EdgeDarkeningOptions opts = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0 || opts.Intensity == 0.0)
        {
            return image.Clone();
        }

        Matrix<DoublePixel> densityField = ComputeDensityFieldCore(image, opts, cancellationToken);
        return PigmentBlend.ApplyDensityField(image, densityField, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Computes the per-pixel edge darkening pigment density offset field for <paramref name="image"/>.
    /// </summary>
    /// <remarks>
    /// The returned field can be passed directly to <see cref="PigmentBlend.ApplyDensityField(IReadOnlyMatrix{Rgb8}, IReadOnlyMatrix{DoublePixel}, double, CancellationToken)"/>
    /// or summed with other pigment density fields before blending.
    /// </remarks>
    /// <param name="image">The input RGB image.</param>
    /// <param name="options">Optional edge darkening configuration parameters. If <see langword="null"/>, default options are used.</param>
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{DoublePixel}"/> containing non-negative edge density offsets matching <paramref name="image"/> dimensions.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> are invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<DoublePixel> ComputeDensityField(
        IReadOnlyMatrix<Rgb8> image,
        EdgeDarkeningOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        EdgeDarkeningOptions opts = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0 || opts.Intensity == 0.0)
        {
            return new SequentialMatrix<DoublePixel>(image.Size);
        }

        return ComputeDensityFieldCore(image, opts, cancellationToken);
    }

    private static Matrix<DoublePixel> ComputeDensityFieldCore(
        IReadOnlyMatrix<Rgb8> image,
        EdgeDarkeningOptions opts,
        CancellationToken cancellationToken)
    {
        var (_, _, edgeStrength) = SpatialGradients.ComputeColorGradients(image, opts.GradientOperator);
        cancellationToken.ThrowIfCancellationRequested();

        Matrix<DoublePixel> smoothedEdges = Blur.GaussianBlur(edgeStrength, opts.BlurSigma);
        cancellationToken.ThrowIfCancellationRequested();

        int width = smoothedEdges.Width;
        double intensity = opts.Intensity;

        if (smoothedEdges.TryGetRawArray() is DoublePixel[] raw)
        {
            ParallelRunner.ForChunks(
                raw.Length,
                cancellationToken,
                (start, end) =>
                {
                    for (int i = start; i < end; i++)
                        raw[i] = new DoublePixel(intensity * ScalarMath.ClampUnitOrDefault(raw[i].Value));
                });
        }
        else
        {
            ParallelRunner.ForRows(
                smoothedEdges.Size,
                cancellationToken,
                (startRow, endRow) =>
                {
                    for (int y = startRow; y < endRow; y++)
                    {
                        for (int x = 0; x < width; x++)
                            smoothedEdges[x, y] = new DoublePixel(intensity * ScalarMath.ClampUnitOrDefault(smoothedEdges[x, y].Value));
                    }
                });
        }

        return smoothedEdges;
    }
}
