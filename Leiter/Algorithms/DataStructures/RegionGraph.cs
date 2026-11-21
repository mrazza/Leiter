namespace Leiter.Algorithms.DataStructures;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Leiter.Algorithms.Internal;
using Leiter.Core;

/// <summary>
/// Represents a Region Adjacency Graph (RAG) built from a 2D pixel partition, exposing dense zero-based region labels,
/// per-region coordinate sets, compressed adjacency queries, and deduplicated inter-region boundaries.
/// </summary>
public sealed class RegionGraph
{
    private readonly SequentialMatrix<int> labels;
    private readonly int[] neighborIds;
    private readonly int[] boundaryIndices;
    private readonly int[] neighborOffsets;

    private RegionGraph(
        SequentialMatrix<int> labels,
        GridConnectivity connectivity,
        Region<Coord>[] regions,
        RegionBoundary[] boundaries,
        int[] neighborIds,
        int[] boundaryIndices,
        int[] neighborOffsets)
    {
        Size = labels.Size;
        Connectivity = connectivity;
        this.labels = labels;
        Labels = labels;
        Regions = regions;
        Boundaries = boundaries;
        this.neighborIds = neighborIds;
        this.boundaryIndices = boundaryIndices;
        this.neighborOffsets = neighborOffsets;
    }

    /// <summary>
    /// Gets the dimensions of the underlying labeled image.
    /// </summary>
    public Size Size { get; }

    /// <summary>
    /// Gets the width of the underlying labeled image in pixels.
    /// </summary>
    public int Width => Size.Width;

    /// <summary>
    /// Gets the height of the underlying labeled image in pixels.
    /// </summary>
    public int Height => Size.Height;

    /// <summary>
    /// Gets the total number of distinct regions (<c>0</c> through <c>RegionCount - 1</c>).
    /// </summary>
    public int RegionCount => Regions.Count;

    /// <summary>
    /// Gets the grid connectivity rule used to discover adjacent regions and shared boundaries.
    /// </summary>
    public GridConnectivity Connectivity { get; }

    /// <summary>
    /// Gets the dense label matrix mapping each pixel coordinate to its zero-based region identifier in <c>[0, RegionCount)</c>.
    /// </summary>
    public IReadOnlyMatrix<int> Labels { get; }

    /// <summary>
    /// Gets the ordered list of regions indexed by their dense region identifier.
    /// </summary>
    public IReadOnlyList<Region<Coord>> Regions { get; }

    /// <summary>
    /// Gets all undirected inter-region boundaries ordered deterministically by <c>(First, Second)</c>.
    /// </summary>
    public IReadOnlyList<RegionBoundary> Boundaries { get; }

    /// <summary>
    /// Constructs a <see cref="RegionGraph"/> from any <see cref="IDisjointSet"/> segmentation,
    /// re-indexing arbitrary partition root identifiers into dense <c>[0, RegionCount)</c> identifiers in row-major first-seen order.
    /// </summary>
    /// <param name="segmentation">The segmented pixel partition.</param>
    /// <param name="connectivity">The grid connectivity used to detect neighboring regions.</param>
    /// <returns>A populated <see cref="RegionGraph"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="segmentation"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="connectivity"/> is invalid.</exception>
    public static RegionGraph FromDisjointSet(
        IDisjointSet segmentation,
        GridConnectivity connectivity = GridConnectivity.EightConnected)
    {
        ArgumentNullException.ThrowIfNull(segmentation);
        return FromLabels(segmentation, connectivity);
    }

