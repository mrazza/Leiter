namespace Leiter.Algorithms.DataStructures;

using System.Runtime.CompilerServices;
using Leiter.Core;

/// <summary>
/// Provides standard 4- and 8-connected 2D pixel grid neighborhood stencils and direction helpers.
/// </summary>
public static class GridNeighborhood
{
    private static readonly GridNeighbor[] All8ConnectedData =
    [
        new(GridDirection.East, new Coord(1, 0), 1.0, 1.0),
        new(GridDirection.South, new Coord(0, 1), 1.0, 1.0),
        new(GridDirection.SouthEast, new Coord(1, 1), ScalarMath.Sqrt2, ScalarMath.InvSqrt2),
        new(GridDirection.SouthWest, new Coord(-1, 1), ScalarMath.Sqrt2, ScalarMath.InvSqrt2),
        new(GridDirection.West, new Coord(-1, 0), 1.0, 1.0),
        new(GridDirection.North, new Coord(0, -1), 1.0, 1.0),
        new(GridDirection.NorthWest, new Coord(-1, -1), ScalarMath.Sqrt2, ScalarMath.InvSqrt2),
        new(GridDirection.NorthEast, new Coord(1, -1), ScalarMath.Sqrt2, ScalarMath.InvSqrt2),
    ];

    private static readonly GridNeighbor[] Forward8ConnectedData =
    [
        All8ConnectedData[(int)GridDirection.East],
        All8ConnectedData[(int)GridDirection.South],
        All8ConnectedData[(int)GridDirection.SouthEast],
        All8ConnectedData[(int)GridDirection.SouthWest],
    ];

    private static readonly GridNeighbor[] Forward4ConnectedData =
    [
        All8ConnectedData[(int)GridDirection.East],
        All8ConnectedData[(int)GridDirection.South],
    ];

    private static readonly GridNeighbor[] All4ConnectedData =
    [
        All8ConnectedData[(int)GridDirection.East],
        All8ConnectedData[(int)GridDirection.South],
        All8ConnectedData[(int)GridDirection.West],
        All8ConnectedData[(int)GridDirection.North],
    ];

    /// <summary>
    /// Gets the two forward 4-connected neighbor steps (<see cref="GridDirection.East"/> and <see cref="GridDirection.South"/>)
    /// for constructing undirected 4-connected lattice edges once per pair.
    /// </summary>
    public static ReadOnlySpan<GridNeighbor> Forward4Connected => Forward4ConnectedData;

    /// <summary>
    /// Gets the four forward 8-connected neighbor steps (<see cref="GridDirection.East"/>, <see cref="GridDirection.South"/>,
    /// <see cref="GridDirection.SouthEast"/>, and <see cref="GridDirection.SouthWest"/>)
    /// for constructing undirected 8-connected lattice edges once per pair.
    /// </summary>
    public static ReadOnlySpan<GridNeighbor> Forward8Connected => Forward8ConnectedData;

    /// <summary>
    /// Gets the four cardinal neighbor steps (<see cref="GridDirection.East"/>, <see cref="GridDirection.South"/>,
    /// <see cref="GridDirection.West"/>, and <see cref="GridDirection.North"/>).
    /// </summary>
    public static ReadOnlySpan<GridNeighbor> All4Connected => All4ConnectedData;

    /// <summary>
    /// Gets all eight neighbor steps ordered by <see cref="GridDirection"/> (<c>0</c> through <c>7</c>).
    /// </summary>
    public static ReadOnlySpan<GridNeighbor> All8Connected => All8ConnectedData;

    /// <summary>
    /// Gets the forward scanline half-stencil for the specified <paramref name="connectivity"/>.
    /// </summary>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <returns>The span of forward neighbor steps for <paramref name="connectivity"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="connectivity"/> is not a defined <see cref="GridConnectivity"/> value.</exception>
    public static ReadOnlySpan<GridNeighbor> GetForward(GridConnectivity connectivity) => connectivity switch
    {
        GridConnectivity.FourConnected => Forward4ConnectedData,
        GridConnectivity.EightConnected => Forward8ConnectedData,
        _ => throw new ArgumentOutOfRangeException(nameof(connectivity), connectivity, $"Unsupported {nameof(GridConnectivity)} value."),
    };

