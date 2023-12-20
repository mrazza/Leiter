namespace Leiter.Core;

using System.Numerics;

public interface INumericOperable<T>
{
    T Add(decimal right);

    T Subtract(decimal right);

    T Multiply(decimal right);

    T Divide(decimal right);

    T Add(double right);

    T Subtract(double right);

    T Multiply(double right);

    T Divide(double right);

    Vector<double> ToDoubleVector();

    static abstract T FromDoubleVector(Vector<double> vector);
}