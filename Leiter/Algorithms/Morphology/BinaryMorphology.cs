namespace Leiter.Algorithms.Morphology;

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Core;

/// <summary>
/// Provides fast binary morphological operations (<see cref="Dilate"/>, <see cref="Erode"/>, <see cref="Open"/>, and <see cref="Close"/>)
/// on boolean mask matrices.
/// </summary>
public static class BinaryMorphology
{
    /// <summary>
    /// The default minimum connected-component area fraction (<c>0.01</c>).
    /// </summary>
    public const double DefaultMinAreaFraction = 0.01;

    /// <summary>
    /// Dilates foreground (<see langword="true"/>) pixels in a boolean mask using a structuring element of the specified radius and shape.
    /// </summary>
    /// <param name="mask">The boolean mask (<see langword="false"/> = background, <see langword="true"/> = foreground).</param>
    /// <param name="radius">The non-negative structuring element radius in pixels.</param>
    /// <param name="shape">The shape of the structuring element.</param>
    /// <param name="borderIsBackground">
    /// If <see langword="true"/>, samples outside the image bounds are treated as background (<see langword="false"/>);
    /// otherwise coordinates are clamped to the nearest image edge. For centered symmetric structuring elements
    /// (<see cref="StructuringElementShape.Disk"/> and <see cref="StructuringElementShape.Square"/>), both modes produce
    /// identical results because clamped edge coordinates already lie within the in-bounds structuring element.
    /// </param>
    /// <returns>A <see cref="MaskMatrix"/> containing <see langword="true"/> for dilated foreground pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is negative or <paramref name="shape"/> is invalid.</exception>
    public static MaskMatrix Dilate(
        IReadOnlyMatrix<bool> mask,
        int radius,
        StructuringElementShape shape = StructuringElementShape.Disk,
        bool borderIsBackground = false) =>
        ApplyMorphology(mask, radius, shape, isErosion: false, borderIsBackground: borderIsBackground);

    /// <summary>
    /// Erodes foreground (<see langword="true"/>) pixels in a boolean mask using a structuring element of the specified radius and shape.
    /// </summary>
    /// <param name="mask">The boolean mask (<see langword="false"/> = background, <see langword="true"/> = foreground).</param>
    /// <param name="radius">The non-negative structuring element radius in pixels.</param>
    /// <param name="shape">The shape of the structuring element.</param>
    /// <param name="borderIsBackground">
    /// If <see langword="true"/>, samples outside the image bounds are treated as background (<see langword="false"/>);
    /// otherwise coordinates are clamped to the nearest image edge.
    /// </param>
    /// <returns>A <see cref="MaskMatrix"/> containing <see langword="true"/> for eroded foreground pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="radius"/> is negative or <paramref name="shape"/> is invalid.</exception>
    public static MaskMatrix Erode(
        IReadOnlyMatrix<bool> mask,
        int radius,
        StructuringElementShape shape = StructuringElementShape.Disk,
        bool borderIsBackground = false) =>
        ApplyMorphology(mask, radius, shape, isErosion: true, borderIsBackground: borderIsBackground);

