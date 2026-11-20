namespace Leiter.Algorithms.Segmentation.Internal;

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Reusable per-image GrabCut graph and GMM state so multi-round pipelines such as
/// <see cref="Leiter.Algorithms.Saliency.SaliencyCut"/> compute pairwise smoothness capacities only once.
/// </summary>
internal sealed class GrabCutSession
{
    private const int SmoothnessEdgeCancellationCheckMask = 4095;
    private readonly record struct TrimapCounts(
        int ForegroundCount,
        int BackgroundCount,
        int HardBackgroundCount);

    private readonly record struct ForwardSmoothnessPair(
        int PixelIndex,
        GridDirection Direction,
        double InverseDistance,
        double DistanceSquared);

    private sealed class SmoothnessScanState(Size size, Rgb8[] pixels, ForwardSmoothnessPair[] pairs)
    {
        public readonly Size Size = size;
        public readonly Rgb8[] Pixels = pixels;
        public readonly ForwardSmoothnessPair[] Pairs = pairs;
        public double SumSquaredDiff;
        public int Count;
    }

    private readonly Size imageSize;
    private readonly int pixelCount;
    private readonly double smoothnessWeight;
    private readonly int defaultIterations;

    private readonly Rgb8[] pixels;
    private readonly GridMinCutSolver? solver;
    private readonly GaussianMixtureModel foregroundGmm;
    private readonly GaussianMixtureModel backgroundGmm;

    private readonly TrimapLabel[] trimapLabels;
    private readonly bool[] isForeground;
    private readonly int[] componentAssignments;
    private readonly double[] sourceCapacities;
    private readonly double[] sinkCapacities;

    public GrabCutSession(
        IReadOnlyMatrix<Rgb8> image,
        GrabCutOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        imageSize = image.Size;
        pixelCount = image.Count;
        smoothnessWeight = options.SmoothnessWeight;
        defaultIterations = options.Iterations;

        pixels = image.ToArray();
        if (imageSize.IsEmpty)
        {
            solver = null;
        }
        else
        {
            solver = new GridMinCutSolver(imageSize);
            InitializeSmoothnessEdges(solver, imageSize, pixels, smoothnessWeight, cancellationToken);
        }

        foregroundGmm = new GaussianMixtureModel(options.MixtureComponentCount);
        backgroundGmm = new GaussianMixtureModel(options.MixtureComponentCount);

        trimapLabels = new TrimapLabel[pixelCount];
        isForeground = new bool[pixelCount];
        componentAssignments = new int[pixelCount];
        sourceCapacities = new double[pixelCount];
        sinkCapacities = new double[pixelCount];
    }

    public MaskMatrix Run(IReadOnlyMatrix<TrimapLabel> trimap, CancellationToken cancellationToken = default) =>
        Run(trimap, defaultIterations, cancellationToken);

    public MaskMatrix Run(
        IReadOnlyMatrix<TrimapLabel> trimap,
        int iterations,
        CancellationToken cancellationToken = default)
    {
        TrimapCounts initialCounts = LoadAndValidateTrimap(trimap, cancellationToken);

        if (initialCounts.ForegroundCount == 0 || solver is null)
            return new MaskMatrix(imageSize);

        if (initialCounts.BackgroundCount == 0)
        {
            var allForeground = new MaskMatrix(imageSize);
            allForeground.SetAll(true);
            return allForeground;
        }

        cancellationToken.ThrowIfCancellationRequested();
        InitializeGmms(initialCounts, cancellationToken);

        int fgCount = initialCounts.ForegroundCount;
        int bgCount = initialCounts.BackgroundCount;
        double hardWeight = (9.0 * smoothnessWeight) + 1.0;

        for (int iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (iteration > 0)
                RefitGmmsFromSegmentation(fgCount, bgCount, cancellationToken);

            ComputeTerminalCapacities(hardWeight, cancellationToken);
            solver.Solve(sourceCapacities, sinkCapacities, cancellationToken);

            bool changed = UpdateSegmentationFromCut(solver, out fgCount);
            bgCount = pixelCount - fgCount;
            if (!changed || fgCount == 0 || bgCount == 0)
                break;
        }

        return BuildMaskFromState();
    }

    private TrimapCounts LoadAndValidateTrimap(IReadOnlyMatrix<TrimapLabel> trimap, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trimap);
        Guard.ThrowIfSizeMismatch(imageSize, trimap.Size, nameof(trimap));
        cancellationToken.ThrowIfCancellationRequested();

        int fgCount = 0;
        int bgCount = 0;
        int hardBgCount = 0;
        int width = imageSize.Width;

