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

        if (regionGraph.Size != image.Size)
        {
            throw new ArgumentException("RegionGraph dimensions must match image dimensions.", nameof(regionGraph));
        }

        if (distanceField.Size != image.Size)
        {
            throw new ArgumentException("DistanceField dimensions must match image dimensions.", nameof(distanceField));
        }

        if (wetInWet is not null)
        {
            if (wetInWet.WetAreaMask.Size != image.Size)
            {
                throw new ArgumentException("WetInWet mask dimensions must match image dimensions.", nameof(wetInWet));
            }

            if (wetInWet.QualifyingBoundaries.Count != regionGraph.Boundaries.Count)
            {
                throw new ArgumentException("WetInWet qualifying boundaries count must match region graph boundaries count.", nameof(wetInWet));
            }
        }

        HandTremorOptions opts = options ?? HandTremorOptions.Default;
        opts.Validate();

        int boundaryCount = regionGraph.Boundaries.Count;
        if (image.Count == 0 || opts.Amplitude == 0.0 || boundaryCount == 0)
        {
            return image.Clone();
        }

        PerlinNoiseOptions primaryNoise = opts.PrimaryNoise ?? HandTremorOptions.DefaultPrimaryNoise;
        PerlinNoiseOptions secondaryNoise = opts.SecondaryNoise ?? HandTremorOptions.DefaultSecondaryNoise;
        Rgb8 paperColor = opts.PaperColor ?? new Rgb8(250, 248, 242);

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
                Rgb64 colorA = stats[b.First].MeanColor;
                Rgb64 colorB = stats[b.Second].MeanColor;
                double chromaA = Math.Max(colorA.R, Math.Max(colorA.G, colorA.B)) - Math.Min(colorA.R, Math.Min(colorA.G, colorA.B));
                double chromaB = Math.Max(colorB.R, Math.Max(colorB.G, colorB.B)) - Math.Min(colorB.R, Math.Min(colorB.G, colorB.B));
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

                int dist = band.Distances[x, y];
                double bandWeight = 1.0;
                if (dist > innerRadius)
                {
                    double t = (double)(dist - innerRadius) / (bandRadius - innerRadius + 1);
                    bandWeight = 1.0 - (t * t * (3.0 - (2.0 * t)));
                }

                double d = distanceField[x, y].Value;
                double clampedD = double.IsFinite(d) ? Math.Clamp(d, 0.0, 1.0) : 0.0;
                double s = (opts.SalientAmplitudeScale + ((1.0 - opts.SalientAmplitudeScale) * clampedD)) * bandWeight;

                RegionBoundary b = regionGraph.Boundaries[nearestBoundaryIndex];
                int a = b.First;
                int bId = b.Second;

                double px1 = x + (s * t1[x, y].X);
                double py1 = y + (s * t1[x, y].Y);

                byte group = boundaryGroup[nearestBoundaryIndex];
                if (group == 1)
                {
                    result[x, y] = image.SampleBilinearWhere(
                        px1,
                        py1,
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
                    double px2 = x + (s * t2[x, y].X);
                    double py2 = y + (s * t2[x, y].Y);

                    if (opts.PaperGapBias != 0.0)
                    {
                        (double nx, double ny) = ComputeBoundaryNormalAToB(regionGraph.Labels, band.Distances, x, y, a, bId);
                        double biasOffset = opts.PaperGapBias * s * opts.Amplitude;
                        px1 += biasOffset * nx;
                        py1 += biasOffset * ny;
                        px2 -= biasOffset * nx;
                        py2 -= biasOffset * ny;
                    }

                    int label1 = regionGraph.Labels.SampleNearest(px1, py1);
                    int label2 = regionGraph.Labels.SampleNearest(px2, py2);

                    bool hasA = label1 == a;
                    bool hasB = label2 == bId;

                    if (hasA && hasB)
                    {
                        Rgb64 colA = image.SampleBilinearWhere(px1, py1, (Labels: regionGraph.Labels, RegionId: a), static (cx, cy, state) => state.Labels[cx, cy] == state.RegionId).ToRgb64();
                        Rgb64 colB = image.SampleBilinearWhere(px2, py2, (Labels: regionGraph.Labels, RegionId: bId), static (cx, cy, state) => state.Labels[cx, cy] == state.RegionId).ToRgb64();
                        result[x, y] = PigmentBlend.MixPigments(colA, colB, opts.OverlapDarkening).ToRgb8();
                    }
                    else if (hasA)
                    {
                        result[x, y] = image.SampleBilinearWhere(px1, py1, (Labels: regionGraph.Labels, RegionId: a), static (cx, cy, state) => state.Labels[cx, cy] == state.RegionId);
                    }
                    else if (hasB)
                    {
                        result[x, y] = image.SampleBilinearWhere(px2, py2, (Labels: regionGraph.Labels, RegionId: bId), static (cx, cy, state) => state.Labels[cx, cy] == state.RegionId);
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
                            Rgb8 src = image[x, y];
                            double opacity = opts.PaperGapOpacity;
                            result[x, y] = new Rgb8(
                                (byte)Math.Clamp(Math.Round(src.R + ((paperColor.R - src.R) * opacity)), 0, 255),
                                (byte)Math.Clamp(Math.Round(src.G + ((paperColor.G - src.G) * opacity)), 0, 255),
                                (byte)Math.Clamp(Math.Round(src.B + ((paperColor.B - src.B) * opacity)), 0, 255));
                        }
                    }
                    else
                    {
                        result[x, y] = image.SampleBilinear(px1, py1, EdgeHandling.Extend);
                    }
                }
            }
        });

        return result;
    }

    private static (double NormalX, double NormalY) ComputeBoundaryNormalAToB(
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
        double len = Math.Sqrt((gx * gx) + (gy * gy));
        if (len <= 1e-6)
        {
            return (0.0, 0.0);
        }

        double invLen = 1.0 / len;
        return (-gx * invLen, -gy * invLen);
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
