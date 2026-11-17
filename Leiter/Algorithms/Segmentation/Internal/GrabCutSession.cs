namespace Leiter.Algorithms.Segmentation.Internal;

using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Reusable per-image GrabCut graph and GMM state so multi-round pipelines such as
/// <see cref="SaliencyCut"/> compute pairwise smoothness capacities only once.
/// </summary>
internal sealed class GrabCutSession
{
    private readonly record struct TrimapCounts(
        int ForegroundCount,
        int BackgroundCount,
        int HardBackgroundCount);

    private readonly Size imageSize;
    private readonly int pixelCount;
    private readonly double smoothnessWeight;

    private readonly Rgb8[] pixels;
    private readonly GridMinCutSolver solver;
    private readonly GaussianMixtureModel foregroundGmm;
    private readonly GaussianMixtureModel backgroundGmm;

    private readonly TrimapLabel[] trimapLabels;
    private readonly bool[] isForeground;
    private readonly int[] componentAssignments;
    private readonly double[] sourceCapacities;
    private readonly double[] sinkCapacities;

    public GrabCutSession(IReadOnlyMatrix<Rgb8> image, int mixtureComponentCount, double smoothnessWeight)
    {
        imageSize = image.Size;
        pixelCount = image.Count;
        this.smoothnessWeight = smoothnessWeight;

        pixels = image.ToArray();
        double beta = ComputeContrastParameterBeta(image);

        solver = new GridMinCutSolver(image.Size);
        PopulateSmoothnessEdges(solver, image, smoothnessWeight, beta);

        foregroundGmm = new GaussianMixtureModel(mixtureComponentCount);
        backgroundGmm = new GaussianMixtureModel(mixtureComponentCount);

        trimapLabels = new TrimapLabel[pixelCount];
        isForeground = new bool[pixelCount];
        componentAssignments = new int[pixelCount];
        sourceCapacities = new double[pixelCount];
        sinkCapacities = new double[pixelCount];
    }

    public MaskMatrix Run(IReadOnlyMatrix<TrimapLabel> trimap, int iterations)
    {
        TrimapCounts initialCounts = LoadAndValidateTrimap(trimap);

        if (initialCounts.ForegroundCount == 0)
            return new MaskMatrix(imageSize);

        if (initialCounts.BackgroundCount == 0)
        {
            var allForeground = new MaskMatrix(imageSize);
            allForeground.SetAll(true);
            return allForeground;
        }

        InitializeGmms(initialCounts);

        int fgCount = initialCounts.ForegroundCount;
        int bgCount = initialCounts.BackgroundCount;
        double hardWeight = (9.0 * smoothnessWeight) + 1.0;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            if (iteration > 0)
                RefitGmmsFromSegmentation(fgCount, bgCount);

            ComputeTerminalCapacities(hardWeight);
            solver.Solve(sourceCapacities, sinkCapacities);

            bool changed = UpdateSegmentationFromCut(out fgCount);
            bgCount = pixelCount - fgCount;
            if (!changed || fgCount == 0 || bgCount == 0)
                break;
        }

