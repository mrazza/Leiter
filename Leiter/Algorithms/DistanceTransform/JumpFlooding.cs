namespace Leiter.Algorithms.DistanceTransform;

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Implements the 1+JFA Jump Flooding Algorithm for computing Voronoi nearest-seed maps
/// and Euclidean distance transforms on 2D grids in <c>O(W · H · log max(W, H))</c> time.
/// </summary>
public static class JumpFlooding
{
    private static readonly Coord NoSeed = new(-1, -1);

    /// <summary>
    /// Extracts all foreground (<see langword="true"/>) coordinates in a boolean mask that border at least one background pixel.
    /// </summary>
    /// <param name="binaryMask">The boolean mask (<see langword="false"/> = background, <see langword="true"/> = foreground).</param>
    /// <param name="backgroundConnectivity">
    /// The neighborhood connectivity used to check adjacent pixels for background (<see langword="false"/>) values.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The list of foreground boundary coordinates in row-major order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binaryMask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="backgroundConnectivity"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static IReadOnlyList<Coord> ExtractBoundarySeeds(
        IReadOnlyMatrix<bool> binaryMask,
        GridConnectivity backgroundConnectivity = GridConnectivity.EightConnected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binaryMask);
        GridNeighbor[] neighbors = GridNeighborhood.GetAll(backgroundConnectivity).ToArray();
        cancellationToken.ThrowIfCancellationRequested();

        Size size = binaryMask.Size;
        if (size.IsEmpty)
            return new List<Coord>();

        int width = size.Width;
        int height = size.Height;
        bool[]? rawMask = binaryMask is SequentialMatrix<bool> sequential ? sequential.RawData : null;
        int minRowParallel = size.Count >= ParallelRunner.DefaultMinParallelCount && height >= 8 ? 2 : height + 1;

        List<Coord>[] stripSeeds = ParallelRunner.MapChunks(
            height,
            cancellationToken,
            (startRow, endRow) =>
            {
                var local = new List<Coord>();
                for (int y = startRow; y < endRow; y++)
                {
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        bool isFg = rawMask is not null ? rawMask[rowBase + x] : binaryMask[x, y];
                        if (!isFg)
                            continue;

                        var coord = new Coord(x, y);
                        for (int n = 0; n < neighbors.Length; n++)
                        {
                            Coord neighborCoord = coord + neighbors[n].Offset;
                            if (!size.Contains(neighborCoord))
                                continue;

                            bool neighborFg = rawMask is not null
                                ? rawMask[(neighborCoord.Y * width) + neighborCoord.X]
                                : binaryMask[neighborCoord.X, neighborCoord.Y];
                            if (!neighborFg)
                            {
                                local.Add(coord);
                                break;
                            }
                        }
                    }
                }

                return local;
            },
            minParallelCount: minRowParallel);

        if (stripSeeds.Length == 1)
            return stripSeeds[0];

        int totalCount = 0;
        for (int i = 0; i < stripSeeds.Length; i++)
            totalCount += stripSeeds[i].Count;

        var seeds = new List<Coord>(totalCount);
        for (int i = 0; i < stripSeeds.Length; i++)
            seeds.AddRange(stripSeeds[i]);

        return seeds;
    }

    /// <summary>
    /// Computes the nearest seed coordinate for every cell in a grid using <c>1+JFA</c>.
    /// </summary>
    /// <param name="size">The dimensions of the grid.</param>
    /// <param name="seeds">The seed coordinates (out-of-bounds coordinates are ignored).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A matrix storing each cell's <see cref="NearestSeed"/>, or <see cref="NearestSeed.None"/> everywhere if no valid in-bounds seeds are supplied.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="seeds"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Matrix<NearestSeed> ComputeNearestSeeds(
        Size size,
        IEnumerable<Coord> seeds,
        CancellationToken cancellationToken = default)
    {
        Guard.ThrowIfNegative(size);
        ArgumentNullException.ThrowIfNull(seeds);
        cancellationToken.ThrowIfCancellationRequested();

        if (size.IsEmpty)
            return new SequentialMatrix<NearestSeed>(size);

        var seedGrid = new Coord[size.Count];
        ParallelRunner.ForChunks(
            seedGrid.Length,
            cancellationToken,
            (start, end) => Array.Fill(seedGrid, NoSeed, start, end - start));

        bool hasAnySeed = false;
        foreach (Coord seed in seeds)
        {
            if (size.Contains(seed))
            {
                seedGrid[size.IndexOf(seed)] = seed;
                hasAnySeed = true;
            }
        }

        return RunJumpFlooding(size, seedGrid, hasAnySeed, cancellationToken);
    }

    /// <summary>
    /// Computes the nearest <see langword="true"/> seed coordinate in <paramref name="seedMask"/> for every cell using <c>1+JFA</c>.
    /// </summary>
    /// <param name="seedMask">The boolean mask whose <see langword="true"/> entries define seed pixels.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// A matrix storing each cell's <see cref="NearestSeed"/>, or <see cref="NearestSeed.None"/> everywhere if <paramref name="seedMask"/> contains no <see langword="true"/> entries.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="seedMask"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Matrix<NearestSeed> ComputeNearestSeeds(
        IReadOnlyMatrix<bool> seedMask,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seedMask);
        cancellationToken.ThrowIfCancellationRequested();

        Size size = seedMask.Size;
        if (size.IsEmpty)
            return new SequentialMatrix<NearestSeed>(size);

        int width = size.Width;
        int height = size.Height;
        var seedGrid = new Coord[seedMask.Count];
        bool[]? rawMask = seedMask is SequentialMatrix<bool> sequential ? sequential.RawData : null;
        int minRowParallel = size.Count >= ParallelRunner.DefaultMinParallelCount && height >= 8 ? 2 : height + 1;

        bool[] stripHasSeed = ParallelRunner.MapChunks(
            height,
            cancellationToken,
            (startRow, endRow) =>
            {
                bool localHasSeed = false;
                for (int y = startRow; y < endRow; y++)
                {
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        int index = rowBase + x;
                        bool isSeed = rawMask is not null ? rawMask[index] : seedMask[x, y];
                        if (isSeed)
                        {
                            seedGrid[index] = new Coord(x, y);
                            localHasSeed = true;
                        }
                        else
                        {
                            seedGrid[index] = NoSeed;
                        }
                    }
                }

                return localHasSeed;
            },
            minParallelCount: minRowParallel);

        bool hasAnySeed = false;
        for (int i = 0; i < stripHasSeed.Length; i++)
            hasAnySeed |= stripHasSeed[i];

        return RunJumpFlooding(size, seedGrid, hasAnySeed, cancellationToken);
    }

    /// <summary>
    /// Computes the Euclidean distance to the nearest seed coordinate for every cell in the grid.
    /// </summary>
    /// <param name="size">The dimensions of the grid.</param>
    /// <param name="seeds">The seed coordinates.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A matrix of Euclidean distances in pixels (<see cref="double.PositiveInfinity"/> if no seeds exist).</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="seeds"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Matrix<DoublePixel> ComputeDistanceTransform(
        Size size,
        IEnumerable<Coord> seeds,
        CancellationToken cancellationToken = default)
    {
        Matrix<NearestSeed> nearestSeeds = ComputeNearestSeeds(size, seeds, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return ComputeDistancesFromNearestSeeds(nearestSeeds);
    }

    /// <summary>
    /// Computes the Euclidean distance to the nearest <see langword="true"/> pixel in <paramref name="seedMask"/>.
    /// </summary>
    /// <param name="seedMask">The boolean mask whose <see langword="true"/> pixels act as seeds.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A matrix of Euclidean distances in pixels (<see cref="double.PositiveInfinity"/> if no seeds exist).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="seedMask"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static Matrix<DoublePixel> ComputeDistanceTransform(
        IReadOnlyMatrix<bool> seedMask,
        CancellationToken cancellationToken = default)
    {
        Matrix<NearestSeed> nearestSeeds = ComputeNearestSeeds(seedMask, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return ComputeDistancesFromNearestSeeds(nearestSeeds);
    }

    private static Matrix<NearestSeed> RunJumpFlooding(
        Size size,
        Coord[] currentSeeds,
        bool hasAnySeed,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new SequentialMatrix<NearestSeed>(size);

        if (!hasAnySeed)
        {
            result.SetAll(NearestSeed.None);
            return result;
        }

        int maxDimension = Math.Max(size.Width, size.Height);
        if (maxDimension > 1)
        {
            var nextSeeds = new Coord[currentSeeds.Length];

            ExecuteJumpPass(currentSeeds, nextSeeds, size, 1, cancellationToken);
            (currentSeeds, nextSeeds) = (nextSeeds, currentSeeds);

            int largestStep = 1 << BitOperations.Log2((uint)(maxDimension - 1));
            for (int step = largestStep; step >= 1; step >>= 1)
            {
                ExecuteJumpPass(currentSeeds, nextSeeds, size, step, cancellationToken);
                (currentSeeds, nextSeeds) = (nextSeeds, currentSeeds);
            }
        }

        NearestSeed[] resultData = result.RawData;
        ParallelRunner.ForChunks(
            currentSeeds.Length,
            cancellationToken,
            (start, end) =>
            {
                for (int i = start; i < end; i++)
                    resultData[i] = NearestSeed.FromCoord(currentSeeds[i]);
            });

        return result;
    }

    private static void ExecuteJumpPass(
        Coord[] source,
        Coord[] destination,
        Size size,
        int step,
        CancellationToken cancellationToken)
    {
        int width = size.Width;
        int height = size.Height;
        int minRowParallel = size.Count >= 4096 && height >= 8 ? 2 : height + 1;

        ParallelRunner.ForChunks(
            height,
            cancellationToken,
            (startRow, endRow) =>
            {
                for (int y = startRow; y < endRow; y++)
                {
                    int rowBase = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        var current = new Coord(x, y);
                        int currentIndex = rowBase + x;
                        Coord bestSeed = source[currentIndex];
                        long bestDistSq = bestSeed.X >= 0
                            ? current.DistanceSquared(bestSeed)
                            : long.MaxValue;

                        for (int dy = -step; dy <= step; dy += step)
                        {
                            int ny = y + dy;
                            if ((uint)ny >= (uint)height)
                                continue;

                            int nRowBase = ny * width;
                            for (int dx = -step; dx <= step; dx += step)
                            {
                                if (dx == 0 && dy == 0)
                                    continue;

                                int nx = x + dx;
                                if ((uint)nx >= (uint)width)
                                    continue;

                                Coord candidateSeed = source[nRowBase + nx];
                                if (candidateSeed.X < 0)
                                    continue;

                                long distSq = current.DistanceSquared(candidateSeed);
                                if (distSq < bestDistSq ||
                                    (distSq == bestDistSq &&
                                     (candidateSeed.Y < bestSeed.Y || (candidateSeed.Y == bestSeed.Y && candidateSeed.X < bestSeed.X))))
                                {
                                    bestDistSq = distSq;
                                    bestSeed = candidateSeed;
                                }
                            }
                        }

                        destination[currentIndex] = bestSeed;
                    }
                }
            },
            minParallelCount: minRowParallel);
    }

    private static Matrix<DoublePixel> ComputeDistancesFromNearestSeeds(IReadOnlyMatrix<NearestSeed> nearestSeeds) =>
        nearestSeeds.MapIndexed(static (x, y, entry) =>
            new DoublePixel(entry.HasSeed ? entry.Seed.Distance(new Coord(x, y)) : double.PositiveInfinity));
}
