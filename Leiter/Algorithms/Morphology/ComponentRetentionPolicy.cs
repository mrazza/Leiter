namespace Leiter.Algorithms.Morphology;

/// <summary>
/// Specifies how connected foreground components are retained when filtering a binary mask.
/// </summary>
public enum ComponentRetentionPolicy
{
    /// <summary>
    /// Retains all connected components whose pixel area meets or exceeds a specified fraction of the total image area.
    /// </summary>
    MinimumAreaFraction,

    /// <summary>
    /// Retains only the single largest connected foreground component.
    /// </summary>
    LargestComponentOnly,

    /// <summary>
    /// Keeps all foreground pixels without connected-component filtering.
    /// </summary>
    KeepAll,
}