        return BuildMaskFromState();
    }

    private TrimapCounts LoadAndValidateTrimap(IReadOnlyMatrix<TrimapLabel> trimap)
    {
        int fgCount = 0;
        int bgCount = 0;
        int hardBgCount = 0;
        int width = imageSize.Width;

        for (int y = 0; y < imageSize.Height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                TrimapLabel label = trimap[x, y];
                if (!Enum.IsDefined(label))
                    throw new ArgumentOutOfRangeException(nameof(trimap), label, "Trimap entries must be valid TrimapLabel values.");

                int p = rowBase + x;
                trimapLabels[p] = label;

                bool fg = label is TrimapLabel.Foreground or TrimapLabel.ProbableForeground;
                isForeground[p] = fg;
                if (fg)
                {
                    fgCount++;
                }
                else
                {
                    bgCount++;
                    if (label == TrimapLabel.Background)
                        hardBgCount++;
                }
            }
        }

        return new TrimapCounts(fgCount, bgCount, hardBgCount);
    }

    private void InitializeGmms(TrimapCounts counts)
    {
        var fgIndices = new int[counts.ForegroundCount];
        bool useHardBackgroundOnly = counts.HardBackgroundCount > 0;
        var bgIndices = new int[useHardBackgroundOnly ? counts.HardBackgroundCount : counts.BackgroundCount];
        int fgCursor = 0;
        int bgCursor = 0;

        for (int p = 0; p < pixelCount; p++)
        {
            if (isForeground[p])
                fgIndices[fgCursor++] = p;
            else if (!useHardBackgroundOnly || trimapLabels[p] == TrimapLabel.Background)
                bgIndices[bgCursor++] = p;
        }

        foregroundGmm.InitializeAndFit(pixels, fgIndices);
        backgroundGmm.InitializeAndFit(pixels, bgIndices);
    }

    private void RefitGmmsFromSegmentation(int fgCount, int bgCount)
    {
        Parallel.For(0, pixelCount, p =>
        {
            Rgb8 pixel = pixels[p];
            componentAssignments[p] = isForeground[p]
                ? foregroundGmm.AssignBestComponent(pixel)
                : backgroundGmm.AssignBestComponent(pixel);
        });

        foregroundGmm.FitFromAssignedComponents(pixels, isForeground, targetForeground: true, componentAssignments, fgCount);
        backgroundGmm.FitFromAssignedComponents(pixels, isForeground, targetForeground: false, componentAssignments, bgCount);
    }

    private void ComputeTerminalCapacities(double hardWeight)
    {
        Parallel.For(0, pixelCount, p =>
        {
            TrimapLabel label = trimapLabels[p];
            if (label == TrimapLabel.Foreground)
            {
                sourceCapacities[p] = hardWeight;
                sinkCapacities[p] = 0.0;
                return;
            }

            if (label == TrimapLabel.Background)
            {
                sourceCapacities[p] = 0.0;
                sinkCapacities[p] = hardWeight;
                return;
            }

            Rgb8 pixel = pixels[p];
            double bgNll = backgroundGmm.EvaluateNegativeLogLikelihood(pixel);
            double fgNll = foregroundGmm.EvaluateNegativeLogLikelihood(pixel);
            if (bgNll >= fgNll)
            {
                sourceCapacities[p] = bgNll - fgNll;
                sinkCapacities[p] = 0.0;
            }
            else
            {
                sourceCapacities[p] = 0.0;
                sinkCapacities[p] = fgNll - bgNll;
            }
        });
    }

    private bool UpdateSegmentationFromCut(out int fgCount)
    {
        bool changed = false;
        fgCount = 0;

        for (int p = 0; p < pixelCount; p++)
        {
            bool nextFg = trimapLabels[p] switch
            {
                TrimapLabel.Foreground => true,
                TrimapLabel.Background => false,
                _ => solver.IsSourceSide(p),
            };

            if (nextFg != isForeground[p])
            {
                isForeground[p] = nextFg;
                changed = true;
            }

            if (nextFg)
                fgCount++;
        }

        return changed;
    }

    private MaskMatrix BuildMaskFromState() => new(imageSize, isForeground);

    private static double ComputeContrastParameterBeta(IReadOnlyMatrix<Rgb8> image)
    {
        double sumSquaredDiff = 0.0;
        long pairCount = 0;

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var self = new Coord(x, y);
                Rgb8 selfPixel = image[self];
                foreach (GridNeighbor neighbor in GridNeighborhood.Forward8Connected)
                {
                    Coord other = self + neighbor.Offset;
                    if (!image.Contains(other))
                        continue;

                    sumSquaredDiff += selfPixel.DistanceSquared(image[other]);
                    pairCount++;
                }
            }
        }

        if (pairCount == 0)
            return 0.0;

        double meanDiffSq = sumSquaredDiff / pairCount;
        return meanDiffSq > 1e-12 ? 1.0 / (2.0 * meanDiffSq) : 0.0;
    }

    private static void PopulateSmoothnessEdges(
        GridMinCutSolver solver,
        IReadOnlyMatrix<Rgb8> image,
        double smoothnessWeight,
        double beta)
    {
        Parallel.For(0, image.Height, y =>
        {
            for (int x = 0; x < image.Width; x++)
            {
                var self = new Coord(x, y);
                Rgb8 selfPixel = image[self];
                foreach (GridNeighbor neighbor in GridNeighborhood.Forward8Connected)
                {
                    Coord other = self + neighbor.Offset;
                    if (!image.Contains(other))
                        continue;

                    double diffSq = selfPixel.DistanceSquared(image[other]);
                    double capacity = smoothnessWeight * neighbor.InverseDistance * Math.Exp(-beta * diffSq);
                    solver.SetUndirectedEdgeCapacity(self, neighbor.Direction, (float)capacity);
                }
            }
        });
    }
}
