namespace Leiter.Algorithms.Segmentation.Internal;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Reusable per-image GrabCut graph and GMM state so multi-round pipelines such as
/// <see cref="Leiter.Algorithms.Saliency.SaliencyCut"/> compute pairwise smoothness capacities only once.
/// </summary>
internal sealed class GrabCutSession
{
    private readonly record struct TrimapCounts(
        int ForegroundCount,
        int BackgroundCount,
        int HardBackgroundCount);

    private readonly record struct TrimapChunkResult(
        int ForegroundCount,
        int BackgroundCount,
        int HardBackgroundCount,
        TrimapLabel InvalidLabel,
        bool HasInvalidLabel);

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
            solver.SolveArrays(sourceCapacities, sinkCapacities, cancellationToken);

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

        int width = imageSize.Width;
        TrimapLabel[]? rawTrimap = trimap.TryGetRawArray();

        TrimapChunkResult[] chunks = ParallelRunner.MapRows(
            imageSize,
            cancellationToken,
            (startRow, endRow) =>
            {
                int localFg = 0;
                int localBg = 0;
                int localHardBg = 0;

                for (int y = startRow; y < endRow; y++)
                {
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int p = rowBase + x;
                        TrimapLabel label = rawTrimap is not null ? rawTrimap[p] : trimap[x, y];
                        if (!Enum.IsDefined(label))
                            return new TrimapChunkResult(0, 0, 0, label, HasInvalidLabel: true);

                        trimapLabels[p] = label;

                        bool fg = label is TrimapLabel.Foreground or TrimapLabel.ProbableForeground;
                        isForeground[p] = fg;
                        if (fg)
                        {
                            localFg++;
                        }
                        else
                        {
                            localBg++;
                            if (label == TrimapLabel.Background)
                                localHardBg++;
                        }
                    }
                }

                return new TrimapChunkResult(localFg, localBg, localHardBg, default, HasInvalidLabel: false);
            },
            minHeight: 2);

        int fgCount = 0;
        int bgCount = 0;
        int hardBgCount = 0;
        for (int i = 0; i < chunks.Length; i++)
        {
            TrimapChunkResult chunk = chunks[i];
            if (chunk.HasInvalidLabel)
                Guard.ThrowIfUndefinedEnum(chunk.InvalidLabel, nameof(trimap));

            fgCount += chunk.ForegroundCount;
            bgCount += chunk.BackgroundCount;
            hardBgCount += chunk.HardBackgroundCount;
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
        if (pixelCount >= ParallelRunner.DefaultMinParallelCount)
        {
            try
            {
                Parallel.Invoke(
                    new ParallelOptions { CancellationToken = cancellationToken },
                    () => foregroundGmm.InitializeAndFit(pixels, fgIndices, cancellationToken),
                    () => backgroundGmm.InitializeAndFit(pixels, bgIndices, cancellationToken));
            }
            catch (AggregateException ex) when (ex.InnerExceptions.All(static inner => inner is OperationCanceledException))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
        else
        {
            foregroundGmm.InitializeAndFit(pixels, fgIndices, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            backgroundGmm.InitializeAndFit(pixels, bgIndices, cancellationToken);
        }
    }

    private void RefitGmmsFromSegmentation(int fgCount, int bgCount, CancellationToken cancellationToken)
    {
        ParallelRunner.ForChunks(
            pixelCount,
            cancellationToken,
            (start, end) =>
            {
                for (int p = start; p < end; p++)
                {
                    Rgb8 pixel = pixels[p];
                    componentAssignments[p] = isForeground[p]
                        ? foregroundGmm.AssignBestComponent(pixel)
                        : backgroundGmm.AssignBestComponent(pixel);
                }
            });

        cancellationToken.ThrowIfCancellationRequested();
        foregroundGmm.FitFromAssignedComponents(pixels, isForeground, targetForeground: true, componentAssignments, fgCount, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        backgroundGmm.FitFromAssignedComponents(pixels, isForeground, targetForeground: false, componentAssignments, bgCount, cancellationToken);
    }

    private void ComputeTerminalCapacities(double hardWeight, CancellationToken cancellationToken)
    {
        ParallelRunner.ForChunks(
            pixelCount,
            cancellationToken,
            (start, end) =>
            {
                for (int p = start; p < end; p++)
                {
                    TrimapLabel label = trimapLabels[p];
                    if (label == TrimapLabel.Foreground)
                    {
                        sourceCapacities[p] = hardWeight;
                        sinkCapacities[p] = 0.0;
                        continue;
                    }

                    if (label == TrimapLabel.Background)
                    {
                        sourceCapacities[p] = 0.0;
                        sinkCapacities[p] = hardWeight;
                        continue;
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
            });
    }

    private bool UpdateSegmentationFromCut(GridMinCutSolver minCutSolver, out int fgCount)
    {
        (int FgCount, bool Changed)[] chunks = ParallelRunner.MapChunks(
            pixelCount,
            CancellationToken.None,
            (start, end) =>
            {
                int localFg = 0;
                bool localChanged = false;

                for (int p = start; p < end; p++)
                {
                    bool nextFg = trimapLabels[p] switch
                    {
                        TrimapLabel.Foreground => true,
                        TrimapLabel.Background => false,
                        _ => minCutSolver.IsSourceSideUnchecked(p),
                    };

                    if (nextFg != isForeground[p])
                    {
                        isForeground[p] = nextFg;
                        localChanged = true;
                    }

                    if (nextFg)
                        localFg++;
                }

                return (localFg, localChanged);
            });

        int totalFg = 0;
        bool changed = false;
        for (int i = 0; i < chunks.Length; i++)
        {
            totalFg += chunks[i].FgCount;
            changed |= chunks[i].Changed;
        }

        fgCount = totalFg;
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

        int height = imageSize.Height;

        // Pass 1: Deterministically accumulate total forward squared color differences to compute beta.
        (double SumSquaredDiff, int Count)[] chunkSums = ParallelRunner.MapRows(
            imageSize,
            cancellationToken,
            (startRow, endRow) => SumForwardSquaredDifferences(imageSize, pixels, startRow, endRow),
            minHeight: 2);

        double sumSquaredDiff = 0.0;
        int pairCount = 0;
        for (int i = 0; i < chunkSums.Length; i++)
        {
            sumSquaredDiff += chunkSums[i].SumSquaredDiff;
            pairCount += chunkSums[i].Count;
        }

        if (pairCount <= 0)
            return;

        double meanDiffSq = sumSquaredDiff / pairCount;
        double beta = meanDiffSq > 1e-12 ? 1.0 / (2.0 * meanDiffSq) : 0.0;

        // Pass 2: Even-row then odd-row parity sweeps ensure forward pairs from distinct rows write disjoint memory.
        if (pixels.Length < ParallelRunner.DefaultMinParallelCount || height < 2)
        {
            for (int y = 0; y < height; y++)
                SetSmoothnessEdgesForRow(solver, imageSize, pixels, y, smoothnessWeight, beta);
            return;
        }

        int evenRowCount = (height + 1) / 2;
        int oddRowCount = height / 2;
        ParallelRunner.For(evenRowCount, cancellationToken, i =>
            SetSmoothnessEdgesForRow(solver, imageSize, pixels, i * 2, smoothnessWeight, beta));
        ParallelRunner.For(oddRowCount, cancellationToken, i =>
            SetSmoothnessEdgesForRow(solver, imageSize, pixels, (i * 2) + 1, smoothnessWeight, beta));
    }

    private static (double SumSquaredDiff, int Count) SumForwardSquaredDifferences(
        Size imageSize,
        Rgb8[] pixels,
        int startRow,
        int endRow)
    {
        int width = imageSize.Width;
        int height = imageSize.Height;
        double sum = 0.0;
        int count = 0;

        for (int y = startRow; y < endRow; y++)
        {
            int rowBase = y * width;
            bool hasSouth = y + 1 < height;

            for (int x = 0; x < width; x++)
            {
                int p = rowBase + x;
                Rgb8 center = pixels[p];
                bool hasEast = x + 1 < width;

                if (hasEast)
                {
                    sum += center.DistanceSquared(pixels[p + 1]);
                    count++;
                }

                if (hasSouth)
                {
                    int south = p + width;
                    sum += center.DistanceSquared(pixels[south]);
                    count++;

                    if (hasEast)
                    {
                        sum += center.DistanceSquared(pixels[south + 1]);
                        count++;
                    }

                    if (x > 0)
                    {
                        sum += center.DistanceSquared(pixels[south - 1]);
                        count++;
                    }
                }
            }
        }

        return (sum, count);
    }

    private static void SetSmoothnessEdgesForRow(
        GridMinCutSolver solver,
        Size imageSize,
        Rgb8[] pixels,
        int y,
        double smoothnessWeight,
        double beta)
    {
        int width = imageSize.Width;
        int rowBase = y * width;
        bool hasSouth = y + 1 < imageSize.Height;
        double diagScale = smoothnessWeight * ScalarMath.InvSqrt2;

        for (int x = 0; x < width; x++)
        {
            int p = rowBase + x;
            Rgb8 center = pixels[p];
            bool hasEast = x + 1 < width;

            if (hasEast)
            {
                double diffSq = center.DistanceSquared(pixels[p + 1]);
                float cap = (float)(smoothnessWeight * Math.Exp(-beta * diffSq));
                solver.SetUndirectedEdgeCapacityUnchecked(p, (int)GridDirection.East, cap);
            }

            if (hasSouth)
            {
                int south = p + width;
                double southDiffSq = center.DistanceSquared(pixels[south]);
                float southCap = (float)(smoothnessWeight * Math.Exp(-beta * southDiffSq));
                solver.SetUndirectedEdgeCapacityUnchecked(p, (int)GridDirection.South, southCap);

                if (hasEast)
                {
                    double seDiffSq = center.DistanceSquared(pixels[south + 1]);
                    float seCap = (float)(diagScale * Math.Exp(-beta * seDiffSq));
                    solver.SetUndirectedEdgeCapacityUnchecked(p, (int)GridDirection.SouthEast, seCap);
                }

                if (x > 0)
                {
                    double swDiffSq = center.DistanceSquared(pixels[south - 1]);
                    float swCap = (float)(diagScale * Math.Exp(-beta * swDiffSq));
                    solver.SetUndirectedEdgeCapacityUnchecked(p, (int)GridDirection.SouthWest, swCap);
                }
            }
        }
    }
}
