namespace Leiter.Core;

public interface ISelfOperable<T>
{
    // TODO: Does this belong here or somewhere else?
    static abstract T Zero { get; }

    T Add(T right);

    T Subtract(T right);

    T Multiply(T right);

    T Divide(T right);
}