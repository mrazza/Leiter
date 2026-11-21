namespace Leiter.Algorithms.Segmentation.Internal;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Core;

/// <summary>
/// Solves 8-connected 2D grid s-t min-cut / max-flow instances using the Boykov–Kolmogorov algorithm.
/// </summary>
internal sealed class GridMinCutSolver
{
    private enum SearchTree : byte
    {
        Free = 0,
        Source = 1,
        Sink = 2,
    }

    private sealed class BandSearchContext(
        int startRow,
        int endRow,
        int width,
        byte[] validDirectionMasks,
        bool[] isActive,
        int initialTimestamp)
    {
        private readonly int topRowEndPixel = (startRow * width) + width;
        private readonly int bottomRowStartPixel = (endRow * width) - width;
        private readonly Queue<int> activeQueue = new();

        public int StartRow { get; } = startRow;
        public int EndRow { get; } = endRow;
        public int StartPixel { get; } = startRow * width;
        public int EndPixel { get; } = endRow * width;
        public Queue<int> OrphanQueue { get; } = new();
        public int CurrentTimestamp { get; set; } = initialTimestamp;
        public bool HasActiveNodes => activeQueue.Count > 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte GetValidDirectionMask(int pixelIndex)
        {
            byte mask = validDirectionMasks[pixelIndex];
            if (pixelIndex < topRowEndPixel)
                mask &= unchecked((byte)~NorthDirectionsMask);
            if (pixelIndex >= bottomRowStartPixel)
                mask &= unchecked((byte)~SouthDirectionsMask);
            return mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int PeekActive() => activeQueue.Peek();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryEnqueueActive(int pixelIndex)
        {
            Debug.Assert(pixelIndex >= StartPixel && pixelIndex < EndPixel);
            if (isActive[pixelIndex])
                return false;

            isActive[pixelIndex] = true;
            activeQueue.Enqueue(pixelIndex);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DequeueActive()
        {
            int pixelIndex = activeQueue.Dequeue();
            isActive[pixelIndex] = false;
            return pixelIndex;
        }
    }

    private const int TerminalParent = -1;
    private const int NoParent = -2;
    private const float CapacityEpsilon = 1e-5f;
    private const int CancellationCheckMask = 1023;

    private const byte NorthDirectionsMask =
        (1 << (int)GridDirection.North) |
        (1 << (int)GridDirection.NorthWest) |
        (1 << (int)GridDirection.NorthEast);

    private const byte SouthDirectionsMask =
        (1 << (int)GridDirection.South) |
        (1 << (int)GridDirection.SouthEast) |
        (1 << (int)GridDirection.SouthWest);

    private const byte WestDirectionsMask =
        (1 << (int)GridDirection.West) |
        (1 << (int)GridDirection.NorthWest) |
        (1 << (int)GridDirection.SouthWest);

    private const byte EastDirectionsMask =
        (1 << (int)GridDirection.East) |
        (1 << (int)GridDirection.NorthEast) |
        (1 << (int)GridDirection.SouthEast);

    private readonly Size gridSize;
    private readonly int pixelCount;
    private readonly int[] neighborOffsets;
    private readonly byte[] validDirectionMasks;

    private readonly float[] baseNeighborCapacities;
    private readonly float[] residualCapacities;
    private readonly double[] terminalExcess;

    private readonly SearchTree[] treeMembership;
    private readonly int[] parentPixel;
    private readonly byte[] directionToParent;

    private readonly bool[] isActive;
    private readonly int[] originTimestamp;
    private readonly int[] distanceToTerminal;
    private int baseTimestamp;
    private bool timestampsDirty;

    public GridMinCutSolver(int width, int height)
        : this(new Size(width, height))
    {
    }

    public GridMinCutSolver(Size size)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size.Width, 0, nameof(size));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(size.Height, 0, nameof(size));

        gridSize = size;
        pixelCount = checked((int)size.Area);
        neighborOffsets = new int[8];
        foreach (GridNeighbor neighbor in GridNeighborhood.All8Connected)
            neighborOffsets[(int)neighbor.Direction] = size.IndexOf(neighbor.Offset);

        validDirectionMasks = BuildValidDirectionMasks(size);
        baseNeighborCapacities = new float[pixelCount * 8];
        residualCapacities = new float[pixelCount * 8];
        terminalExcess = new double[pixelCount];

        treeMembership = new SearchTree[pixelCount];
        parentPixel = new int[pixelCount];
        directionToParent = new byte[pixelCount];

        isActive = new bool[pixelCount];
        originTimestamp = new int[pixelCount];
        distanceToTerminal = new int[pixelCount];
    }

