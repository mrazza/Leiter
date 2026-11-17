namespace Leiter.Algorithms;

/// <summary>
/// Specifies the gradient operator kernel used for spatial derivative computation.
/// </summary>
public enum GradientOperator
{
    /// <summary>
    /// Sobel operator with smoothing weights [1, 2, 1], derivative weights [-1, 0, 1], and scaling factor 1/8.
    /// </summary>
    Sobel = 0,

    /// <summary>
    /// Scharr operator with smoothing weights [3, 10, 3], derivative weights [-1, 0, 1], and scaling factor 1/32.
    /// </summary>
    Scharr = 1
}
