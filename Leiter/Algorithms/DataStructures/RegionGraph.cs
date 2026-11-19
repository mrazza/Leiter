namespace Leiter.Algorithms.DataStructures;

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

        var denseLabels = new SequentialMatrix<int>(labels.Size);
        var idMap = new Dictionary<TLabel, int>();

        for (int y = 0; y < labels.Height; y++)
        {
            for (int x = 0; x < labels.Width; x++)
            {
                TLabel rawId = labels[x, y];
                if (!idMap.TryGetValue(rawId, out int denseId))
                {
                    denseId = idMap.Count;
                    idMap.Add(rawId, denseId);
                }

                denseLabels[x, y] = denseId;
            }
        }

        return BuildFromDenseLabels(denseLabels, idMap.Count, connectivity);
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

        Parallel.For(0, RegionCount, regionId => results[regionId] = selector(Regions[regionId]));
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
        if (mask.Size != Size)
            throw new ArgumentException($"Mask dimensions {mask.Size} must match RegionGraph dimensions {Size}.", nameof(mask));

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
    /// Computes the unit normal vector at boundary pixel <paramref name="coord"/> pointing from
    /// <paramref name="fromRegionId"/> toward <paramref name="towardsRegionId"/> over its 8-connected neighborhood.
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
        ReadOnlySpan<GridNeighbor> neighbors = GridNeighborhood.All8Connected;

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
    /// Computes a multi-source 8-connected geodesic distance band around selected inter-region boundaries.
    /// </summary>
    /// <param name="maxRadius">The maximum Chebyshev grid step radius to expand from boundary seed pixels.</param>
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
        if (maxRadius < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRadius), maxRadius, "Maximum band radius must be non-negative.");

        int width = Width;
        int height = Height;
        int totalPixels = labels.Count;

        var distData = new int[totalPixels];
        var boundaryData = new int[totalPixels];
        var seedData = new int[totalPixels];
        Array.Fill(distData, int.MaxValue);
        Array.Fill(boundaryData, -1);
        Array.Fill(seedData, -1);

        if (totalPixels == 0 || Boundaries.Count == 0)
        {
            return new RegionBoundaryBand(
                Size,
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
                int idx = (p.Y * width) + p.X;
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

        ReadOnlySpan<GridNeighbor> neighbors = GridNeighborhood.All8Connected;
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
            int curX = curIdx % width;
            int curY = curIdx / width;

            for (int s = 0; s < neighbors.Length; s++)
            {
                Coord offset = neighbors[s].Offset;
                int nx = curX + offset.X;
                int ny = curY + offset.Y;
                if ((uint)nx >= (uint)width || (uint)ny >= (uint)height)
                    continue;

                int nIdx = (ny * width) + nx;
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
            Size,
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
        int width = denseLabels.Width;
        int height = denseLabels.Height;

        var pixelCounts = new int[regionCount];
        for (int i = 0; i < denseLabels.Count; i++)
            pixelCounts[denseLabels[i]]++;

        var regions = new Region<Coord>[regionCount];
        for (int id = 0; id < regionCount; id++)
            regions[id] = new Region<Coord>(pixelCounts[id]) { Id = id };

        var pairToBoundaryBuilder = new Dictionary<ulong, (List<int> FirstIndices, List<int> SecondIndices)>();
        ReadOnlySpan<GridNeighbor> forwardSteps = GridNeighborhood.GetForward(connectivity);

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int firstIndex = rowBase + x;
                int firstLabel = denseLabels[firstIndex];
                var coord = new Coord(x, y);
                regions[firstLabel].Pixels.Add(coord);

                for (int s = 0; s < forwardSteps.Length; s++)
                {
                    Coord neighborCoord = coord + forwardSteps[s].Offset;
                    if (!denseLabels.Contains(neighborCoord))
                        continue;

                    int secondIndex = (neighborCoord.Y * width) + neighborCoord.X;
                    int secondLabel = denseLabels[secondIndex];
                    if (firstLabel == secondLabel)
                        continue;

                    int lowRegion;
                    int highRegion;
                    int lowPixelIndex;
                    int highPixelIndex;
                    if (firstLabel < secondLabel)
                    {
                        lowRegion = firstLabel;
                        highRegion = secondLabel;
                        lowPixelIndex = firstIndex;
                        highPixelIndex = secondIndex;
                    }
                    else
                    {
                        lowRegion = secondLabel;
                        highRegion = firstLabel;
                        lowPixelIndex = secondIndex;
                        highPixelIndex = firstIndex;
                    }

                    ulong pairKey = ((ulong)(uint)lowRegion << 32) | (uint)highRegion;
                    if (!pairToBoundaryBuilder.TryGetValue(pairKey, out var lists))
                    {
                        lists = (new List<int>(), new List<int>());
                        pairToBoundaryBuilder.Add(pairKey, lists);
                    }

                    lists.FirstIndices.Add(lowPixelIndex);
                    lists.SecondIndices.Add(highPixelIndex);
                }
            }
        }

        ulong[] sortedKeys = pairToBoundaryBuilder.Keys.ToArray();
        Array.Sort(sortedKeys);

        int boundaryCount = sortedKeys.Length;
        var sortedPairs = new (int First, int Second, int[] FirstUnique, int[] SecondUnique)[boundaryCount];
        int totalBorderCoords = 0;

        for (int i = 0; i < boundaryCount; i++)
        {
            ulong key = sortedKeys[i];
            int firstRegion = (int)(key >> 32);
            int secondRegion = (int)(uint)key;
            var (rawFirst, rawSecond) = pairToBoundaryBuilder[key];

            int[] firstUnique = SortAndDeduplicate(rawFirst);
            int[] secondUnique = SortAndDeduplicate(rawSecond);
            totalBorderCoords += firstUnique.Length + secondUnique.Length;
            sortedPairs[i] = (firstRegion, secondRegion, firstUnique, secondUnique);
        }

        var packedBorderCoords = new Coord[totalBorderCoords];
        var boundaries = new RegionBoundary[boundaryCount];
        var degrees = new int[regionCount];
        int coordCursor = 0;

        for (int i = 0; i < boundaryCount; i++)
        {
            var (firstRegion, secondRegion, firstUnique, secondUnique) = sortedPairs[i];

            int firstOffset = coordCursor;
            for (int k = 0; k < firstUnique.Length; k++)
                packedBorderCoords[coordCursor++] = denseLabels.CoordFromIndex(firstUnique[k]);

            int secondOffset = coordCursor;
            for (int k = 0; k < secondUnique.Length; k++)
                packedBorderCoords[coordCursor++] = denseLabels.CoordFromIndex(secondUnique[k]);

            boundaries[i] = new RegionBoundary(
                firstRegion,
                secondRegion,
                packedBorderCoords.AsMemory(firstOffset, firstUnique.Length),
                packedBorderCoords.AsMemory(secondOffset, secondUnique.Length));

            degrees[firstRegion]++;
            degrees[secondRegion]++;
        }

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

    private static void ValidateConnectivity(GridConnectivity connectivity)
    {
        if (!Enum.IsDefined(connectivity))
            throw new ArgumentOutOfRangeException(nameof(connectivity), connectivity, "Unsupported grid connectivity.");
    }

    private void ValidateRegionId(int regionId, string paramName)
    {
        if ((uint)regionId >= (uint)RegionCount)
            throw new ArgumentOutOfRangeException(paramName, regionId, $"Region ID must be in [0, {RegionCount}).");
    }
}
