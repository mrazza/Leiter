namespace Leiter.Algorithms.Segmentation;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Efficient Graph-Based Image Segmentation
/// </summary>
/// <remarks>
/// This class implements a derivative of the Efficient Graph-Based Image Segmentation
/// algorithm by Pedro F. Felzenszwalb and Daniel P. Huttenlocher.
///
/// Felzenszwalb, P.F., Huttenlocher, D.P. Efficient Graph-Based Image Segmentation.
/// International Journal of Computer Vision 59, 167–181 (2004).
/// https://doi.org/10.1023/B:VISI.0000022288.19776.77
/// </remarks>
public static class EgbiSegmentation
{
    private const int CancellationCheckMask = 4095;
    private readonly record struct PartitionInfo(double InternalDifference);

    private readonly struct EgbiEdgeComparer : IComparer<UndirectedGraphEdge<int>>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(UndirectedGraphEdge<int> a, UndirectedGraphEdge<int> b)
        {
            int cmp = a.Weight.CompareTo(b.Weight);
            if (cmp != 0)
                return cmp;

            ulong aTieBreaker = SplitMix64.Mix(((ulong)(uint)a.First << 32) | (uint)a.Second);
            ulong bTieBreaker = SplitMix64.Mix(((ulong)(uint)b.First << 32) | (uint)b.Second);
            cmp = aTieBreaker.CompareTo(bTieBreaker);
            if (cmp != 0)
                return cmp;

            cmp = a.First.CompareTo(b.First);
            return cmp != 0 ? cmp : a.Second.CompareTo(b.Second);
        }
    }

    /// <summary>
    /// Segments an image using the Efficient Graph-Based Image Segmentation (EGBI) algorithm.
    /// </summary>
    /// <remarks>
    /// This method performs the segmentation and returns a disjoint-set partition, one region per segment.
    /// It is recommended to perform a Gaussian or similar blur to the image before segmentation to
    /// remove compression or other artifacts which may show up in the resulting segmentation. The
    /// original paper recommends a Gaussian blur with a sigma value of 0.8.
    ///
    /// Default values in <see cref="EgbiOptions"/> are tuned for larger images in the L*a*b* color space
    /// using the CIEDE2000 color difference method. For segmentation using other color spaces or
    /// differencing methodologies, customize the options accordingly.
    /// </remarks>
    /// <typeparam name="T">The type of the pixels for the underlying image.</typeparam>
    /// <param name="image">The image to segment.</param>
    /// <param name="options">The EGBI configuration options, or <see langword="null"/> to use default options.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An <see cref="IDisjointSet"/> representing the partitioned regions of the image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static IDisjointSet Segment<T>(
        IReadOnlyMatrix<T> image,
        EgbiOptions? options = null,
        CancellationToken cancellationToken = default)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(image);

        EgbiOptions resolvedOptions = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        double kFactor = resolvedOptions.KFactor;
        double epsilon = resolvedOptions.Epsilon;
        int minRegionSize = resolvedOptions.MinRegionSize;

        var disjointSet = new DisjointSet<PartitionInfo>(image, new PartitionInfo(kFactor));
        if (image.Count == 0)
            return disjointSet;

        UndirectedGraphEdge<int>[] graphEdges = BuildSortedGraphEdges(image, resolvedOptions.Connectivity, cancellationToken);

        // Reuse graphEdges in place for deferred edges: Segment owns this array exclusively,
        // and deferredCount <= i holds on every iteration so unread entries are never overwritten.
        int deferredCount = 0;
        for (int i = 0; i < graphEdges.Length; i++)
        {
            if ((i & CancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            UndirectedGraphEdge<int> edge = graphEdges[i];
            int firstRoot = disjointSet.FindRoot(edge.First);
            int secondRoot = disjointSet.FindRoot(edge.Second);

            if (firstRoot == secondRoot)
                continue;

            int firstSize = disjointSet.GetRootSize(firstRoot);
            int secondSize = disjointSet.GetRootSize(secondRoot);

            if (ShouldMergePartitions(disjointSet, firstRoot, secondRoot, edge.Weight, epsilon))
            {
                MergePartitions(disjointSet, firstRoot, secondRoot, firstSize, secondSize, edge.Weight, kFactor);
            }
            else if (minRegionSize > 0 && (firstSize < minRegionSize || secondSize < minRegionSize))
            {
                // Component sizes grow monotonically, so an edge whose endpoints already satisfy minRegionSize can never merge in Pass 2.
                graphEdges[deferredCount++] = edge;
            }
        }

        if (minRegionSize > 0 && deferredCount > 0)
        {
            MergeSmallPartitions(disjointSet, graphEdges.AsSpan(0, deferredCount), minRegionSize, kFactor, cancellationToken);
        }

        return disjointSet;
    }

    private static UndirectedGraphEdge<int>[] BuildSortedGraphEdges<T>(
        IReadOnlyMatrix<T> image,
        GridConnectivity connectivity,
        CancellationToken cancellationToken)
        where T : struct, IPixel<T>
    {
        Size size = image.Size;
        int width = size.Width;
        int height = size.Height;
        bool isEightConnected = connectivity == GridConnectivity.EightConnected;
        int rowEdgeStride = isEightConnected ? (4 * width) - 3 : (2 * width) - 1;
        int totalEdges = ((height - 1) * rowEdgeStride) + (width - 1);
        if (totalEdges <= 0)
            return [];

        var edges = new UndirectedGraphEdge<int>[totalEdges];
        if (image.TryGetRawArray() is T[] raw)
        {
            var source = new ArrayPixelSource<T>(raw);
            ParallelRunner.For(
                height,
                cancellationToken,
                y => PopulateRowEdges<T, ArrayPixelSource<T>>(source, size, y, isEightConnected, edges, rowEdgeStride));
        }
        else
        {
            var source = new MatrixPixelSource<T>(image);
            ParallelRunner.For(
                height,
                cancellationToken,
                y => PopulateRowEdges<T, MatrixPixelSource<T>>(source, size, y, isEightConnected, edges, rowEdgeStride));
        }

        return ParallelSort.SortAndReturn(edges, new EgbiEdgeComparer(), cancellationToken);
    }

    private static void PopulateRowEdges<T, TSource>(
        TSource source,
        Size size,
        int y,
        bool isEightConnected,
        UndirectedGraphEdge<int>[] edges,
        int rowEdgeStride)
        where T : struct, IPixel<T>
        where TSource : struct, IPixelSource<T>
    {
        int width = size.Width;
        int rowBase = size.IndexOf(0, y);
        bool hasSouth = y + 1 < size.Height;
        int writeIndex = y * rowEdgeStride;

        for (int x = 0; x < width; x++)
        {
            int firstIndex = rowBase + x;
            T firstPixel = source[firstIndex];
            bool hasEast = x + 1 < width;

            if (hasEast)
            {
                int eastIndex = firstIndex + 1;
                edges[writeIndex++] = new UndirectedGraphEdge<int>(firstIndex, eastIndex, firstPixel.Distance(source[eastIndex]));
            }

            if (hasSouth)
            {
                int southIndex = firstIndex + width;
                edges[writeIndex++] = new UndirectedGraphEdge<int>(firstIndex, southIndex, firstPixel.Distance(source[southIndex]));

                if (isEightConnected)
                {
                    if (hasEast)
                    {
                        int southEastIndex = southIndex + 1;
                        edges[writeIndex++] = new UndirectedGraphEdge<int>(firstIndex, southEastIndex, firstPixel.Distance(source[southEastIndex]));
                    }

                    if (x > 0)
                    {
                        int southWestIndex = southIndex - 1;
                        edges[writeIndex++] = new UndirectedGraphEdge<int>(firstIndex, southWestIndex, firstPixel.Distance(source[southWestIndex]));
                    }
                }
            }
        }

        Debug.Assert(writeIndex == (y * rowEdgeStride) + (hasSouth ? rowEdgeStride : width - 1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldMergePartitions(
        DisjointSet<PartitionInfo> disjointSet,
        int firstRoot,
        int secondRoot,
        double edgeWeight,
        double epsilon) =>
        edgeWeight <= epsilon ||
        (edgeWeight <= disjointSet.GetRootValue(firstRoot).InternalDifference &&
         edgeWeight <= disjointSet.GetRootValue(secondRoot).InternalDifference);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void MergePartitions(
        DisjointSet<PartitionInfo> disjointSet,
        int firstRoot,
        int secondRoot,
        int firstSize,
        int secondSize,
        double edgeWeight,
        double kFactor)
    {
        int combinedSize = firstSize + secondSize;
        disjointSet.MergeRootsUnchecked(firstRoot, secondRoot, new PartitionInfo(edgeWeight + (kFactor / combinedSize)));
    }

    private static void MergeSmallPartitions(
        DisjointSet<PartitionInfo> disjointSet,
        ReadOnlySpan<UndirectedGraphEdge<int>> deferredEdges,
        int minRegionSize,
        double kFactor,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < deferredEdges.Length; i++)
        {
            if ((i & CancellationCheckMask) == 0)
                cancellationToken.ThrowIfCancellationRequested();

            UndirectedGraphEdge<int> edge = deferredEdges[i];
            int firstRoot = disjointSet.FindRoot(edge.First);
            int secondRoot = disjointSet.FindRoot(edge.Second);

            if (firstRoot == secondRoot)
                continue;

            int firstSize = disjointSet.GetRootSize(firstRoot);
            int secondSize = disjointSet.GetRootSize(secondRoot);

            if (firstSize < minRegionSize || secondSize < minRegionSize)
                MergePartitions(disjointSet, firstRoot, secondRoot, firstSize, secondSize, edge.Weight, kFactor);
        }
    }
}