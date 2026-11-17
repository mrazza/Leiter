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
    /// Excludes any boundary incident to at least one salient region, confining wet-in-wet bleeding entirely to non-salient background areas.
    /// </summary>
    ExcludeSalientRegions = 2,

    /// <summary>
    /// Disables wet-in-wet filtering entirely, returning an unmodified copy of the input image.
    /// </summary>
    Disabled = 3
}
