namespace Leiter.Core;

/// <summary>
/// Specifies how out-of-bounds coordinate access is handled at matrix edges.
/// </summary>
public enum EdgeHandling
{
    /// <summary>
    /// Clamps out-of-bounds coordinates to the nearest valid edge pixel.
    /// </summary>
    EXTEND
}
