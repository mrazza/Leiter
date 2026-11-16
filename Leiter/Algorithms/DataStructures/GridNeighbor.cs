namespace Leiter.Algorithms.DataStructures;

using Leiter.Core;

/// <summary>
/// Describes a neighbor offset on a 2D grid along with its direction and Euclidean step metrics.
/// </summary>
/// <param name="Direction">The discrete grid direction from the origin cell to this neighbor.</param>
/// <param name="Offset">The integer coordinate delta to reach the neighbor.</param>
/// <param name="Distance">The Euclidean step length (<c>1</c> for orthogonal, <c>√2</c> for diagonal).</param>
/// <param name="InverseDistance">The reciprocal Euclidean step length (<c>1</c> for orthogonal, <c>1 / √2</c> for diagonal).</param>
public readonly record struct GridNeighbor(
    GridDirection Direction,
    Coord Offset,
    double Distance,
    double InverseDistance);