    /// <summary>
    /// Constructs a <see cref="RegionGraph"/> from any label matrix,
    /// re-indexing arbitrary equatable labels into dense <c>[0, RegionCount)</c> identifiers in row-major first-seen order.
    /// </summary>
    /// <typeparam name="TLabel">The value type storing pixel region labels.</typeparam>
    /// <param name="labels">The label matrix.</param>
    /// <param name="connectivity">The grid connectivity used to detect neighboring regions.</param>
    /// <returns>A populated <see cref="RegionGraph"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labels"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="connectivity"/> is invalid.</exception>
    public static RegionGraph FromLabels<TLabel>(
        IReadOnlyMatrix<TLabel> labels,
        GridConnectivity connectivity = GridConnectivity.EightConnected)
        where TLabel : struct, IEquatable<TLabel>
    {
        ArgumentNullException.ThrowIfNull(labels);
        ValidateConnectivity(connectivity);

        if (labels.Count == 0)
            return CreateEmpty(labels.Size, connectivity);

        SequentialMatrix<int> denseLabels = RemapLabelsToDense(labels, out int regionCount);
        return BuildFromDenseLabels(denseLabels, regionCount, connectivity);
    }

    private sealed class StripRemapResult<TLabel>(
        int startRow,
        int endRow,
        List<TLabel> firstSeenOrder)
        where TLabel : struct, IEquatable<TLabel>
    {
        public readonly int StartRow = startRow;
        public readonly int EndRow = endRow;
        public readonly List<TLabel> FirstSeenOrder = firstSeenOrder;
        public int[]? LocalToGlobal;
        public bool IsIdentityMapping;
    }

    private readonly record struct BoundaryIndexLists(List<int> FirstIndices, List<int> SecondIndices);

    private readonly record struct DeduplicatedBoundary(int First, int Second, int[] FirstUnique, int[] SecondUnique);

    private sealed class StripBoundaryAccumulator
    {
        public readonly Dictionary<ulong, BoundaryIndexLists> Pairs = new();

        public void AddPair(int firstIndex, int secondIndex, int firstLabel, int secondLabel)
        {
            ulong pairKey = GridNeighborhood.PackUnorderedPairKey(firstLabel, secondLabel);
            if (!Pairs.TryGetValue(pairKey, out BoundaryIndexLists lists))
            {
                lists = new BoundaryIndexLists(new List<int>(), new List<int>());
                Pairs.Add(pairKey, lists);
            }

            if (firstLabel < secondLabel)
            {
                lists.FirstIndices.Add(firstIndex);
                lists.SecondIndices.Add(secondIndex);
            }
            else
            {
                lists.FirstIndices.Add(secondIndex);
                lists.SecondIndices.Add(firstIndex);
            }
        }
    }

    private static SequentialMatrix<int> RemapLabelsToDense<TLabel>(
        IReadOnlyMatrix<TLabel> labels,
        out int regionCount)
        where TLabel : struct, IEquatable<TLabel>
    {
        int width = labels.Width;
        int height = labels.Height;
        var denseLabels = new SequentialMatrix<int>(labels.Size);
        int[] denseData = denseLabels.RawData;
        TLabel[]? rawLabels = labels.TryGetRawArray();

        if (labels.Count < ParallelRunner.DefaultMinParallelCount || height < 8 || Environment.ProcessorCount <= 1)
        {
            var idMap = new Dictionary<TLabel, int>();
            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                {
                    int index = rowBase + x;
                    TLabel rawId = rawLabels is not null ? rawLabels[index] : labels[index];
                    if (!idMap.TryGetValue(rawId, out int denseId))
                    {
                        denseId = idMap.Count;
                        idMap.Add(rawId, denseId);
                    }

                    denseData[index] = denseId;
                }
            }

            regionCount = idMap.Count;
            return denseLabels;
        }