        for (int y = 0; y < imageSize.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                TrimapLabel label = trimap[x, y];
                Guard.ThrowIfUndefinedEnum(label, nameof(trimap));

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

    private void InitializeGmms(TrimapCounts counts, CancellationToken cancellationToken)
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

        cancellationToken.ThrowIfCancellationRequested();
        foregroundGmm.InitializeAndFit(pixels, fgIndices);
        cancellationToken.ThrowIfCancellationRequested();
        backgroundGmm.InitializeAndFit(pixels, bgIndices);
    }

    private void RefitGmmsFromSegmentation(int fgCount, int bgCount, CancellationToken cancellationToken)
    {
        void AssignPixel(int p)
        {
            Rgb8 pixel = pixels[p];
            componentAssignments[p] = isForeground[p]
                ? foregroundGmm.AssignBestComponent(pixel)
                : backgroundGmm.AssignBestComponent(pixel);
        }

        if (cancellationToken.CanBeCanceled)
        {
            Parallel.For(0, pixelCount, new ParallelOptions { CancellationToken = cancellationToken }, AssignPixel);
        }
        else
        {
            Parallel.For(0, pixelCount, AssignPixel);
        }

        cancellationToken.ThrowIfCancellationRequested();
        foregroundGmm.FitFromAssignedComponents(pixels, isForeground, targetForeground: true, componentAssignments, fgCount);
        cancellationToken.ThrowIfCancellationRequested();
        backgroundGmm.FitFromAssignedComponents(pixels, isForeground, targetForeground: false, componentAssignments, bgCount);
    }

    private void ComputeTerminalCapacities(double hardWeight, CancellationToken cancellationToken)
    {
        void ComputePixel(int p)
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
        }

        if (cancellationToken.CanBeCanceled)
        {
            Parallel.For(0, pixelCount, new ParallelOptions { CancellationToken = cancellationToken }, ComputePixel);
        }
        else
        {
            Parallel.For(0, pixelCount, ComputePixel);
        }
    }

    private bool UpdateSegmentationFromCut(GridMinCutSolver minCutSolver, out int fgCount)
    {
        bool changed = false;
        fgCount = 0;

        for (int p = 0; p < pixelCount; p++)
        {
            bool nextFg = trimapLabels[p] switch
            {
                TrimapLabel.Foreground => true,
                TrimapLabel.Background => false,
                _ => minCutSolver.IsSourceSide(p),
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

    private static void InitializeSmoothnessEdges(
        GridMinCutSolver solver,
        Size imageSize,
        Rgb8[] pixels,
        double smoothnessWeight,
        CancellationToken cancellationToken)
    {
        if (smoothnessWeight == 0.0 || imageSize.IsEmpty)
            return;

        int pairCapacity =
            ((imageSize.Width - 1) * imageSize.Height) +
            (imageSize.Width * (imageSize.Height - 1)) +
            (2 * (imageSize.Width - 1) * (imageSize.Height - 1));
        if (pairCapacity <= 0)
            return;

        var state = new SmoothnessScanState(imageSize, pixels, new ForwardSmoothnessPair[pairCapacity]);
        for (int y = 0; y < imageSize.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GridNeighborhood.ForEachForwardPairInRow(
                imageSize,
                y,
                GridConnectivity.EightConnected,
                state,
                static (first, second, neighbor, s) =>
                {
                    int firstIndex = s.Size.IndexOf(first);
                    int secondIndex = s.Size.IndexOf(second);
                    double diffSq = s.Pixels[firstIndex].DistanceSquared(s.Pixels[secondIndex]);
                    s.SumSquaredDiff += diffSq;
                    s.Pairs[s.Count++] = new ForwardSmoothnessPair(
                        firstIndex,
                        neighbor.Direction,
                        neighbor.InverseDistance,
                        diffSq);
                });
        }

        Debug.Assert(state.Count == pairCapacity);

        double meanDiffSq = state.SumSquaredDiff / state.Count;
        double beta = meanDiffSq > 1e-12 ? 1.0 / (2.0 * meanDiffSq) : 0.0;

        ForwardSmoothnessPair[] pairs = state.Pairs;
        for (int i = 0; i < state.Count; i++)
        {
            if ((i & SmoothnessEdgeCancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            ref readonly ForwardSmoothnessPair pair = ref pairs[i];
            double capacity = smoothnessWeight * pair.InverseDistance * Math.Exp(-beta * pair.DistanceSquared);
            solver.SetUndirectedEdgeCapacity(pair.PixelIndex, (int)pair.Direction, (float)capacity);
        }
    }
}
