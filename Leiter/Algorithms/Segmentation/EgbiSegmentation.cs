namespace Leiter.Algorithms.Segmentation;

using System.Collections.Immutable;
using System.Linq;
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
    /// Disjoint Set Union (DSU) data structure for use with the EGBI algorithm, also known as Union-Find or Merge-Find.
    /// </summary>
    /// <remarks>
    /// This is written explicitly for the purposes of placing individual pixels into sets and calculating the
    /// internal difference (color variation) of the pixels inside each set. Further, this implementation is
    /// optimized for speed and memory usage. As a result, it is not a general purpose Disjoint Set Union
    /// data structure.
    /// 
    /// Roots/set IDs are arbitrary integers. They are not guaranteed to be sequential or in any particular order.
    /// The only guarantee is that pixels in the same set will have the same root ID.
    /// 
    /// For background on the datastructure, see: https://en.wikipedia.org/wiki/Disjoint-set_data_structure
    /// </remarks>
    private class DisjointSet<T>
        where T : struct, IPixel<T>
    {
        private readonly IReadOnlyMatrix<T> image;
        private readonly int[] parent;
        private readonly int[] size;
        private readonly double[] internalDifference;

        /// <summary>
        /// Initializes a new instance of the <see cref="DisjointSet{T}"/> class.
        /// </summary>
        /// <param name="image">The image to segment.</param>
        /// <param name="kFactor">The k factor of the EGBI algorithm.</param>
        public DisjointSet(IReadOnlyMatrix<T> image, double kFactor)
        {
            this.image = image;
            parent = new int[image.Count];
            size = new int[image.Count];
            internalDifference = new double[image.Count];

            for (int index = 0; index < image.Count; index++)
            {
                parent[index] = index;
                size[index] = 1;
                internalDifference[index] = kFactor;
            }
        }

        /// <summary>
        /// Finds the root of the set the provided pixel is in.
        /// </summary>
        /// <param name="pixel">The pixel to find the root of.</param>
        /// <returns>The root of the set the pixel is in.</returns>
        public int Find(Coord pixel)
        {
            return Find(image.IndexFromCoord(pixel));
        }

        /// <summary>
        /// Finds the root of the set the provided pixel is in.
        /// </summary>
        /// <param name="index">The index of the pixel.</param>
        /// <returns>The root of the set the pixel is in.</returns>
        public int Find(int index)
        {
            if (parent[index] == index)
                return index;

            parent[index] = Find(parent[index]);
            return parent[index];
        }

        /// <summary>
        /// Unions the two sets the provided pixels are in.
        /// </summary>
        /// <remarks>
        /// The union operation is performed using the union by size algorithm.
        /// If both pixels are already in the same set, no action is taken.
        /// </remarks>
        /// <param name="firstPixel">The first pixel.</param>
        /// <param name="secondPixel">The second pixel.</param>
        /// <param name="weight">The weight of the edge between the two pixels.</param>
        /// <param name="kFactor">The k factor of the EGBI algorithm.</param>
        public void Union(Coord firstPixel, Coord secondPixel, double weight, double kFactor)
        {
            Union(image.IndexFromCoord(firstPixel), image.IndexFromCoord(secondPixel), weight, kFactor);
        }

        /// <summary>
        /// Unions the two sets the provided pixels are in.
        /// </summary>
        /// <remarks>
        /// The union operation is performed using the union by size algorithm.
        /// If both pixels are already in the same set, no action is taken.
        /// </remarks>
        /// <param name="firstIndex">The index of the first pixel.</param>
        /// <param name="secondIndex">The index of the second pixel.</param>
        /// <param name="weight">The weight of the edge between the two sets.</param>
        /// <param name="kFactor">The k factor of the EGBI algorithm.</param>
        public void Union(int firstIndex, int secondIndex, double weight, double kFactor)
        {
            int firstSetRoot = Find(firstIndex);
            int secondSetRoot = Find(secondIndex);
            if (firstSetRoot == secondSetRoot)
                return;

            if (size[firstSetRoot] < size[secondSetRoot])
            {
                (firstSetRoot, secondSetRoot) = (secondSetRoot, firstSetRoot);
            }

            parent[secondSetRoot] = firstSetRoot;
            size[firstSetRoot] += size[secondSetRoot];
            internalDifference[firstSetRoot] = weight + (kFactor / size[firstSetRoot]);
        }

        /// <summary>
        /// Gets the size of the set the provided pixel is in.
        /// </summary>
        /// <param name="pixel">The pixel whose set to get the size of.</param>
        /// <returns>The size of the set the pixel is in.</returns>
        public int GetSize(Coord pixel) => GetSize(image.IndexFromCoord(pixel));

        /// <summary>
        /// Gets the internal difference of the set the provided pixel is in.
        /// </summary>
        /// <param name="pixel">The pixel whose set to get the internal difference of.</param>
        /// <returns>The internal difference of the set the pixel is in.</returns>
        public double GetInternalDifference(Coord pixel) => GetInternalDifference(image.IndexFromCoord(pixel));

        /// <summary>
        /// Gets the size of the set the provided index is in.
        /// </summary>
        /// <param name="index">The index to get the size of.</param>
        /// <returns>The size of the set the index is in.</returns>
        public int GetSize(int index) => size[Find(index)];

        /// <summary>
        /// Gets the internal difference of the set the provided index is in.
        /// </summary>
        /// <param name="index">The index to get the internal difference of.</param>
        /// <returns>The internal difference of the set the index is in.</returns>
        public double GetInternalDifference(int index) => internalDifference[Find(index)];
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
        var neighbors = new[] { new Coord(0, 1), new Coord(1, 1), new Coord(1, 0), new Coord(1, -1) };
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

        var disjointSet = new DisjointSet<T>(image, kFactor);

        foreach (var edge in graphEdges)
        {
            int firstIndex = image.IndexFromCoord(edge.First);
            int secondIndex = image.IndexFromCoord(edge.Second);

            int firstRoot = disjointSet.Find(firstIndex);
            int secondRoot = disjointSet.Find(secondIndex);

            if (firstRoot != secondRoot)
            {
                if (edge.Weight <= epsilon || (edge.Weight <= disjointSet.GetInternalDifference(firstRoot) && edge.Weight <= disjointSet.GetInternalDifference(secondRoot)))
                {
                    disjointSet.Union(firstRoot, secondRoot, edge.Weight, kFactor);
                }
            }
        }

        if (minSegmentSize > 0)
        {
            foreach (var edge in graphEdges)
            {
                int firstIndex = image.IndexFromCoord(edge.First);
                int secondIndex = image.IndexFromCoord(edge.Second);

                int firstRoot = disjointSet.Find(firstIndex);
                int secondRoot = disjointSet.Find(secondIndex);

                if (firstRoot != secondRoot)
                {
                    if (disjointSet.GetSize(firstRoot) < minSegmentSize || disjointSet.GetSize(secondRoot) < minSegmentSize)
                    {
                        disjointSet.Union(firstRoot, secondRoot, edge.Weight, kFactor);
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
                int index = image.IndexFromCoord(new Coord(x, y));
                int root = disjointSet.Find(index);

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