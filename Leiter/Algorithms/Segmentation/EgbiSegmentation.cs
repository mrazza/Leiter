namespace Leiter.Algorithms.Segmentation;

using System.Collections.Immutable;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Algorithms;
using System.Linq;

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
    /// Internal structure to manage disjoint sets for image segmentation.
    /// </summary>
    private struct DisjointSet
    {
        private readonly int[] parent;
        private readonly int[] size;
        private readonly double[] internalDifference;

        public DisjointSet(int count, double kFactor)
        {
            parent = new int[count];
            size = new int[count];
            internalDifference = new double[count];

            for (int i = 0; i < count; i++)
            {
                parent[i] = i;
                size[i] = 1;
                internalDifference[i] = kFactor; // k/1 = k
            }
        }

        public int Find(int i)
        {
            if (parent[i] == i)
                return i;
            
            parent[i] = Find(parent[i]); // Path compression
            return parent[i];
        }

        public void Union(int i, int j, double weight, double kFactor)
        {
            int rootI = Find(i);
            int rootJ = Find(j);

            if (rootI != rootJ)
            {
                // Union by size
                if (size[rootI] < size[rootJ])
                {
                    (rootI, rootJ) = (rootJ, rootI);
                }

                parent[rootJ] = rootI;
                size[rootI] += size[rootJ];
                internalDifference[rootI] = weight + (kFactor / size[rootI]);
            }
        }

        public int GetSize(int i) => size[Find(i)];
        public double GetInternalDifference(int i) => internalDifference[Find(i)];
    }

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
    public static IImmutableSet<Region<Coord>> Segment<T>(IReadOnlyMatrix<T> image, double kFactor = 100.0, double epsilon = 1.0, int minSegmentSize = 100)
        where T : struct, IPixel<T>
    {
        var neighbors = new[]{new Coord(0, 1), new Coord(1, 1), new Coord(1, 0), new Coord(1, -1)};
        List<UndirectedGraphEdge<Coord>> graphEdges = new(image.Count * 4);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var self = new Coord(x, y);
                foreach (var neighbor in neighbors)
                {
                    var other = new Coord(x + neighbor.X, y + neighbor.Y);
                    if (other.X >= image.Width || other.Y >= image.Height || other.X < 0 || other.Y < 0)
                        continue;

                    T selfPixel = image[self];
                    T otherPixel = image[other];
                    graphEdges.Add(new UndirectedGraphEdge<Coord>
                    {
                        First = self,
                        Second = other,
                        Weight = selfPixel.Distance(otherPixel)
                    });
                }
            }
        }
        graphEdges.Sort();

        var dsu = new DisjointSet(image.Count, kFactor);

        foreach (var edge in graphEdges)
        {
            int idx1 = edge.First.X + edge.First.Y * image.Width;
            int idx2 = edge.Second.X + edge.Second.Y * image.Width;

            int root1 = dsu.Find(idx1);
            int root2 = dsu.Find(idx2);

            if (root1 != root2)
            {
                if (edge.Weight <= epsilon || (edge.Weight <= dsu.GetInternalDifference(root1) && edge.Weight <= dsu.GetInternalDifference(root2)))
                {
                    dsu.Union(root1, root2, edge.Weight, kFactor);
                }
            }
        }

        if (minSegmentSize > 0)
        {
            foreach (var edge in graphEdges)
            {
                int idx1 = edge.First.X + edge.First.Y * image.Width;
                int idx2 = edge.Second.X + edge.Second.Y * image.Width;

                int root1 = dsu.Find(idx1);
                int root2 = dsu.Find(idx2);

                if (root1 != root2)
                {
                    if (dsu.GetSize(root1) < minSegmentSize || dsu.GetSize(root2) < minSegmentSize)
                    {
                        dsu.Union(root1, root2, edge.Weight, kFactor);
                    }
                }
            }
        }

        // Group pixels by component
        var components = new Dictionary<int, Region<Coord>>();
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                int idx = x + y * image.Width;
                int root = dsu.Find(idx);

                if (!components.TryGetValue(root, out var region))
                {
                    region = new Region<Coord>();
                    components[root] = region;
                }
                region.Pixels.Add(new Coord(x, y));
            }
        }

        return components.Values.ToImmutableHashSet();
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
}