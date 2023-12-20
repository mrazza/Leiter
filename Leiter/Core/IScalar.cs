namespace Leiter.Core;

public interface IScalar<Self, T> : ISelfOperable<Self>, INumericOperable<Self>
    where T : unmanaged, IConvertible
    where Self : notnull
{
    T Value {get;}

    double AsDouble();
}