    /// <summary>
    /// Gets all outgoing neighbor steps for the specified <paramref name="connectivity"/>.
    /// </summary>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <returns>The span of all neighbor steps for <paramref name="connectivity"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="connectivity"/> is not a defined <see cref="GridConnectivity"/> value.</exception>
    public static ReadOnlySpan<GridNeighbor> GetAll(GridConnectivity connectivity) => connectivity switch
    {
        GridConnectivity.FourConnected => All4ConnectedData,
        GridConnectivity.EightConnected => All8ConnectedData,
        _ => throw new ArgumentOutOfRangeException(nameof(connectivity), connectivity, $"Unsupported {nameof(GridConnectivity)} value."),
    };

    /// <summary>
    /// Returns the opposite grid direction.
    /// </summary>
    /// <param name="direction">The grid direction to invert.</param>
    /// <returns>The opposite <see cref="GridDirection"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="direction"/> is not a defined <see cref="GridDirection"/> value.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GridDirection Opposite(this GridDirection direction)
    {
        if ((byte)direction >= 8)
            throw new ArgumentOutOfRangeException(nameof(direction), direction, $"Unsupported {nameof(GridDirection)} value.");

        return (GridDirection)((byte)direction ^ 4);
    }

    /// <summary>
    /// Returns the coordinate offset corresponding to the specified grid direction.
    /// </summary>
    /// <param name="direction">The grid direction.</param>
    /// <returns>The <see cref="Coord"/> delta for one step in <paramref name="direction"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="direction"/> is not a defined <see cref="GridDirection"/> value.</exception>
    public static Coord ToOffset(this GridDirection direction)
    {
        if ((byte)direction >= 8)
            throw new ArgumentOutOfRangeException(nameof(direction), direction, $"Unsupported {nameof(GridDirection)} value.");

        return All8ConnectedData[(byte)direction].Offset;
    }

    /// <summary>
    /// Packs two 32-bit integer identifiers into a canonical order-independent 64-bit pair key.
    /// </summary>
    /// <param name="first">The first identifier.</param>
    /// <param name="second">The second identifier.</param>
    /// <returns>A 64-bit key storing the smaller signed value in the upper 32 bits and the larger in the lower 32 bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong PackUnorderedPairKey(int first, int second)
    {
        (int low, int high) = first < second ? (first, second) : (second, first);
        return ((ulong)(uint)low << 32) | (uint)high;
    }