    private static MaskMatrix ApplyMorphology(
        IReadOnlyMatrix<bool> mask,
        int radius,
        StructuringElementShape shape,
        bool isErosion,
        bool borderIsBackground)
    {
        ValidateInputs(mask, radius, shape);

        if (mask.Count == 0 || radius == 0)
            return CloneMask(mask);

        Size size = mask.Size;
        int width = size.Width;
        int height = size.Height;
        int rowStride = width + 1;
        int[] rowPrefixSums = BuildRowPrefixSums(mask);
        int[] horizontalHalfWidths = BuildHorizontalHalfWidths(radius, shape);
        var result = new MaskMatrix(width, height);
        bool[] resultData = result.RawData;

        Parallel.For(0, height, y =>
        {
            if (isErosion && borderIsBackground && (y - radius < 0 || y + radius >= height))
                return;

            int minNeighborY = Math.Max(0, y - radius);
            int maxNeighborY = Math.Min(height - 1, y + radius);
            int dstRowBase = size.IndexOf(0, y);

            for (int x = 0; x < width; x++)
            {
                if (isErosion && borderIsBackground && (x - radius < 0 || x + radius >= width))
                    continue;

                bool resultValue = isErosion;
                for (int neighborY = minNeighborY; neighborY <= maxNeighborY; neighborY++)
                {
                    int halfWidth = horizontalHalfWidths[Math.Abs(neighborY - y)];
                    int left = Math.Max(0, x - halfWidth);
                    int right = Math.Min(width - 1, x + halfWidth);
                    int rowOffset = neighborY * rowStride;
                    int foregroundCount = rowPrefixSums[rowOffset + right + 1] - rowPrefixSums[rowOffset + left];

                    if (isErosion)
                    {
                        if (foregroundCount < right - left + 1)
                        {
                            resultValue = false;
                            break;
                        }
                    }
                    else if (foregroundCount > 0)
                    {
                        resultValue = true;
                        break;
                    }
                }

                resultData[dstRowBase + x] = resultValue;
            }
        });

        return result;
    }

    /// <summary>
    /// Applies morphological opening (<see cref="Erode"/> followed by <see cref="Dilate"/>) to remove isolated foreground specks.
    /// </summary>
    /// <param name="mask">The boolean mask.</param>
    /// <param name="radius">The non-negative structuring element radius in pixels.</param>
    /// <param name="shape">The shape of the structuring element.</param>
    /// <param name="borderIsBackground">Whether out-of-bounds samples are treated as background.</param>
    /// <returns>The opened <see cref="MaskMatrix"/>.</returns>
    public static MaskMatrix Open(
        IReadOnlyMatrix<bool> mask,
        int radius,
        StructuringElementShape shape = StructuringElementShape.Disk,
        bool borderIsBackground = false)
    {
        return Dilate(Erode(mask, radius, shape, borderIsBackground), radius, shape, borderIsBackground);
    }

    /// <summary>
    /// Applies morphological closing (<see cref="Dilate"/> followed by <see cref="Erode"/>) to fill small background holes.
    /// </summary>
    /// <param name="mask">The boolean mask.</param>
    /// <param name="radius">The non-negative structuring element radius in pixels.</param>
    /// <param name="shape">The shape of the structuring element.</param>
    /// <param name="borderIsBackground">Whether out-of-bounds samples are treated as background.</param>
    /// <returns>The closed <see cref="MaskMatrix"/>.</returns>
    public static MaskMatrix Close(
        IReadOnlyMatrix<bool> mask,
        int radius,
        StructuringElementShape shape = StructuringElementShape.Disk,
        bool borderIsBackground = false)
    {
        return Erode(Dilate(mask, radius, shape, borderIsBackground), radius, shape, borderIsBackground);
    }

