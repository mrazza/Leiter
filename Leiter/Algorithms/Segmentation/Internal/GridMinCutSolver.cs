namespace Leiter.Algorithms.Segmentation.Internal;

/// <summary>
/// Solves 8-connected 2D grid s-t min-cut / max-flow instances using the Boykov–Kolmogorov algorithm.
/// </summary>
internal sealed class GridMinCutSolver
{
    private const byte FreeTree = 0;
    private const byte SourceTree = 1;
    private const byte SinkTree = 2;

    private const int TerminalParent = -1;
    private const int NoParent = -2;
    private const float CapacityEpsilon = 1e-5f;

    // Direction indices chosen so (dir ^ 4) is always the opposite vector.
    private static readonly int[] DirectionDeltaX = [1, 0, 1, -1, -1, 0, -1, 1];
    private static readonly int[] DirectionDeltaY = [0, 1, 1, 1, 0, -1, -1, -1];

    private readonly int pixelCount;
    private readonly int[] neighborOffsets;
    private readonly byte[] validDirectionMasks;

    private readonly float[] baseNeighborCapacities;
    private readonly float[] residualCapacities;
    private readonly double[] terminalExcess;

    private readonly byte[] treeMembership;
    private readonly int[] parentPixel;
    private readonly byte[] directionToParent;

    private readonly int[] activeQueue;
    private readonly bool[] isInActiveQueue;
    private readonly Queue<int> orphanQueue;
    private readonly long[] originTimestamp;
    private readonly int[] distanceToTerminal;

    private int queueHead;
    private int queueTail;
    private int queueCount;
    private long currentTimestamp;

    public GridMinCutSolver(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(width, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(height, 0);

        pixelCount = checked(width * height);
        neighborOffsets = new int[8];
        for (int dir = 0; dir < 8; dir++)
            neighborOffsets[dir] = DirectionDeltaX[dir] + (DirectionDeltaY[dir] * width);

        validDirectionMasks = BuildValidDirectionMasks(width, height);
        baseNeighborCapacities = new float[pixelCount * 8];
        residualCapacities = new float[pixelCount * 8];
        terminalExcess = new double[pixelCount];

        treeMembership = new byte[pixelCount];
        parentPixel = new int[pixelCount];
        directionToParent = new byte[pixelCount];

        activeQueue = new int[pixelCount];
        isInActiveQueue = new bool[pixelCount];
        orphanQueue = new Queue<int>();
        originTimestamp = new long[pixelCount];
        distanceToTerminal = new int[pixelCount];
    }

    public void SetUndirectedEdgeCapacity(int pixelIndex, int direction, float capacity)
    {
        int neighborIndex = pixelIndex + neighborOffsets[direction];
        baseNeighborCapacities[(pixelIndex << 3) | direction] = capacity;
        baseNeighborCapacities[(neighborIndex << 3) | (direction ^ 4)] = capacity;
    }

    public bool IsSourceSide(int pixelIndex) => treeMembership[pixelIndex] == SourceTree;

    public double Solve(ReadOnlySpan<double> sourceCapacities, ReadOnlySpan<double> sinkCapacities)
    {
        if (sourceCapacities.Length != pixelCount || sinkCapacities.Length != pixelCount)
            throw new ArgumentException("Terminal capacity spans must match the grid pixel count.");

        Array.Copy(baseNeighborCapacities, residualCapacities, residualCapacities.Length);
        Array.Clear(isInActiveQueue);
        Array.Clear(originTimestamp);
        orphanQueue.Clear();
        queueHead = 0;
        queueTail = 0;
        queueCount = 0;
        currentTimestamp = 0;

        double totalFlow = InitializeTerminalTrees(sourceCapacities, sinkCapacities);
        SeedBoundaryActiveNodes();

        while (true)
        {
            if (!TryGrowToAugmentingPath(out int sourceEndpoint, out int sinkEndpoint, out int dirSourceToSink))
                break;

            totalFlow += AugmentPath(sourceEndpoint, sinkEndpoint, dirSourceToSink);
            AdoptOrphans();
        }

        return totalFlow;
    }

    private static byte[] BuildValidDirectionMasks(int width, int height)
    {
        var masks = new byte[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte mask = 0;
                for (int dir = 0; dir < 8; dir++)
                {
                    int nx = x + DirectionDeltaX[dir];
                    int ny = y + DirectionDeltaY[dir];
                    if ((uint)nx < (uint)width && (uint)ny < (uint)height)
                        mask |= (byte)(1 << dir);
                }
                masks[(y * width) + x] = mask;
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
                treeMembership[p] = SourceTree;
                parentPixel[p] = TerminalParent;
            }
            else if (excess < -CapacityEpsilon)
            {
                treeMembership[p] = SinkTree;
                parentPixel[p] = TerminalParent;
            }
            else
            {
                treeMembership[p] = FreeTree;
                parentPixel[p] = NoParent;
            }
        }

        return initialFlow;
    }

