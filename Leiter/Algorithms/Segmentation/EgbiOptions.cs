namespace Leiter.Algorithms.Segmentation;

using System;
using Leiter.Algorithms.DataStructures;

/// <summary>
/// Specifies configuration parameters for Felzenszwalb–Huttenlocher Efficient Graph-Based Image Segmentation (EGBI).
/// </summary>
/// <param name="KFactor">The scale parameter controlling preference for larger segments. Must be a finite non-negative number.</param>
/// <param name="Epsilon">The minimum color distance threshold below which adjacent pixels are always merged into the same segment. Must be a finite non-negative number.</param>
/// <param name="MinRegionSize">The minimum pixel count required per region during post-processing; set to <c>0</c> to disable small-region merging. Must be non-negative.</param>
/// <param name="Connectivity">The grid neighborhood connectivity used to construct adjacent pixel graph edges.</param>
public sealed record EgbiOptions(
    double KFactor = 100.0,
    double Epsilon = 1.0,
    int MinRegionSize = 100,
    GridConnectivity Connectivity = GridConnectivity.EightConnected)
{
    /// <summary>
    /// Validates the options, throwing <see cref="ArgumentOutOfRangeException"/> if any parameter is outside its valid range.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any configuration property is invalid.</exception>
    internal void Validate()
    {
        if (!double.IsFinite(KFactor) || KFactor < 0.0)
            throw new ArgumentOutOfRangeException(nameof(KFactor), KFactor, "KFactor must be a finite non-negative number.");

        if (!double.IsFinite(Epsilon) || Epsilon < 0.0)
            throw new ArgumentOutOfRangeException(nameof(Epsilon), Epsilon, "Epsilon must be a finite non-negative number.");

        ArgumentOutOfRangeException.ThrowIfNegative(MinRegionSize);

        if (!Enum.IsDefined(Connectivity))
            throw new ArgumentOutOfRangeException(nameof(Connectivity), Connectivity, "Connectivity must be a defined GridConnectivity value.");
    }
}
