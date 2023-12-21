namespace Leiter.Core;

public interface IScalarOperable<T>
{
    T Add<S>(IScalar<S> right) where S : IScalar<S>;

    T Subtract<S>(IScalar<S> right) where S : IScalar<S>;

    T Multiply<S>(IScalar<S> right) where S : IScalar<S>;

    T Divide<S>(IScalar<S> right) where S : IScalar<S>;
}