    private void SeedBoundaryActiveNodes()
    {
        for (int p = 0; p < pixelCount; p++)
        {
            byte ownTree = treeMembership[p];
            if (ownTree == FreeTree)
                continue;

            byte validMask = validDirectionMasks[p];
            for (int dir = 0; dir < 8; dir++)
            {
                if ((validMask & (1 << dir)) == 0)
                    continue;

                int q = p + neighborOffsets[dir];
                if (treeMembership[q] != ownTree)
                {
                    EnqueueActive(p);
                    break;
                }
            }
        }
    }

    private void EnqueueActive(int pixelIndex)
    {
        if (isInActiveQueue[pixelIndex])
            return;

        isInActiveQueue[pixelIndex] = true;
        activeQueue[queueTail] = pixelIndex;
        queueTail++;
        if (queueTail == pixelCount)
            queueTail = 0;
        queueCount++;
    }

    private void DequeueActiveHead()
    {
        int pixelIndex = activeQueue[queueHead];
        isInActiveQueue[pixelIndex] = false;
        queueHead++;
        if (queueHead == pixelCount)
            queueHead = 0;
        queueCount--;
    }

    private bool TryGrowToAugmentingPath(out int sourceEndpoint, out int sinkEndpoint, out int dirSourceToSink)
    {
        while (queueCount > 0)
        {
            int p = activeQueue[queueHead];
            byte tree = treeMembership[p];
            if (tree == FreeTree)
            {
                DequeueActiveHead();
                continue;
            }

            byte validMask = validDirectionMasks[p];
            for (int dir = 0; dir < 8; dir++)
            {
                if ((validMask & (1 << dir)) == 0)
                    continue;

                int q = p + neighborOffsets[dir];
                float capacity = tree == SourceTree
                    ? residualCapacities[(p << 3) | dir]
                    : residualCapacities[(q << 3) | (dir ^ 4)];

                if (capacity <= CapacityEpsilon)
                    continue;

                byte neighborTree = treeMembership[q];
                if (neighborTree == FreeTree)
                {
                    treeMembership[q] = tree;
                    parentPixel[q] = p;
                    directionToParent[q] = (byte)(dir ^ 4);
                    EnqueueActive(q);
                }
                else if (neighborTree != tree)
                {
                    if (tree == SourceTree)
                    {
                        sourceEndpoint = p;
                        sinkEndpoint = q;
                        dirSourceToSink = dir;
                    }
                    else
                    {
                        sourceEndpoint = q;
                        sinkEndpoint = p;
                        dirSourceToSink = dir ^ 4;
                    }
                    return true;
                }
            }

            DequeueActiveHead();
        }

        sourceEndpoint = -1;
        sinkEndpoint = -1;
        dirSourceToSink = 0;
        return false;
    }

