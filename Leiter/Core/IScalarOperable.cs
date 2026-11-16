namespace Leiter.Core;

/// <summary>
/// Defines arithmetic operations with scalar values for a type.
/// </summary>
/// <typeparam name="T">The type that implements this interface.</typeparam>
public interface IScalarOperable<T>
{
    /// <summary>
    /// Adds a scalar value to this instance.
    /// </summary>
    /// <typeparam name="S">The scalar type.</typeparam>
    /// <param name="right">The scalar value to add.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the addition.</returns>
    T Add<S>(IScalar<S> right) where S : IScalar<S>;

    /// <summary>
    /// Subtracts a scalar value from this instance.
    /// </summary>
    /// <typeparam name="S">The scalar type.</typeparam>
    /// <param name="right">The scalar value to subtract.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the subtraction.</returns>
    T Subtract<S>(IScalar<S> right) where S : IScalar<S>;

    /// <summary>
    /// Multiplies this instance by a scalar value.
    /// </summary>
    /// <typeparam name="S">The scalar type.</typeparam>
    /// <param name="right">The scalar multiplier.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the multiplication.</returns>
    T Multiply<S>(IScalar<S> right) where S : IScalar<S>;

    /// <summary>
    /// Divides this instance by a scalar value.
    /// </summary>
    /// <typeparam name="S">The scalar type.</typeparam>
    /// <param name="right">The scalar divisor.</param>
    /// <returns>A new <typeparamref name="T"/> containing the result of the division.</returns>
    T Divide<S>(IScalar<S> right) where S : IScalar<S>;
}
