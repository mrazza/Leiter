namespace Leiter.Algorithms.Segmentation.Internal;

using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Represents the continuous spatial and color coordinate mode of a pixel in Mean-Shift feature space.
/// </summary>
/// <param name="X">The continuous horizontal spatial coordinate of the mode.</param>
/// <param name="Y">The continuous vertical spatial coordinate of the mode.</param>
/// <param name="Color">The CIELAB color coordinate of the mode.</param>
internal readonly record struct MeanShiftMode(double X, double Y, Lab32 Color)
{
    /// <summary>
    /// Calculates the squared Euclidean spatial distance between this mode and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The other mode to compare against.</param>
    /// <returns>The squared Euclidean spatial distance <c>(Δx² + Δy²)</c>.</returns>
    public double SpatialDistanceSquared(MeanShiftMode other) =>
        new Vector2D(X, Y).DistanceSquared(new Vector2D(other.X, other.Y));
}
