namespace Leiter.Algorithms.Stylization.Watercolor;

/// <summary>
/// Specifies the boundary filtering mode for wet-in-wet watercolor edge bleeding.
/// </summary>
public enum WetInWetMode
{
    /// <summary>
    /// Default mode: processes qualifying boundaries regardless of region saliency.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Excludes boundaries that separate a salient region from a non-salient region, preserving sharpness between foreground and background.
    /// </summary>
    ExcludeSalientBoundaries = 1,

    /// <summary>
    /// Excludes boundaries between two salient regions (the interior of salient objects), preventing wet-in-wet bleeding
    /// inside salient regions while still allowing bleeding near outer salient boundaries and across background regions.
    /// </summary>
    ExcludeSalientRegions = 2,

    /// <summary>
    /// Disables wet-in-wet filtering entirely, returning an unmodified copy of the input image.
    /// </summary>
    Disabled = 3
}