    public void SetUndirectedEdgeCapacity(int pixelIndex, int direction, float capacity)
    {
        Debug.Assert((uint)pixelIndex < (uint)pixelCount);
        Debug.Assert((uint)direction < 8);
        Debug.Assert((validDirectionMasks[pixelIndex] & (1 << direction)) != 0, $"Direction {direction} is out of grid bounds for pixel {pixelIndex}.");

        int neighborIndex = pixelIndex + neighborOffsets[direction];
        baseNeighborCapacities[DirectedEdgeIndex(pixelIndex, direction)] = capacity;
        baseNeighborCapacities[DirectedEdgeIndex(neighborIndex, OppositeDirection(direction))] = capacity;
    }

    public void SetUndirectedEdgeCapacity(Coord pixel, GridDirection direction, float capacity) =>
        SetUndirectedEdgeCapacity(gridSize.IndexOf(pixel), (int)direction, capacity);

    public bool IsSourceSide(int pixelIndex) => treeMembership[pixelIndex] == SearchTree.Source;

    public bool IsSourceSide(Coord pixel) => IsSourceSide(gridSize.IndexOf(pixel));

    public double Solve(
        ReadOnlySpan<double> sourceCapacities,
        ReadOnlySpan<double> sinkCapacities,
        CancellationToken cancellationToken = default)
    {
        if (sourceCapacities.Length != pixelCount)
            throw new ArgumentException("Source capacity span length must match the grid pixel count.", nameof(sourceCapacities));
        if (sinkCapacities.Length != pixelCount)
            throw new ArgumentException("Sink capacity span length must match the grid pixel count.", nameof(sinkCapacities));

        cancellationToken.ThrowIfCancellationRequested();

        if (pixelCount < ParallelRunner.DefaultMinParallelCount || gridSize.Height < 64 || Environment.ProcessorCount <= 1)
            return SolveSingleBand(sourceCapacities, sinkCapacities, cancellationToken);

        return SolveArrays(sourceCapacities.ToArray(), sinkCapacities.ToArray(), cancellationToken);
    }

    public double SolveArrays(
        double[] sourceCapacities,
        double[] sinkCapacities,
        CancellationToken cancellationToken = default) =>
        Solve(sourceCapacities, sinkCapacities, Environment.ProcessorCount, cancellationToken);

    internal double Solve(
        double[] sourceCapacities,
        double[] sinkCapacities,
        int maxBands,
        CancellationToken cancellationToken = default,
        int minParallelPixels = ParallelRunner.DefaultMinParallelCount)
    {
        ArgumentNullException.ThrowIfNull(sourceCapacities);
        ArgumentNullException.ThrowIfNull(sinkCapacities);
        if (sourceCapacities.Length != pixelCount)
            throw new ArgumentException("Source capacity array length must match the grid pixel count.", nameof(sourceCapacities));
        if (sinkCapacities.Length != pixelCount)
            throw new ArgumentException("Sink capacity array length must match the grid pixel count.", nameof(sinkCapacities));

        cancellationToken.ThrowIfCancellationRequested();

        int bandCount = Math.Min(maxBands, gridSize.Height / 32);
        if (maxBands <= 1 || bandCount <= 1 || pixelCount < minParallelPixels || gridSize.Height < 64)
            return SolveSingleBand(sourceCapacities.AsSpan(), sinkCapacities.AsSpan(), cancellationToken);

        return SolveMultiBand(sourceCapacities, sinkCapacities, bandCount, cancellationToken);
    }

    private double SolveSingleBand(
        ReadOnlySpan<double> sourceCapacities,
        ReadOnlySpan<double> sinkCapacities,
        CancellationToken cancellationToken)
    {
        PrepareTimestampsForSolve();
        Array.Copy(baseNeighborCapacities, residualCapacities, residualCapacities.Length);

        var context = new BandSearchContext(0, gridSize.Height, gridSize.Width, validDirectionMasks, isActive, baseTimestamp);
        double initialFlow = InitializeTerminalTreesBand(sourceCapacities, sinkCapacities, context);
        SeedBoundaryActiveNodes(context);
        double totalFlow = initialFlow + RunBoykovKolmogorovBand(context, cancellationToken);

        CommitTimestampsFromSolve(context.CurrentTimestamp);
        return totalFlow;
    }

