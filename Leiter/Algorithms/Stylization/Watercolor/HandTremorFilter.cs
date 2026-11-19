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
/// Implements hand tremor edge perturbation for watercolor stylization (Bousseau et al. 2006).
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
                double hueDiff = ColorMetrics.HueDifferenceDegrees(stats[b.First].MeanHueDegrees, stats[b.Second].MeanHueDegrees);
                boundaryGroup[bi] = hueDiff < opts.SimilarHueThresholdDegrees ? (byte)1 : (byte)2;
            }
        }

        int width = image.Width;
        int height = image.Height;
        int bandRadius = (int)Math.Ceiling(opts.Amplitude) + 2;

        RegionBoundaryBand band = regionGraph.ComputeBoundaryBand(bandRadius);

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

                double d = distanceField[x, y].Value;
                double clampedD = double.IsFinite(d) ? Math.Clamp(d, 0.0, 1.0) : 0.0;
                double s = opts.SalientAmplitudeScale + ((1.0 - opts.SalientAmplitudeScale) * clampedD);

                byte group = boundaryGroup[nearestBoundaryIndex];
                if (group == 1)
                {
                    result[x, y] = image.SampleBilinear(x + (s * t1[x, y].X), y + (s * t1[x, y].Y), EdgeHandling.Extend);
                }
                else
                {
                    RegionBoundary b = regionGraph.Boundaries[nearestBoundaryIndex];
                    int a = b.First;
                    int bId = b.Second;

                    double px1 = x + (s * t1[x, y].X);
                    double py1 = y + (s * t1[x, y].Y);
                    double px2 = x + (s * t2[x, y].X);
                    double py2 = y + (s * t2[x, y].Y);

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
                    else if (label1 == bId || label2 == a)
                    {
                        result[x, y] = paperColor;
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
}
