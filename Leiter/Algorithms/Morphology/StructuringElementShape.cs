namespace Leiter.Algorithms.Morphology;

/// <summary>
/// Specifies the shape of a two-dimensional binary morphological structuring element.
/// </summary>
public enum StructuringElementShape
{
    /// <summary>
    /// A circular (Euclidean disk) structuring element of radius <c>r</c>, including all offsets with <c>dx² + dy² ≤ r²</c>.
    /// </summary>
    Disk,

    /// <summary>
    /// A square structuring element of side <c>2r + 1</c>, including all offsets with <c>|dx| ≤ r</c> and <c>|dy| ≤ r</c>.
    /// </summary>
    Square,
}
