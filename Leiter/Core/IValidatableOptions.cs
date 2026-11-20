namespace Leiter.Core;

/// <summary>
/// Represents an algorithm options object capable of validating its parameters.
/// </summary>
internal interface IValidatableOptions
{
    /// <summary>
    /// Validates that all option parameters fall within their required domains.
    /// </summary>
    void Validate();
}
