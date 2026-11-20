namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Implements hand tremor edge perturbation for watercolor stylization (Wang et al. 2014, Sec. 3.5).
/// Perturbs segment boundaries with Perlin noise displacement fields, producing authentic paper gaps
/// and darkened pigment glaze overlaps along contrasting boundaries while preserving smooth blending across wet-in-wet and similar-hue edges.
/// </summary>
public static class HandTremor
{
    private const int BandTaperWidth = 3;

    private enum BoundaryGroup : byte
    {
        WetInWetPreserved = 0,
        SimilarHue = 1,
        ContrastingHue = 2
    }

    private static readonly Func<int, int, (IReadOnlyMatrix<int> Labels, int First, int Second), bool> MatchesRegionPairPredicate =
        static (cx, cy, state) =>
        {
            int label = state.Labels[cx, cy];
            return label == state.First || label == state.Second;
        };

    /// <summary>
    /// Applies hand tremor perturbation to an image using precomputed region adjacency and distance transform fields.
    /// </summary>
    /// <param name="image">The input RGB image to perturb.</param>
    /// <param name="regionGraph">The region adjacency graph representing the image segmentation.</param>
    /// <param name="distanceField">The normalized saliency distance field.</param>
    /// <param name="wetInWet">Optional wet-in-wet filtering result; boundaries and wet areas qualifying in wet-in-wet are preserved without tremor perturbation.</param>
    /// <param name="options">Optional hand tremor configuration options.</param>
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the tremor-perturbed watercolor image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="regionGraph"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="regionGraph"/>, <paramref name="distanceField"/>, or <paramref name="wetInWet"/> do not match <paramref name="image"/>, or boundary counts do not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        WetInWetResult? wetInWet = null,
        HandTremorOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(regionGraph);
        Guard.ThrowIfSizeMismatch(image.Size, regionGraph.Size, nameof(regionGraph));
        Guard.ThrowIfSizeMismatch(image, distanceField);

        wetInWet?.ValidateAgainst(regionGraph, nameof(wetInWet));