    private double SolveMultiBand(
        double[] sourceCapacities,
        double[] sinkCapacities,
        int bandCount,
        CancellationToken cancellationToken)
    {
        PrepareTimestampsForSolve();

        int width = gridSize.Width;
        int height = gridSize.Height;
        var leafContexts = new BandSearchContext[bandCount];
        var leafFlows = new double[bandCount];

        // Phase 1: Solve each horizontal band independently on disjoint memory ranges.
        ParallelRunner.For(bandCount, cancellationToken, bandIndex =>
        {
            int startRow = (int)((long)bandIndex * height / bandCount);
            int endRow = (int)((long)(bandIndex + 1) * height / bandCount);
            var context = new BandSearchContext(startRow, endRow, width, validDirectionMasks, isActive, baseTimestamp);

            int pixelLength = context.EndPixel - context.StartPixel;
            Array.Copy(baseNeighborCapacities, context.StartPixel << 3, residualCapacities, context.StartPixel << 3, pixelLength << 3);

            double initialFlow = InitializeTerminalTreesBand(
                sourceCapacities.AsSpan(context.StartPixel, pixelLength),
                sinkCapacities.AsSpan(context.StartPixel, pixelLength),
                context);

            SeedBoundaryActiveNodes(context);
            leafFlows[bandIndex] = initialFlow + RunBoykovKolmogorovBand(context, cancellationToken);
            leafContexts[bandIndex] = context;
        });

        double totalFlow = 0.0;
        for (int i = 0; i < leafFlows.Length; i++)
            totalFlow += leafFlows[i];

        // Phase 2: Merge adjacent bands bottom-up along shared horizontal seams, reusing existing search trees.
        BandSearchContext[] activeBands = leafContexts;
        while (activeBands.Length > 1)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int pairCount = activeBands.Length / 2;
            int nextCount = (activeBands.Length + 1) / 2;
            var nextBands = new BandSearchContext[nextCount];
            var mergeFlows = new double[pairCount];

            ParallelRunner.For(pairCount, cancellationToken, pairIndex =>
            {
                BandSearchContext left = activeBands[pairIndex * 2];
                BandSearchContext right = activeBands[(pairIndex * 2) + 1];
                int seamRow = left.EndRow;
                Debug.Assert(seamRow == right.StartRow);

                var mergedContext = new BandSearchContext(
                    left.StartRow,
                    right.EndRow,
                    width,
                    validDirectionMasks,
                    isActive,
                    Math.Max(left.CurrentTimestamp, right.CurrentTimestamp));

                SeedSeamActiveNodes(seamRow, mergedContext);
                mergeFlows[pairIndex] = RunBoykovKolmogorovBand(mergedContext, cancellationToken);
                nextBands[pairIndex] = mergedContext;
            });

            if ((activeBands.Length & 1) != 0)
                nextBands[pairCount] = activeBands[^1];

            for (int i = 0; i < mergeFlows.Length; i++)
                totalFlow += mergeFlows[i];

            activeBands = nextBands;
        }

