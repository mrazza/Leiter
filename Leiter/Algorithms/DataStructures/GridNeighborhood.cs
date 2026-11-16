namespace Leiter.Algorithms.DataStructures;

using Leiter.Core;

/// <summary>
/// Provides standard 4- and 8-connected 2D pixel grid neighborhood stencils and direction helpers.
/// </summary>
public static class GridNeighborhood
{
    private const double Sqrt2 = 1.4142135623730951;
    private const double InvSqrt2 = 0.7071067811865475;

    private static readonly GridNeighbor[] All8ConnectedData =
    [
        new(GridDirection.East, new Coord(1, 0), 1.0, 1.0),
        new(GridDirection.South, new Coord(0, 1), 1.0, 1.0),
        new(GridDirection.SouthEast, new Coord(1, 1), Sqrt2, InvSqrt2),
        new(GridDirection.SouthWest, new Coord(-1, 1), Sqrt2, InvSqrt2),
        new(GridDirection.West, new Coord(-1, 0), 1.0, 1.0),
        new(GridDirection.North, new Coord(0, -1), 1.0, 1.0),
        new(GridDirection.NorthWest, new Coord(-1, -1), Sqrt2, InvSqrt2),
        new(GridDirection.NorthEast, new Coord(1, -1), Sqrt2, InvSqrt2),
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
    /// Gets all eight neighbor steps ordered by <see cref="GridDirection"/> (<c>0</c> through <c>7</c>).
    /// </summary>
    public static ReadOnlySpan<GridNeighbor> All8Connected => All8ConnectedData;

    /// <summary>
    /// Returns the opposite grid direction.
    /// </summary>
    /// <param name="direction">The grid direction to invert.</param>
    /// <returns>The opposite <see cref="GridDirection"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="direction"/> is not a defined <see cref="GridDirection"/> value.</exception>
    public static GridDirection Opposite(this GridDirection direction)
    {
        if ((byte)direction >= 8)
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported grid direction.");

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
            throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported grid direction.");

        return All8ConnectedData[(byte)direction].Offset;
    }
}
