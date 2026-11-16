namespace Leiter.Algorithms.Saliency;

/// <summary>
/// Specifies how connected components are retained when filtering a binary foreground mask.
/// </summary>
public enum ComponentRetentionPolicy
{
    /// <summary>
    /// Retains all 8-connected components whose pixel area meets or exceeds a specified fraction of the image area.
    /// </summary>
    MinimumAreaFraction,

    /// <summary>
    /// Retains only the single largest 8-connected foreground component.
    /// </summary>
    LargestComponentOnly,

    /// <summary>
    /// Keeps all foreground pixels without connected-component filtering.
    /// </summary>
    KeepAll,
}
