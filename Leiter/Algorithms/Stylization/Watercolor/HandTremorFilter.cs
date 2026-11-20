namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
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
public static class HandTremorFilter
{
    /// <summary>
    /// Applies hand tremor perturbation to an image using precomputed region adjacency and distance transform fields.
    /// </summary>
    /// <param name="image">The input RGB image to perturb.</param>
    /// <param name="regionGraph">The region adjacency graph representing the image segmentation.</param>
    /// <param name="distanceField">The normalized saliency distance field.</param>
    /// <param name="wetInWet">Optional wet-in-wet filtering result; boundaries and wet areas qualifying in wet-in-wet are preserved without tremor perturbation.</param>
    /// <param name="options">Optional hand tremor configuration options.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the tremor-perturbed watercolor image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="regionGraph"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="regionGraph"/>, <paramref name="distanceField"/>, or <paramref name="wetInWet"/> do not match <paramref name="image"/>, or boundary counts do not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        WetInWetResult? wetInWet = null,
        HandTremorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(regionGraph);
        ArgumentNullException.ThrowIfNull(distanceField);
        Guard.ThrowIfSizeMismatch(image.Size, regionGraph.Size, nameof(regionGraph));
        Guard.ThrowIfSizeMismatch(image, distanceField);

        if (wetInWet is not null)
        {
            Guard.ThrowIfSizeMismatch(image, wetInWet.WetAreaMask, nameof(wetInWet));

            if (wetInWet.QualifyingBoundaries.Count != regionGraph.Boundaries.Count)
            {
                throw new ArgumentException("WetInWet qualifying boundaries count must match region graph boundaries count.", nameof(wetInWet));
            }
        }

        HandTremorOptions opts = OptionsResolver.Resolve(options, HandTremorOptions.Default);

