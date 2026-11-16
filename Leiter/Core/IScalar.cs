namespace Leiter.Core;

/// <summary>
/// Represents a scalar value that supports self-arithmetic and numeric scaling operations.
/// </summary>
/// <typeparam name="Self">The implementing scalar type.</typeparam>
public interface IScalar<Self> : ISelfOperable<Self>, INumericOperable<Self>
    where Self : notnull
{
    /// <summary>
    /// Converts this scalar value to a double-precision floating-point number.
    /// </summary>
    /// <returns>The scalar value represented as a <see cref="double"/>.</returns>
    double AsDouble();
}