        // Step 1: Discover per-strip unique labels in row-major order and write strip-local IDs.
        StripRemapResult<TLabel>[] strips = ParallelRunner.MapChunks(
            height,
            CancellationToken.None,
            (startRow, endRow) =>
            {
                var localMap = new Dictionary<TLabel, int>();
                var firstSeen = new List<TLabel>();

                for (int y = startRow; y < endRow; y++)
                {
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int index = rowBase + x;
                        TLabel rawId = rawLabels is not null ? rawLabels[index] : labels[index];
                        if (!localMap.TryGetValue(rawId, out int localId))
                        {
                            localId = firstSeen.Count;
                            localMap.Add(rawId, localId);
                            firstSeen.Add(rawId);
                        }

                        denseData[index] = localId;
                    }
                }

                return new StripRemapResult<TLabel>(startRow, endRow, firstSeen);
            },
            minParallelCount: 2);

        // Step 2: Assign global dense IDs sequentially across strip first-seen lists to match exact row-major order.
        var globalMap = new Dictionary<TLabel, int>();
        for (int s = 0; s < strips.Length; s++)
        {
            StripRemapResult<TLabel> strip = strips[s];
            List<TLabel> firstSeen = strip.FirstSeenOrder;
            var localToGlobal = new int[firstSeen.Count];
            bool identity = true;

            for (int i = 0; i < firstSeen.Count; i++)
            {
                TLabel rawId = firstSeen[i];
                if (!globalMap.TryGetValue(rawId, out int globalId))
                {
                    globalId = globalMap.Count;
                    globalMap.Add(rawId, globalId);
                }

                localToGlobal[i] = globalId;
                if (globalId != i)
                    identity = false;
            }

            strip.LocalToGlobal = localToGlobal;
            strip.IsIdentityMapping = identity;
        }

        regionCount = globalMap.Count;

        // Step 3: Translate strip-local IDs to global dense IDs in parallel.
        ParallelRunner.For(strips.Length, CancellationToken.None, s =>
        {
            StripRemapResult<TLabel> strip = strips[s];
            if (strip.IsIdentityMapping)
                return;

            int[] localToGlobal = strip.LocalToGlobal!;
            int startIndex = strip.StartRow * width;
            int endIndex = strip.EndRow * width;
            for (int i = startIndex; i < endIndex; i++)
                denseData[i] = localToGlobal[denseData[i]];
        });

        return denseLabels;
    }

    /// <summary>
    /// Returns the sorted span of adjacent region identifiers for <paramref name="regionId"/>.
    /// </summary>
    /// <param name="regionId">The dense region identifier in <c>[0, RegionCount)</c>.</param>
    /// <returns>The ascending span of neighboring region identifiers.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="regionId"/> is outside <c>[0, RegionCount)</c>.</exception>
    public ReadOnlySpan<int> GetNeighborIds(int regionId)
    {
        ValidateRegionId(regionId, nameof(regionId));
        int start = neighborOffsets[regionId];
        int length = neighborOffsets[regionId + 1] - start;
        return neighborIds.AsSpan(start, length);
    }

    /// <summary>
    /// Returns the indices into <see cref="Boundaries"/> corresponding positionally to <see cref="GetNeighborIds"/>.
    /// </summary>
    /// <param name="regionId">The dense region identifier in <c>[0, RegionCount)</c>.</param>
    /// <returns>The span of boundary indices incident to <paramref name="regionId"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="regionId"/> is outside <c>[0, RegionCount)</c>.</exception>
    public ReadOnlySpan<int> GetBoundaryIndices(int regionId)
    {
        ValidateRegionId(regionId, nameof(regionId));
        int start = neighborOffsets[regionId];
        int length = neighborOffsets[regionId + 1] - start;
        return boundaryIndices.AsSpan(start, length);
    }

    /// <summary>
    /// Determines whether two distinct regions are directly adjacent in this graph.
    /// </summary>
    /// <param name="firstRegionId">The first dense region identifier.</param>
    /// <param name="secondRegionId">The second dense region identifier.</param>
    /// <returns><see langword="true"/> if <paramref name="firstRegionId"/> and <paramref name="secondRegionId"/> are distinct and share a boundary; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either region identifier is outside <c>[0, RegionCount)</c>.</exception>
    public bool AreNeighbors(int firstRegionId, int secondRegionId)
    {
        ValidateRegionId(firstRegionId, nameof(firstRegionId));
        ValidateRegionId(secondRegionId, nameof(secondRegionId));

        if (firstRegionId == secondRegionId)
            return false;

        ReadOnlySpan<int> firstNeighbors = GetNeighborIds(firstRegionId);
        ReadOnlySpan<int> secondNeighbors = GetNeighborIds(secondRegionId);

        return firstNeighbors.Length <= secondNeighbors.Length
            ? firstNeighbors.BinarySearch(secondRegionId) >= 0
            : secondNeighbors.BinarySearch(firstRegionId) >= 0;
    }

    /// <summary>
    /// Projects all region boundaries into weighted <see cref="UndirectedGraphEdge{T}"/> entries.
    /// </summary>
    /// <param name="weightSelector">Delegate computing a scalar edge weight for each <see cref="RegionBoundary"/>.</param>
    /// <returns>An array of weighted undirected graph edges matching <see cref="Boundaries"/> in length and order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="weightSelector"/> is <see langword="null"/>.</exception>
    public UndirectedGraphEdge<int>[] BuildWeightedEdges(Func<RegionBoundary, double> weightSelector)
    {
        ArgumentNullException.ThrowIfNull(weightSelector);

        var edges = new UndirectedGraphEdge<int>[Boundaries.Count];
        for (int i = 0; i < Boundaries.Count; i++)
        {
            RegionBoundary boundary = Boundaries[i];
            edges[i] = boundary.ToEdge(weightSelector(boundary));
        }

        return edges;
    }

    /// <summary>
    /// Computes a per-region summary value in parallel across all regions in this graph.
    /// </summary>
    /// <typeparam name="TStat">The summary type produced per region.</typeparam>
    /// <param name="selector">Delegate evaluating a summary for a single <see cref="Region{Coord}"/>.</param>
    /// <returns>An array indexed by dense region identifier containing the computed summaries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="selector"/> is <see langword="null"/>.</exception>
    public TStat[] ComputeRegionStatistics<TStat>(Func<Region<Coord>, TStat> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var results = new TStat[RegionCount];
        if (RegionCount == 0)
            return results;

        ParallelRunner.For(RegionCount, CancellationToken.None, regionId => results[regionId] = selector(Regions[regionId]));
        return results;
    }

    /// <summary>
    /// Computes the fraction of pixels in each region for which <paramref name="mask"/> is <see langword="true"/>.
    /// </summary>
    /// <param name="mask">A boolean mask matrix whose dimensions match this graph.</param>
    /// <returns>An array indexed by dense region identifier containing fractions in <c>[0, 1]</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="mask"/> dimensions do not match this graph.</exception>
    public double[] ComputeRegionFractions(IReadOnlyMatrix<bool> mask)
    {
        ArgumentNullException.ThrowIfNull(mask);
        Guard.ThrowIfSizeMismatch(Size, mask.Size, nameof(mask));

        return ComputeRegionStatistics(region =>
        {
            HashSet<Coord> pixels = region.Pixels;
            if (pixels.Count == 0)
                return 0.0;

            int trueCount = 0;
            foreach (Coord coord in pixels)
            {
                if (mask[coord])
                    trueCount++;
            }

            return (double)trueCount / pixels.Count;
        });
    }

    /// <summary>
    /// Classifies each region in this graph as <see langword="true"/> when the fraction of <see langword="true"/> pixels
    /// inside that region meets or exceeds <paramref name="minOverlapFraction"/>.
    /// </summary>
    /// <param name="mask">A boolean mask matrix whose dimensions match this graph.</param>
    /// <param name="minOverlapFraction">The inclusive minimum fraction in <c>[0.0, 1.0]</c> required for a region to be flagged.</param>
    /// <returns>A boolean array indexed by dense region identifier.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="mask"/> dimensions do not match this graph.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="minOverlapFraction"/> is non-finite or outside <c>[0.0, 1.0]</c>.</exception>
    public bool[] ComputeRegionFlags(IReadOnlyMatrix<bool> mask, double minOverlapFraction)
    {
        ArgumentNullException.ThrowIfNull(mask);
        Guard.ThrowIfSizeMismatch(Size, mask.Size, nameof(mask));
        Guard.ThrowIfNotFiniteInRange(minOverlapFraction, 0.0, 1.0);

        return ComputeRegionFlags(ComputeRegionFractions(mask), minOverlapFraction);
    }

    internal static bool[] ComputeRegionFlags(ReadOnlySpan<double> values, double threshold)
    {
        Guard.ThrowIfNotFinite(threshold);

        var flags = new bool[values.Length];
        for (int i = 0; i < values.Length; i++)
            flags[i] = values[i] >= threshold;

        return flags;
    }

    /// <summary>
    /// Computes the unit normal vector at boundary pixel <paramref name="coord"/> pointing from
    /// <paramref name="fromRegionId"/> toward <paramref name="towardsRegionId"/> over the graph's <see cref="Connectivity"/> neighborhood.
    /// </summary>
    /// <param name="coord">The pixel coordinate at which to evaluate the boundary normal.</param>
    /// <param name="towardsRegionId">The region identifier toward which the returned normal points.</param>
    /// <param name="fromRegionId">The opposing region identifier.</param>
    /// <returns>
    /// A unit-length <see cref="Vector2D"/> pointing toward <paramref name="towardsRegionId"/>,
    /// or <see cref="Vector2D.Zero"/> if no opposing neighborhood gradient exists at <paramref name="coord"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="coord"/> is outside image bounds or either region ID is outside <c>[0, RegionCount)</c>.
    /// </exception>
    public Vector2D ComputeBoundaryNormal(Coord coord, int towardsRegionId, int fromRegionId)
    {
        if (!labels.Contains(coord))
            throw new ArgumentOutOfRangeException(nameof(coord), coord, $"Coordinate {coord} is outside graph dimensions {Size}.");

        ValidateRegionId(towardsRegionId, nameof(towardsRegionId));
        ValidateRegionId(fromRegionId, nameof(fromRegionId));

        if (towardsRegionId == fromRegionId)
            return Vector2D.Zero;

        int pLabel = labels[coord];
        if (pLabel != towardsRegionId && pLabel != fromRegionId)
            return Vector2D.Zero;

        double sumX = 0.0;
        double sumY = 0.0;
        ReadOnlySpan<GridNeighbor> neighbors = GridNeighborhood.GetAll(Connectivity);

        for (int s = 0; s < neighbors.Length; s++)
        {
            Coord q = coord + neighbors[s].Offset;
            if (!labels.Contains(q))
                continue;

            int qLabel = labels[q];
            if (pLabel == towardsRegionId && qLabel == fromRegionId)
            {
                sumX += coord.X - q.X;
                sumY += coord.Y - q.Y;
            }
            else if (pLabel == fromRegionId && qLabel == towardsRegionId)
            {
                sumX += q.X - coord.X;
                sumY += q.Y - coord.Y;
            }
        }

        return new Vector2D(sumX, sumY).Normalize();
    }

    /// <summary>
    /// Computes a multi-source geodesic distance band around selected inter-region boundaries using the graph's <see cref="Connectivity"/>.
    /// </summary>
    /// <param name="maxRadius">The maximum grid step radius to expand from boundary seed pixels.</param>
    /// <param name="boundaryPredicate">
    /// Optional filter predicate receiving a boundary index in <c>[0, Boundaries.Count)</c>;
    /// when <see langword="null"/>, all boundaries seed the band.
    /// </param>
    /// <param name="restrictToIncidentRegions">
    /// When <see langword="true"/>, expansion from a boundary pixel only enters pixels belonging to that boundary's
    /// <see cref="RegionBoundary.First"/> or <see cref="RegionBoundary.Second"/> regions.
    /// </param>
    /// <returns>A <see cref="RegionBoundaryBand"/> storing per-pixel distances, nearest boundaries, and nearest seeds.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxRadius"/> is negative.</exception>
    public RegionBoundaryBand ComputeBoundaryBand(
        int maxRadius,
        Func<int, bool>? boundaryPredicate = null,
        bool restrictToIncidentRegions = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRadius);

        Size size = Size;
        int width = size.Width;
        int height = size.Height;
        int totalPixels = size.Count;

        var distData = new int[totalPixels];
        var boundaryData = new int[totalPixels];
        var seedData = new int[totalPixels];
        Array.Fill(distData, int.MaxValue);
        Array.Fill(boundaryData, -1);
        Array.Fill(seedData, -1);

        if (totalPixels == 0 || Boundaries.Count == 0)
        {
            return new RegionBoundaryBand(
                size,
                maxRadius,
                new SequentialMatrix<int>(width, height, distData),
                new SequentialMatrix<int>(width, height, boundaryData),
                new SequentialMatrix<int>(width, height, seedData),
                []);
        }

        var seedPixels = new List<Coord>();
        var queue = new Queue<int>();

        void EnqueueBorderPixels(ReadOnlySpan<Coord> borderPixels, int boundaryIndex)
        {
            for (int i = 0; i < borderPixels.Length; i++)
            {
                Coord p = borderPixels[i];
                int idx = size.IndexOf(p);
                if (distData[idx] > 0)
                {
                    distData[idx] = 0;
                    boundaryData[idx] = boundaryIndex;
                    seedData[idx] = idx;
                    seedPixels.Add(p);
                    queue.Enqueue(idx);
                }
            }
        }

        for (int bi = 0; bi < Boundaries.Count; bi++)
        {
            if (boundaryPredicate is not null && !boundaryPredicate(bi))
                continue;

            RegionBoundary b = Boundaries[bi];
            EnqueueBorderPixels(b.FirstBorderPixels.Span, bi);
            EnqueueBorderPixels(b.SecondBorderPixels.Span, bi);
        }

        int[] neighborOffsets = GridNeighborhood.GetLinearOffsets(width, Connectivity);
        int neighborCount = neighborOffsets.Length;
        byte[] neighborMasks = GridNeighborhood.BuildValidNeighborMasks(size, Connectivity);
        ReadOnlySpan<int> labelSpan = labels.AsReadOnlySpan();

        while (queue.Count > 0)
        {
            int curIdx = queue.Dequeue();
            int curDist = distData[curIdx];
            if (curDist >= maxRadius)
                continue;

            int curBoundary = boundaryData[curIdx];
            int curSeed = seedData[curIdx];
            RegionBoundary b = Boundaries[curBoundary];
            byte validMask = neighborMasks[curIdx];

            for (int s = 0; s < neighborCount; s++)
            {
                if ((validMask & (1 << s)) == 0)
                    continue;

                int nIdx = curIdx + neighborOffsets[s];
                if (restrictToIncidentRegions)
                {
                    int nLabel = labelSpan[nIdx];
                    if (nLabel != b.First && nLabel != b.Second)
                        continue;
                }

                int nDist = curDist + 1;
                if (nDist < distData[nIdx])
                {
                    distData[nIdx] = nDist;
                    boundaryData[nIdx] = curBoundary;
                    seedData[nIdx] = curSeed;
                    queue.Enqueue(nIdx);
                }
            }
        }

        return new RegionBoundaryBand(
            size,
            maxRadius,
            new SequentialMatrix<int>(width, height, distData),
            new SequentialMatrix<int>(width, height, boundaryData),
            new SequentialMatrix<int>(width, height, seedData),
            seedPixels.ToArray());
    }

    private static RegionGraph CreateEmpty(Size size, GridConnectivity connectivity) =>
        new(
            new SequentialMatrix<int>(size),
            connectivity,
            [],
            [],
            [],
            [],
            [0]);

    private static RegionGraph BuildFromDenseLabels(
        SequentialMatrix<int> denseLabels,
        int regionCount,
        GridConnectivity connectivity)
    {
        Size size = denseLabels.Size;
        int width = size.Width;
        int height = size.Height;
        int[] labelData = denseLabels.RawData;

        Region<Coord>[] regions = BuildRegions(size, labelData, regionCount);

        StripBoundaryAccumulator[] stripAccumulators = ParallelRunner.MapRows(
            size,
            CancellationToken.None,
            (startRow, endRow) =>
            {
                var acc = new StripBoundaryAccumulator();
                var state = (labelData, acc);
                for (int y = startRow; y < endRow; y++)
                {
                    GridNeighborhood.ForEachForwardPairInRow(
                        size,
                        y,
                        connectivity,
                        state,
                        static (firstIndex, secondIndex, s) =>
                        {
                            int firstLabel = s.labelData[firstIndex];
                            int secondLabel = s.labelData[secondIndex];
                            if (firstLabel != secondLabel)
                                s.acc.AddPair(firstIndex, secondIndex, firstLabel, secondLabel);
                        });
                }

                return acc;
            });

        StripBoundaryAccumulator merged = MergeStripBoundaries(stripAccumulators);

        ulong[] sortedKeys = merged.Pairs.Keys.ToArray();
        Array.Sort(sortedKeys);

        int boundaryCount = sortedKeys.Length;
        var rawPairs = new BoundaryIndexLists[boundaryCount];
        for (int i = 0; i < boundaryCount; i++)
            rawPairs[i] = merged.Pairs[sortedKeys[i]];

        var sortedPairs = new DeduplicatedBoundary[boundaryCount];
        ParallelRunner.For(boundaryCount, CancellationToken.None, i =>
        {
            ulong key = sortedKeys[i];
            int firstRegion = (int)(key >> 32);
            int secondRegion = (int)(uint)key;
            var (rawFirst, rawSecond) = rawPairs[i];
            sortedPairs[i] = new DeduplicatedBoundary(
                firstRegion,
                secondRegion,
                SortAndDeduplicate(rawFirst),
                SortAndDeduplicate(rawSecond));
        });

        var boundaryCoordOffsets = new int[boundaryCount];
        var degrees = new int[regionCount];
        int totalBorderCoords = 0;
        for (int i = 0; i < boundaryCount; i++)
        {
            var (firstRegion, secondRegion, firstUnique, secondUnique) = sortedPairs[i];
            boundaryCoordOffsets[i] = totalBorderCoords;
            totalBorderCoords += firstUnique.Length + secondUnique.Length;
            degrees[firstRegion]++;
            degrees[secondRegion]++;
        }

        var packedBorderCoords = new Coord[totalBorderCoords];
        var boundaries = new RegionBoundary[boundaryCount];
        ParallelRunner.For(boundaryCount, CancellationToken.None, i =>
        {
            var (firstRegion, secondRegion, firstUnique, secondUnique) = sortedPairs[i];
            int firstOffset = boundaryCoordOffsets[i];
            int cursor = firstOffset;

            for (int k = 0; k < firstUnique.Length; k++)
                packedBorderCoords[cursor++] = size.CoordOf(firstUnique[k]);

            int secondOffset = cursor;
            for (int k = 0; k < secondUnique.Length; k++)
                packedBorderCoords[cursor++] = size.CoordOf(secondUnique[k]);

            boundaries[i] = new RegionBoundary(
                firstRegion,
                secondRegion,
                packedBorderCoords.AsMemory(firstOffset, firstUnique.Length),
                packedBorderCoords.AsMemory(secondOffset, secondUnique.Length));
        });

        var neighborOffsets = new int[regionCount + 1];
        for (int id = 0; id < regionCount; id++)
            neighborOffsets[id + 1] = neighborOffsets[id] + degrees[id];

        int totalDirectedEdges = neighborOffsets[regionCount];
        var neighborIds = new int[totalDirectedEdges];
        var boundaryIndices = new int[totalDirectedEdges];
        var cursors = neighborOffsets[..regionCount];

        // Because boundaries are sorted ascending by (First, Second),
        // scattering them in order guarantees each region's adjacency slice is strictly ascending.
        for (int b = 0; b < boundaryCount; b++)
        {
            int first = boundaries[b].First;
            int second = boundaries[b].Second;

            int pos1 = cursors[first]++;
            neighborIds[pos1] = second;
            boundaryIndices[pos1] = b;

            int pos2 = cursors[second]++;
            neighborIds[pos2] = first;
            boundaryIndices[pos2] = b;
        }

        return new RegionGraph(
            denseLabels,
            connectivity,
            regions,
            boundaries,
            neighborIds,
            boundaryIndices,
            neighborOffsets);
    }

    private static Region<Coord>[] BuildRegions(Size size, int[] labelData, int regionCount)
    {
        int width = size.Width;
        int height = size.Height;
        int stripCount = ParallelRunner.ResolveChunkCount(height);
        bool useParallelScatter =
            labelData.Length >= ParallelRunner.DefaultMinParallelCount &&
            height >= 8 &&
            Environment.ProcessorCount > 1 &&
            ((long)stripCount * regionCount) <= 4_000_000L;

        if (!useParallelScatter)
        {
            var pixelCounts = new int[regionCount];
            for (int i = 0; i < labelData.Length; i++)
                pixelCounts[labelData[i]]++;

            var sequentialRegions = new Region<Coord>[regionCount];
            for (int id = 0; id < regionCount; id++)
                sequentialRegions[id] = new Region<Coord>(pixelCounts[id]) { Id = id };

            for (int y = 0; y < height; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                    sequentialRegions[labelData[rowBase + x]].Pixels.Add(new Coord(x, y));
            }

            return sequentialRegions;
        }

        var stripBounds = new (int StartRow, int EndRow)[stripCount];
        var stripWriteOffsets = new int[stripCount][];

        ParallelRunner.For(stripCount, CancellationToken.None, s =>
        {
            (int startRow, int endRow) = ParallelRunner.GetChunkBounds(s, stripCount, height);
            stripBounds[s] = (startRow, endRow);

            var counts = new int[regionCount];
            int startIndex = startRow * width;
            int endIndex = endRow * width;
            for (int i = startIndex; i < endIndex; i++)
                counts[labelData[i]]++;

            stripWriteOffsets[s] = counts;
        });

        int[] regionOffsets = ParallelRunner.PrefixSumTransposedBuckets(stripWriteOffsets, regionCount);

        var packedRegionCoords = new Coord[labelData.Length];
        ParallelRunner.For(stripCount, CancellationToken.None, s =>
        {
            (int startRow, int endRow) = stripBounds[s];
            int[] cursors = stripWriteOffsets[s];
            for (int y = startRow; y < endRow; y++)
            {
                int rowBase = y * width;
                for (int x = 0; x < width; x++)
                    packedRegionCoords[cursors[labelData[rowBase + x]]++] = new Coord(x, y);
            }
        });

        var regions = new Region<Coord>[regionCount];
        ParallelRunner.For(regionCount, CancellationToken.None, id =>
        {
            int start = regionOffsets[id];
            int count = regionOffsets[id + 1] - start;
            var region = new Region<Coord>(count) { Id = id };
            for (int i = start; i < start + count; i++)
                region.Pixels.Add(packedRegionCoords[i]);
            regions[id] = region;
        });

        return regions;
    }

    private static StripBoundaryAccumulator MergeStripBoundaries(StripBoundaryAccumulator[] stripAccumulators)
    {
        if (stripAccumulators.Length == 1)
            return stripAccumulators[0];

        var merged = new StripBoundaryAccumulator();
        for (int s = 0; s < stripAccumulators.Length; s++)
        {
            foreach (var (key, lists) in stripAccumulators[s].Pairs)
            {
                if (!merged.Pairs.TryGetValue(key, out var target))
                {
                    merged.Pairs.Add(key, lists);
                }
                else
                {
                    target.FirstIndices.AddRange(lists.FirstIndices);
                    target.SecondIndices.AddRange(lists.SecondIndices);
                }
            }
        }

        return merged;
    }

    private static int[] SortAndDeduplicate(List<int> indices)
    {
        indices.Sort();
        int uniqueCount = 0;
        int prev = -1;
        for (int i = 0; i < indices.Count; i++)
        {
            int curr = indices[i];
            if (curr != prev)
            {
                indices[uniqueCount++] = curr;
                prev = curr;
            }
        }

        var result = new int[uniqueCount];
        indices.CopyTo(0, result, 0, uniqueCount);
        return result;
    }

    private static void ValidateConnectivity(GridConnectivity connectivity) =>
        Guard.ThrowIfUndefinedEnum(connectivity);

    private void ValidateRegionId(int regionId, string paramName)
    {
        if ((uint)regionId >= (uint)RegionCount)
            throw new ArgumentOutOfRangeException(paramName, regionId, $"Region ID must be in [0, {RegionCount}).");
    }
}
