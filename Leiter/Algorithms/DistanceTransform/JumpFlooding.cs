namespace Leiter.Algorithms.DistanceTransform;

using System.Numerics;
using Leiter.Algorithms.DataStructures;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Implements the 1+JFA Jump Flooding Algorithm for computing Voronoi nearest-seed maps
/// and Euclidean distance transforms on 2D grids in <c>O(W · H · log max(W, H))</c> time.
/// </summary>
public static class JumpFlooding
{
    /// <summary>
    /// Extracts all foreground (<see langword="true"/>) coordinates in a boolean mask that border at least one background pixel.
    /// </summary>
    /// <param name="binaryMask">The boolean mask (<see langword="false"/> = background, <see langword="true"/> = foreground).</param>
    /// <param name="eightConnectedBackground">
    /// If <see langword="true"/>, checks all 8 neighbors for background pixels; otherwise checks only the 4 cardinal neighbors.
    /// </param>
    /// <returns>The list of foreground boundary coordinates in row-major order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binaryMask"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<Coord> ExtractBoundarySeeds(
        IReadOnlyMatrix<bool> binaryMask,
        bool eightConnectedBackground = true)
    {
        ArgumentNullException.ThrowIfNull(binaryMask);

        var seeds = new List<Coord>();
        Size size = binaryMask.Size;
        if (size.IsEmpty)
            return seeds;

        ReadOnlySpan<GridNeighbor> neighbors = GridNeighborhood.GetAll(eightConnectedBackground ? GridConnectivity.EightConnected : GridConnectivity.FourConnected);
        int width = size.Width;
        int height = size.Height;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (!binaryMask[x, y])
                    continue;

                var coord = new Coord(x, y);
                foreach (GridNeighbor neighbor in neighbors)
                {
                    Coord neighborCoord = coord + neighbor.Offset;
                    if (size.Contains(neighborCoord) && !binaryMask[neighborCoord])
                    {
                        seeds.Add(coord);
                        break;
                    }
                }
            }
        }

        return seeds;
    }

    /// <summary>
    /// Computes the nearest seed coordinate for every cell in a grid using <c>1+JFA</c>.
    /// </summary>
    /// <param name="size">The dimensions of the grid.</param>
    /// <param name="seeds">The seed coordinates (out-of-bounds coordinates are ignored).</param>
    /// <returns>
    /// A matrix storing each cell's <see cref="NearestSeed"/>, or <see cref="NearestSeed.None"/> everywhere if no valid in-bounds seeds are supplied.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="seeds"/> is <see langword="null"/>.</exception>
    public static Matrix<NearestSeed> ComputeNearestSeeds(Size size, IEnumerable<Coord> seeds)
    {
        Guard.ThrowIfNegative(size);
        ArgumentNullException.ThrowIfNull(seeds);

        if (size.IsEmpty)
            return new SequentialMatrix<NearestSeed>(size);

        var seedGrid = new int[size.Count];
        Array.Fill(seedGrid, -1);

        bool hasAnySeed = false;
        foreach (Coord seed in seeds)
        {
            if (size.Contains(seed))
            {
                int index = size.IndexOf(seed);
                seedGrid[index] = index;
                hasAnySeed = true;
            }
        }

        return RunJumpFlooding(size, seedGrid, hasAnySeed);
    }

    /// <summary>
    /// Computes the nearest <see langword="true"/> seed coordinate in <paramref name="seedMask"/> for every cell using <c>1+JFA</c>.
    /// </summary>
    /// <param name="seedMask">The boolean mask whose <see langword="true"/> entries define seed pixels.</param>
    /// <returns>
    /// A matrix storing each cell's <see cref="NearestSeed"/>, or <see cref="NearestSeed.None"/> everywhere if <paramref name="seedMask"/> contains no <see langword="true"/> entries.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="seedMask"/> is <see langword="null"/>.</exception>
    public static Matrix<NearestSeed> ComputeNearestSeeds(IReadOnlyMatrix<bool> seedMask)
    {
        ArgumentNullException.ThrowIfNull(seedMask);

        Size size = seedMask.Size;
        if (size.IsEmpty)
            return new SequentialMatrix<NearestSeed>(size);

        var seedGrid = new int[seedMask.Count];
        bool hasAnySeed = false;

        int width = size.Width;
        int height = size.Height;
        for (int y = 0; y < height; y++)
        {
            int rowBase = size.IndexOf(0, y);
            for (int x = 0; x < width; x++)
            {
                int index = rowBase + x;
                if (seedMask[x, y])
                {
                    seedGrid[index] = index;
                    hasAnySeed = true;
                }
                else
                {
                    seedGrid[index] = -1;
                }
            }
        }

        return RunJumpFlooding(size, seedGrid, hasAnySeed);
    }

    /// <summary>
    /// Computes the Euclidean distance to the nearest seed coordinate for every cell in the grid.
    /// </summary>
    /// <param name="size">The dimensions of the grid.</param>
    /// <param name="seeds">The seed coordinates.</param>
    /// <returns>A matrix of Euclidean distances in pixels (<see cref="double.PositiveInfinity"/> if no seeds exist).</returns>
    public static Matrix<DoublePixel> ComputeDistanceTransform(Size size, IEnumerable<Coord> seeds) =>
        ComputeDistancesFromNearestSeeds(ComputeNearestSeeds(size, seeds));

    /// <summary>
    /// Computes the Euclidean distance to the nearest <see langword="true"/> pixel in <paramref name="seedMask"/>.
    /// </summary>
    /// <param name="seedMask">The boolean mask whose <see langword="true"/> pixels act as seeds.</param>
    /// <returns>A matrix of Euclidean distances in pixels (<see cref="double.PositiveInfinity"/> if no seeds exist).</returns>
    public static Matrix<DoublePixel> ComputeDistanceTransform(IReadOnlyMatrix<bool> seedMask) =>
        ComputeDistancesFromNearestSeeds(ComputeNearestSeeds(seedMask));

    private static Matrix<NearestSeed> RunJumpFlooding(Size size, int[] currentSeeds, bool hasAnySeed)
    {
        var result = new SequentialMatrix<NearestSeed>(size);

        if (!hasAnySeed)
        {
            result.SetAll(NearestSeed.None);
            return result;
        }

        int maxDimension = Math.Max(size.Width, size.Height);
        if (maxDimension > 1)
        {
            var nextSeeds = new int[currentSeeds.Length];

            ExecuteJumpPass(currentSeeds, nextSeeds, size, 1);
            (currentSeeds, nextSeeds) = (nextSeeds, currentSeeds);

            int largestStep = 1 << BitOperations.Log2((uint)(maxDimension - 1));
            for (int step = largestStep; step >= 1; step >>= 1)
            {
                ExecuteJumpPass(currentSeeds, nextSeeds, size, step);
                (currentSeeds, nextSeeds) = (nextSeeds, currentSeeds);
            }
        }

        Span<NearestSeed> resultSpan = result.AsSpan();
        for (int i = 0; i < currentSeeds.Length; i++)
        {
            resultSpan[i] = NearestSeed.FromCoord(size.CoordOf(currentSeeds[i]));
        }

        return result;
    }

    private static void ExecuteJumpPass(int[] source, int[] destination, Size size, int step)
    {
        int width = size.Width;
        int height = size.Height;

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                var current = new Coord(x, y);
                int currentIndex = size.IndexOf(current);
                int bestSeed = source[currentIndex];
                long bestDistSq = bestSeed >= 0
                    ? current.DistanceSquared(size.CoordOf(bestSeed))
                    : long.MaxValue;

                for (int dy = -step; dy <= step; dy += step)
                {
                    int ny = y + dy;
                    if ((uint)ny >= (uint)height)
                        continue;

                    for (int dx = -step; dx <= step; dx += step)
                    {
                        if (dx == 0 && dy == 0)
                            continue;

                        int nx = x + dx;
                        if ((uint)nx >= (uint)width)
                            continue;

                        int candidateSeed = source[size.IndexOf(nx, ny)];
                        if (candidateSeed < 0)
                            continue;

                        long distSq = current.DistanceSquared(size.CoordOf(candidateSeed));
                        if (distSq < bestDistSq || (distSq == bestDistSq && candidateSeed < bestSeed))
                        {
                            bestDistSq = distSq;
                            bestSeed = candidateSeed;
                        }
                    }
                }

                destination[currentIndex] = bestSeed;
            }
        });
    }

    private static Matrix<DoublePixel> ComputeDistancesFromNearestSeeds(IReadOnlyMatrix<NearestSeed> nearestSeeds) =>
        nearestSeeds.MapIndexed(static (x, y, entry) =>
            new DoublePixel(entry.HasSeed ? entry.Seed.Distance(new Coord(x, y)) : double.PositiveInfinity));
}
