namespace Leiter.Algorithms.Segmentation.Internal;

using System;
using System.Collections.Generic;
using System.Threading;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Clusters converged Mean-Shift modes into connected regions using gradient-ordered,
/// centroid-bounded breadth-first basin growth followed by small-region merging.
/// </summary>
internal static class MeanShiftRegionClusterer
{
    private const int GradientBucketCount = 1024;
    private const double GradientBucketScale = 4.0;
    private const double SeedColorBoundMultiplier = 1.5;
    private const int CancellationCheckMask = 4095;

    public static IDisjointSet ClusterModes(
        IUntypedMatrix targetImage,
        Lab32[] sourcePixels,
        MeanShiftMode[] modes,
        MeanShiftOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetImage);
        ArgumentNullException.ThrowIfNull(sourcePixels);
        ArgumentNullException.ThrowIfNull(modes);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        Size size = targetImage.Size;
        int pixelCount = size.Count;
        if (sourcePixels.Length != pixelCount)
            throw new ArgumentException($"Source pixel count ({sourcePixels.Length}) must match image pixel count ({pixelCount}).", nameof(sourcePixels));
        if (modes.Length != pixelCount)
            throw new ArgumentException($"Mode array length ({modes.Length}) must match image pixel count ({pixelCount}).", nameof(modes));

        cancellationToken.ThrowIfCancellationRequested();

        var disjointSet = DisjointSet.CreateSingletons(targetImage);

        if (pixelCount <= 1)
            return disjointSet;

        int[] orderedPixels = OrderPixelsByModeGradient(modes, size, cancellationToken);
        ClusterDensityBasins(disjointSet, modes, orderedPixels, size, options, cancellationToken);

        if (options.MinRegionSize > 1)
        {
            PruneSmallRegions(disjointSet, sourcePixels, size, options, cancellationToken);
        }

