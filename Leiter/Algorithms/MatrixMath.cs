namespace Leiter.Algorithms;

using System.Numerics;
using Leiter.Core;

public static class MatrixMath
{
    public static Matrix<T> HadamardProduct<T>(this Matrix<T> self, IReadOnlyMatrix<T> right)
        where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
    {
        if (self.Height != right.Height || self.Width != right.Width)
            throw new ArgumentException("Hadamard multiplication requires matrices of the same dimensions.");

        var result = self.Clone();

        for (int index = 0; index < self.Count; index++)
            result[index] = self[index].Multiply(right[index]);

        return result;
    }

    public static Matrix<T> HadamardProduct<T, R, V>(this Matrix<T> self, IReadOnlyMatrix<R> right)
        where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
        where R : struct, IScalar<R, V>, ISelfOperable<R>, INumericOperable<R>, IScalarOperable<R>
        where V : unmanaged, IConvertible
    {
        if (self.Height != right.Height || self.Width != right.Width)
            throw new ArgumentException("Hadamard multiplication requires matrices of the same dimensions.");

        var result = self.Clone();

        for (int index = 0; index < self.Count; index++)
            result[index] = self[index].Multiply(right[index]);

        return result;
    }

    public static T FrobeniusProduct<T>(this IReadOnlyMatrix<T> self, IReadOnlyMatrix<T> right)
        where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
    {
        if (self.Height != right.Height || self.Width != right.Width)
            throw new ArgumentException("Frobenius multiplication requires matrices of the same dimensions.");

        return T.FromDoubleVector(
                self.Zip(right)
                    .Aggregate(Vector<double>.Zero,
                           (currentValue, elements) => currentValue + (elements.First.ToDoubleVector() * elements.Second.ToDoubleVector())));
    }

    public static T FrobeniusProduct<T, R, V>(this IReadOnlyMatrix<T> self, IReadOnlyMatrix<R> right)
        where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
        where R : struct, IScalar<R, V>, ISelfOperable<R>, INumericOperable<R>, IScalarOperable<R>
        where V : unmanaged, IConvertible
    {
        if (self.Height != right.Height || self.Width != right.Width)
            throw new ArgumentException("Frobenius multiplication requires matrices of the same dimensions.");

        return T.FromDoubleVector(
                self.Zip(right)
                    .Aggregate(Vector<double>.Zero,
                           (currentValue, elements) => currentValue + (elements.First.ToDoubleVector() * elements.Second.AsDouble())));
    }

    public static Matrix<T> Convolve<T, R, V>(this Matrix<T> self, IReadOnlyMatrix<R> kernel)
        where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
        where R : struct, IScalar<R, V>, ISelfOperable<R>, INumericOperable<R>, IScalarOperable<R>
        where V : unmanaged, IConvertible
    {
        var result = self.Clone();

        Enumerable.Range(0, self.Width)
            .AsParallel()
            .SelectMany((_) => Enumerable.Range(0, self.Height),
                        (x, y) => new{ x, y, view = new MatrixView<T>(self, x - (kernel.Width / 2), y - (kernel.Height / 2), kernel.Width, kernel.Height, EdgeHandling.EXTEND) })
            .ForAll((data) => result[data.x, data.y] = data.view.FrobeniusProduct<T, R, V>(kernel));

        return result;
    }
}