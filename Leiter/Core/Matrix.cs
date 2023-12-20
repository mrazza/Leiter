namespace Leiter.Core;

using System.Collections;

public abstract class Matrix<T> : IReadOnlyMatrix<T>
    where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
{
    public int Width {get; private init;}
    public int Height {get; private init;}

    protected Matrix(int width, int height)
    {
        Width = width;
        Height = height;
    }

    public T this[int index]
    {
        get => GetElement(index);
        set => SetElement(index, value);
    }

    public T this[int x, int y]
    {
        get => GetElement(x, y);
        set => SetElement(x, y, value);
    }

    public T this[Coord coord]
    {
        get => GetElement(coord.X, coord.Y);
        set => SetElement(coord.X, coord.Y, value);
    }

    public int Count => Width * Height;

    public Size Size => new(Width, Height);

    public abstract T GetElement(int index);

    public abstract T GetElement(int x, int y);

    public abstract void SetElement(int index, T value);

    public abstract void SetElement(int x, int y, T value);

    public abstract void SetAll(T value);

    public abstract Matrix<T> Multiply(IReadOnlyMatrix<T> right);

    public abstract Matrix<R> Map<R>(Func<T, R> func)
        where R : struct, ISelfOperable<R>, INumericOperable<R>, IScalarOperable<R>;

    public abstract Matrix<T> Clone();

    public override string? ToString() => IReadOnlyMatrix<T>.ToString(this);

    public virtual IEnumerator<T> GetEnumerator()
    {
        for (int index = 0; index < Count; index++)
            yield return this[index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}