        HandTremorOptions opts = OptionsResolver.Resolve(options);
        return ApplyCore(image, regionGraph, distanceField, wetInWet, opts, cancellationToken);
    }

    /// <summary>
    /// Applies hand tremor perturbation to an image using a disjoint-set pixel partition and saliency distance transform field.
    /// </summary>
    /// <param name="image">The input RGB image to perturb.</param>
    /// <param name="segmentation">The segmented pixel partition.</param>
    /// <param name="distanceField">The normalized saliency distance field.</param>
    /// <param name="wetInWet">Optional wet-in-wet filtering result; boundaries and wet areas qualifying in wet-in-wet are preserved without tremor perturbation.</param>
    /// <param name="options">Optional hand tremor configuration options.</param>
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the tremor-perturbed watercolor image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="segmentation"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="segmentation"/>, <paramref name="distanceField"/>, or <paramref name="wetInWet"/> do not match <paramref name="image"/>, or boundary counts do not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        IDisjointSet segmentation,
        IReadOnlyMatrix<DoublePixel> distanceField,
        WetInWetResult? wetInWet = null,
        HandTremorOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.ThrowIfSizeMismatch(image, segmentation);
        Guard.ThrowIfSizeMismatch(image, distanceField);
        Guard.ThrowIfSizeMismatchIfNotNull(image, wetInWet?.Image, nameof(wetInWet));

        HandTremorOptions opts = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();
        RegionGraph regionGraph = segmentation.ToRegionGraph();
        wetInWet?.ValidateAgainst(regionGraph, nameof(wetInWet));

        return ApplyCore(image, regionGraph, distanceField, wetInWet, opts, cancellationToken);
    }

    internal static Matrix<Rgb8> ApplyCore(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        WetInWetResult? wetInWet,
        HandTremorOptions opts,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int boundaryCount = regionGraph.Boundaries.Count;
        if (image.Count == 0 || opts.Amplitude == 0.0 || boundaryCount == 0)
        {
            return image.Clone();
        }

        PerlinNoiseOptions primaryNoise = opts.PrimaryNoise ?? HandTremorOptions.DefaultPrimaryNoise;
        PerlinNoiseOptions secondaryNoise = opts.SecondaryNoise ?? HandTremorOptions.DefaultSecondaryNoise;
        Rgb8 paperColor = opts.PaperColor ?? HandTremorOptions.DefaultPaperColor;

        RegionColorStatistics<Rgb64>[] stats = RegionColorStatistics.Compute(regionGraph, image);
        cancellationToken.ThrowIfCancellationRequested();

        bool canCancel = cancellationToken.CanBeCanceled;
        double minContrastingChroma = opts.MinContrastingChroma;
        var boundaryGroup = new BoundaryGroup[boundaryCount];
        for (int bi = 0; bi < boundaryCount; bi++)
        {
            if (canCancel && (bi & 4095) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (wetInWet is not null && wetInWet.QualifyingBoundaries[bi])
            {
                boundaryGroup[bi] = BoundaryGroup.WetInWetPreserved;
            }
            else
            {
                RegionBoundary b = regionGraph.Boundaries[bi];
                double chromaA = stats[b.First].MeanColor.GetChroma();
                double chromaB = stats[b.Second].MeanColor.GetChroma();
                double hueDiff = ColorMetrics.HueDifferenceDegrees(stats[b.First].MeanHueDegrees, stats[b.Second].MeanHueDegrees);
                boundaryGroup[bi] = (chromaA >= minContrastingChroma &&
                                     chromaB >= minContrastingChroma &&
                                     hueDiff >= opts.SimilarHueThresholdDegrees)
                    ? BoundaryGroup.ContrastingHue
                    : BoundaryGroup.SimilarHue;
            }
        }

        int width = image.Width;
        int height = image.Height;
        int maxUsefulBandRadius = width + height;
        double rawInnerRadius = Math.Ceiling(opts.Amplitude * (1.0 + Math.Abs(opts.PaperGapBias)));
        int innerRadius = (int)Math.Min(rawInnerRadius, maxUsefulBandRadius);
        int bandRadius = innerRadius + BandTaperWidth;
        ArgumentOutOfRangeException.ThrowIfLessThan(bandRadius, innerRadius);

        cancellationToken.ThrowIfCancellationRequested();
        RegionBoundaryBand band = regionGraph.ComputeBoundaryBand(bandRadius, restrictToIncidentRegions: true);
        cancellationToken.ThrowIfCancellationRequested();

        Matrix<Vector2D> t1 = PerlinNoise.GenerateDisplacementField(image.Size, opts.Amplitude, primaryNoise);
        cancellationToken.ThrowIfCancellationRequested();
        Matrix<Vector2D> t2 = PerlinNoise.GenerateDisplacementField(image.Size, opts.Amplitude, secondaryNoise);
        cancellationToken.ThrowIfCancellationRequested();

        var result = new SequentialMatrix<Rgb8>(width, height);

        void ProcessRow(int y)
        {
            for (int x = 0; x < width; x++)
            {
                int nearestBoundaryIndex = band.NearestBoundaryIndices[x, y];
                if ((wetInWet is not null && wetInWet.WetAreaMask[x, y]) ||
                    nearestBoundaryIndex < 0 ||
                    boundaryGroup[nearestBoundaryIndex] == BoundaryGroup.WetInWetPreserved)
                {
                    result[x, y] = image[x, y];
                    continue;
                }

                double bandWeight = ScalarMath.OuterBandSmoothstepWeightUnchecked(band.Distances[x, y], innerRadius, bandRadius);
                double clampedD = ScalarMath.ClampUnitOrDefault(distanceField[x, y].Value);
                double s = ScalarMath.Lerp(opts.SalientAmplitudeScale, 1.0, clampedD) * bandWeight;

                RegionBoundary b = regionGraph.Boundaries[nearestBoundaryIndex];
                int a = b.First;
                int bId = b.Second;

                var basePos = new Vector2D(x, y);
                Vector2D p1 = basePos + (s * t1[x, y]);

                BoundaryGroup group = boundaryGroup[nearestBoundaryIndex];
                if (group == BoundaryGroup.SimilarHue)
                {
                    result[x, y] = SampleInRegion(image, regionGraph.Labels, p1, a, bId, image[x, y]);
                }
                else
                {
                    Vector2D p2 = basePos + (s * t2[x, y]);

                    if (opts.PaperGapBias != 0.0)
                    {
                        Vector2D normal = ComputeBoundaryNormalAToB(regionGraph.Labels, band.Distances, x, y, a, bId, bandRadius);
                        Vector2D bias = (opts.PaperGapBias * s * opts.Amplitude) * normal;
                        p1 += bias;
                        p2 -= bias;
                    }

                    p1 = new(EdgeHandling.Extend.ResolveCoordinate(p1.X, width), EdgeHandling.Extend.ResolveCoordinate(p1.Y, height));
                    p2 = new(EdgeHandling.Extend.ResolveCoordinate(p2.X, width), EdgeHandling.Extend.ResolveCoordinate(p2.Y, height));

                    int label1 = MatrixResampler.SampleNearestUnchecked(regionGraph.Labels, p1.X, p1.Y);
                    int label2 = MatrixResampler.SampleNearestUnchecked(regionGraph.Labels, p2.X, p2.Y);

                    bool hasA = label1 == a;
                    bool hasB = label2 == bId;

                    if (hasA && hasB)
                    {
                        Rgb64 fallbackColor = image[x, y].ToRgb64();
                        Rgb64 colA = SampleInRegionRgb64(image, regionGraph.Labels, p1, a, fallbackColor);
                        Rgb64 colB = SampleInRegionRgb64(image, regionGraph.Labels, p2, bId, fallbackColor);
                        result[x, y] = PigmentBlend.MixPigmentsUnchecked(colA, colB, opts.OverlapDarkening).ToRgb8();
                    }
                    else if (hasA)
                    {
                        result[x, y] = SampleInRegion(image, regionGraph.Labels, p1, a, a, image[x, y]);
                    }
                    else if (hasB)
                    {
                        result[x, y] = SampleInRegion(image, regionGraph.Labels, p2, bId, bId, image[x, y]);
                    }
                    else if (label1 == bId && label2 == a)
                    {
                        result[x, y] = Rgb8.Lerp(image[x, y], paperColor, opts.PaperGapOpacity);
                    }
                    else
                    {
                        result[x, y] = MatrixResampler.SampleBilinearUnchecked(image, p1.X, p1.Y, EdgeHandling.Extend);
                    }
                }
            }
        }

        if (canCancel)
        {
            Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, ProcessRow);
        }
        else
        {
            Parallel.For(0, height, ProcessRow);
        }

        return result;
    }

    private static Rgb8 SampleInRegion(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<int> labels,
        Vector2D pos,
        int firstRegionId,
        int secondRegionId,
        Rgb8 fallback) =>
        MatrixResampler.SampleBilinearWhereUnchecked(
            image,
            pos.X,
            pos.Y,
            (labels, firstRegionId, secondRegionId),
            MatchesRegionPairPredicate,
            fallback);

    private static Rgb64 SampleInRegionRgb64(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<int> labels,
        Vector2D pos,
        int regionId,
        Rgb64 fallback) =>
        MatrixResampler.SampleBilinearRgb64WhereUnchecked(
            image,
            pos.X,
            pos.Y,
            (labels, regionId, regionId),
            MatchesRegionPairPredicate,
            fallback);

    // Evaluates the signed geodesic distance gradient across [0, bandRadius] rather than RegionGraph.ComputeBoundaryNormal,
    // which inspects only immediate 1-ring border neighbors (returning Vector2D.Zero at dist >= 1) and whose nearest-seed
    // tie-breaking breaks diagonal x <-> y symmetry on outer band rims.
    private static Vector2D ComputeBoundaryNormalAToB(
        IReadOnlyMatrix<int> labels,
        IReadOnlyMatrix<int> distances,
        int x,
        int y,
        int regionA,
        int regionB,
        int bandRadius)
    {
        double sdLeft = SampleSignedDistance(labels, distances, x - 1, y, regionA, regionB, bandRadius);
        double sdRight = SampleSignedDistance(labels, distances, x + 1, y, regionA, regionB, bandRadius);
        double sdUp = SampleSignedDistance(labels, distances, x, y - 1, regionA, regionB, bandRadius);
        double sdDown = SampleSignedDistance(labels, distances, x, y + 1, regionA, regionB, bandRadius);

        double gx = sdRight - sdLeft;
        double gy = sdDown - sdUp;
        return new Vector2D(-gx, -gy).Normalize();
    }

    private static double SampleSignedDistance(
        IReadOnlyMatrix<int> labels,
        IReadOnlyMatrix<int> distances,
        int x,
        int y,
        int regionA,
        int regionB,
        int bandRadius)
    {
        int cx = EdgeHandling.Extend.ResolveIndex(x, labels.Width);
        int cy = EdgeHandling.Extend.ResolveIndex(y, labels.Height);
        int lbl = labels[cx, cy];
        if (lbl == regionA || lbl == regionB)
        {
            int dist = distances[cx, cy] == int.MaxValue ? bandRadius + 1 : distances[cx, cy];
            return lbl == regionA ? dist + 0.5 : -(dist + 0.5);
        }

        return 0.0;
    }
}
