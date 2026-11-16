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
    /// discontinuities continuously across the boundary via <see cref="Blur.GaussianBlurRecursive"/>.
    /// </summary>
    /// <param name="salientMask">The boolean salient mask (<see langword="false"/> = non-salient, <see langword="true"/> = salient).</param>
    /// <param name="smoothingSigma">
    /// Optional explicit Gaussian smoothing σ; when <see langword="null"/>, defaults to
    /// <c>defaultSigmaScale * min(Width, Height)</c>. Passing <c>0</c> disables smoothing.
    /// </param>
    /// <param name="defaultSigmaScale">The fraction of <c>min(Width, Height)</c> used when <paramref name="smoothingSigma"/> is <see langword="null"/> (default <c>0.03</c>).</param>
    /// <returns>The smoothed [0, 1] saliency distance field.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="salientMask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="smoothingSigma"/> or <paramref name="defaultSigmaScale"/> is negative or non-finite.</exception>
    public static Matrix<DoublePixel> Compute(
        IReadOnlyMatrix<bool> salientMask,
        double? smoothingSigma = null,
        double defaultSigmaScale = 0.03)
    {
        ArgumentNullException.ThrowIfNull(salientMask);
        if (!double.IsFinite(defaultSigmaScale) || defaultSigmaScale < 0.0)
            throw new ArgumentOutOfRangeException(nameof(defaultSigmaScale), defaultSigmaScale, "Default sigma scale must be finite and non-negative.");
        if (smoothingSigma is double explicitSigma && (!double.IsFinite(explicitSigma) || explicitSigma < 0.0))
            throw new ArgumentOutOfRangeException(nameof(smoothingSigma), smoothingSigma, "Smoothing sigma must be finite and non-negative.");

        Matrix<DoublePixel> normalizedField = ComputeNormalizedField(salientMask);
        if (normalizedField.Count == 0)
            return normalizedField;

        double resolvedSigma = smoothingSigma ?? (defaultSigmaScale * Math.Min(salientMask.Width, salientMask.Height));
        if (resolvedSigma <= 0.0)
            return normalizedField;

        Matrix<DoublePixel> smoothed = Blur.GaussianBlurRecursive(normalizedField, resolvedSigma);
        for (int i = 0; i < smoothed.Count; i++)
            smoothed[i] = Math.Clamp(smoothed[i].Value, 0.0, 1.0);

        return smoothed;
    }

    /// <summary>
    /// End-to-end Stage 2 helper that extracts a boolean salient mask via <see cref="SaliencyCut"/> and computes
    /// its smoothed [0, 1] saliency distance field.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="saliencyMap">The continuous [0, 1] saliency map matching <paramref name="image"/> dimensions.</param>
    /// <param name="saliencyCutOptions">Optional <see cref="SaliencyCut"/> configuration parameters.</param>
    /// <param name="smoothingSigma">Optional explicit Gaussian smoothing σ (defaults to <c>3%</c> of the smaller image dimension).</param>
    /// <returns>The extracted <c>SalientMask</c> and its smoothed [0, 1] <c>DistanceField</c>.</returns>
    public static (MaskMatrix SalientMask, Matrix<DoublePixel> DistanceField) Compute(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> saliencyMap,
        SaliencyCutOptions? saliencyCutOptions = null,
        double? smoothingSigma = null)
    {
        MaskMatrix salientMask = SaliencyCut.Extract(image, saliencyMap, saliencyCutOptions);
        Matrix<DoublePixel> distanceField = Compute(salientMask, smoothingSigma);
        return (salientMask, distanceField);
    }
}
