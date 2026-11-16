namespace Leiter.Algorithms.DataStructures;

/// <summary>
/// Specifies how neighboring pixels are connected on a discrete two-dimensional grid.
/// </summary>
public enum GridConnectivity : byte
{
    /// <summary>
    /// Connects only the four cardinal neighbors (<see cref="GridDirection.East"/>, <see cref="GridDirection.South"/>,
    /// <see cref="GridDirection.West"/>, and <see cref="GridDirection.North"/>).
    /// </summary>
    FourConnected = 0,

    /// <summary>
    /// Connects all eight cardinal and diagonal neighbors.
    /// </summary>
    EightConnected = 1,
}
