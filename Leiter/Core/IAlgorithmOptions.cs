namespace Leiter.Core;

/// <summary>
/// Defines a validatable algorithm options type with a canonical default configuration.
/// </summary>
/// <typeparam name="TSelf">The concrete options type implementing this interface.</typeparam>
public interface IAlgorithmOptions<TSelf> : IValidatableOptions
    where TSelf : IAlgorithmOptions<TSelf>
{
    /// <summary>
    /// Gets the default validated configuration for <typeparamref name="TSelf"/>.
    /// </summary>
    static abstract TSelf Default { get; }
}