        CommitTimestampsFromSolve(activeBands[0].CurrentTimestamp);
        return totalFlow;
    }

    private void PrepareTimestampsForSolve()
    {
        if (timestampsDirty || baseTimestamp > 1_000_000_000)
        {
            Array.Clear(originTimestamp);
            baseTimestamp = 0;
        }

        timestampsDirty = true;
    }

    private void CommitTimestampsFromSolve(int finalTimestamp)
    {
        baseTimestamp = finalTimestamp;
        timestampsDirty = false;
    }

    private double RunBoykovKolmogorovBand(BandSearchContext context, CancellationToken cancellationToken)
    {
        double augmentedFlow = 0.0;
        int augmentationCount = 0;

        while (TryGrowToAugmentingPath(context, out int sourceEndpoint, out int sinkEndpoint, out int dirSourceToSink, cancellationToken))
        {
            if ((augmentationCount++ & CancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            augmentedFlow += AugmentPath(sourceEndpoint, sinkEndpoint, dirSourceToSink, context);
            AdoptOrphans(context, cancellationToken);
        }

        return augmentedFlow;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DirectedEdgeIndex(int fromPixel, int direction) => (fromPixel << 3) | direction;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int OppositeDirection(int direction)
    {
        Debug.Assert((uint)direction < 8);
        return direction ^ 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GetTreeGrowthCapacity(SearchTree tree, int fromPixel, int toPixel, int dirFromTo) =>
        tree == SearchTree.Source
            ? residualCapacities[DirectedEdgeIndex(fromPixel, dirFromTo)]
            : residualCapacities[DirectedEdgeIndex(toPixel, OppositeDirection(dirFromTo))];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float GetParentLinkCapacity(SearchTree tree, int childPixel, int parentPixelIndex, int dirChildToParent) =>
        tree == SearchTree.Source
            ? residualCapacities[DirectedEdgeIndex(parentPixelIndex, OppositeDirection(dirChildToParent))]
            : residualCapacities[DirectedEdgeIndex(childPixel, dirChildToParent)];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool PushDirectedFlow(int fromPixel, int toPixel, int dirFromTo, float flow)
    {
        residualCapacities[DirectedEdgeIndex(toPixel, OppositeDirection(dirFromTo))] += flow;
        return SubtractResidual(DirectedEdgeIndex(fromPixel, dirFromTo), flow);
    }

    private static byte[] BuildValidDirectionMasks(Size size)
    {
        int width = size.Width;
        int height = size.Height;
        var masks = new byte[size.Count];
        Array.Fill(masks, (byte)0xFF);

        int bottomRowBase = (height - 1) * width;
        for (int x = 0; x < width; x++)
        {
            masks[x] &= unchecked((byte)~NorthDirectionsMask);
            masks[bottomRowBase + x] &= unchecked((byte)~SouthDirectionsMask);
        }

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            masks[rowBase] &= unchecked((byte)~WestDirectionsMask);
            masks[rowBase + width - 1] &= unchecked((byte)~EastDirectionsMask);
        }

        return masks;
    }

    private double InitializeTerminalTreesBand(
        ReadOnlySpan<double> sourceCapacities,
        ReadOnlySpan<double> sinkCapacities,
        BandSearchContext context)
    {
        double initialFlow = 0.0;
        int startPixel = context.StartPixel;

        for (int offset = 0; offset < sourceCapacities.Length; offset++)
        {
            int p = startPixel + offset;
            isActive[p] = false;

            double src = Math.Max(0.0, sourceCapacities[offset]);
            double snk = Math.Max(0.0, sinkCapacities[offset]);
            initialFlow += Math.Min(src, snk);

            double excess = src - snk;
            terminalExcess[p] = excess;

            if (excess > CapacityEpsilon)
            {
                treeMembership[p] = SearchTree.Source;
                parentPixel[p] = TerminalParent;
            }
            else if (excess < -CapacityEpsilon)
            {
                treeMembership[p] = SearchTree.Sink;
                parentPixel[p] = TerminalParent;
            }
            else
            {
                treeMembership[p] = SearchTree.Free;
                parentPixel[p] = NoParent;
            }
        }

        return initialFlow;
    }

    private void SeedBoundaryActiveNodes(BandSearchContext context)
    {
        for (int p = context.StartPixel; p < context.EndPixel; p++)
        {
            SearchTree ownTree = treeMembership[p];
            if (ownTree == SearchTree.Free)
                continue;

            byte validMask = context.GetValidDirectionMask(p);
            for (int dir = 0; dir < 8; dir++)
            {
                if ((validMask & (1 << dir)) == 0)
                    continue;

                int q = p + neighborOffsets[dir];
                if (treeMembership[q] != ownTree && GetTreeGrowthCapacity(ownTree, p, q, dir) > CapacityEpsilon)
                {
                    context.TryEnqueueActive(p);
                    break;
                }
            }
        }
    }

    private void SeedSeamActiveNodes(int seamRow, BandSearchContext context)
    {
        int width = gridSize.Width;
        int topRowStart = (seamRow - 1) * width;
        int bottomRowStart = seamRow * width;

        SeedSeamRow(topRowStart, topRowStart + width, SouthDirectionsMask, context);
        SeedSeamRow(bottomRowStart, bottomRowStart + width, NorthDirectionsMask, context);
    }

    private void SeedSeamRow(int rowStart, int rowEnd, byte seamDirectionMask, BandSearchContext context)
    {
        for (int p = rowStart; p < rowEnd; p++)
        {
            SearchTree ownTree = treeMembership[p];
            if (ownTree == SearchTree.Free)
                continue;

            byte mask = (byte)(context.GetValidDirectionMask(p) & seamDirectionMask);
            for (int dir = 0; dir < 8; dir++)
            {
                if ((mask & (1 << dir)) == 0)
                    continue;

                int q = p + neighborOffsets[dir];
                if (treeMembership[q] != ownTree && GetTreeGrowthCapacity(ownTree, p, q, dir) > CapacityEpsilon)
                {
                    context.TryEnqueueActive(p);
                    break;
                }
            }
        }
    }

    private bool TryGrowToAugmentingPath(
        BandSearchContext context,
        out int sourceEndpoint,
        out int sinkEndpoint,
        out int dirSourceToSink,
        CancellationToken cancellationToken)
    {
        int stepCount = 0;
        while (context.HasActiveNodes)
        {
            if ((++stepCount & CancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            int p = context.PeekActive();
            SearchTree tree = treeMembership[p];
            if (tree == SearchTree.Free)
            {
                context.DequeueActive();
                continue;
            }

            byte validMask = context.GetValidDirectionMask(p);
            for (int dir = 0; dir < 8; dir++)
            {
                if ((validMask & (1 << dir)) == 0)
                    continue;

                int q = p + neighborOffsets[dir];
                if (GetTreeGrowthCapacity(tree, p, q, dir) <= CapacityEpsilon)
                    continue;

                SearchTree neighborTree = treeMembership[q];
                if (neighborTree == SearchTree.Free)
                {
                    treeMembership[q] = tree;
                    parentPixel[q] = p;
                    directionToParent[q] = (byte)OppositeDirection(dir);
                    context.TryEnqueueActive(q);
                }
                else if (neighborTree != tree)
                {
                    if (tree == SearchTree.Source)
                    {
                        sourceEndpoint = p;
                        sinkEndpoint = q;
                        dirSourceToSink = dir;
                    }
                    else
                    {
                        sourceEndpoint = q;
                        sinkEndpoint = p;
                        dirSourceToSink = OppositeDirection(dir);
                    }
                    return true;
                }
            }

            context.DequeueActive();
        }

        sourceEndpoint = -1;
        sinkEndpoint = -1;
        dirSourceToSink = 0;
        return false;
    }

    private double AugmentPath(int sourceEndpoint, int sinkEndpoint, int dirSourceToSink, BandSearchContext context)
    {
        double bottleneck = FindBottleneckCapacity(sourceEndpoint, sinkEndpoint, dirSourceToSink);
        PushFlowAlongAugmentingPath(sourceEndpoint, sinkEndpoint, dirSourceToSink, bottleneck, context);
        return bottleneck;
    }

    private double FindBottleneckCapacity(int sourceEndpoint, int sinkEndpoint, int dirSourceToSink)
    {
        double bottleneck = residualCapacities[DirectedEdgeIndex(sourceEndpoint, dirSourceToSink)];

        int u = sourceEndpoint;
        while (parentPixel[u] >= 0)
        {
            int parent = parentPixel[u];
            double edgeCap = GetParentLinkCapacity(SearchTree.Source, u, parent, directionToParent[u]);
            if (edgeCap < bottleneck)
                bottleneck = edgeCap;
            u = parent;
        }

        double sourceRootCap = terminalExcess[u];
        if (sourceRootCap < bottleneck)
            bottleneck = sourceRootCap;

        int v = sinkEndpoint;
        while (parentPixel[v] >= 0)
        {
            int parent = parentPixel[v];
            double edgeCap = GetParentLinkCapacity(SearchTree.Sink, v, parent, directionToParent[v]);
            if (edgeCap < bottleneck)
                bottleneck = edgeCap;
            v = parent;
        }

        double sinkRootCap = -terminalExcess[v];
        if (sinkRootCap < bottleneck)
            bottleneck = sinkRootCap;

        return bottleneck;
    }

    private void PushFlowAlongAugmentingPath(
        int sourceEndpoint,
        int sinkEndpoint,
        int dirSourceToSink,
        double bottleneck,
        BandSearchContext context)
    {
        float floatBottleneck = (float)bottleneck;
        PushDirectedFlow(sourceEndpoint, sinkEndpoint, dirSourceToSink, floatBottleneck);

        int u = sourceEndpoint;
        while (parentPixel[u] >= 0)
        {
            int parent = parentPixel[u];
            int dirParentToChild = OppositeDirection(directionToParent[u]);
            if (!PushDirectedFlow(parent, u, dirParentToChild, floatBottleneck))
                MarkAsOrphan(u, context);

            u = parent;
        }

        terminalExcess[u] -= bottleneck;
        if (terminalExcess[u] <= CapacityEpsilon)
            MarkAsOrphan(u, context);

        int v = sinkEndpoint;
        while (parentPixel[v] >= 0)
        {
            int parent = parentPixel[v];
            if (!PushDirectedFlow(v, parent, directionToParent[v], floatBottleneck))
                MarkAsOrphan(v, context);

            v = parent;
        }

        terminalExcess[v] += bottleneck;
        if (-terminalExcess[v] <= CapacityEpsilon)
            MarkAsOrphan(v, context);
    }

    private void MarkAsOrphan(int pixelIndex, BandSearchContext context)
    {
        parentPixel[pixelIndex] = NoParent;
        context.OrphanQueue.Enqueue(pixelIndex);
    }

    private bool SubtractResidual(int edgeIndex, float flow)
    {
        float remaining = residualCapacities[edgeIndex] - flow;
        if (remaining <= CapacityEpsilon)
        {
            residualCapacities[edgeIndex] = 0f;
            return false;
        }

        residualCapacities[edgeIndex] = remaining;
        return true;
    }

    private void AdoptOrphans(BandSearchContext context, CancellationToken cancellationToken)
    {
        Queue<int> orphanQueue = context.OrphanQueue;
        if (orphanQueue.Count == 0)
            return;

        if (context.CurrentTimestamp == int.MaxValue)
        {
            originTimestamp.AsSpan(context.StartPixel, context.EndPixel - context.StartPixel).Clear();
            context.CurrentTimestamp = 0;
        }

        context.CurrentTimestamp++;
        int stepCount = 0;
        while (orphanQueue.Count > 0)
        {
            if ((++stepCount & CancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            int orphan = orphanQueue.Dequeue();
            if (!TryFindReplacementParent(orphan, context))
                DemoteOrphanToFree(orphan, context);
        }
    }

    private bool TryFindReplacementParent(int orphan, BandSearchContext context)
    {
        SearchTree tree = treeMembership[orphan];

        int bestParent = -1;
        byte bestDir = 0;
        int bestDistance = int.MaxValue;
        byte validMask = context.GetValidDirectionMask(orphan);

        for (int dir = 0; dir < 8; dir++)
        {
            if ((validMask & (1 << dir)) == 0)
                continue;

            int candidate = orphan + neighborOffsets[dir];
            if (treeMembership[candidate] != tree)
                continue;

            if (GetParentLinkCapacity(tree, orphan, candidate, dir) <= CapacityEpsilon)
                continue;

            int distance = ComputeDistanceToTerminal(candidate, context);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestParent = candidate;
                bestDir = (byte)dir;
            }
        }

        if (bestParent < 0)
            return false;

        parentPixel[orphan] = bestParent;
        directionToParent[orphan] = bestDir;
        originTimestamp[orphan] = context.CurrentTimestamp;
        distanceToTerminal[orphan] = bestDistance + 1;
        return true;
    }

    private void DemoteOrphanToFree(int orphan, BandSearchContext context)
    {
        SearchTree tree = treeMembership[orphan];
        byte validMask = context.GetValidDirectionMask(orphan);

        for (int dir = 0; dir < 8; dir++)
        {
            if ((validMask & (1 << dir)) == 0)
                continue;

            int neighbor = orphan + neighborOffsets[dir];
            if (treeMembership[neighbor] != tree)
                continue;

            if (GetParentLinkCapacity(tree, orphan, neighbor, dir) > CapacityEpsilon)
                context.TryEnqueueActive(neighbor);

            if (parentPixel[neighbor] == orphan)
                MarkAsOrphan(neighbor, context);
        }

        treeMembership[orphan] = SearchTree.Free;
        parentPixel[orphan] = NoParent;
        originTimestamp[orphan] = 0;
    }

    private int ComputeDistanceToTerminal(int startNode, BandSearchContext context)
    {
        int timestamp = context.CurrentTimestamp;
        int traversedDepth = 0;
        int current = startNode;

        while (true)
        {
            if (originTimestamp[current] == timestamp)
            {
                traversedDepth += distanceToTerminal[current];
                break;
            }

            int parent = parentPixel[current];
            if (parent == TerminalParent)
                break;

            if (parent == NoParent)
                return int.MaxValue;

            traversedDepth++;
            current = parent;
        }

        int remainingDist = traversedDepth;
        int trace = startNode;
        while (trace != current)
        {
            originTimestamp[trace] = timestamp;
            distanceToTerminal[trace] = remainingDist--;
            trace = parentPixel[trace];
        }

        if (originTimestamp[current] != timestamp)
        {
            originTimestamp[current] = timestamp;
            distanceToTerminal[current] = 0;
        }

        return traversedDepth;
    }
}
