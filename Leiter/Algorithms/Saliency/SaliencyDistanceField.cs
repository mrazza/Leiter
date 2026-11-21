namespace Leiter.Algorithms.Saliency;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms.DistanceTransform;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Computes Wang et al.'s normalized and smoothed saliency distance field (Sec. 3.2 of "Towards Photo Watercolorization"),
/// where <c>0</c> marks salient regions and values increase toward <c>1</c> approaching image borders.
/// </summary>
public static class SaliencyDistanceField
{
    /// <summary>
    /// Extracts a binary <see cref="MaskMatrix"/> identifying pixels whose saliency distance does not exceed <paramref name="threshold"/>.
    /// </summary>
    /// <param name="distanceField">The normalized saliency distance field to threshold.</param>
    /// <param name="threshold">The inclusive maximum distance considered salient (default <c>1e-6</c>).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="MaskMatrix"/> where finite entries with value &lt;= <paramref name="threshold"/> are <see langword="true"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="threshold"/> is negative or non-finite.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static MaskMatrix ExtractSalientMask(
        IReadOnlyMatrix<DoublePixel> distanceField,
        double threshold = 1e-6,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(distanceField);
        Guard.ThrowIfNotFiniteNonNegative(threshold);
        cancellationToken.ThrowIfCancellationRequested();

