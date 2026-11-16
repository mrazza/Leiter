namespace Leiter.Core;

/// <summary>
/// Represents a scalar wrapping an underlying primitive value.
/// </summary>
/// <typeparam name="Self">The implementing scalar type.</typeparam>
/// <typeparam name="T">The underlying primitive type of the scalar value.</typeparam>
public interface ITypedScalar<Self, T> : IScalar<Self>
    where T : unmanaged, IConvertible
    where Self : notnull
{
    /// <summary>
    /// Gets the underlying primitive value of this scalar.
    /// </summary>
    T Value { get; }
}
