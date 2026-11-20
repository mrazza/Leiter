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
}