    /// <summary>
    /// Filters connected foreground components in a boolean mask according to the specified retention policy and grid connectivity.
    /// </summary>
    /// <param name="mask">The boolean mask (<see langword="false"/> = background, <see langword="true"/> = foreground).</param>
    /// <param name="retentionPolicy">The connected-component retention rule.</param>
    /// <param name="minAreaFraction">The minimum fraction of total pixels in [0, 1] required when using <see cref="ComponentRetentionPolicy.MinimumAreaFraction"/>.</param>
    /// <param name="connectivity">The neighborhood connectivity used to group connected foreground pixels.</param>
    /// <returns>A filtered <see cref="MaskMatrix"/> containing only the retained components.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mask"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="retentionPolicy"/> or <paramref name="connectivity"/> is invalid,
    /// or <paramref name="minAreaFraction"/> is outside [0, 1].
    /// </exception>
    public static MaskMatrix FilterConnectedComponents(
        IReadOnlyMatrix<bool> mask,
        ComponentRetentionPolicy retentionPolicy = ComponentRetentionPolicy.MinimumAreaFraction,
        double minAreaFraction = DefaultMinAreaFraction,
        GridConnectivity connectivity = GridConnectivity.EightConnected)
    {
        ArgumentNullException.ThrowIfNull(mask);
        Guard.ThrowIfUndefinedEnum(retentionPolicy);
        Guard.ThrowIfNotFiniteInRange(minAreaFraction, 0.0, 1.0);
        Guard.ThrowIfUndefinedEnum(connectivity);

        if (mask.Count == 0)
            return new MaskMatrix(mask.Size);

        if (retentionPolicy == ComponentRetentionPolicy.KeepAll)
            return MaskMatrix.From(mask);

        Size size = mask.Size;
        int width = size.Width;
        int height = size.Height;
        bool[] isForeground = ExtractForegroundArray(mask);

        if (!isForeground.AsSpan().Contains(true))
            return new MaskMatrix(width, height);

        var forest = new DisjointSetForest(isForeground.Length);
        bool isEightConnected = connectivity == GridConnectivity.EightConnected;
        int stripCount = isForeground.Length >= ParallelRunner.DefaultMinParallelCount && height >= 8 && Environment.ProcessorCount > 1
            ? Math.Min(height, Math.Clamp(Environment.ProcessorCount * 2, 2, 64))
            : 1;

        var stripRanges = new (int StartRow, int EndRow)[stripCount];
        for (int s = 0; s < stripCount; s++)
            stripRanges[s] = ParallelRunner.GetChunkBounds(s, stripCount, height);

        // Step 1: Union foreground pixels within disjoint horizontal strips in parallel.
        ParallelRunner.For(stripCount, CancellationToken.None, s =>
        {
            (int startRow, int endRow) = stripRanges[s];
            for (int y = startRow; y < endRow; y++)
            {
                int rowBase = y * width;
                bool hasSouthInStrip = y + 1 < endRow;
                for (int x = 0; x < width; x++)
                {
                    int p = rowBase + x;
                    if (!isForeground[p])
                        continue;

                    if (x + 1 < width && isForeground[p + 1])
                        forest.Union(p, p + 1);

                    if (hasSouthInStrip)
                        UnionSouthNeighbors(forest, isForeground, p, x, width, isEightConnected);
                }
            }
        });

        // Step 2: Stitch adjacent horizontal strips sequentially across the P - 1 shared seams.
        for (int s = 0; s < stripCount - 1; s++)
        {
            int seamTopRow = stripRanges[s].EndRow - 1;
            int rowBase = seamTopRow * width;
            for (int x = 0; x < width; x++)
            {
                int p = rowBase + x;
                if (isForeground[p])
                    UnionSouthNeighbors(forest, isForeground, p, x, width, isEightConnected);
            }
        }

        // Step 3: Retain components matching the policy in parallel (parent array is now read-only).
        var filtered = new MaskMatrix(width, height);
        bool[] filteredData = filtered.RawData;
        if (retentionPolicy == ComponentRetentionPolicy.LargestComponentOnly)
        {
            (int BestRoot, int BestSize)[] chunkBest = ParallelRunner.MapChunks(
                isForeground.Length,
                CancellationToken.None,
                (start, end) =>
                {
                    int bestRoot = -1;
                    int bestSize = 0;
                    for (int i = start; i < end; i++)
                    {
                        if (!isForeground[i])
                            continue;

                        int root = forest.FindReadOnly(i);
                        int componentSize = forest.GetRootSize(root);
                        if (componentSize > bestSize)
                        {
                            bestSize = componentSize;
                            bestRoot = root;
                        }
                    }

                    return (bestRoot, bestSize);
                });

            int largestRoot = -1;
            int largestSize = 0;
            for (int i = 0; i < chunkBest.Length; i++)
            {
                if (chunkBest[i].BestSize > largestSize)
                {
                    largestSize = chunkBest[i].BestSize;
                    largestRoot = chunkBest[i].BestRoot;
                }
            }

            ParallelRunner.ForChunks(
                isForeground.Length,
                CancellationToken.None,
                (start, end) =>
                {
                    for (int i = start; i < end; i++)
                    {
                        if (isForeground[i] && forest.FindReadOnly(i) == largestRoot)
                            filteredData[i] = true;
                    }
                });
        }
        else
        {
            int minPixels = Math.Max(1, (int)Math.Ceiling(minAreaFraction * mask.Count));
            ParallelRunner.ForChunks(
                isForeground.Length,
                CancellationToken.None,
                (start, end) =>
                {
                    for (int i = start; i < end; i++)
                    {
                        if (isForeground[i] && forest.GetRootSize(forest.FindReadOnly(i)) >= minPixels)
                            filteredData[i] = true;
                    }
                });
        }

        return filtered;
    }