        return distanceField.ToMaskMatrix(d => double.IsFinite(d.Value) && d.Value <= threshold);
    }

    /// <summary>
    /// Computes the unsmoothed [0, 1] saliency distance field from a boolean salient mask using
    /// Jump Flooding boundary seeds and collinear ray–border normalization (<c>dx / dy</c>).
    /// </summary>
    /// <param name="salientMask">The boolean salient mask (<see langword="false"/> = non-salient, <see langword="true"/> = salient).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A [0, 1] distance field where salient pixels have value <c>0</c> and non-salient pixels equal <c>dx / dy</c>.
    /// All-background masks return <c>1</c> everywhere; all-salient masks return <c>0</c> everywhere.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="salientMask"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Matrix<DoublePixel> ComputeNormalizedField(
        IReadOnlyMatrix<bool> salientMask,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(salientMask);
        cancellationToken.ThrowIfCancellationRequested();

        int width = salientMask.Width;
        int height = salientMask.Height;
        var field = new SequentialMatrix<DoublePixel>(width, height);
        if (salientMask.Count == 0)
            return field;

        int salientCount = salientMask.CountTrue();
        if (salientCount == 0)
        {
            field.SetAll(1.0);
            return field;
        }

        if (salientCount == salientMask.Count)
            return field;

        IReadOnlyList<Coord> boundarySeeds = JumpFlooding.ExtractBoundarySeeds(salientMask, cancellationToken: cancellationToken);
        Matrix<NearestSeed> nearestSeeds = JumpFlooding.ComputeNearestSeeds(salientMask.Size, boundarySeeds, cancellationToken);

        void NormalizeRow(int y)
        {
            for (int x = 0; x < width; x++)
            {
                if (salientMask[x, y])
                {
                    field[x, y] = 0.0;
                    continue;
                }

                Coord seed = nearestSeeds[x, y].Seed;
                int deltaX = x - seed.X;
                int deltaY = y - seed.Y;

                double tX = deltaX > 0
                    ? (double)(width - 1 - seed.X) / deltaX
                    : deltaX < 0
                        ? (double)(-seed.X) / deltaX
                        : double.PositiveInfinity;

                double tY = deltaY > 0
                    ? (double)(height - 1 - seed.Y) / deltaY
                    : deltaY < 0
                        ? (double)(-seed.Y) / deltaY
                        : double.PositiveInfinity;

                double borderRayParameter = Math.Min(tX, tY);
                field[x, y] = ScalarMath.ClampUnit(1.0 / borderRayParameter);
            }
        }

        if (cancellationToken.CanBeCanceled)
        {
            Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, NormalizeRow);
        }
        else
        {
            Parallel.For(0, height, NormalizeRow);
        }

        return field;
    }

    /// <summary>
    /// Smooths a previously computed unsmoothed [0, 1] saliency distance field across non-salient regions via
    /// <see cref="Blur.GaussianBlur{T}(IReadOnlyMatrix{T}, double)"/> while clamping salient mask pixels back to <c>0</c>.
    /// </summary>
    /// <param name="rawDistanceField">The unsmoothed [0, 1] saliency distance field (typically produced by <see cref="ComputeNormalizedField"/>).</param>
    /// <param name="salientMask">The boolean salient mask matching <paramref name="rawDistanceField"/> dimensions (<see langword="false"/> = non-salient, <see langword="true"/> = salient).</param>
    /// <param name="options">Optional smoothing parameters controlling <see cref="SaliencyDistanceFieldOptions.SmoothingSigma"/> and <see cref="SaliencyDistanceFieldOptions.DefaultSigmaScale"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A newly allocated smoothed [0, 1] saliency distance field with salient pixels fixed at <c>0</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rawDistanceField"/> or <paramref name="salientMask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="salientMask"/> dimensions do not match <paramref name="rawDistanceField"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are negative or non-finite.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Matrix<DoublePixel> SmoothNormalizedField(
        IReadOnlyMatrix<DoublePixel> rawDistanceField,
        IReadOnlyMatrix<bool> salientMask,
        SaliencyDistanceFieldOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.ThrowIfSizeMismatch(rawDistanceField, salientMask);
        SaliencyDistanceFieldOptions resolvedOptions = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        return SmoothNormalizedFieldCore(
            rawDistanceField,
            salientMask,
            resolvedOptions,
            reuseUnblurredInput: false,
            cancellationToken);
    }

    /// <summary>
    /// Computes the saliency distance field from a boolean salient mask and smooths medial-axis / concavity
    /// discontinuities continuously across non-salient regions via <see cref="Blur.GaussianBlur{T}(IReadOnlyMatrix{T}, double)"/>,
    /// clamping salient mask pixels back to <c>0</c>.
    /// </summary>
    /// <param name="salientMask">The boolean salient mask (<see langword="false"/> = non-salient, <see langword="true"/> = salient).</param>
    /// <param name="options">Optional smoothing parameters controlling <see cref="SaliencyDistanceFieldOptions.SmoothingSigma"/> and <see cref="SaliencyDistanceFieldOptions.DefaultSigmaScale"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The smoothed [0, 1] saliency distance field with salient pixels fixed at <c>0</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="salientMask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are negative or non-finite.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Matrix<DoublePixel> Compute(
        IReadOnlyMatrix<bool> salientMask,
        SaliencyDistanceFieldOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(salientMask);
        SaliencyDistanceFieldOptions resolvedOptions = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        return ComputeCore(salientMask, resolvedOptions, cancellationToken);
    }

    private static Matrix<DoublePixel> ComputeCore(
        IReadOnlyMatrix<bool> salientMask,
        SaliencyDistanceFieldOptions resolvedOptions,
        CancellationToken cancellationToken)
    {
        Matrix<DoublePixel> normalizedField = ComputeNormalizedField(salientMask, cancellationToken);
        return SmoothNormalizedFieldCore(
            normalizedField,
            salientMask,
            resolvedOptions,
            reuseUnblurredInput: true,
            cancellationToken);
    }

    private static Matrix<DoublePixel> SmoothNormalizedFieldCore(
        IReadOnlyMatrix<DoublePixel> rawDistanceField,
        IReadOnlyMatrix<bool> salientMask,
        SaliencyDistanceFieldOptions resolvedOptions,
        bool reuseUnblurredInput,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int width = salientMask.Width;
        int height = salientMask.Height;
        if (rawDistanceField.Count == 0)
            return reuseUnblurredInput && rawDistanceField is Matrix<DoublePixel> ownedEmpty
                ? ownedEmpty
                : new SequentialMatrix<DoublePixel>(width, height);

        double resolvedSigma = resolvedOptions.SmoothingSigma ?? (resolvedOptions.DefaultSigmaScale * Math.Min(width, height));
        if (resolvedSigma <= 0.0)
        {
            if (reuseUnblurredInput && rawDistanceField is Matrix<DoublePixel> ownedField)
                return ownedField;

            return ClampAndMaskField(
                rawDistanceField,
                salientMask,
                new SequentialMatrix<DoublePixel>(width, height),
                cancellationToken);
        }

        Matrix<DoublePixel> smoothed = Blur.GaussianBlur(rawDistanceField, resolvedSigma);
        return ClampAndMaskField(smoothed, salientMask, smoothed, cancellationToken);
    }

    private static Matrix<DoublePixel> ClampAndMaskField(
        IReadOnlyMatrix<DoublePixel> sourceField,
        IReadOnlyMatrix<bool> salientMask,
        Matrix<DoublePixel> destination,
        CancellationToken cancellationToken)
    {
        int width = sourceField.Width;
        int height = sourceField.Height;

        if (sourceField is SequentialMatrix<DoublePixel> srcSeq &&
            salientMask is SequentialMatrix<bool> maskSeq &&
            destination is SequentialMatrix<DoublePixel> dstSeq)
        {
            DoublePixel[] srcRaw = srcSeq.RawData;
            bool[] maskRaw = maskSeq.RawData;
            DoublePixel[] dstRaw = dstSeq.RawData;

            ParallelRunner.ForChunks(
                dstRaw.Length,
                cancellationToken,
                (start, end) =>
                {
                    for (int i = start; i < end; i++)
                        dstRaw[i] = maskRaw[i] ? 0.0 : ScalarMath.ClampUnitOrDefault(srcRaw[i].Value);
                });

            return destination;
        }

        int minRowParallel = sourceField.Count >= ParallelRunner.DefaultMinParallelCount && height >= 8
            ? 2
            : height + 1;

        ParallelRunner.ForChunks(
            height,
            cancellationToken,
            (startRow, endRow) =>
            {
                for (int y = startRow; y < endRow; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        destination[x, y] = salientMask[x, y]
                            ? 0.0
                            : ScalarMath.ClampUnitOrDefault(sourceField[x, y].Value);
                    }
                }
            },
            minParallelCount: minRowParallel);

        return destination;
    }

    /// <summary>
    /// End-to-end Stage 2 helper that extracts a boolean salient mask via <see cref="SaliencyCut"/> and computes
    /// its smoothed [0, 1] saliency distance field.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="saliencyMap">The continuous [0, 1] saliency map matching <paramref name="image"/> dimensions.</param>
    /// <param name="saliencyCutOptions">Optional <see cref="SaliencyCut"/> configuration parameters.</param>
    /// <param name="distanceFieldOptions">Optional <see cref="SaliencyDistanceField"/> smoothing parameters.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The extracted <c>SalientMask</c> and its smoothed [0, 1] <c>DistanceField</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="saliencyMap"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="saliencyMap"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="saliencyCutOptions"/> or <paramref name="distanceFieldOptions"/> parameters are invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static (MaskMatrix SalientMask, Matrix<DoublePixel> DistanceField) Compute(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        SaliencyCutOptions? saliencyCutOptions = null,
        SaliencyDistanceFieldOptions? distanceFieldOptions = null,
        CancellationToken cancellationToken = default)
    {
        Guard.ThrowIfSizeMismatch(image, saliencyMap);
        SaliencyCutOptions resolvedCutOptions = OptionsResolver.Resolve(saliencyCutOptions);
        SaliencyDistanceFieldOptions resolvedDistanceOptions = OptionsResolver.Resolve(distanceFieldOptions);
        cancellationToken.ThrowIfCancellationRequested();

        MaskMatrix salientMask = SaliencyCut.Extract(image, saliencyMap, resolvedCutOptions, cancellationToken);
        Matrix<DoublePixel> distanceField = ComputeCore(salientMask, resolvedDistanceOptions, cancellationToken);
        return (salientMask, distanceField);
    }
}