        return ApplyCore(image, regionGraph, distanceField, wetInWet, opts);
    }

    internal static Matrix<Rgb8> ApplyCore(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        WetInWetResult? wetInWet,
        HandTremorOptions opts)
    {
        int boundaryCount = regionGraph.Boundaries.Count;
        if (image.Count == 0 || opts.Amplitude == 0.0 || boundaryCount == 0)
        {
            return image.Clone();
        }

        PerlinNoiseOptions primaryNoise = opts.PrimaryNoise ?? HandTremorOptions.DefaultPrimaryNoise;
        PerlinNoiseOptions secondaryNoise = opts.SecondaryNoise ?? HandTremorOptions.DefaultSecondaryNoise;
        Rgb8 paperColor = opts.PaperColor ?? HandTremorOptions.DefaultPaperColor;

        RegionColorStatistics<Rgb64>[] stats = RegionColorStatistics.Compute(regionGraph, image);

        double minContrastingChroma = opts.MinContrastingChroma;
        byte[] boundaryGroup = new byte[boundaryCount];
        for (int bi = 0; bi < boundaryCount; bi++)
        {
            if (wetInWet is not null && wetInWet.QualifyingBoundaries[bi])
            {
                boundaryGroup[bi] = 0;
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
                    ? (byte)2
                    : (byte)1;
            }
        }

        int width = image.Width;
        int height = image.Height;
        int innerRadius = (int)Math.Ceiling(opts.Amplitude * (1.0 + Math.Abs(opts.PaperGapBias)));
        int bandRadius = innerRadius + 3;

        RegionBoundaryBand band = regionGraph.ComputeBoundaryBand(bandRadius, restrictToIncidentRegions: true);

        Matrix<Vector2D> t1 = PerlinNoise.GenerateDisplacementField(image.Size, opts.Amplitude, primaryNoise);
        Matrix<Vector2D> t2 = PerlinNoise.GenerateDisplacementField(image.Size, opts.Amplitude, secondaryNoise);

        var result = new SequentialMatrix<Rgb8>(width, height);

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int nearestBoundaryIndex = band.NearestBoundaryIndices[x, y];
                if ((wetInWet is not null && wetInWet.WetAreaMask[x, y]) ||
                    nearestBoundaryIndex < 0 ||
                    boundaryGroup[nearestBoundaryIndex] == 0)
                {
                    result[x, y] = image[x, y];
                    continue;
                }

                double bandWeight = ScalarMath.OuterBandSmoothstepWeight(band.Distances[x, y], innerRadius, bandRadius);
                double clampedD = ScalarMath.ClampUnitOrDefault(distanceField[x, y].Value);
                double s = ScalarMath.Lerp(opts.SalientAmplitudeScale, 1.0, clampedD) * bandWeight;

                RegionBoundary b = regionGraph.Boundaries[nearestBoundaryIndex];
                int a = b.First;
                int bId = b.Second;

                var basePos = new Vector2D(x, y);
                Vector2D p1 = basePos + (s * t1[x, y]);

                byte group = boundaryGroup[nearestBoundaryIndex];
                if (group == 1)
                {
                    result[x, y] = MatrixResampler.SampleBilinearWhereUnchecked(
                        image,
                        p1.X,
                        p1.Y,
                        (Labels: regionGraph.Labels, First: a, Second: bId),
                        static (cx, cy, state) =>
                        {
                            int lbl = state.Labels[cx, cy];
                            return lbl == state.First || lbl == state.Second;
                        },
                        fallback: image[x, y]);
                }
                else
                {
                    Vector2D p2 = basePos + (s * t2[x, y]);

                    if (opts.PaperGapBias != 0.0)
                    {
                        Vector2D normal = ComputeBoundaryNormalAToB(regionGraph.Labels, band.Distances, x, y, a, bId);
                        Vector2D bias = (opts.PaperGapBias * s * opts.Amplitude) * normal;
                        p1 += bias;
                        p2 -= bias;
                    }

                    int label1 = MatrixResampler.SampleNearestUnchecked(regionGraph.Labels, p1.X, p1.Y);
                    int label2 = MatrixResampler.SampleNearestUnchecked(regionGraph.Labels, p2.X, p2.Y);

                    bool hasA = label1 == a;
                    bool hasB = label2 == bId;

                    if (hasA && hasB)
                    {
                        Rgb64 colA = MatrixResampler.SampleBilinearRgb64WhereUnchecked(image, p1.X, p1.Y, (Labels: regionGraph.Labels, RegionId: a), static (cx, cy, state) => state.Labels[cx, cy] == state.RegionId);
                        Rgb64 colB = MatrixResampler.SampleBilinearRgb64WhereUnchecked(image, p2.X, p2.Y, (Labels: regionGraph.Labels, RegionId: bId), static (cx, cy, state) => state.Labels[cx, cy] == state.RegionId);
                        result[x, y] = PigmentBlend.MixPigmentsUnchecked(colA, colB, opts.OverlapDarkening).ToRgb8();
                    }
                    else if (hasA)
                    {
                        result[x, y] = MatrixResampler.SampleBilinearWhereUnchecked(image, p1.X, p1.Y, (Labels: regionGraph.Labels, RegionId: a), static (cx, cy, state) => state.Labels[cx, cy] == state.RegionId);
                    }
                    else if (hasB)
                    {
                        result[x, y] = MatrixResampler.SampleBilinearWhereUnchecked(image, p2.X, p2.Y, (Labels: regionGraph.Labels, RegionId: bId), static (cx, cy, state) => state.Labels[cx, cy] == state.RegionId);
                    }
                    else if (label1 == bId && label2 == a)
                    {
                        if (opts.PaperGapOpacity >= 1.0)
                        {
                            result[x, y] = paperColor;
                        }
                        else if (opts.PaperGapOpacity <= 0.0)
                        {
                            result[x, y] = image[x, y];
                        }
                        else
                        {
                            result[x, y] = Rgb8.Lerp(image[x, y], paperColor, opts.PaperGapOpacity);
                        }
                    }
                    else
                    {
                        result[x, y] = MatrixResampler.SampleBilinearUnchecked(image, p1.X, p1.Y, EdgeHandling.Extend);
                    }
                }
            }
        });

        return result;
    }

    private static Vector2D ComputeBoundaryNormalAToB(
        IReadOnlyMatrix<int> labels,
        IReadOnlyMatrix<int> distances,
        int x,
        int y,
        int regionA,
        int regionB)
    {
        double sdLeft = SampleSignedDistance(labels, distances, x - 1, y, regionA, regionB);
        double sdRight = SampleSignedDistance(labels, distances, x + 1, y, regionA, regionB);
        double sdUp = SampleSignedDistance(labels, distances, x, y - 1, regionA, regionB);
        double sdDown = SampleSignedDistance(labels, distances, x, y + 1, regionA, regionB);

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
        int regionB)
    {
        int cx = Math.Clamp(x, 0, labels.Width - 1);
        int cy = Math.Clamp(y, 0, labels.Height - 1);
        int lbl = labels[cx, cy];
        if (lbl == regionA)
            return distances[cx, cy] + 0.5;
        if (lbl == regionB)
            return -(distances[cx, cy] + 0.5);
        return 0.0;
    }
}
