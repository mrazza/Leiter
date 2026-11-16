namespace Leiter.Algorithms.DistanceTransform;

using System.Numerics;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Implements the 1+JFA Jump Flooding Algorithm for computing Voronoi nearest-seed maps
/// and Euclidean distance transforms on 2D grids in <c>O(W · H · log max(W, H))</c> time.
/// </summary>
public static class JumpFlooding
{
    private static readonly Coord[] FourConnectedOffsets =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    private static readonly Coord[] EightConnectedOffsets =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
        new(1, 1),
        new(1, -1),
        new(-1, 1),
        new(-1, -1),
    ];

    /// <summary>
    /// Extracts all foreground coordinates in a binary mask that border at least one background pixel.
    /// </summary>
    /// <param name="binaryMask">The binary mask (<c>0</c> = background, non-zero = foreground).</param>
    /// <param name="eightConnectedBackground">
    /// If <see langword="true"/>, checks all 8 neighbors for background pixels; otherwise checks only the 4 cardinal neighbors.
    /// </param>
    /// <returns>The list of foreground boundary coordinates in row-major order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="binaryMask"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<Coord> ExtractBoundarySeeds(
        IReadOnlyMatrix<LongPixel> binaryMask,
        bool eightConnectedBackground = true)
    {
        ArgumentNullException.ThrowIfNull(binaryMask);

        var seeds = new List<Coord>();
        int width = binaryMask.Width;
        int height = binaryMask.Height;
        if (width == 0 || height == 0)
            return seeds;

        Coord[] offsets = eightConnectedBackground ? EightConnectedOffsets : FourConnectedOffsets;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (binaryMask[x, y].Value == 0)
                    continue;

                foreach (Coord offset in offsets)
                {
                    int nx = x + offset.X;
                    int ny = y + offset.Y;
                    if ((uint)nx < (uint)width && (uint)ny < (uint)height && binaryMask[nx, ny].Value == 0)
                    {
                        seeds.Add(new Coord(x, y));
                        break;
                    }
                }
            }
        }

        return seeds;
    }

    /// <summary>
    /// Computes the row-major linear index of the nearest seed pixel for every cell in a grid using <c>1+JFA</c>.
    /// </summary>
    /// <param name="size">The dimensions of the grid.</param>
    /// <param name="seeds">The seed coordinates (out-of-bounds coordinates are ignored).</param>
    /// <returns>
    /// A matrix storing the nearest seed linear index (<c>y * Width + x</c>) per pixel,
    /// or <c>-1</c> everywhere if no valid in-bounds seeds are supplied.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="seeds"/> is <see langword="null"/>.</exception>
    public static Matrix<LongPixel> ComputeNearestSeedIndices(Size size, IEnumerable<Coord> seeds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size.Width, nameof(size));
        ArgumentOutOfRangeException.ThrowIfNegative(size.Height, nameof(size));
        ArgumentNullException.ThrowIfNull(seeds);

        if (size.Width == 0 || size.Height == 0)
            return new SequentialMatrix<LongPixel>(size);

        var seedGrid = new int[size.Width * size.Height];
        Array.Fill(seedGrid, -1);

        bool hasAnySeed = false;
        foreach (Coord seed in seeds)
        {
            if ((uint)seed.X < (uint)size.Width && (uint)seed.Y < (uint)size.Height)
            {
                int index = (seed.Y * size.Width) + seed.X;
                seedGrid[index] = index;
                hasAnySeed = true;
            }
        }

        return RunJumpFlooding(size, seedGrid, hasAnySeed);
    }

    /// <summary>
    /// Computes the row-major linear index of the nearest non-zero mask pixel for every cell using <c>1+JFA</c>.
    /// </summary>
    /// <param name="seedMask">The mask whose non-zero entries define seed pixels.</param>
    /// <returns>
    /// A matrix storing the nearest seed linear index (<c>y * Width + x</c>) per pixel,
    /// or <c>-1</c> everywhere if <paramref name="seedMask"/> contains no non-zero entries.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="seedMask"/> is <see langword="null"/>.</exception>
    public static Matrix<LongPixel> ComputeNearestSeedIndices(IReadOnlyMatrix<LongPixel> seedMask)
    {
        ArgumentNullException.ThrowIfNull(seedMask);

        if (seedMask.Count == 0)
            return new SequentialMatrix<LongPixel>(seedMask.Size);

        int width = seedMask.Width;
        int height = seedMask.Height;
        var seedGrid = new int[seedMask.Count];
        bool hasAnySeed = false;

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
            {
                int index = rowBase + x;
                if (seedMask[x, y].Value != 0)
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

        return RunJumpFlooding(seedMask.Size, seedGrid, hasAnySeed);
    }

    /// <summary>
    /// Computes the Euclidean distance to the nearest seed coordinate for every cell in the grid.
    /// </summary>
    /// <param name="size">The dimensions of the grid.</param>
    /// <param name="seeds">The seed coordinates.</param>
    /// <returns>A matrix of Euclidean distances in pixels (<see cref="double.PositiveInfinity"/> if no seeds exist).</returns>
    public static Matrix<DoublePixel> ComputeDistanceTransform(Size size, IEnumerable<Coord> seeds) =>
        ComputeDistancesFromSeedIndices(ComputeNearestSeedIndices(size, seeds));

    /// <summary>
    /// Computes the Euclidean distance to the nearest non-zero pixel in <paramref name="seedMask"/>.
    /// </summary>
    /// <param name="seedMask">The mask whose non-zero pixels act as seeds.</param>
    /// <returns>A matrix of Euclidean distances in pixels (<see cref="double.PositiveInfinity"/> if no seeds exist).</returns>
    public static Matrix<DoublePixel> ComputeDistanceTransform(IReadOnlyMatrix<LongPixel> seedMask) =>
        ComputeDistancesFromSeedIndices(ComputeNearestSeedIndices(seedMask));

    private static Matrix<LongPixel> RunJumpFlooding(Size size, int[] currentSeeds, bool hasAnySeed)
    {
        int width = size.Width;
        int height = size.Height;
        var result = new SequentialMatrix<LongPixel>(width, height);

        if (!hasAnySeed)
        {
            result.SetAll(-1L);
            return result;
        }

        int maxDimension = Math.Max(width, height);
        if (maxDimension > 1)
        {
            var nextSeeds = new int[currentSeeds.Length];

            ExecuteJumpPass(currentSeeds, nextSeeds, width, height, 1);
            (currentSeeds, nextSeeds) = (nextSeeds, currentSeeds);

            int largestStep = 1 << BitOperations.Log2((uint)(maxDimension - 1));
            for (int step = largestStep; step >= 1; step >>= 1)
            {
                ExecuteJumpPass(currentSeeds, nextSeeds, width, height, step);
                (currentSeeds, nextSeeds) = (nextSeeds, currentSeeds);
            }
        }

        for (int i = 0; i < currentSeeds.Length; i++)
            result[i] = currentSeeds[i];

        return result;
    }

    private static void ExecuteJumpPass(int[] source, int[] destination, int width, int height, int step)
    {
        Parallel.For(0, height, y =>
        {
            int rowBase = y * width;

            for (int x = 0; x < width; x++)
            {
                int currentIndex = rowBase + x;
                int bestSeed = source[currentIndex];
                long bestDistSq = bestSeed >= 0
                    ? DistanceSquared(x, y, bestSeed % width, bestSeed / width)
                    : long.MaxValue;

                for (int dy = -step; dy <= step; dy += step)
                {
                    int ny = y + dy;
                    if ((uint)ny >= (uint)height)
                        continue;

                    int neighborRowBase = ny * width;
                    for (int dx = -step; dx <= step; dx += step)
                    {
                        if (dx == 0 && dy == 0)
                            continue;

                        int nx = x + dx;
                        if ((uint)nx >= (uint)width)
                            continue;

                        int candidateSeed = source[neighborRowBase + nx];
                        if (candidateSeed < 0)
                            continue;

                        long distSq = DistanceSquared(x, y, candidateSeed % width, candidateSeed / width);
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

    private static Matrix<DoublePixel> ComputeDistancesFromSeedIndices(Matrix<LongPixel> seedIndices)
    {
        int width = seedIndices.Width;
        int height = seedIndices.Height;
        var distances = new SequentialMatrix<DoublePixel>(width, height);

        if (seedIndices.Count == 0)
            return distances;

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                long seedIndex = seedIndices[x, y].Value;
                if (seedIndex < 0)
                {
                    distances[x, y] = double.PositiveInfinity;
                }
                else
                {
                    int seedX = (int)(seedIndex % width);
                    int seedY = (int)(seedIndex / width);
                    distances[x, y] = Math.Sqrt(DistanceSquared(x, y, seedX, seedY));
                }
            }
        });

        return distances;
    }

    private static long DistanceSquared(int x0, int y0, int x1, int y1)
    {
        long dx = x0 - x1;
        long dy = y0 - y1;
        return (dx * dx) + (dy * dy);
    }
}
