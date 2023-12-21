namespace Leiter.Core;

public interface IScalar<Self> : ISelfOperable<Self>, INumericOperable<Self>
    where Self : notnull
{
    double AsDouble();
}