    /// <summary>
    /// Iterates sequentially over all unique forward neighbor index pairs in row-major order.
    /// </summary>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="visitor">Callback invoked with <c>(firstIndex, secondIndex)</c> for each valid forward neighbor pair.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="connectivity"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPair(
        Size size,
        GridConnectivity connectivity,
        Action<int, int> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ForEachForwardPair(size, connectivity, visitor, static (firstIndex, secondIndex, action) => action(firstIndex, secondIndex));
    }

    /// <summary>
    /// Iterates sequentially over all unique forward neighbor index pairs in row-major order, passing <paramref name="state"/> without per-pixel allocations.
    /// </summary>
    /// <typeparam name="TState">The caller state type forwarded to <paramref name="visitor"/>.</typeparam>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="state">Caller state forwarded to <paramref name="visitor"/>.</param>
    /// <param name="visitor">Callback invoked with <c>(firstIndex, secondIndex, state)</c> for each valid forward neighbor pair.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="connectivity"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPair<TState>(
        Size size,
        GridConnectivity connectivity,
        TState state,
        Action<int, int, TState> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ValidateGridAndConnectivity(size, connectivity);
        if (size.IsEmpty)
            return;

        for (int y = 0; y < size.Height; y++)
            ForEachForwardPairInRowCore(size, y, connectivity, state, visitor);
    }

    /// <summary>
    /// Iterates sequentially over all unique forward neighbor coordinate pairs in row-major order.
    /// </summary>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="visitor">Callback invoked with <c>(firstCoord, secondCoord, neighbor)</c> for each valid forward neighbor pair.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="connectivity"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPair(
        Size size,
        GridConnectivity connectivity,
        Action<Coord, Coord, GridNeighbor> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ForEachForwardPair(size, connectivity, visitor, static (first, second, neighbor, action) => action(first, second, neighbor));
    }

    /// <summary>
    /// Iterates sequentially over all unique forward neighbor coordinate pairs in row-major order, passing <paramref name="state"/> without per-pixel allocations.
    /// </summary>
    /// <typeparam name="TState">The caller state type forwarded to <paramref name="visitor"/>.</typeparam>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="state">Caller state forwarded to <paramref name="visitor"/>.</param>
    /// <param name="visitor">Callback invoked with <c>(firstCoord, secondCoord, neighbor, state)</c> for each valid forward neighbor pair.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="connectivity"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPair<TState>(
        Size size,
        GridConnectivity connectivity,
        TState state,
        Action<Coord, Coord, GridNeighbor, TState> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ValidateGridAndConnectivity(size, connectivity);
        if (size.IsEmpty)
            return;

        for (int y = 0; y < size.Height; y++)
            ForEachForwardPairInRowCore(size, y, connectivity, state, visitor);
    }

    /// <summary>
    /// Iterates over all unique forward neighbor index pairs originating in row <paramref name="y"/>.
    /// </summary>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="y">The zero-based row index in <c>[0, size.Height)</c>.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="visitor">Callback invoked with <c>(firstIndex, secondIndex)</c> for each valid forward neighbor pair in row <paramref name="y"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="size"/> has negative dimensions, <paramref name="y"/> is outside <c>[0, size.Height)</c>, or <paramref name="connectivity"/> is invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPairInRow(
        Size size,
        int y,
        GridConnectivity connectivity,
        Action<int, int> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ForEachForwardPairInRow(size, y, connectivity, visitor, static (firstIndex, secondIndex, action) => action(firstIndex, secondIndex));
    }

    /// <summary>
    /// Iterates over all unique forward neighbor index pairs originating in row <paramref name="y"/>, passing <paramref name="state"/> without per-pixel allocations.
    /// </summary>
    /// <typeparam name="TState">The caller state type forwarded to <paramref name="visitor"/>.</typeparam>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="y">The zero-based row index in <c>[0, size.Height)</c>.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="state">Caller state forwarded to <paramref name="visitor"/>.</param>
    /// <param name="visitor">Callback invoked with <c>(firstIndex, secondIndex, state)</c> for each valid forward neighbor pair in row <paramref name="y"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="size"/> has negative dimensions, <paramref name="y"/> is outside <c>[0, size.Height)</c>, or <paramref name="connectivity"/> is invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPairInRow<TState>(
        Size size,
        int y,
        GridConnectivity connectivity,
        TState state,
        Action<int, int, TState> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ValidateRowAndConnectivity(size, y, connectivity);
        if (size.Width == 0)
            return;

        ForEachForwardPairInRowCore(size, y, connectivity, state, visitor);
    }

    /// <summary>
    /// Iterates over all unique forward neighbor coordinate pairs originating in row <paramref name="y"/>.
    /// </summary>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="y">The zero-based row index in <c>[0, size.Height)</c>.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="visitor">Callback invoked with <c>(firstCoord, secondCoord, neighbor)</c> for each valid forward neighbor pair in row <paramref name="y"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="size"/> has negative dimensions, <paramref name="y"/> is outside <c>[0, size.Height)</c>, or <paramref name="connectivity"/> is invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPairInRow(
        Size size,
        int y,
        GridConnectivity connectivity,
        Action<Coord, Coord, GridNeighbor> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ForEachForwardPairInRow(size, y, connectivity, visitor, static (first, second, neighbor, action) => action(first, second, neighbor));
    }

    /// <summary>
    /// Iterates over all unique forward neighbor coordinate pairs originating in row <paramref name="y"/>, passing <paramref name="state"/> without per-pixel allocations.
    /// </summary>
    /// <typeparam name="TState">The caller state type forwarded to <paramref name="visitor"/>.</typeparam>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="y">The zero-based row index in <c>[0, size.Height)</c>.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="state">Caller state forwarded to <paramref name="visitor"/>.</param>
    /// <param name="visitor">Callback invoked with <c>(firstCoord, secondCoord, neighbor, state)</c> for each valid forward neighbor pair in row <paramref name="y"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="size"/> has negative dimensions, <paramref name="y"/> is outside <c>[0, size.Height)</c>, or <paramref name="connectivity"/> is invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPairInRow<TState>(
        Size size,
        int y,
        GridConnectivity connectivity,
        TState state,
        Action<Coord, Coord, GridNeighbor, TState> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ValidateRowAndConnectivity(size, y, connectivity);
        if (size.Width == 0)
            return;

        ForEachForwardPairInRowCore(size, y, connectivity, state, visitor);
    }

    /// <summary>
    /// Iterates in parallel by row over all unique forward neighbor coordinate pairs in the grid.
    /// </summary>
    /// <remarks>
    /// <paramref name="visitor"/> may be invoked concurrently from multiple threads across distinct rows and must be thread-safe.
    /// </remarks>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="visitor">Thread-safe callback invoked with <c>(firstCoord, secondCoord, neighbor)</c> for each valid forward neighbor pair.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="connectivity"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPairParallel(
        Size size,
        GridConnectivity connectivity,
        Action<Coord, Coord, GridNeighbor> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ForEachForwardPairParallel(size, connectivity, visitor, static (first, second, neighbor, action) => action(first, second, neighbor));
    }

    /// <summary>
    /// Iterates in parallel by row over all unique forward neighbor coordinate pairs in the grid, passing <paramref name="state"/> without per-pixel allocations.
    /// </summary>
    /// <remarks>
    /// <paramref name="visitor"/> may be invoked concurrently from multiple threads across distinct rows and must be thread-safe.
    /// </remarks>
    /// <typeparam name="TState">The caller state type forwarded to <paramref name="visitor"/>.</typeparam>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="state">Thread-safe caller state forwarded to <paramref name="visitor"/>.</param>
    /// <param name="visitor">Thread-safe callback invoked with <c>(firstCoord, secondCoord, neighbor, state)</c> for each valid forward neighbor pair.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="connectivity"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPairParallel<TState>(
        Size size,
        GridConnectivity connectivity,
        TState state,
        Action<Coord, Coord, GridNeighbor, TState> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ValidateGridAndConnectivity(size, connectivity);
        if (size.IsEmpty)
            return;

        Parallel.For(0, size.Height, y => ForEachForwardPairInRowCore(size, y, connectivity, state, visitor));
    }

    /// <summary>
    /// Iterates in parallel by row over all unique forward neighbor index pairs in the grid.
    /// </summary>
    /// <remarks>
    /// <paramref name="visitor"/> may be invoked concurrently from multiple threads across distinct rows and must be thread-safe.
    /// </remarks>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="visitor">Thread-safe callback invoked with <c>(firstIndex, secondIndex)</c> for each valid forward neighbor pair.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="connectivity"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPairParallel(
        Size size,
        GridConnectivity connectivity,
        Action<int, int> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ForEachForwardPairParallel(size, connectivity, visitor, static (firstIndex, secondIndex, action) => action(firstIndex, secondIndex));
    }

    /// <summary>
    /// Iterates in parallel by row over all unique forward neighbor index pairs in the grid, passing <paramref name="state"/> without per-pixel allocations.
    /// </summary>
    /// <remarks>
    /// <paramref name="visitor"/> may be invoked concurrently from multiple threads across distinct rows and must be thread-safe.
    /// </remarks>
    /// <typeparam name="TState">The caller state type forwarded to <paramref name="visitor"/>.</typeparam>
    /// <param name="size">The 2D grid dimensions.</param>
    /// <param name="connectivity">The grid connectivity mode.</param>
    /// <param name="state">Thread-safe caller state forwarded to <paramref name="visitor"/>.</param>
    /// <param name="visitor">Thread-safe callback invoked with <c>(firstIndex, secondIndex, state)</c> for each valid forward neighbor pair.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="connectivity"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="visitor"/> is <see langword="null"/>.</exception>
    public static void ForEachForwardPairParallel<TState>(
        Size size,
        GridConnectivity connectivity,
        TState state,
        Action<int, int, TState> visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        ValidateGridAndConnectivity(size, connectivity);
        if (size.IsEmpty)
            return;

        Parallel.For(0, size.Height, y => ForEachForwardPairInRowCore(size, y, connectivity, state, visitor));
    }

    private static void ValidateGridAndConnectivity(Size size, GridConnectivity connectivity)
    {
        Guard.ThrowIfNegative(size);
        Guard.ThrowIfUndefinedEnum(connectivity);
    }

    private static void ValidateRowAndConnectivity(Size size, int y, GridConnectivity connectivity)
    {
        ValidateGridAndConnectivity(size, connectivity);
        if ((uint)y >= (uint)size.Height)
            throw new ArgumentOutOfRangeException(nameof(y), y, $"Row index {y} must be within [0, {size.Height}).");
    }

    private static void ForEachForwardPairInRowCore<TState>(
        Size size,
        int y,
        GridConnectivity connectivity,
        TState state,
        Action<int, int, TState> visitor)
    {
        int width = size.Width;
        int rowBase = size.IndexOf(0, y);
        bool hasSouth = y + 1 < size.Height;

        if (connectivity == GridConnectivity.FourConnected)
        {
            for (int x = 0; x < width; x++)
            {
                int firstIndex = rowBase + x;
                if (x + 1 < width)
                    visitor(firstIndex, firstIndex + 1, state);
                if (hasSouth)
                    visitor(firstIndex, firstIndex + width, state);
            }

            return;
        }

        for (int x = 0; x < width; x++)
        {
            int firstIndex = rowBase + x;
            bool hasEast = x + 1 < width;
            if (hasEast)
                visitor(firstIndex, firstIndex + 1, state);

            if (hasSouth)
            {
                int southIndex = firstIndex + width;
                visitor(firstIndex, southIndex, state);
                if (hasEast)
                    visitor(firstIndex, southIndex + 1, state);
                if (x > 0)
                    visitor(firstIndex, southIndex - 1, state);
            }
        }
    }

    private static void ForEachForwardPairInRowCore<TState>(
        Size size,
        int y,
        GridConnectivity connectivity,
        TState state,
        Action<Coord, Coord, GridNeighbor, TState> visitor)
    {
        GridNeighbor east = All8ConnectedData[(int)GridDirection.East];
        GridNeighbor south = All8ConnectedData[(int)GridDirection.South];
        GridNeighbor southEast = All8ConnectedData[(int)GridDirection.SouthEast];
        GridNeighbor southWest = All8ConnectedData[(int)GridDirection.SouthWest];
        int width = size.Width;
        bool hasSouth = y + 1 < size.Height;

        if (connectivity == GridConnectivity.FourConnected)
        {
            for (int x = 0; x < width; x++)
            {
                var first = new Coord(x, y);
                if (x + 1 < width)
                    visitor(first, new Coord(x + 1, y), east, state);
                if (hasSouth)
                    visitor(first, new Coord(x, y + 1), south, state);
            }

            return;
        }

        for (int x = 0; x < width; x++)
        {
            var first = new Coord(x, y);
            bool hasEast = x + 1 < width;
            if (hasEast)
                visitor(first, new Coord(x + 1, y), east, state);

            if (hasSouth)
            {
                visitor(first, new Coord(x, y + 1), south, state);
                if (hasEast)
                    visitor(first, new Coord(x + 1, y + 1), southEast, state);
                if (x > 0)
                    visitor(first, new Coord(x - 1, y + 1), southWest, state);
            }
        }
    }
}