        return disjointSet;
    }

    private static int[] OrderPixelsByModeGradient(
        MeanShiftMode[] modes,
        Size size,
        CancellationToken cancellationToken)
    {
        int pixelCount = size.Count;
        double[] gradientSquared = ComputeMaxCardinalGradientSquared(modes, size, cancellationToken);

        var bucketCounts = new int[GradientBucketCount];
        for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            if ((pixelIndex & CancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            bucketCounts[GetGradientBucketIndex(gradientSquared[pixelIndex])]++;
        }

        var bucketWriteOffsets = new int[GradientBucketCount];
        int runningOffset = 0;
        for (int bucket = 0; bucket < GradientBucketCount; bucket++)
        {
            bucketWriteOffsets[bucket] = runningOffset;
            runningOffset += bucketCounts[bucket];
        }

        var orderedPixels = new int[pixelCount];
        for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
            if ((pixelIndex & CancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            int bucket = GetGradientBucketIndex(gradientSquared[pixelIndex]);
            orderedPixels[bucketWriteOffsets[bucket]++] = pixelIndex;
        }

        return orderedPixels;
    }

    private static double[] ComputeMaxCardinalGradientSquared(
        MeanShiftMode[] modes,
        Size size,
        CancellationToken cancellationToken)
    {
        var gradientSquared = new double[size.Count];
        var state = (modes, gradientSquared);
        for (int y = 0; y < size.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GridNeighborhood.ForEachForwardPairInRow(
                size,
                y,
                GridConnectivity.FourConnected,
                state,
                static (firstIndex, secondIndex, s) =>
                {
                    double differenceSquared = s.modes[firstIndex].Color.EuclideanDistanceSquared(s.modes[secondIndex].Color);
                    if (differenceSquared > s.gradientSquared[firstIndex])
                        s.gradientSquared[firstIndex] = differenceSquared;
                    if (differenceSquared > s.gradientSquared[secondIndex])
                        s.gradientSquared[secondIndex] = differenceSquared;
                });
        }

        return gradientSquared;
    }

    private static int GetGradientBucketIndex(double gradientSquared) =>
        double.IsFinite(gradientSquared)
            ? Math.Clamp((int)(gradientSquared * GradientBucketScale), 0, GradientBucketCount - 1)
            : GradientBucketCount - 1;

    private static void ClusterDensityBasins(
        DisjointSet<byte> disjointSet,
        MeanShiftMode[] modes,
        int[] orderedPixels,
        Size size,
        MeanShiftOptions options,
        CancellationToken cancellationToken)
    {
        double spatialRadius = options.SpatialRadius;
        double rangeRadius = options.RangeRadius;
        double spatialRadiusSquared = spatialRadius * spatialRadius;
        double rangeRadiusSquared = rangeRadius * rangeRadius;
        double seedColorBoundSquared = (SeedColorBoundMultiplier * rangeRadius) * (SeedColorBoundMultiplier * rangeRadius);

        ReadOnlySpan<GridNeighbor> neighbors = GridNeighborhood.GetAll(options.Connectivity);
        var visited = new bool[size.Count];
        var frontierQueue = new Queue<int>();

        for (int i = 0; i < orderedPixels.Length; i++)
        {
            if ((i & CancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            int seedPixel = orderedPixels[i];
            if (visited[seedPixel])
                continue;

            GrowRegionFromSeed(
                disjointSet,
                modes,
                neighbors,
                visited,
                frontierQueue,
                size,
                seedPixel,
                spatialRadiusSquared,
                rangeRadiusSquared,
                seedColorBoundSquared,
                cancellationToken);
        }
    }

    private static void GrowRegionFromSeed(
        DisjointSet<byte> disjointSet,
        MeanShiftMode[] modes,
        ReadOnlySpan<GridNeighbor> neighbors,
        bool[] visited,
        Queue<int> frontierQueue,
        Size size,
        int seedPixel,
        double spatialRadiusSquared,
        double rangeRadiusSquared,
        double seedColorBoundSquared,
        CancellationToken cancellationToken)
    {
        visited[seedPixel] = true;
        frontierQueue.Enqueue(seedPixel);

        var regionPartition = disjointSet.GetPartition(seedPixel);
        Lab32 seedColor = modes[seedPixel].Color;
        Vector3D colorSum = seedColor.ToVector3D();
        int memberCount = 1;
        int dequeuedCount = 0;

        while (frontierQueue.Count > 0)
        {
            if ((++dequeuedCount & CancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            int currentPixel = frontierQueue.Dequeue();
            Coord current = size.CoordOf(currentPixel);
            MeanShiftMode currentMode = modes[currentPixel];
            Lab32 regionMeanColor = ComputeMeanColor(colorSum, memberCount);

            for (int n = 0; n < neighbors.Length; n++)
            {
                Coord neighbor = current + neighbors[n].Offset;
                if (!size.Contains(neighbor))
                    continue;

                int neighborPixel = size.IndexOf(neighbor);
                if (visited[neighborPixel])
                    continue;

                MeanShiftMode neighborMode = modes[neighborPixel];
                if (!CanMergeNeighborMode(
                    currentMode,
                    neighborMode,
                    regionMeanColor,
                    seedColor,
                    spatialRadiusSquared,
                    rangeRadiusSquared,
                    seedColorBoundSquared))
                {
                    continue;
                }

                visited[neighborPixel] = true;
                regionPartition = disjointSet.Union(regionPartition, disjointSet.GetPartition(neighborPixel), 0);
                colorSum += neighborMode.Color.ToVector3D();
                memberCount++;
                regionMeanColor = ComputeMeanColor(colorSum, memberCount);
                frontierQueue.Enqueue(neighborPixel);
            }
        }
    }

    private static Lab32 ComputeMeanColor(Vector3D colorSum, int memberCount)
    {
        Vector3D mean = colorSum * (1.0 / memberCount);
        return new Lab32((float)mean.X, (float)mean.Y, (float)mean.Z);
    }

    private static bool CanMergeNeighborMode(
        MeanShiftMode currentMode,
        MeanShiftMode neighborMode,
        Lab32 regionMeanColor,
        Lab32 seedColor,
        double spatialRadiusSquared,
        double rangeRadiusSquared,
        double seedColorBoundSquared) =>
        currentMode.SpatialDistanceSquared(neighborMode) <= spatialRadiusSquared &&
        currentMode.Color.EuclideanDistanceSquared(neighborMode.Color) <= rangeRadiusSquared &&
        neighborMode.Color.EuclideanDistanceSquared(regionMeanColor) <= rangeRadiusSquared &&
        neighborMode.Color.EuclideanDistanceSquared(seedColor) <= seedColorBoundSquared;

    private static void PruneSmallRegions(
        DisjointSet<byte> disjointSet,
        Lab32[] sourcePixels,
        Size size,
        MeanShiftOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RegionGraph regionGraph = disjointSet.ToRegionGraph(options.Connectivity);
        int regionCount = regionGraph.RegionCount;
        if (regionCount <= 1 || regionGraph.Boundaries.Count == 0)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        var sourceMatrix = new SequentialMatrix<Lab32>(size.Width, size.Height, sourcePixels);
        RegionColorStatistics<Lab32>[] initialStats = RegionColorStatistics.Compute(regionGraph, sourceMatrix);

        var regionParent = new int[regionCount];
        var regionSizes = new int[regionCount];
        var regionColorSums = new Vector3D[regionCount];
        var regionMeanColors = new Lab32[regionCount];
        var representativePixels = new int[regionCount];

        for (int r = 0; r < regionCount; r++)
        {
            if ((r & CancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            regionParent[r] = r;
            Region<Coord> region = regionGraph.Regions[r];
            int pixelCount = region.Pixels.Count;
            regionSizes[r] = pixelCount;
            Lab32 meanColor = initialStats[r].MeanColor;
            regionMeanColors[r] = meanColor;
            regionColorSums[r] = meanColor.ToVector3D() * pixelCount;

            foreach (Coord coord in region.Pixels)
            {
                representativePixels[r] = size.IndexOf(coord);
                break;
            }
        }

        int minRegionSize = options.MinRegionSize;
        IReadOnlyList<RegionBoundary> initialBoundaries = regionGraph.Boundaries;
        var activeEdges = new List<(int First, int Second)>(initialBoundaries.Count);
        for (int i = 0; i < initialBoundaries.Count; i++)
        {
            if ((i & CancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            RegionBoundary boundary = initialBoundaries[i];
            if (regionSizes[boundary.First] < minRegionSize || regionSizes[boundary.Second] < minRegionSize)
                activeEdges.Add((boundary.First, boundary.Second));
        }

        if (activeEdges.Count == 0)
            return;

        var seenPairs = new HashSet<ulong>();
        var candidateEdges = new List<UndirectedGraphEdge<int>>(activeEdges.Count);

        while (activeEdges.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            seenPairs.Clear();
            candidateEdges.Clear();

            for (int i = 0; i < activeEdges.Count; i++)
            {
                if ((i & CancellationCheckMask) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                (int rawFirst, int rawSecond) = activeEdges[i];
                int rootA = FindRegionRoot(regionParent, rawFirst);
                int rootB = FindRegionRoot(regionParent, rawSecond);
                if (rootA == rootB)
                    continue;

                ulong pairKey = GridNeighborhood.PackUnorderedPairKey(rootA, rootB);
                if (!seenPairs.Add(pairKey))
                    continue;

                int firstRoot = (int)(pairKey >> 32);
                int secondRoot = (int)(uint)pairKey;
                if (regionSizes[firstRoot] >= minRegionSize && regionSizes[secondRoot] >= minRegionSize)
                    continue;

                double weight = regionMeanColors[firstRoot].EuclideanDistance(regionMeanColors[secondRoot]);
                candidateEdges.Add(new UndirectedGraphEdge<int>(firstRoot, secondRoot, weight));
            }

            if (candidateEdges.Count == 0)
                break;

            cancellationToken.ThrowIfCancellationRequested();
            candidateEdges.Sort(CompareRegionEdges);

            bool mergedInPass = false;
            activeEdges.Clear();
            for (int i = 0; i < candidateEdges.Count; i++)
            {
                if ((i & CancellationCheckMask) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                UndirectedGraphEdge<int> edge = candidateEdges[i];
                int r1 = FindRegionRoot(regionParent, edge.First);
                int r2 = FindRegionRoot(regionParent, edge.Second);
                if (r1 == r2 || (regionSizes[r1] >= minRegionSize && regionSizes[r2] >= minRegionSize))
                    continue;

                (int survivor, int absorbed) = regionSizes[r1] >= regionSizes[r2] ? (r1, r2) : (r2, r1);
                regionParent[absorbed] = survivor;
                int combinedSize = regionSizes[survivor] + regionSizes[absorbed];
                regionSizes[survivor] = combinedSize;
                regionColorSums[survivor] += regionColorSums[absorbed];
                regionMeanColors[survivor] = ComputeMeanColor(regionColorSums[survivor], combinedSize);

                disjointSet.TryUnion(representativePixels[survivor], representativePixels[absorbed], 0, out _);
                mergedInPass = true;
            }

            if (!mergedInPass)
                break;

            for (int i = 0; i < candidateEdges.Count; i++)
            {
                if ((i & CancellationCheckMask) == 0)
                    cancellationToken.ThrowIfCancellationRequested();

                UndirectedGraphEdge<int> edge = candidateEdges[i];
                int r1 = FindRegionRoot(regionParent, edge.First);
                int r2 = FindRegionRoot(regionParent, edge.Second);
                if (r1 != r2 && (regionSizes[r1] < minRegionSize || regionSizes[r2] < minRegionSize))
                    activeEdges.Add((r1, r2));
            }
        }
    }

    private static int FindRegionRoot(int[] regionParent, int regionId)
    {
        int root = regionId;
        while (regionParent[root] != root)
            root = regionParent[root];

        int current = regionId;
        while (current != root)
        {
            int next = regionParent[current];
            regionParent[current] = root;
            current = next;
        }

        return root;
    }

    private static int CompareRegionEdges(UndirectedGraphEdge<int> a, UndirectedGraphEdge<int> b)
    {
        int cmp = a.Weight.CompareTo(b.Weight);
        if (cmp != 0)
            return cmp;
        cmp = a.First.CompareTo(b.First);
        return cmp != 0 ? cmp : a.Second.CompareTo(b.Second);
    }
}
