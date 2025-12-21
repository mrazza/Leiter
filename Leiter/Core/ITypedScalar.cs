namespace Leiter.Core;

public interface ITypedScalar<Self, T> : IScalar<Self>
    where T : unmanaged, IConvertible
    where Self : notnull
{
    T Value { get; }
}