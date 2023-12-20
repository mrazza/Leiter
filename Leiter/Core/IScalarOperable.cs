namespace Leiter.Core;

public interface IScalarOperable<T>
{
    T Add<S, R>(IScalar<S, R> right) where R : unmanaged, IConvertible where S : IScalar<S, R>;

    T Subtract<S, R>(IScalar<S, R> right) where R : unmanaged, IConvertible where S : IScalar<S, R>;

    T Multiply<S, R>(IScalar<S, R> right) where R : unmanaged, IConvertible where S : IScalar<S, R>;

    T Divide<S, R>(IScalar<S, R> right) where R : unmanaged, IConvertible where S : IScalar<S, R>;
}