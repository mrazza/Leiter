namespace Leiter.Algorithms.Segmentation.Internal;

using System;
using System.Collections.Generic;
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

    private readonly struct RegionColorAccumulator(Vector3D[] sums)
    {
        public static RegionColorAccumulator Build(DisjointSet<byte> disjointSet, ReadOnlySpan<Lab32> sourcePixels)
        {
            int pixelCount = sourcePixels.Length;
            var sums = new Vector3D[pixelCount];

            for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
            {
                int rootIndex = disjointSet.GetPartition(pixelIndex).RootIndex;
                sums[rootIndex] += ToColorVector(sourcePixels[pixelIndex]);
            }

            return new RegionColorAccumulator(sums);
        }

        public Lab32 GetMeanColor(int rootIndex, int regionSize) =>
            ComputeMeanColor(sums[rootIndex], regionSize);

        public void Absorb(int targetRoot, int absorbedRoot) =>
            sums[targetRoot] += sums[absorbedRoot];
    }

    public static IDisjointSet ClusterModes(
        IUntypedMatrix targetImage,
        ReadOnlySpan<Lab32> sourcePixels,
        MeanShiftMode[] modes,
        MeanShiftOptions options)
    {
        Size size = targetImage.Size;
        int pixelCount = size.Count;
        var disjointSet = DisjointSet.CreateSingletons(targetImage);

        if (pixelCount <= 1)
            return disjointSet;

        int[] orderedPixels = OrderPixelsByModeGradient(modes, size);
        ClusterDensityBasins(disjointSet, modes, orderedPixels, size, options);

        if (options.MinRegionSize > 1)
        {
            PruneSmallRegions(disjointSet, sourcePixels, size, options);
        }

        return disjointSet;
    }

    private static int[] OrderPixelsByModeGradient(MeanShiftMode[] modes, Size size)
    {
        int pixelCount = size.Count;
        double[] gradientSquared = ComputeMaxCardinalGradientSquared(modes, size);

        var bucketCounts = new int[GradientBucketCount];
        for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
        {
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
            int bucket = GetGradientBucketIndex(gradientSquared[pixelIndex]);
            orderedPixels[bucketWriteOffsets[bucket]++] = pixelIndex;
        }

        return orderedPixels;
    }

    private static double[] ComputeMaxCardinalGradientSquared(MeanShiftMode[] modes, Size size)
    {
        int width = size.Width;
        int height = size.Height;
        ReadOnlySpan<GridNeighbor> cardinalNeighbors = GridNeighborhood.All4Connected;
        var gradientSquared = new double[size.Count];

        for (int y = 0; y < height; y++)
        {
            int rowStart = size.IndexOf(0, y);
            for (int x = 0; x < width; x++)
            {
                int pixelIndex = rowStart + x;
                Lab32 centerColor = modes[pixelIndex].Color;
                double maxDifferenceSquared = 0.0;

                for (int n = 0; n < cardinalNeighbors.Length; n++)
                {
                    int nx = x + cardinalNeighbors[n].Offset.X;
                    int ny = y + cardinalNeighbors[n].Offset.Y;
                    if (!size.Contains(nx, ny))
                        continue;

                    double differenceSquared = centerColor.EuclideanDistanceSquared(modes[size.IndexOf(nx, ny)].Color);
                    if (differenceSquared > maxDifferenceSquared)
                        maxDifferenceSquared = differenceSquared;
                }

                gradientSquared[pixelIndex] = maxDifferenceSquared;
            }
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
        MeanShiftOptions options)
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
                seedColorBoundSquared);
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
        double seedColorBoundSquared)
    {
        visited[seedPixel] = true;
        frontierQueue.Enqueue(seedPixel);

        var regionPartition = disjointSet.GetPartition(seedPixel);
        Lab32 seedColor = modes[seedPixel].Color;
        Vector3D colorSum = ToColorVector(seedColor);
        int memberCount = 1;

        while (frontierQueue.Count > 0)
        {
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
                colorSum += ToColorVector(neighborMode.Color);
                memberCount++;
                regionMeanColor = ComputeMeanColor(colorSum, memberCount);
                frontierQueue.Enqueue(neighborPixel);
            }
        }
    }

    private static Vector3D ToColorVector(Lab32 color) => new(color.L, color.A, color.B);

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
        ReadOnlySpan<Lab32> sourcePixels,
        Size size,
        MeanShiftOptions options)
    {
        int minRegionSize = options.MinRegionSize;
        ReadOnlySpan<GridNeighbor> forwardNeighbors = GridNeighborhood.GetForward(options.Connectivity);
        RegionColorAccumulator colorAccumulator = RegionColorAccumulator.Build(disjointSet, sourcePixels);

        var seenPairs = new HashSet<ulong>();
        var candidateEdges = new List<UndirectedGraphEdge<int>>();

        while (true)
        {
            CollectSmallRegionBoundaryEdges(
                disjointSet,
                in colorAccumulator,
                size,
                minRegionSize,
                forwardNeighbors,
                seenPairs,
                candidateEdges);

            if (candidateEdges.Count == 0)
                break;

            if (!MergeSmallRegionEdges(disjointSet, in colorAccumulator, minRegionSize, candidateEdges))
                break;
        }
    }

    private static void CollectSmallRegionBoundaryEdges(
        DisjointSet<byte> disjointSet,
        in RegionColorAccumulator colorAccumulator,
        Size size,
        int minRegionSize,
        ReadOnlySpan<GridNeighbor> forwardNeighbors,
        HashSet<ulong> seenPairs,
        List<UndirectedGraphEdge<int>> candidateEdges)
    {
        seenPairs.Clear();
        candidateEdges.Clear();

        int width = size.Width;
        int height = size.Height;

        for (int y = 0; y < height; y++)
        {
            int rowStart = size.IndexOf(0, y);
            for (int x = 0; x < width; x++)
            {
                int rootP = disjointSet.GetPartition(rowStart + x).RootIndex;

                for (int n = 0; n < forwardNeighbors.Length; n++)
                {
                    int nx = x + forwardNeighbors[n].Offset.X;
                    int ny = y + forwardNeighbors[n].Offset.Y;
                    if (!size.Contains(nx, ny))
                        continue;

                    int rootQ = disjointSet.GetPartition(size.IndexOf(nx, ny)).RootIndex;
                    if (rootP == rootQ)
                        continue;

                    (int firstRoot, int secondRoot) = rootP < rootQ ? (rootP, rootQ) : (rootQ, rootP);
                    ulong pairKey = ScalarMath.PackUnorderedPairKey(firstRoot, secondRoot);
                    if (!seenPairs.Add(pairKey))
                        continue;

                    var part1 = disjointSet.GetPartition(firstRoot);
                    var part2 = disjointSet.GetPartition(secondRoot);
                    if (part1.Size >= minRegionSize && part2.Size >= minRegionSize)
                        continue;

                    Lab32 mean1 = colorAccumulator.GetMeanColor(firstRoot, part1.Size);
                    Lab32 mean2 = colorAccumulator.GetMeanColor(secondRoot, part2.Size);
                    candidateEdges.Add(new UndirectedGraphEdge<int>
                    {
                        First = firstRoot,
                        Second = secondRoot,
                        Weight = mean1.EuclideanDistance(mean2)
                    });
                }
            }
        }
    }

    private static bool MergeSmallRegionEdges(
        DisjointSet<byte> disjointSet,
        in RegionColorAccumulator colorAccumulator,
        int minRegionSize,
        List<UndirectedGraphEdge<int>> candidateEdges)
    {
        var sortedEdges = candidateEdges.ToArray();
        Array.Sort(sortedEdges, CompareRegionEdges);

        bool mergedInPass = false;
        for (int i = 0; i < sortedEdges.Length; i++)
        {
            UndirectedGraphEdge<int> edge = sortedEdges[i];
            var p1 = disjointSet.GetPartition(edge.First);
            var p2 = disjointSet.GetPartition(edge.Second);

            if (p1.RootIndex != p2.RootIndex && (p1.Size < minRegionSize || p2.Size < minRegionSize))
            {
                int oldRoot1 = p1.RootIndex;
                int oldRoot2 = p2.RootIndex;
                var merged = disjointSet.Union(p1, p2, 0);
                int newRoot = merged.RootIndex;
                int absorbedRoot = newRoot == oldRoot1 ? oldRoot2 : oldRoot1;
                colorAccumulator.Absorb(newRoot, absorbedRoot);
                mergedInPass = true;
            }
        }

        return mergedInPass;
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