    private static bool[] ExtractForegroundArray(IReadOnlyMatrix<bool> mask)
    {
        if (mask.TryGetRawArray() is bool[] rawMask)
            return rawMask;

        int width = mask.Width;
        int height = mask.Height;
        var isForeground = new bool[mask.Count];
        if (mask.TryGetReadOnlySpan(out ReadOnlySpan<bool> maskSpan))
        {
            maskSpan.CopyTo(isForeground);
            return isForeground;
        }

        for (int y = 0; y < height; y++)
        {
            int rowBase = y * width;
            for (int x = 0; x < width; x++)
                isForeground[rowBase + x] = mask[x, y];
        }

        return isForeground;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UnionSouthNeighbors(
        DisjointSetForest forest,
        bool[] isForeground,
        int pixelIndex,
        int x,
        int width,
        bool isEightConnected)
    {
        int south = pixelIndex + width;
        if (isForeground[south])
            forest.Union(pixelIndex, south);

        if (isEightConnected)
        {
            if (x + 1 < width && isForeground[south + 1])
                forest.Union(pixelIndex, south + 1);

            if (x > 0 && isForeground[south - 1])
                forest.Union(pixelIndex, south - 1);
        }
    }

    private static void ValidateInputs(IReadOnlyMatrix<bool> mask, int radius, StructuringElementShape shape)
    {
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        Guard.ThrowIfUndefinedEnum(shape);
    }

    private static MaskMatrix CloneMask(IReadOnlyMatrix<bool> mask) =>
        MaskMatrix.From(mask);

    private static int[] BuildRowPrefixSums(IReadOnlyMatrix<bool> mask)
    {
        Size size = mask.Size;
        int width = size.Width;
        int height = size.Height;
        int rowStride = width + 1;
        var rowPrefixSums = new int[height * rowStride];

        if (mask is SequentialMatrix<bool> sequentialMask)
        {
            bool[] rawMask = sequentialMask.RawData;
            Parallel.For(0, height, y =>
            {
                int srcRowOffset = size.IndexOf(0, y);
                int dstRowOffset = y * rowStride;
                int runningSum = 0;
                for (int x = 0; x < width; x++)
                {
                    if (rawMask[srcRowOffset + x])
                        runningSum++;
                    rowPrefixSums[dstRowOffset + x + 1] = runningSum;
                }
            });
        }
        else
        {
            Parallel.For(0, height, y =>
            {
                int rowOffset = y * rowStride;
                int runningSum = 0;
                for (int x = 0; x < width; x++)
                {
                    if (mask[x, y])
                        runningSum++;
                    rowPrefixSums[rowOffset + x + 1] = runningSum;
                }
            });
        }

        return rowPrefixSums;
    }

    private static int[] BuildHorizontalHalfWidths(int radius, StructuringElementShape shape)
    {
        var halfWidths = new int[radius + 1];
        long radiusSquared = (long)radius * radius;

        for (int absDy = 0; absDy <= radius; absDy++)
        {
            halfWidths[absDy] = shape == StructuringElementShape.Square
                ? radius
                : (int)Math.Floor(Math.Sqrt(radiusSquared - ((long)absDy * absDy)));
        }

        return halfWidths;
    }
}
