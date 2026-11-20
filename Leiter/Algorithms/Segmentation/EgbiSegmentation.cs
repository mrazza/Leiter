namespace Leiter.Algorithms.Segmentation;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

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

        EgbiOptions resolvedOptions = OptionsResolver.Resolve(options, EgbiOptions.Default);

        double kFactor = resolvedOptions.KFactor;
        double epsilon = resolvedOptions.Epsilon;
        int minRegionSize = resolvedOptions.MinRegionSize;

        var disjointSet = new DisjointSet<PartitionInfo>(image, (_) => new PartitionInfo(kFactor));
        if (image.Count == 0)
            return disjointSet;

        UndirectedGraphEdge<Coord>[] graphEdges = BuildSortedGraphEdges(image, resolvedOptions.Connectivity);

        // Reuse graphEdges in place for deferred edges: Segment owns this array exclusively,
        // and deferredCount <= i holds on every iteration so unread entries are never overwritten.
        // This avoids both per-edge LinkedListNode heap allocations / pointer chasing and
        // Large Object Heap array resizes from growing a separate List<UndirectedGraphEdge<Coord>>.
        int deferredCount = 0;
        for (int i = 0; i < graphEdges.Length; i++)
        {
            UndirectedGraphEdge<Coord> edge = graphEdges[i];
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
                graphEdges[deferredCount++] = edge;
            }
        }

        if (minRegionSize > 0 && deferredCount > 0)
        {
            MergeSmallPartitions(disjointSet, graphEdges.AsSpan(0, deferredCount), minRegionSize, kFactor);
        }

        return disjointSet;
    }

    private static UndirectedGraphEdge<Coord>[] BuildSortedGraphEdges<T>(IReadOnlyMatrix<T> image, GridConnectivity connectivity)
        where T : struct, IPixel<T>
    {
        int edgesPerPixel = connectivity == GridConnectivity.EightConnected ? 4 : 2;
        var rowEdges = new List<UndirectedGraphEdge<Coord>>[image.Height];
        Parallel.For(0, image.Height, y =>
        {
            ReadOnlySpan<GridNeighbor> forwardNeighbors = GridNeighborhood.GetForward(connectivity);
            var localEdges = new List<UndirectedGraphEdge<Coord>>(image.Width * edgesPerPixel);
            for (int x = 0; x < image.Width; x++)
            {
                var self = new Coord(x, y);
                T selfPixel = image[self];
                for (int n = 0; n < forwardNeighbors.Length; n++)
                {
                    Coord other = self + forwardNeighbors[n].Offset;
                    if (!image.Contains(other))
                        continue;

                    localEdges.Add(new UndirectedGraphEdge<Coord>
                    {
                        First = self,
                        Second = other,
                        Weight = selfPixel.Distance(image[other]),
                    });
                }
            }

            rowEdges[y] = localEdges;
        });

        int totalEdges = 0;
        for (int y = 0; y < image.Height; y++)
            totalEdges += rowEdges[y].Count;

        var graphEdges = new UndirectedGraphEdge<Coord>[totalEdges];
        int offset = 0;
        for (int y = 0; y < image.Height; y++)
        {
            rowEdges[y].CopyTo(graphEdges, offset);
            offset += rowEdges[y].Count;
        }

        Array.Sort(graphEdges, CompareEdges);
        return graphEdges;
    }

    private static int CompareEdges(UndirectedGraphEdge<Coord> a, UndirectedGraphEdge<Coord> b)
    {
        int cmp = a.Weight.CompareTo(b.Weight);
        if (cmp != 0) return cmp;
        cmp = ComputeEdgeTieBreaker(a).CompareTo(ComputeEdgeTieBreaker(b));
        if (cmp != 0) return cmp;
        cmp = a.First.Y.CompareTo(b.First.Y);
        if (cmp != 0) return cmp;
        cmp = a.First.X.CompareTo(b.First.X);
        if (cmp != 0) return cmp;
        cmp = a.Second.Y.CompareTo(b.Second.Y);
        return cmp != 0 ? cmp : a.Second.X.CompareTo(b.Second.X);
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
        ReadOnlySpan<UndirectedGraphEdge<Coord>> deferredEdges,
        int minRegionSize,
        double kFactor)
    {
        foreach (ref readonly UndirectedGraphEdge<Coord> edge in deferredEdges)
        {
            var firstPartition = disjointSet.GetPartition(edge.First);
            var secondPartition = disjointSet.GetPartition(edge.Second);

            if (firstPartition.RootIndex == secondPartition.RootIndex)
                continue;

            if (firstPartition.Size < minRegionSize || secondPartition.Size < minRegionSize)
                MergePartitions(disjointSet, firstPartition, secondPartition, edge.Weight, kFactor);
        }
    }

    /// <summary>
    /// Computes a deterministic, fixed-seed SplitMix64 tie-breaker from edge endpoint coordinates
    /// so equal-weight edges break ties pseudo-randomly rather than sweeping in scanline order.
    /// </summary>
    /// <param name="edge">The graph edge whose endpoints are hashed.</param>
    /// <returns>A 64-bit pseudo-random hash value for tie-breaking.</returns>
    private static ulong ComputeEdgeTieBreaker(UndirectedGraphEdge<Coord> edge) =>
        SplitMix64.HashCoordinates(edge.First.X, edge.First.Y, edge.Second.X, edge.Second.Y);
}