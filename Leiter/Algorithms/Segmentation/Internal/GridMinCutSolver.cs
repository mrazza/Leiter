namespace Leiter.Algorithms.Segmentation.Internal;

using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    private const int TerminalParent = -1;
    private const int NoParent = -2;
    private const float CapacityEpsilon = 1e-5f;

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

    private readonly UniqueIntQueue activeQueue;
    private readonly Queue<int> orphanQueue;
    private readonly long[] originTimestamp;
    private readonly int[] distanceToTerminal;
    private long currentTimestamp;

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

        activeQueue = new UniqueIntQueue(pixelCount);
        orphanQueue = new Queue<int>();
        originTimestamp = new long[pixelCount];
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

    public double Solve(ReadOnlySpan<double> sourceCapacities, ReadOnlySpan<double> sinkCapacities)
    {
        if (sourceCapacities.Length != pixelCount)
            throw new ArgumentException("Source capacity span length must match the grid pixel count.", nameof(sourceCapacities));
        if (sinkCapacities.Length != pixelCount)
            throw new ArgumentException("Sink capacity span length must match the grid pixel count.", nameof(sinkCapacities));

        Array.Copy(baseNeighborCapacities, residualCapacities, residualCapacities.Length);
        Array.Clear(originTimestamp);
        activeQueue.Clear();
        orphanQueue.Clear();
        currentTimestamp = 0;

        double totalFlow = InitializeTerminalTrees(sourceCapacities, sinkCapacities);
        SeedBoundaryActiveNodes();

        while (TryGrowToAugmentingPath(out int sourceEndpoint, out int sinkEndpoint, out int dirSourceToSink))
        {
            totalFlow += AugmentPath(sourceEndpoint, sinkEndpoint, dirSourceToSink);
            AdoptOrphans();
        }

        return totalFlow;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int DirectedEdgeIndex(int fromPixel, int direction) => (fromPixel << 3) | direction;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int OppositeDirection(int direction) => (int)((GridDirection)direction).Opposite();

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
        Debug.Assert(GridNeighborhood.All8Connected.Length == 8);

        var masks = new byte[size.Count];
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                var coord = new Coord(x, y);
                byte mask = 0;
                foreach (GridNeighbor neighbor in GridNeighborhood.All8Connected)
                {
                    if (size.Contains(coord + neighbor.Offset))
                        mask |= (byte)(1 << (int)neighbor.Direction);
                }
                masks[size.IndexOf(coord)] = mask;
            }
        }
        return masks;
    }

    private double InitializeTerminalTrees(ReadOnlySpan<double> sourceCapacities, ReadOnlySpan<double> sinkCapacities)
    {
        double initialFlow = 0.0;

        for (int p = 0; p < pixelCount; p++)
        {
            double src = Math.Max(0.0, sourceCapacities[p]);
            double snk = Math.Max(0.0, sinkCapacities[p]);
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

    private void SeedBoundaryActiveNodes()
    {
        for (int p = 0; p < pixelCount; p++)
        {
            SearchTree ownTree = treeMembership[p];
            if (ownTree == SearchTree.Free)
                continue;

            byte validMask = validDirectionMasks[p];
            for (int dir = 0; dir < 8; dir++)
            {
                if ((validMask & (1 << dir)) == 0)
                    continue;

                int q = p + neighborOffsets[dir];
                if (treeMembership[q] != ownTree)
                {
                    activeQueue.Enqueue(p);
                    break;
                }
            }
        }
    }

    private bool TryGrowToAugmentingPath(out int sourceEndpoint, out int sinkEndpoint, out int dirSourceToSink)
    {
        while (activeQueue.Count > 0)
        {
            int p = activeQueue.Peek();
            SearchTree tree = treeMembership[p];
            if (tree == SearchTree.Free)
            {
                activeQueue.Dequeue();
                continue;
            }

            byte validMask = validDirectionMasks[p];
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
                    activeQueue.Enqueue(q);
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

            activeQueue.Dequeue();
        }

        sourceEndpoint = -1;
        sinkEndpoint = -1;
        dirSourceToSink = 0;
        return false;
    }

    private double AugmentPath(int sourceEndpoint, int sinkEndpoint, int dirSourceToSink)
    {
        double bottleneck = FindBottleneckCapacity(sourceEndpoint, sinkEndpoint, dirSourceToSink);
        PushFlowAlongAugmentingPath(sourceEndpoint, sinkEndpoint, dirSourceToSink, bottleneck);
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

    private void PushFlowAlongAugmentingPath(int sourceEndpoint, int sinkEndpoint, int dirSourceToSink, double bottleneck)
    {
        float floatBottleneck = (float)bottleneck;
        PushDirectedFlow(sourceEndpoint, sinkEndpoint, dirSourceToSink, floatBottleneck);

        int u = sourceEndpoint;
        while (parentPixel[u] >= 0)
        {
            int parent = parentPixel[u];
            int dirParentToChild = OppositeDirection(directionToParent[u]);
            if (!PushDirectedFlow(parent, u, dirParentToChild, floatBottleneck))
                MarkAsOrphan(u);

            u = parent;
        }

        terminalExcess[u] -= bottleneck;
        if (terminalExcess[u] <= CapacityEpsilon)
            MarkAsOrphan(u);

        int v = sinkEndpoint;
        while (parentPixel[v] >= 0)
        {
            int parent = parentPixel[v];
            if (!PushDirectedFlow(v, parent, directionToParent[v], floatBottleneck))
                MarkAsOrphan(v);

            v = parent;
        }

        terminalExcess[v] += bottleneck;
        if (-terminalExcess[v] <= CapacityEpsilon)
            MarkAsOrphan(v);
    }

    private void MarkAsOrphan(int pixelIndex)
    {
        parentPixel[pixelIndex] = NoParent;
        orphanQueue.Enqueue(pixelIndex);
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

    private void AdoptOrphans()
    {
        if (orphanQueue.Count == 0)
            return;

        currentTimestamp++;
        while (orphanQueue.Count > 0)
        {
            int orphan = orphanQueue.Dequeue();
            if (!TryFindReplacementParent(orphan))
                DemoteOrphanToFree(orphan);
        }
    }

    private bool TryFindReplacementParent(int orphan)
    {
        SearchTree tree = treeMembership[orphan];

        int bestParent = -1;
        byte bestDir = 0;
        int bestDistance = int.MaxValue;
        byte validMask = validDirectionMasks[orphan];

        for (int dir = 0; dir < 8; dir++)
        {
            if ((validMask & (1 << dir)) == 0)
                continue;

            int candidate = orphan + neighborOffsets[dir];
            if (treeMembership[candidate] != tree)
                continue;

            if (GetParentLinkCapacity(tree, orphan, candidate, dir) <= CapacityEpsilon)
                continue;

            int distance = ComputeDistanceToTerminal(candidate);
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
        originTimestamp[orphan] = currentTimestamp;
        distanceToTerminal[orphan] = bestDistance + 1;
        return true;
    }

    private void DemoteOrphanToFree(int orphan)
    {
        SearchTree tree = treeMembership[orphan];
        byte validMask = validDirectionMasks[orphan];

        for (int dir = 0; dir < 8; dir++)
        {
            if ((validMask & (1 << dir)) == 0)
                continue;

            int neighbor = orphan + neighborOffsets[dir];
            if (treeMembership[neighbor] != tree)
                continue;

            if (GetParentLinkCapacity(tree, orphan, neighbor, dir) > CapacityEpsilon)
                activeQueue.Enqueue(neighbor);

            if (parentPixel[neighbor] == orphan)
                MarkAsOrphan(neighbor);
        }

        treeMembership[orphan] = SearchTree.Free;
        parentPixel[orphan] = NoParent;
        originTimestamp[orphan] = 0;
    }

    private int ComputeDistanceToTerminal(int startNode)
    {
        int traversedDepth = 0;
        int current = startNode;

        while (true)
        {
            if (originTimestamp[current] == currentTimestamp)
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
            originTimestamp[trace] = currentTimestamp;
            distanceToTerminal[trace] = remainingDist--;
            trace = parentPixel[trace];
        }

        if (originTimestamp[current] != currentTimestamp)
        {
            originTimestamp[current] = currentTimestamp;
            distanceToTerminal[current] = 0;
        }

        return traversedDepth;
    }
}
