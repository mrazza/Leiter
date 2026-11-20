namespace Leiter.Core;

using System.Text;

/// <summary>
/// Represents a three-dimensional Euclidean vector with 64-bit floating-point components.
/// </summary>
/// <param name="X">The X component.</param>
/// <param name="Y">The Y component.</param>
/// <param name="Z">The Z component.</param>
public readonly record struct Vector3D(double X, double Y, double Z)
{
    /// <summary>
    /// Gets a vector with all components set to zero.
    /// </summary>
    public static Vector3D Zero => default;

    /// <summary>
    /// Gets the squared Euclidean length of this vector.
    /// </summary>
    public double LengthSquared => (X * X) + (Y * Y) + (Z * Z);

    /// <summary>
    /// Gets the Euclidean length (magnitude) of this vector.
    /// </summary>
    public double Length => Math.Sqrt(LengthSquared);

    /// <summary>
    /// Computes the dot product of this vector with another vector.
    /// </summary>
    /// <param name="other">The other vector operand.</param>
    /// <returns>The scalar dot product.</returns>
    public double Dot(Vector3D other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

    /// <summary>
    /// Computes the cross product <c>this × other</c> in a right-handed coordinate system.
    /// </summary>
    /// <param name="other">The right-hand vector operand.</param>
    /// <returns>A vector orthogonal to both operands.</returns>
    public Vector3D Cross(Vector3D other) => new(
        (Y * other.Z) - (Z * other.Y),
        (Z * other.X) - (X * other.Z),
        (X * other.Y) - (Y * other.X));

    /// <summary>
    /// Computes a normalized (unit-length) vector in the same direction, or <see cref="Zero"/> if the length is near zero or non-finite.
    /// </summary>
    /// <returns>A unit-length vector with magnitude 1.0, or <see cref="Zero"/> if the vector has near-zero or non-finite length.</returns>
    public Vector3D Normalize()
    {
        double lenSq = LengthSquared;
        if (!double.IsFinite(lenSq) || lenSq <= 1e-24)
            return Zero;

        double invLen = 1.0 / Math.Sqrt(lenSq);
        return new(X * invLen, Y * invLen, Z * invLen);
    }

    /// <summary>
    /// Adds two vectors component-wise.
    /// </summary>
    /// <param name="left">The left vector operand.</param>
    /// <param name="right">The right vector operand.</param>
    /// <returns>The component-wise sum of the two vectors.</returns>
    public static Vector3D operator +(Vector3D left, Vector3D right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    /// <summary>
    /// Subtracts the right vector from the left vector component-wise.
    /// </summary>
    /// <param name="left">The left vector operand.</param>
    /// <param name="right">The right vector operand.</param>
    /// <returns>The component-wise difference of the two vectors.</returns>
    public static Vector3D operator -(Vector3D left, Vector3D right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    /// <summary>
    /// Negates all three components of the vector.
    /// </summary>
    /// <param name="vector">The vector to negate.</param>
    /// <returns>A vector with negated components.</returns>
    public static Vector3D operator -(Vector3D vector) => new(-vector.X, -vector.Y, -vector.Z);

    /// <summary>
    /// Multiplies a vector by a scalar value component-wise.
    /// </summary>
    /// <param name="vector">The vector operand.</param>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector3D operator *(Vector3D vector, double scalar) => new(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);

    /// <summary>
    /// Multiplies a scalar value by a vector component-wise.
    /// </summary>
    /// <param name="scalar">The scalar multiplier.</param>
    /// <param name="vector">The vector operand.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector3D operator *(double scalar, Vector3D vector) => new(vector.X * scalar, vector.Y * scalar, vector.Z * scalar);

    /// <summary>
    /// Divides a vector by a scalar value component-wise.
    /// </summary>
    /// <param name="vector">The vector operand.</param>
    /// <param name="scalar">The scalar divisor.</param>
    /// <returns>The scaled vector.</returns>
    public static Vector3D operator /(Vector3D vector, double scalar) => new(vector.X / scalar, vector.Y / scalar, vector.Z / scalar);

    /// <summary>
    /// Computes the squared Euclidean distance between this vector and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The target vector.</param>
    /// <returns>The squared Euclidean distance between the two vectors.</returns>
    public double DistanceSquared(Vector3D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        double dz = Z - other.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    /// <summary>
    /// Computes the Euclidean distance between this vector and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The target vector.</param>
    /// <returns>The Euclidean distance between the two vectors.</returns>
    public double Distance(Vector3D other) => Math.Sqrt(DistanceSquared(other));

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"X = {X}, Y = {Y}, Z = {Z}");
        return true;
    }
}
