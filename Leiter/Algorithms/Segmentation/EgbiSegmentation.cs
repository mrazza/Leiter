namespace Leiter.Algorithms.Segmentation;

using System;
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
    /// <summary>
    /// Internal data structure used to track the internal difference of partitions/regions of the segmented image.
    /// </summary>
    /// <param name="InternalDifference">The maximum edge weight in the minimum spanning tree of the partition plus threshold penalty.</param>
    private readonly record struct PartitionInfo(double InternalDifference);

    private readonly record struct SortableEdge(UndirectedGraphEdge<int> Edge, ulong TieBreaker);

    private sealed class EdgeBuildState<T>(
        Size size,
        IReadOnlyMatrix<T> image,
        T[]? rawPixels,
        SortableEdge[] edges,
        int[] rowCursors)
        where T : struct, IPixel<T>
    {
        public readonly Size Size = size;
        public readonly IReadOnlyMatrix<T> Image = image;
        public readonly T[]? RawPixels = rawPixels;
        public readonly SortableEdge[] Edges = edges;
        public readonly int[] RowCursors = rowCursors;
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
    /// <returns>An <see cref="IDisjointSet"/> representing the partitioned regions of the image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any option in <paramref name="options"/> is invalid.</exception>
    public static IDisjointSet Segment<T>(IReadOnlyMatrix<T> image, EgbiOptions? options = null)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(image);

        EgbiOptions resolvedOptions = OptionsResolver.Resolve(options);

        double kFactor = resolvedOptions.KFactor;
        double epsilon = resolvedOptions.Epsilon;
        int minRegionSize = resolvedOptions.MinRegionSize;

        var disjointSet = new DisjointSet<PartitionInfo>(image, (_) => new PartitionInfo(kFactor));
        if (image.Count == 0)
            return disjointSet;

        SortableEdge[] graphEdges = BuildSortedGraphEdges(image, resolvedOptions.Connectivity);

        // Reuse graphEdges in place for deferred edges: Segment owns this array exclusively,
        // and deferredCount <= i holds on every iteration so unread entries are never overwritten.
        int deferredCount = 0;
        for (int i = 0; i < graphEdges.Length; i++)
        {
            UndirectedGraphEdge<int> edge = graphEdges[i].Edge;
            var firstPartition = disjointSet.GetPartition(edge.First);
            var secondPartition = disjointSet.GetPartition(edge.Second);

            if (firstPartition.RootIndex == secondPartition.RootIndex)
                continue;

            if (ShouldMergePartitions(edge.Weight, firstPartition, secondPartition, epsilon))
            {
                MergePartitions(disjointSet, firstPartition, secondPartition, edge.Weight, kFactor);
            }
            else if (minRegionSize > 0)
            {
                graphEdges[deferredCount++] = graphEdges[i];
            }
        }

        if (minRegionSize > 0 && deferredCount > 0)
        {
            MergeSmallPartitions(disjointSet, graphEdges.AsSpan(0, deferredCount), minRegionSize, kFactor);
        }

        return disjointSet;
    }

    private static SortableEdge[] BuildSortedGraphEdges<T>(IReadOnlyMatrix<T> image, GridConnectivity connectivity)
        where T : struct, IPixel<T>
    {
        Size size = image.Size;
        int width = size.Width;
        int height = size.Height;
        int rowEdgeStride = connectivity == GridConnectivity.EightConnected ? (4 * width) - 3 : (2 * width) - 1;
        int totalEdges = ((height - 1) * rowEdgeStride) + (width - 1);
        if (totalEdges <= 0)
            return [];

        var rowCursors = new int[height];
        for (int y = 0; y < height; y++)
            rowCursors[y] = y * rowEdgeStride;

        var edges = new SortableEdge[totalEdges];
        T[]? rawPixels = image is SequentialMatrix<T> sequential ? sequential.RawData : null;
        var state = new EdgeBuildState<T>(size, image, rawPixels, edges, rowCursors);

        GridNeighborhood.ForEachForwardPairParallel(
            size,
            connectivity,
            state,
            static (first, second, _, s) =>
            {
                int firstIndex = s.Size.IndexOf(first);
                int secondIndex = s.Size.IndexOf(second);
                double weight = s.RawPixels is not null
                    ? s.RawPixels[firstIndex].Distance(s.RawPixels[secondIndex])
                    : s.Image[firstIndex].Distance(s.Image[secondIndex]);

                int writeIndex = s.RowCursors[first.Y]++;
                ulong tieBreaker = SplitMix64.HashCoordinates(first.X, first.Y, second.X, second.Y);
                s.Edges[writeIndex] = new SortableEdge(
                    new UndirectedGraphEdge<int>(firstIndex, secondIndex, weight),
                    tieBreaker);
            });

        Array.Sort(edges, CompareEdges);
        return edges;
    }

    private static int CompareEdges(SortableEdge a, SortableEdge b)
    {
        int cmp = a.Edge.Weight.CompareTo(b.Edge.Weight);
        if (cmp != 0)
            return cmp;

        cmp = a.TieBreaker.CompareTo(b.TieBreaker);
        if (cmp != 0)
            return cmp;

        cmp = a.Edge.First.CompareTo(b.Edge.First);
        return cmp != 0 ? cmp : a.Edge.Second.CompareTo(b.Edge.Second);
    }

    private static bool ShouldMergePartitions(
        double edgeWeight,
        DisjointSet<PartitionInfo>.Partition first,
        DisjointSet<PartitionInfo>.Partition second,
        double epsilon) =>
        edgeWeight <= epsilon ||
        (edgeWeight <= first.Value.InternalDifference && edgeWeight <= second.Value.InternalDifference);

    private static void MergePartitions(
        DisjointSet<PartitionInfo> disjointSet,
        DisjointSet<PartitionInfo>.Partition first,
        DisjointSet<PartitionInfo>.Partition second,
        double edgeWeight,
        double kFactor)
    {
        int combinedSize = first.Size + second.Size;
        disjointSet.Union(first, second, new PartitionInfo(edgeWeight + (kFactor / combinedSize)));
    }

    private static void MergeSmallPartitions(
        DisjointSet<PartitionInfo> disjointSet,
        ReadOnlySpan<SortableEdge> deferredEdges,
        int minRegionSize,
        double kFactor)
    {
        foreach (ref readonly SortableEdge item in deferredEdges)
        {
            UndirectedGraphEdge<int> edge = item.Edge;
            var firstPartition = disjointSet.GetPartition(edge.First);
            var secondPartition = disjointSet.GetPartition(edge.Second);

            if (firstPartition.RootIndex == secondPartition.RootIndex)
                continue;

            if (firstPartition.Size < minRegionSize || secondPartition.Size < minRegionSize)
                MergePartitions(disjointSet, firstPartition, secondPartition, edge.Weight, kFactor);
        }
    }
}