namespace Leiter.Algorithms.Segmentation;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
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

    /// <summary>
    /// Segments an image using the Efficient Graph-Based Image Segmentation (EGBI) algorithm.
    /// </summary>
    /// <remarks>
    /// This method performs the segmentation and returns a set of regions, one for each segment.
    /// It is recommended to perform a Gaussian or similar blur to the image before segmentation to
    /// remove compression or other artifacts which may show up in the resulting segmentation. The
    /// original paper recommends a Gaussian blur with a sigma value of 0.8.
    ///
    /// This method has a number of configurable constants that can be used to tune the final output.
    /// The default values are tuned for larger images in the L*a*b* color space using the CIEDE2000
    /// color difference method. For segmentation using other color spaces or different differencing
    /// methodologies, they may not produce good results.
    ///
    /// <c>epsilon</c> is a non-standard EGBI parameter which controls how pixel difference is interpreted.
    /// If two adjacent pixels have a color difference of less than epsilon, they will, necessarily,
    /// be part of the same segment.
    ///
    /// <c>minSegmentSize</c> is a non-standard EGBI parameter. If, after running the EGBI algorithm, a
    /// segment is less than <c>minSegmentSize</c> it will be joined with its most similar neighboring pixel.
    /// </remarks>
    /// <typeparam name="T">The type of the pixels for the underlying image.</typeparam>
    /// <param name="image">The image to segment.</param>
    /// <param name="kFactor">The k value of the EGBI algorithm. Larger values will bias the system towards larger segment regions.</param>
    /// <param name="epsilon">Specifies the minimum distance for two pixels to be considered different.</param>
    /// <param name="minSegmentSize">The minimum size for any one segment.</param>
    /// <returns>A collection of regions (themselves a collection of pixel coordinates); one for each segment.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    public static IDisjointSet Segment<T>(IReadOnlyMatrix<T> image, double kFactor = 100.0, double epsilon = 1.0, int minSegmentSize = 100)
        where T : struct, IPixel<T>
    {
        ArgumentNullException.ThrowIfNull(image);

        var disjointSet = new DisjointSet<PartitionInfo>(image, (_) => new PartitionInfo(kFactor));
        if (image.Count == 0)
            return disjointSet;

        UndirectedGraphEdge<Coord>[] graphEdges = BuildSortedGraphEdges(image);

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
            else if (minSegmentSize > 0)
            {
                graphEdges[deferredCount++] = edge;
            }
        }

        if (minSegmentSize > 0 && deferredCount > 0)
        {
            MergeSmallPartitions(disjointSet, graphEdges.AsSpan(0, deferredCount), minSegmentSize, kFactor);
        }

        return disjointSet;
    }

    private static UndirectedGraphEdge<Coord>[] BuildSortedGraphEdges<T>(IReadOnlyMatrix<T> image)
        where T : struct, IPixel<T>
    {
        var rowEdges = new List<UndirectedGraphEdge<Coord>>[image.Height];
        Parallel.For(0, image.Height, y =>
        {
            var localEdges = new List<UndirectedGraphEdge<Coord>>(image.Width * 4);
            for (int x = 0; x < image.Width; x++)
            {
                var self = new Coord(x, y);
                T selfPixel = image[self];
                foreach (GridNeighbor neighbor in GridNeighborhood.Forward8Connected)
                {
                    Coord other = self + neighbor.Offset;
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
        int minSegmentSize,
        double kFactor)
    {
        foreach (ref readonly UndirectedGraphEdge<Coord> edge in deferredEdges)
        {
            var firstPartition = disjointSet.GetPartition(edge.First);
            var secondPartition = disjointSet.GetPartition(edge.Second);

            if (firstPartition.RootIndex == secondPartition.RootIndex)
                continue;

            if (firstPartition.Size < minSegmentSize || secondPartition.Size < minSegmentSize)
                MergePartitions(disjointSet, firstPartition, secondPartition, edge.Weight, kFactor);
        }
    }

    /// <summary>
    /// Delegate type for a method that determines the color (or pixel value) for a
    /// given region.
    /// </summary>
    /// <typeparam name="T">The type of the pixel.</typeparam>
    /// <param name="image">The original image used to generate the segmentation.</param>
    /// <param name="segment">The segment for which a color is required.</param>
    /// <returns>A color for the given segment.</returns>
    public delegate T ColorAssigner<T>(IReadOnlyMatrix<T> image, Region<Coord> segment)
        where T : struct, IPixel<T>;

    /// <summary>
    /// Returns a <see cref="ColorAssigner{T}"/> that assigns a random color to each segment.
    /// </summary>
    /// <param name="randomSeed">An optional seed for the random number generator.</param>
    /// <returns>A <see cref="ColorAssigner{T}"/> for use in <see cref="ColorImageBySegmentation{T}"/>.</returns>
    public static ColorAssigner<Rgb8> RandomColorAssigner(int? randomSeed = null)
    {
        Random r = randomSeed is int seed ? new Random(seed) : new Random();
        return (_, _) => new Rgb8((byte)r.Next(255), (byte)r.Next(255), (byte)r.Next(255));
    }

    /// <summary>
    /// Returns a <see cref="ColorAssigner{T}"/> that assigns each segment the average color of all the pixels
    /// contained in the segment.
    /// </summary>
    /// <returns>A <see cref="ColorAssigner{T}"/> for use in <see cref="ColorImageBySegmentation{T}"/>.</returns>
    public static ColorAssigner<Rgb8> AverageColorAssigner()
        => (image, region) =>
        {
            if (region.Pixels.Count <= 0)
                throw new ArgumentException("All regions of the segmentation must have at least one pixel.");

            return region.Pixels.Select(coord => image[coord].ToRgb64()).Aggregate(Rgb64.Zero, (current, next) => current.Add(next)).Divide((double)region.Pixels.Count).ToRgb8();
        };

    /// <summary>
    /// Returns a <see cref="ColorAssigner{T}"/> that assigns each segment a unique integer.
    /// </summary>
    /// <returns>A <see cref="ColorAssigner{T}"/> for use in <see cref="ColorImageBySegmentation{T}"/>.</returns>
    public static ColorAssigner<LongPixel> IntegerColorAssigner()
    {
        long index = 0;
        return (_, _) => index++;
    }

    /// <summary>
    /// Colors the specified 8-bit RGB image matrix such that each segment in the specified segmentation is a random color.
    /// </summary>
    /// <param name="image">The image to color.</param>
    /// <param name="segmentation">The segmentation.</param>
    public static void ColorImageBySegmentation(Matrix<Rgb8> image, IImmutableSet<Region<Coord>> segmentation)
        => ColorImageBySegmentation(image, segmentation, RandomColorAssigner());

    /// <summary>
    /// Colors the specified image matrix such that each segment in the specified segmentation is colored
    /// according to the specified <see cref="ColorAssigner{T}"/>.
    /// </summary>
    /// <typeparam name="T">The pixel type for the input matrix.</typeparam>
    /// <param name="image">The image to color.</param>
    /// <param name="segmentation">The segmentation.</param>
    /// <param name="colorAssigner">The <see cref="ColorAssigner{T}"/> used to select a color for each segment.</param>
    public static void ColorImageBySegmentation<T>(Matrix<T> image, IImmutableSet<Region<Coord>> segmentation, ColorAssigner<T> colorAssigner)
        where T : struct, IPixel<T>
    {
        foreach (var currSegment in segmentation)
        {
            T color = colorAssigner(image, currSegment);
            foreach (var pixel in currSegment.Pixels)
            {
                image[pixel.X, pixel.Y] = color;
            }
        }
    }

    /// <summary>
    /// Computes a deterministic, fixed-seed SplitMix64 tie-breaker from edge endpoint coordinates
    /// so equal-weight edges break ties pseudo-randomly rather than sweeping in scanline order.
    /// </summary>
    /// <param name="edge">The graph edge whose endpoints are hashed.</param>
    /// <returns>A 64-bit pseudo-random hash value for tie-breaking.</returns>
    private static ulong ComputeEdgeTieBreaker(UndirectedGraphEdge<Coord> edge)
    {
        ulong x1 = (uint)edge.First.X;
        ulong y1 = (uint)edge.First.Y;
        ulong x2 = (uint)edge.Second.X;
        ulong y2 = (uint)edge.Second.Y;

        ulong z = (x1 * 0x9E3779B97F4A7C15UL)
                ^ (y1 * 0xBF58476D1CE4E5B9UL)
                ^ (x2 * 0x94D049BB133111EBUL)
                ^ (y2 * 0xD6E8FEB86659FD93UL);
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}