namespace Leiter.Core;

/// <summary>
/// Represents a two-dimensional normalized floating-point coordinate, typically in the [0, 1] range.
/// </summary>
/// <param name="X">The normalized horizontal X-coordinate component.</param>
/// <param name="Y">The normalized vertical Y-coordinate component.</param>
public readonly record struct NormalizedCoord(double X, double Y)
{
    /// <summary>
    /// Computes the squared Euclidean distance between this coordinate and another normalized coordinate.
    /// </summary>
    /// <param name="other">The other normalized coordinate to calculate the squared distance to.</param>
    /// <returns>The squared Euclidean distance between the two coordinates.</returns>
    public double DistanceSquared(NormalizedCoord other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>
    /// Computes the Euclidean distance between this coordinate and another normalized coordinate.
    /// </summary>
    /// <param name="other">The other normalized coordinate to calculate the distance to.</param>
    /// <returns>The Euclidean distance between the two coordinates.</returns>
    public double Distance(NormalizedCoord other) =>
        Math.Sqrt(DistanceSquared(other));

    /// <summary>
    /// Adds two normalized coordinates together component-wise.
    /// </summary>
    /// <param name="left">The first coordinate to add.</param>
    /// <param name="right">The second coordinate to add.</param>
    /// <returns>The component-wise sum of the two coordinates.</returns>
    public static NormalizedCoord operator +(NormalizedCoord left, NormalizedCoord right) => new(left.X + right.X, left.Y + right.Y);

    /// <summary>
    /// Subtracts a scalar value from both components of a normalized coordinate.
    /// </summary>
    /// <param name="left">The normalized coordinate to subtract from.</param>
    /// <param name="right">The scalar value to subtract from each component.</param>
    /// <returns>A new <see cref="NormalizedCoord"/> with the scalar subtracted from each component.</returns>
    public static NormalizedCoord operator -(NormalizedCoord left, double right) => new(left.X - right, left.Y - right);

    /// <summary>
    /// Multiplies a normalized coordinate by a scalar value component-wise.
    /// </summary>
    /// <param name="coord">The normalized coordinate operand.</param>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <returns>The scaled <see cref="NormalizedCoord"/>.</returns>
    public static NormalizedCoord operator *(NormalizedCoord coord, double scalar) => new(coord.X * scalar, coord.Y * scalar);

    /// <summary>
    /// Multiplies a scalar value by a normalized coordinate component-wise.
    /// </summary>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <param name="coord">The normalized coordinate operand.</param>
    /// <returns>The scaled <see cref="NormalizedCoord"/>.</returns>
    public static NormalizedCoord operator *(double scalar, NormalizedCoord coord) => new(coord.X * scalar, coord.Y * scalar);

    /// <summary>
    /// Divides a normalized coordinate by a scalar value component-wise.
    /// </summary>
    /// <param name="coord">The normalized coordinate operand.</param>
    /// <param name="scalar">The scalar divisor.</param>
    /// <returns>The scaled <see cref="NormalizedCoord"/>.</returns>
    public static NormalizedCoord operator /(NormalizedCoord coord, double scalar) => new(coord.X / scalar, coord.Y / scalar);
}
