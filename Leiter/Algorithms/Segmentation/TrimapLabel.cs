namespace Leiter.Algorithms.Segmentation;

/// <summary>
/// Specifies pixel trimap labels used to constrain <see cref="GrabCut"/> foreground/background segmentation.
/// </summary>
public enum TrimapLabel : byte
{
    /// <summary>
    /// Pixel is firmly constrained to belong to the background.
    /// </summary>
    Background = 0,

    /// <summary>
    /// Pixel is firmly constrained to belong to the foreground.
    /// </summary>
    Foreground = 1,

    /// <summary>
    /// Pixel is initialized as background but may be reassigned during optimization.
    /// </summary>
    ProbableBackground = 2,

    /// <summary>
    /// Pixel is initialized as foreground but may be reassigned during optimization.
    /// </summary>
    ProbableForeground = 3,
}
