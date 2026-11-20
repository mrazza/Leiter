namespace Leiter.Algorithms.Saliency;

using Leiter.Algorithms.DistanceTransform;
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
    /// <returns>A <see cref="MaskMatrix"/> where finite entries with value &lt;= <paramref name="threshold"/> are <see langword="true"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="threshold"/> is negative or non-finite.</exception>
    public static MaskMatrix ExtractSalientMask(
        IReadOnlyMatrix<DoublePixel> distanceField,
        double threshold = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(distanceField);
        Guard.ThrowIfNotFiniteNonNegative(threshold);

        return distanceField.ToMaskMatrix(d => double.IsFinite(d.Value) && d.Value <= threshold);
    }

    /// <summary>
    /// Computes the unsmoothed [0, 1] saliency distance field from a boolean salient mask using
    /// Jump Flooding boundary seeds and collinear ray–border normalization (<c>dx / dy</c>).
    /// </summary>
    /// <param name="salientMask">The boolean salient mask (<see langword="false"/> = non-salient, <see langword="true"/> = salient).</param>
    /// <returns>
    /// A [0, 1] distance field where salient pixels have value <c>0</c> and non-salient pixels equal <c>dx / dy</c>.
    /// All-background masks return <c>1</c> everywhere; all-salient masks return <c>0</c> everywhere.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="salientMask"/> is <see langword="null"/>.</exception>
    public static Matrix<DoublePixel> ComputeNormalizedField(IReadOnlyMatrix<bool> salientMask)
    {
        ArgumentNullException.ThrowIfNull(salientMask);

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

        IReadOnlyList<Coord> boundarySeeds = JumpFlooding.ExtractBoundarySeeds(salientMask);
        Matrix<NearestSeed> nearestSeeds = JumpFlooding.ComputeNearestSeeds(salientMask.Size, boundarySeeds);

        Parallel.For(0, height, y =>
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
                field[x, y] = Math.Clamp(1.0 / borderRayParameter, 0.0, 1.0);
            }
        });

        return field;
    }

    /// <summary>
    /// Computes the saliency distance field from a boolean salient mask and smooths medial-axis / concavity
    /// discontinuities continuously across non-salient regions via <see cref="Blur.GaussianBlurRecursive"/>,
    /// clamping salient mask pixels back to <c>0</c>.
    /// </summary>
    /// <param name="salientMask">The boolean salient mask (<see langword="false"/> = non-salient, <see langword="true"/> = salient).</param>
    /// <param name="options">Optional smoothing parameters controlling <see cref="SaliencyDistanceFieldOptions.SmoothingSigma"/> and <see cref="SaliencyDistanceFieldOptions.DefaultSigmaScale"/>.</param>
    /// <returns>The smoothed [0, 1] saliency distance field with salient pixels fixed at <c>0</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="salientMask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are negative or non-finite.</exception>
    public static Matrix<DoublePixel> Compute(
        IReadOnlyMatrix<bool> salientMask,
        SaliencyDistanceFieldOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(salientMask);
        SaliencyDistanceFieldOptions resolvedOptions = OptionsResolver.Resolve(options, SaliencyDistanceFieldOptions.Default);
        return ComputeCore(salientMask, resolvedOptions);
    }

    private static Matrix<DoublePixel> ComputeCore(
        IReadOnlyMatrix<bool> salientMask,
        SaliencyDistanceFieldOptions resolvedOptions)
    {
        Matrix<DoublePixel> normalizedField = ComputeNormalizedField(salientMask);
        if (normalizedField.Count == 0)
            return normalizedField;

        double resolvedSigma = resolvedOptions.SmoothingSigma ?? (resolvedOptions.DefaultSigmaScale * Math.Min(salientMask.Width, salientMask.Height));
        if (resolvedSigma <= 0.0)
            return normalizedField;

        Matrix<DoublePixel> smoothed = Blur.GaussianBlurRecursive(normalizedField, resolvedSigma);
        for (int y = 0; y < salientMask.Height; y++)
        {
            for (int x = 0; x < salientMask.Width; x++)
            {
                smoothed[x, y] = salientMask[x, y]
                    ? 0.0
                    : Math.Clamp(smoothed[x, y].Value, 0.0, 1.0);
            }
        }

        return smoothed;
    }

    /// <summary>
    /// End-to-end Stage 2 helper that extracts a boolean salient mask via <see cref="SaliencyCut"/> and computes
    /// its smoothed [0, 1] saliency distance field.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="saliencyMap">The continuous [0, 1] saliency map matching <paramref name="image"/> dimensions.</param>
    /// <param name="saliencyCutOptions">Optional <see cref="SaliencyCut"/> configuration parameters.</param>
    /// <param name="distanceFieldOptions">Optional <see cref="SaliencyDistanceField"/> smoothing parameters.</param>
    /// <returns>The extracted <c>SalientMask</c> and its smoothed [0, 1] <c>DistanceField</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="saliencyMap"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="saliencyMap"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="saliencyCutOptions"/> or <paramref name="distanceFieldOptions"/> parameters are invalid.</exception>
    public static (MaskMatrix SalientMask, Matrix<DoublePixel> DistanceField) Compute(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        SaliencyCutOptions? saliencyCutOptions = null,
        SaliencyDistanceFieldOptions? distanceFieldOptions = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        Guard.ThrowIfSizeMismatch(image, saliencyMap);
        SaliencyDistanceFieldOptions resolvedDistanceOptions = OptionsResolver.Resolve(distanceFieldOptions, SaliencyDistanceFieldOptions.Default);

        MaskMatrix salientMask = SaliencyCut.Extract(image, saliencyMap, saliencyCutOptions);
        Matrix<DoublePixel> distanceField = ComputeCore(salientMask, resolvedDistanceOptions);
        return (salientMask, distanceField);
    }
}
