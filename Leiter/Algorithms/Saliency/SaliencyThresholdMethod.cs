namespace Leiter.Algorithms.Saliency;

/// <summary>
/// Specifies the thresholding strategy used by <see cref="SaliencyMask"/> to binarize a continuous saliency map.
/// </summary>
public enum SaliencyThresholdMethod
{
    /// <summary>
    /// Computes the optimal threshold maximizing inter-class variance (Otsu's method) over quantized [0, 1] values.
    /// </summary>
    Otsu,

    /// <summary>
    /// Computes an adaptive threshold equal to twice the mean saliency (<c>min(2 * mean, 0.95)</c>).
    /// </summary>
    AdaptiveMean,

    /// <summary>
    /// Compares saliency values directly against a caller-supplied fixed threshold in [0, 1].
    /// </summary>
    Fixed,
}
