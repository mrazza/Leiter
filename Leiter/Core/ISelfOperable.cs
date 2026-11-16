namespace Leiter.Core;

/// <summary>
/// Defines arithmetic operations between instances of the same type.
/// </summary>
/// <typeparam name="T">The type that implements this interface.</typeparam>
public interface ISelfOperable<T>
{
    // TODO: Does this belong here or somewhere else?
    /// <summary>
    /// Gets the additive identity value (zero) for the type.
    /// </summary>
    static abstract T Zero { get; }

    /// <summary>
    /// Adds another instance of <typeparamref name="T"/> to this instance.
    /// </summary>
    /// <param name="right">The instance to add.</param>
    /// <returns>A new <typeparamref name="T"/> representing the sum.</returns>
    T Add(T right);

    /// <summary>
    /// Subtracts another instance of <typeparamref name="T"/> from this instance.
    /// </summary>
    /// <param name="right">The instance to subtract.</param>
    /// <returns>A new <typeparamref name="T"/> representing the difference.</returns>
    T Subtract(T right);

    /// <summary>
    /// Multiplies this instance by another instance of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="right">The instance to multiply with.</param>
    /// <returns>A new <typeparamref name="T"/> representing the product.</returns>
    T Multiply(T right);

    /// <summary>
    /// Divides this instance by another instance of <typeparamref name="T"/>.
    /// </summary>
    /// <param name="right">The instance to divide by.</param>
    /// <returns>A new <typeparamref name="T"/> representing the quotient.</returns>
    T Divide(T right);
}
