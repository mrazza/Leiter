namespace Leiter.Algorithms.Segmentation.Internal;

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
    private const double DiagonalDistanceFactor = 0.7071067811865475; // 1 / sqrt(2)

    private readonly int width;
    private readonly int height;
    private readonly int pixelCount;
    private readonly double smoothnessWeight;

    private readonly float[] interleavedRgb;
    private readonly GridMinCutSolver solver;
    private readonly GaussianMixtureModel foregroundGmm;
    private readonly GaussianMixtureModel backgroundGmm;

    private readonly TrimapLabel[] trimapLabels;
    private readonly byte[] currentForeground;
    private readonly int[] componentAssignments;
    private readonly double[] sourceCapacities;
    private readonly double[] sinkCapacities;

    public GrabCutSession(IReadOnlyMatrix<Rgb8> image, int mixtureComponentCount, double smoothnessWeight)
    {
        width = image.Width;
        height = image.Height;
        pixelCount = image.Count;
        this.smoothnessWeight = smoothnessWeight;

        interleavedRgb = ExtractRgbArray(image, width, height);
        double beta = ComputeContrastParameterBeta(interleavedRgb, width, height);

        solver = new GridMinCutSolver(width, height);
        PopulateSmoothnessEdges(solver, interleavedRgb, width, height, smoothnessWeight, beta);

        foregroundGmm = new GaussianMixtureModel(mixtureComponentCount);
        backgroundGmm = new GaussianMixtureModel(mixtureComponentCount);

        trimapLabels = new TrimapLabel[pixelCount];
        currentForeground = new byte[pixelCount];
        componentAssignments = new int[pixelCount];
        sourceCapacities = new double[pixelCount];
        sinkCapacities = new double[pixelCount];
    }

    public MaskMatrix Run(IReadOnlyMatrix<TrimapLabel> trimap, int iterations)
    {
        int initialFgCount = 0;
        int initialBgCount = 0;
        int hardBgCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                TrimapLabel label = trimap[x, y];
                if ((byte)label > (byte)TrimapLabel.ProbableForeground)
                    throw new ArgumentOutOfRangeException(nameof(trimap), label, "Trimap entries must be valid TrimapLabel values.");

                int p = (y * width) + x;
                trimapLabels[p] = label;

                bool isForeground = label is TrimapLabel.Foreground or TrimapLabel.ProbableForeground;
                currentForeground[p] = isForeground ? (byte)1 : (byte)0;
                if (isForeground)
                {
                    initialFgCount++;
                }
                else
                {
                    initialBgCount++;
                    if (label == TrimapLabel.Background)
                        hardBgCount++;
                }
            }
        }

        if (initialFgCount == 0)
            return new MaskMatrix(width, height);

        if (initialBgCount == 0)
        {
            var allForeground = new MaskMatrix(width, height);
            allForeground.SetAll(true);
            return allForeground;
        }

        InitializeGmms(initialFgCount, initialBgCount, hardBgCount);

        int fgCount = initialFgCount;
        int bgCount = initialBgCount;
        double hardWeight = (9.0 * smoothnessWeight) + 1.0;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            if (iteration > 0)
            {
                Parallel.For(0, pixelCount, p =>
                {
                    int rgbOffset = p * 3;
                    float r = interleavedRgb[rgbOffset + 0];
                    float g = interleavedRgb[rgbOffset + 1];
                    float b = interleavedRgb[rgbOffset + 2];

                    componentAssignments[p] = currentForeground[p] != 0
                        ? foregroundGmm.AssignBestComponent(r, g, b)
                        : backgroundGmm.AssignBestComponent(r, g, b);
                });

                foregroundGmm.FitFromAssignedComponents(interleavedRgb, currentForeground, 1, componentAssignments, fgCount);
                backgroundGmm.FitFromAssignedComponents(interleavedRgb, currentForeground, 0, componentAssignments, bgCount);
            }

            Parallel.For(0, pixelCount, p =>
            {
                TrimapLabel label = trimapLabels[p];
                if (label == TrimapLabel.Foreground)
                {
                    sourceCapacities[p] = hardWeight;
                    sinkCapacities[p] = 0.0;
                }
                else if (label == TrimapLabel.Background)
                {
                    sourceCapacities[p] = 0.0;
                    sinkCapacities[p] = hardWeight;
                }
                else
                {
                    int rgbOffset = p * 3;
                    float r = interleavedRgb[rgbOffset + 0];
                    float g = interleavedRgb[rgbOffset + 1];
                    float b = interleavedRgb[rgbOffset + 2];

                    double bgNll = backgroundGmm.EvaluateNegativeLogLikelihood(r, g, b);
                    double fgNll = foregroundGmm.EvaluateNegativeLogLikelihood(r, g, b);
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
                }
            });

            solver.Solve(sourceCapacities, sinkCapacities);

            bool changed = false;
            fgCount = 0;

            for (int p = 0; p < pixelCount; p++)
            {
                byte nextFg = trimapLabels[p] switch
                {
                    TrimapLabel.Foreground => 1,
                    TrimapLabel.Background => 0,
                    _ => solver.IsSourceSide(p) ? (byte)1 : (byte)0,
                };

                if (nextFg != currentForeground[p])
                {
                    currentForeground[p] = nextFg;
                    changed = true;
                }

                if (nextFg != 0)
                    fgCount++;
            }

            bgCount = pixelCount - fgCount;
            if (!changed || fgCount == 0 || bgCount == 0)
                break;
        }

        return BuildMaskFromState();
    }

    private void InitializeGmms(int initialFgCount, int initialBgCount, int hardBgCount)
    {
        var fgIndices = new int[initialFgCount];
        bool useHardBackgroundOnly = hardBgCount > 0;
        var bgIndices = new int[useHardBackgroundOnly ? hardBgCount : initialBgCount];
        int fgCursor = 0;
        int bgCursor = 0;

        for (int p = 0; p < pixelCount; p++)
        {
            if (currentForeground[p] != 0)
                fgIndices[fgCursor++] = p;
            else if (!useHardBackgroundOnly || trimapLabels[p] == TrimapLabel.Background)
                bgIndices[bgCursor++] = p;
        }

        foregroundGmm.InitializeAndFit(interleavedRgb, fgIndices);
        backgroundGmm.InitializeAndFit(interleavedRgb, bgIndices);
    }

    private MaskMatrix BuildMaskFromState()
    {
        var result = new MaskMatrix(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                result[x, y] = currentForeground[(y * width) + x] != 0;
        }
        return result;
    }

    private static float[] ExtractRgbArray(IReadOnlyMatrix<Rgb8> image, int width, int height)
    {
        var rgb = new float[width * height * 3];
        Parallel.For(0, height, y =>
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                Rgb8 pixel = image[x, y];
                int offset = (rowBase + x) * 3;
                rgb[offset + 0] = pixel.R;
                rgb[offset + 1] = pixel.G;
                rgb[offset + 2] = pixel.B;
            }
        });
        return rgb;
    }

    private static double ComputeContrastParameterBeta(float[] rgb, int width, int height)
    {
        double sumSquaredDiff = 0.0;
        long pairCount = 0;

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int p = rowBase + x;
                if (x + 1 < width)
                {
                    sumSquaredDiff += ColorDistanceSquared(rgb, p, p + 1);
                    pairCount++;
                }

                if (y + 1 < height)
                {
                    int below = p + width;
                    sumSquaredDiff += ColorDistanceSquared(rgb, p, below);
                    pairCount++;

                    if (x + 1 < width)
                    {
                        sumSquaredDiff += ColorDistanceSquared(rgb, p, below + 1);
                        pairCount++;
                    }

                    if (x - 1 >= 0)
                    {
                        sumSquaredDiff += ColorDistanceSquared(rgb, p, below - 1);
                        pairCount++;
                    }
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
        float[] rgb,
        int width,
        int height,
        double smoothnessWeight,
        double beta)
    {
        double diagonalWeight = smoothnessWeight * DiagonalDistanceFactor;

        Parallel.For(0, height, y =>
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int p = rowBase + x;
                if (x + 1 < width)
                {
                    double diffSq = ColorDistanceSquared(rgb, p, p + 1);
                    solver.SetUndirectedEdgeCapacity(p, 0, (float)(smoothnessWeight * Math.Exp(-beta * diffSq)));
                }

                if (y + 1 < height)
                {
                    int below = p + width;
                    double verticalDiffSq = ColorDistanceSquared(rgb, p, below);
                    solver.SetUndirectedEdgeCapacity(p, 1, (float)(smoothnessWeight * Math.Exp(-beta * verticalDiffSq)));

                    if (x + 1 < width)
                    {
                        double downRightDiffSq = ColorDistanceSquared(rgb, p, below + 1);
                        solver.SetUndirectedEdgeCapacity(p, 2, (float)(diagonalWeight * Math.Exp(-beta * downRightDiffSq)));
                    }

                    if (x - 1 >= 0)
                    {
                        double downLeftDiffSq = ColorDistanceSquared(rgb, p, below - 1);
                        solver.SetUndirectedEdgeCapacity(p, 3, (float)(diagonalWeight * Math.Exp(-beta * downLeftDiffSq)));
                    }
                }
            }
        });
    }

    private static double ColorDistanceSquared(float[] rgb, int pixelA, int pixelB)
    {
        int offsetA = pixelA * 3;
        int offsetB = pixelB * 3;
        double dr = rgb[offsetA + 0] - rgb[offsetB + 0];
        double dg = rgb[offsetA + 1] - rgb[offsetB + 1];
        double db = rgb[offsetA + 2] - rgb[offsetB + 2];
        return (dr * dr) + (dg * dg) + (db * db);
    }
}
