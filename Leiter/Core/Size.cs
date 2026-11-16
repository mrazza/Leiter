namespace Leiter.Core;

/// <summary>
/// Represents the two-dimensional size dimensions (width and height) of a matrix or image.
/// </summary>
/// <param name="Width">The horizontal dimension or width in elements/pixels.</param>
/// <param name="Height">The vertical dimension or height in elements/pixels.</param>
public readonly record struct Size(int Width, int Height);
