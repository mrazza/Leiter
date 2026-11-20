namespace Leiter.Core;

using System.Runtime.Intrinsics;

/// <summary>
/// Defines arithmetic operations with numeric scalars and vector conversions for a type.
/// </summary>
/// <typeparam name="T">The type that implements this interface.</typeparam>
public interface INumericOperable<T>
{
    /// <summary>
    /// Adds a decimal scalar value to this instance.
    /// </summary>
    /// <param name="right">The decimal scalar value to add.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the addition.</returns>
    T Add(decimal right);

    /// <summary>
    /// Subtracts a decimal scalar value from this instance.
    /// </summary>
    /// <param name="right">The decimal scalar value to subtract.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the subtraction.</returns>
    T Subtract(decimal right);

    /// <summary>
    /// Multiplies this instance by a decimal scalar value.
    /// </summary>
    /// <param name="right">The decimal scalar multiplier.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the multiplication.</returns>
    T Multiply(decimal right);

    /// <summary>
    /// Divides this instance by a decimal scalar value.
    /// </summary>
    /// <param name="right">The decimal scalar divisor.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the division.</returns>
    T Divide(decimal right);

    /// <summary>
    /// Adds a double-precision floating-point scalar value to this instance.
    /// </summary>
    /// <param name="right">The double scalar value to add.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the addition.</returns>
    T Add(double right);

    /// <summary>
    /// Subtracts a double-precision floating-point scalar value from this instance.
    /// </summary>
    /// <param name="right">The double scalar value to subtract.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the subtraction.</returns>
    T Subtract(double right);

    /// <summary>
    /// Multiplies this instance by a double-precision floating-point scalar value.
    /// </summary>
    /// <param name="right">The double scalar multiplier.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the multiplication.</returns>
    T Multiply(double right);

    /// <summary>
    /// Divides this instance by a double-precision floating-point scalar value.
    /// </summary>
    /// <param name="right">The double scalar divisor.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the division.</returns>
    T Divide(double right);

    /// <summary>
    /// Converts this instance to a double-precision SIMD vector representation.
    /// </summary>
    /// <returns>A <see cref="Vector256{T}"/> of <see cref="double"/> representing the components of this instance.</returns>
    Vector256<double> ToDoubleVector();

    /// <summary>
    /// Creates a new instance of <typeparamref name="T"/> from a double-precision SIMD vector.
    /// </summary>
    /// <param name="vector">The SIMD vector containing the component values.</param>
    /// <returns>A new <typeparamref name="T"/> reconstructed from the vector.</returns>
    static abstract T FromDoubleVector(Vector256<double> vector);
}