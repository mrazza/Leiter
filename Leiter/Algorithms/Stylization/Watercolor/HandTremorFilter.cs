namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
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
    /// <param name="salientMask">Optional boolean salient mask; used when computing region color statistics.</param>
    /// <param name="options">Optional hand tremor configuration options.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the tremor-perturbed watercolor image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="regionGraph"/>, or <paramref name="distanceField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when dimensions of <paramref name="regionGraph"/>, <paramref name="distanceField"/>, <paramref name="salientMask"/>, or <paramref name="wetInWet"/> do not match <paramref name="image"/>, or boundary counts do not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> parameters are invalid.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        RegionGraph regionGraph,
        IReadOnlyMatrix<DoublePixel> distanceField,
        WetInWetResult? wetInWet = null,
        IReadOnlyMatrix<bool>? salientMask = null,
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

        if (salientMask is not null && salientMask.Size != image.Size)
        {
            throw new ArgumentException("SalientMask dimensions must match image dimensions.", nameof(salientMask));
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

        HandTremorOptions opts = options ?? new HandTremorOptions();
        opts.Validate();

        int boundaryCount = regionGraph.Boundaries.Count;
        if (image.Count == 0 || opts.Amplitude == 0.0 || boundaryCount == 0)
        {
            return image.Clone();
        }

        PerlinNoiseOptions primaryNoise = opts.PrimaryNoise ?? new PerlinNoiseOptions(Seed: 101, Frequency: 0.04, Octaves: 3);
        PerlinNoiseOptions secondaryNoise = opts.SecondaryNoise ?? new PerlinNoiseOptions(Seed: 202, Frequency: 0.04, Octaves: 3);
        Rgb8 paperColor = opts.PaperColor ?? new Rgb8(250, 248, 242);

        RegionColorStatistics[] stats = RegionColorStatistics.Compute(regionGraph, image, salientMask);

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
        int totalPixels = width * height;
        int bandRadius = (int)Math.Ceiling(opts.Amplitude) + 2;

        int[] nearestDist = new int[totalPixels];
        Array.Fill(nearestDist, int.MaxValue);

        int[] nearestBoundary = new int[totalPixels];
        Array.Fill(nearestBoundary, -1);

        var queue = new Queue<int>();

        for (int bi = 0; bi < boundaryCount; bi++)
        {
            RegionBoundary b = regionGraph.Boundaries[bi];
            EnqueueBoundaryPixels(b.FirstBorderPixels.Span, bi, width, nearestDist, nearestBoundary, queue);
            EnqueueBoundaryPixels(b.SecondBorderPixels.Span, bi, width, nearestDist, nearestBoundary, queue);
        }

        while (queue.Count > 0)
        {
            int curIdx = queue.Dequeue();
            int curDist = nearestDist[curIdx];
            if (curDist >= bandRadius)
            {
                continue;
            }

            int curBoundary = nearestBoundary[curIdx];
            int curX = curIdx % width;
            int curY = curIdx / width;

            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = curY + dy;
                if (ny < 0 || ny >= height)
                {
                    continue;
                }

                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }

                    int nx = curX + dx;
                    if (nx < 0 || nx >= width)
                    {
                        continue;
                    }

                    int nIdx = (ny * width) + nx;
                    int nDist = curDist + 1;

                    if (nDist < nearestDist[nIdx])
                    {
                        nearestDist[nIdx] = nDist;
                        nearestBoundary[nIdx] = curBoundary;
                        queue.Enqueue(nIdx);
                    }
                }
            }
        }

        Matrix<Vector2D> t1 = PerlinNoise.GenerateDisplacementField(image.Size, opts.Amplitude, primaryNoise);
        Matrix<Vector2D> t2 = PerlinNoise.GenerateDisplacementField(image.Size, opts.Amplitude, secondaryNoise);

        var result = new SequentialMatrix<Rgb8>(width, height);

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int idx = (y * width) + x;
                if ((wetInWet is not null && wetInWet.WetAreaMask[x, y]) ||
                    nearestBoundary[idx] < 0 ||
                    boundaryGroup[nearestBoundary[idx]] == 0)
                {
                    result[x, y] = image[x, y];
                    continue;
                }

                double d = distanceField[x, y].Value;
                double clampedD = double.IsFinite(d) ? Math.Clamp(d, 0.0, 1.0) : 0.0;
                double s = opts.SalientAmplitudeScale + ((1.0 - opts.SalientAmplitudeScale) * clampedD);

                byte group = boundaryGroup[nearestBoundary[idx]];
                if (group == 1)
                {
                    result[x, y] = image.SampleBilinear(x + (s * t1[x, y].X), y + (s * t1[x, y].Y), EdgeHandling.EXTEND);
                }
                else
                {
                    RegionBoundary b = regionGraph.Boundaries[nearestBoundary[idx]];
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
                        Rgb64 colA = SampleSameRegionBilinear(image, regionGraph.Labels, px1, py1, a).ToRgb64();
                        Rgb64 colB = SampleSameRegionBilinear(image, regionGraph.Labels, px2, py2, bId).ToRgb64();
                        result[x, y] = PigmentBlend.MixPigments(colA, colB, opts.OverlapDarkening).ToRgb8();
                    }
                    else if (hasA)
                    {
                        result[x, y] = SampleSameRegionBilinear(image, regionGraph.Labels, px1, py1, a);
                    }
                    else if (hasB)
                    {
                        result[x, y] = SampleSameRegionBilinear(image, regionGraph.Labels, px2, py2, bId);
                    }
                    else if (label1 == bId || label2 == a)
                    {
                        result[x, y] = paperColor;
                    }
                    else
                    {
                        result[x, y] = image.SampleBilinear(px1, py1, EdgeHandling.EXTEND);
                    }
                }
            }
        });

        return result;
    }

    private static Rgb8 SampleSameRegionBilinear(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<int> labels,
        double px,
        double py,
        int targetRegionId)
    {
        double cx = Math.Clamp(px, 0.0, image.Width - 1.0);
        double cy = Math.Clamp(py, 0.0, image.Height - 1.0);

        int x0 = (int)Math.Floor(cx);
        int x1 = Math.Min(image.Width - 1, x0 + 1);
        double wx = cx - x0;

        int y0 = (int)Math.Floor(cy);
        int y1 = Math.Min(image.Height - 1, y0 + 1);
        double wy = cy - y0;

        double w00 = (1.0 - wx) * (1.0 - wy);
        double w10 = wx * (1.0 - wy);
        double w01 = (1.0 - wx) * wy;
        double w11 = wx * wy;

        double sumR = 0.0;
        double sumG = 0.0;
        double sumB = 0.0;
        double weightSum = 0.0;

        if (labels[x0, y0] == targetRegionId)
        {
            Rgb8 p = image[x0, y0];
            sumR += p.R * w00;
            sumG += p.G * w00;
            sumB += p.B * w00;
            weightSum += w00;
        }

        if (labels[x1, y0] == targetRegionId)
        {
            Rgb8 p = image[x1, y0];
            sumR += p.R * w10;
            sumG += p.G * w10;
            sumB += p.B * w10;
            weightSum += w10;
        }

        if (labels[x0, y1] == targetRegionId)
        {
            Rgb8 p = image[x0, y1];
            sumR += p.R * w01;
            sumG += p.G * w01;
            sumB += p.B * w01;
            weightSum += w01;
        }

        if (labels[x1, y1] == targetRegionId)
        {
            Rgb8 p = image[x1, y1];
            sumR += p.R * w11;
            sumG += p.G * w11;
            sumB += p.B * w11;
            weightSum += w11;
        }

        double invWeight = 1.0 / weightSum;
        return new Rgb8(
            Rgb8.ClampRoundedByte(sumR * invWeight),
            Rgb8.ClampRoundedByte(sumG * invWeight),
            Rgb8.ClampRoundedByte(sumB * invWeight));
    }

    private static void EnqueueBoundaryPixels(
        ReadOnlySpan<Coord> borderPixels,
        int boundaryIndex,
        int width,
        int[] nearestDist,
        int[] nearestBoundary,
        Queue<int> queue)
    {
        for (int i = 0; i < borderPixels.Length; i++)
        {
            Coord p = borderPixels[i];
            int idx = (p.Y * width) + p.X;
            if (nearestDist[idx] > 0)
            {
                nearestDist[idx] = 0;
                nearestBoundary[idx] = boundaryIndex;
                queue.Enqueue(idx);
            }
        }
    }
}
