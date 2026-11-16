namespace Leiter.Algorithms.DataStructures;

/// <summary>
/// Identifies the eight discrete neighbor directions on a 2D pixel grid.
/// Values are ordered so <c>(GridDirection)((byte)direction ^ 4)</c> is always the opposite direction,
/// and values <c>0</c> through <c>3</c> form the forward scanline half-stencil.
/// </summary>
public enum GridDirection : byte
{
    /// <summary>
    /// Positive X direction <c>(+1, 0)</c>.
    /// </summary>
    East = 0,

    /// <summary>
    /// Positive Y direction <c>(0, +1)</c>.
    /// </summary>
    South = 1,

    /// <summary>
    /// Positive diagonal direction <c>(+1, +1)</c>.
    /// </summary>
    SouthEast = 2,

    /// <summary>
    /// Down-left diagonal direction <c>(-1, +1)</c>.
    /// </summary>
    SouthWest = 3,

    /// <summary>
    /// Negative X direction <c>(-1, 0)</c>.
    /// </summary>
    West = 4,

    /// <summary>
    /// Negative Y direction <c>(0, -1)</c>.
    /// </summary>
    North = 5,

    /// <summary>
    /// Negative diagonal direction <c>(-1, -1)</c>.
    /// </summary>
    NorthWest = 6,

    /// <summary>
    /// Up-right diagonal direction <c>(+1, -1)</c>.
    /// </summary>
    NorthEast = 7,
}
