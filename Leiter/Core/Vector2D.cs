namespace Leiter.Core;

using System.Text;

/// <summary>
/// Represents a two-dimensional Euclidean vector with 64-bit floating-point components.
/// </summary>
/// <param name="X">The horizontal X component.</param>
/// <param name="Y">The vertical Y component.</param>
public readonly record struct Vector2D(double X, double Y)
{
    /// <summary>
    /// Gets a vector with all components set to zero.
    /// </summary>
    public static Vector2D Zero => default;

    /// <summary>
    /// Gets the squared Euclidean length of this vector.
    /// </summary>
    public double LengthSquared => (X * X) + (Y * Y);

    /// <summary>
    /// Gets the Euclidean length (magnitude) of this vector.
    /// </summary>
    public double Length => Math.Sqrt(LengthSquared);

    /// <summary>
    /// Gets a vector perpendicular to this vector, rotated 90 degrees counter-clockwise (-Y, X).
    /// </summary>
    public Vector2D Perpendicular => new(-Y, X);

    /// <summary>
    /// Computes the dot product of this vector with another vector.
    /// </summary>
    /// <param name="other">The other vector operand.</param>
    /// <returns>The scalar dot product.</returns>
    public double Dot(Vector2D other) => (X * other.X) + (Y * other.Y);

    /// <summary>
    /// Computes a normalized (unit-length) vector in the same direction, or <see cref="Zero"/> if the length is near zero or non-finite.
    /// </summary>
    /// <returns>A unit-length vector with magnitude 1.0, or <see cref="Zero"/> if the vector has near-zero or non-finite length.</returns>
    public Vector2D Normalize()
    {
        double lenSq = LengthSquared;
        if (!double.IsFinite(lenSq) || lenSq <= ScalarMath.NormalizationEpsilonSquared)
            return Zero;

        double invLen = 1.0 / Math.Sqrt(lenSq);
        return new(X * invLen, Y * invLen);
    }

    /// <summary>
    /// Adds two vectors component-wise.
    /// </summary>
    /// <param name="left">The left vector operand.</param>
    /// <param name="right">The right vector operand.</param>
    /// <returns>The component-wise sum of the two vectors.</returns>
    public static Vector2D operator +(Vector2D left, Vector2D right) => new(left.X + right.X, left.Y + right.Y);

    /// <summary>
    /// Subtracts the right vector from the left vector component-wise.
    /// </summary>
    /// <param name="left">The left vector operand.</param>
    /// <param name="right">The right vector operand.</param>
    /// <returns>The component-wise difference of the two vectors.</returns>
    public static Vector2D operator -(Vector2D left, Vector2D right) => new(left.X - right.X, left.Y - right.Y);

    /// <summary>
    /// Negates both components of the vector.
    /// </summary>
    /// <param name="vector">The vector to negate.</param>
    /// <returns>A vector with negated components.</returns>
    public static Vector2D operator -(Vector2D vector) => new(-vector.X, -vector.Y);

    /// <summary>
    /// Multiplies a vector by a scalar value component-wise.
    /// </summary>
    /// <param name="vector">The vector operand.</param>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector2D operator *(Vector2D vector, double scalar) => new(vector.X * scalar, vector.Y * scalar);

    /// <summary>
    /// Multiplies a scalar value by a vector component-wise.
    /// </summary>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <param name="vector">The vector operand.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector2D operator *(double scalar, Vector2D vector) => new(vector.X * scalar, vector.Y * scalar);

    /// <summary>
    /// Divides a vector by a scalar value component-wise.
    /// </summary>
    /// <param name="vector">The vector operand.</param>
    /// <param name="scalar">The scalar divisor.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector2D operator /(Vector2D vector, double scalar) => new(vector.X / scalar, vector.Y / scalar);

    /// <summary>
    /// Computes the squared Euclidean distance between this vector and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The target vector.</param>
    /// <returns>The squared Euclidean distance between the two vectors.</returns>
    public double DistanceSquared(Vector2D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return (dx * dx) + (dy * dy);
    }

    /// <summary>
    /// Computes the Euclidean distance between this vector and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The target vector.</param>
    /// <returns>The Euclidean distance between the two vectors.</returns>
    public double Distance(Vector2D other) => Math.Sqrt(DistanceSquared(other));

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"X = {X}, Y = {Y}");
        return true;
    }
}
