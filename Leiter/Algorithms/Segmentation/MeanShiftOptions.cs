namespace Leiter.Algorithms.Segmentation;

using System;
using Leiter.Algorithms.DataStructures;

/// <summary>
/// Specifies configuration parameters for Mean-Shift filtering and segmentation.
/// </summary>
/// <param name="SpatialRadius">The radius of the spatial search window in pixels. Must be greater than or equal to 1.</param>
/// <param name="RangeRadius">The radius of the CIELAB color range search window. Must be a finite positive number.</param>
/// <param name="MinRegionSize">The minimum number of pixels required for a segmented region. Smaller regions are pruned and merged into adjacent regions. Must be greater than or equal to 1.</param>
/// <param name="MaxIterations">The maximum number of Mean-Shift mode-seeking iterations per pixel. Must be greater than or equal to 1.</param>
/// <param name="ConvergenceEpsilon">The convergence distance threshold in joint 5D spatial-range feature space below which iteration stops. Must be a finite positive number.</param>
/// <param name="Connectivity">The grid connectivity used to construct adjacent neighbor edges for mode grouping and region pruning.</param>
public sealed record MeanShiftOptions(
    int SpatialRadius = 8,
    double RangeRadius = 5.5,
    int MinRegionSize = 50,
    int MaxIterations = 15,
    double ConvergenceEpsilon = 0.05,
    GridConnectivity Connectivity = GridConnectivity.EightConnected)
{
    /// <summary>
    /// Validates the options, throwing <see cref="ArgumentOutOfRangeException"/> if any parameter is outside its valid range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any configuration property is invalid.</exception>
    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(SpatialRadius, 1);
        if (!double.IsFinite(RangeRadius) || RangeRadius <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(RangeRadius), RangeRadius, "Range radius must be a finite positive number.");
        ArgumentOutOfRangeException.ThrowIfLessThan(MinRegionSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxIterations, 1);
        if (!double.IsFinite(ConvergenceEpsilon) || ConvergenceEpsilon <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(ConvergenceEpsilon), ConvergenceEpsilon, "Convergence epsilon must be a finite positive number.");
        if (!Enum.IsDefined(Connectivity))
            throw new ArgumentOutOfRangeException(nameof(Connectivity), Connectivity, "Connectivity must be a defined GridConnectivity value.");
    }
}
