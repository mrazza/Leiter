namespace Leiter.Core;

/// <summary>
/// Represents a two-dimensional integer coordinate.
/// </summary>
/// <param name="X">The horizontal X-coordinate component.</param>
/// <param name="Y">The vertical Y-coordinate component.</param>
public readonly record struct Coord(int X, int Y)
{
    /// <summary>
    /// Computes the Euclidean distance between this coordinate and another coordinate.
    /// </summary>
    /// <param name="other">The other coordinate to calculate the distance to.</param>
    /// <returns>The Euclidean distance between the two coordinates.</returns>
    public readonly double Distance(Coord other) =>
        Math.Sqrt(Math.Pow(this.X - other.X, 2) + Math.Pow(this.Y - other.Y, 2));

    /// <summary>
    /// Adds two coordinates together component-wise.
    /// </summary>
    /// <param name="left">The first coordinate to add.</param>
    /// <param name="right">The second coordinate to add.</param>
    /// <returns>The component-wise sum of the two coordinates.</returns>
    public static Coord operator +(Coord left, Coord right) => new(left.X + right.X, left.Y + right.Y);
}