    private double AugmentPath(int sourceEndpoint, int sinkEndpoint, int dirSourceToSink)
    {
        double bottleneck = residualCapacities[(sourceEndpoint << 3) | dirSourceToSink];

        int u = sourceEndpoint;
        while (parentPixel[u] >= 0)
        {
            int parent = parentPixel[u];
            double edgeCap = residualCapacities[(parent << 3) | (directionToParent[u] ^ 4)];
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
            double edgeCap = residualCapacities[(v << 3) | directionToParent[v]];
            if (edgeCap < bottleneck)
                bottleneck = edgeCap;
            v = parentPixel[v];
        }
        double sinkRootCap = -terminalExcess[v];
        if (sinkRootCap < bottleneck)
            bottleneck = sinkRootCap;

        float floatBottleneck = (float)bottleneck;
        SubtractResidual((sourceEndpoint << 3) | dirSourceToSink, floatBottleneck);
        residualCapacities[(sinkEndpoint << 3) | (dirSourceToSink ^ 4)] += floatBottleneck;

        u = sourceEndpoint;
        while (parentPixel[u] >= 0)
        {
            int parent = parentPixel[u];
            byte dirToParent = directionToParent[u];
            int forwardEdge = (parent << 3) | (dirToParent ^ 4);
            residualCapacities[(u << 3) | dirToParent] += floatBottleneck;

            if (!SubtractResidual(forwardEdge, floatBottleneck))
            {
                parentPixel[u] = NoParent;
                orphanQueue.Enqueue(u);
            }

            u = parent;
        }

        terminalExcess[u] -= bottleneck;
        if (terminalExcess[u] <= CapacityEpsilon)
        {
            parentPixel[u] = NoParent;
            orphanQueue.Enqueue(u);
        }

        v = sinkEndpoint;
        while (parentPixel[v] >= 0)
        {
            int parent = parentPixel[v];
            byte dirToParent = directionToParent[v];
            int forwardEdge = (v << 3) | dirToParent;
            residualCapacities[(parent << 3) | (dirToParent ^ 4)] += floatBottleneck;

            if (!SubtractResidual(forwardEdge, floatBottleneck))
            {
                parentPixel[v] = NoParent;
                orphanQueue.Enqueue(v);
            }

            v = parent;
        }

        terminalExcess[v] += bottleneck;
        if (-terminalExcess[v] <= CapacityEpsilon)
        {
            parentPixel[v] = NoParent;
            orphanQueue.Enqueue(v);
        }

        return bottleneck;
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
        while (orphanQueue.Count > 0)
        {
            int orphan = orphanQueue.Dequeue();
            byte tree = treeMembership[orphan];
            currentTimestamp++;

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

                float treeArcCapacity = tree == SourceTree
                    ? residualCapacities[(candidate << 3) | (dir ^ 4)]
                    : residualCapacities[(orphan << 3) | dir];

                if (treeArcCapacity <= CapacityEpsilon)
                    continue;

                int distance = ComputeDistanceToTerminal(candidate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestParent = candidate;
                    bestDir = (byte)dir;
                }
            }

            if (bestParent >= 0)
            {
                parentPixel[orphan] = bestParent;
                directionToParent[orphan] = bestDir;
                originTimestamp[orphan] = currentTimestamp;
                distanceToTerminal[orphan] = bestDistance + 1;
                continue;
            }

            for (int dir = 0; dir < 8; dir++)
            {
                if ((validMask & (1 << dir)) == 0)
                    continue;

                int neighbor = orphan + neighborOffsets[dir];
                if (treeMembership[neighbor] != tree)
                    continue;

                float treeArcCapacity = tree == SourceTree
                    ? residualCapacities[(neighbor << 3) | (dir ^ 4)]
                    : residualCapacities[(orphan << 3) | dir];

                if (treeArcCapacity > CapacityEpsilon)
                    EnqueueActive(neighbor);

                if (parentPixel[neighbor] == orphan)
                {
                    parentPixel[neighbor] = NoParent;
                    orphanQueue.Enqueue(neighbor);
                }
            }

            treeMembership[orphan] = FreeTree;
            parentPixel[orphan] = NoParent;
            originTimestamp[orphan] = 0;
        }
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
