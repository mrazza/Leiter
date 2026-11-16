namespace Leiter.Algorithms.Stylization;

using System;

/// <summary>
/// Configures parameters for Wang et al.'s saliency-guided level-of-detail (LOD) abstraction (Sec. 3.3).
/// </summary>
/// <param name="SalientKernelSize">The fixed kernel size applied within salient regions (default 5).</param>
/// <param name="MinNonSalientKernelSize">The minimum kernel size applied in non-salient regions near salient boundaries (default 4).</param>
/// <param name="MaxNonSalientKernelSize">The maximum kernel size applied in non-salient regions near image borders (default 9).</param>
/// <param name="DistanceKernelScale">The scaling multiplier mapping normalized distance to kernel size (default 10.0).</param>
/// <param name="DistanceKernelBias">The additive bias offset for distance-based kernel sizing (default 0.3).</param>
/// <param name="CrossSegmentColorScale">The color difference threshold scale factor relative to distance d for cross-segment filtering (default 0.3).</param>
/// <param name="SalientOverlapThreshold">The minimum fraction of salient pixels required for a segmented region to be classified as salient (default 0.5).</param>
public sealed record LodAbstractionOptions(
    int SalientKernelSize = 5,
    int MinNonSalientKernelSize = 4,
    int MaxNonSalientKernelSize = 9,
    double DistanceKernelScale = 10.0,
    double DistanceKernelBias = 0.3,
    double CrossSegmentColorScale = 0.3,
    double SalientOverlapThreshold = 0.5)
{
    /// <summary>
    /// Validates that all configuration parameters are within their required domains.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any parameter is negative, non-finite, or outside its valid range.</exception>
    internal void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(SalientKernelSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MinNonSalientKernelSize, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaxNonSalientKernelSize, MinNonSalientKernelSize);
        if (!double.IsFinite(DistanceKernelScale) || DistanceKernelScale < 0.0)
            throw new ArgumentOutOfRangeException(nameof(DistanceKernelScale), DistanceKernelScale, "DistanceKernelScale must be finite and non-negative.");
        if (!double.IsFinite(DistanceKernelBias))
            throw new ArgumentOutOfRangeException(nameof(DistanceKernelBias), DistanceKernelBias, "DistanceKernelBias must be finite.");
        if (!double.IsFinite(CrossSegmentColorScale) || CrossSegmentColorScale < 0.0)
            throw new ArgumentOutOfRangeException(nameof(CrossSegmentColorScale), CrossSegmentColorScale, "CrossSegmentColorScale must be finite and non-negative.");
        if (!double.IsFinite(SalientOverlapThreshold) || SalientOverlapThreshold < 0.0 || SalientOverlapThreshold > 1.0)
            throw new ArgumentOutOfRangeException(nameof(SalientOverlapThreshold), SalientOverlapThreshold, "SalientOverlapThreshold must be finite and within [0, 1].");
    }
}
