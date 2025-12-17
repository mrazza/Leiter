namespace Leiter.Core;

using System.Linq;

public class SequentialMatrix<T> : Matrix<T>
    where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
{
    private readonly T[] data;

    public SequentialMatrix(int width, int height)
        : this(width, height, new T[width * height]) { }

    public SequentialMatrix(Size size)
        : this(size.Width, size.Height) { }

    public SequentialMatrix(T[,] values)
        : this(values.GetLength(1), values.GetLength(0), values.Cast<T>().ToArray()) { }

    private SequentialMatrix(int width, int height, T[] data)
        : base(width, height)
    {
        if (data.Length != width * height)
            throw new ArgumentException("Data length does not match matrix size.");

        this.data = data;
    }

    public override T GetElement(int index)
    {
        if (index < 0 || index >= data.Length)
            throw new IndexOutOfRangeException($"Index must be between 0 and matrix size of {Count} (exclusive) but was {index}.");

        return data[index];
    }

    public override T GetElement(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            throw new IndexOutOfRangeException($"Element index must be between {{0, 0}} (inclusive) and {{{Width}, {Height}}} (exclusive) but was {{{x}, {y}}}.");

        return data[x + (y * Width)];
    }

    public override void SetElement(int index, T value)
    {
        if (index < 0 || index >= data.Length)
            throw new IndexOutOfRangeException($"{index} exceeds Matrix size of {Count}");

        data[index] = value;
    }

    public override void SetElement(int x, int y, T value)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
            throw new IndexOutOfRangeException($"Element index must be between {{0, 0}} (inclusive) and {{{Width}, {Height}}} (exclusive) but was {{{x}, {y}}}.");

        data[x + (y * Width)] = value;
    }

    public override void SetAll(T value)
    {
        Array.Fill(data, value);
    }

    public override Matrix<T> Multiply(IReadOnlyMatrix<T> right)
    {
        if (Width != right.Height)
            throw new ArgumentException("Right hand side matrix must have height equal to width of the left hand side matrix.");

        // TODO: Improve this. This simple implementation is O(n^3).
        var result = new SequentialMatrix<T>(right.Width, Height);
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < right.Width; x++)
            {
                T sum = T.Zero;
                for (int k = 0; k < Width; k++)
                {
                    sum = sum.Add(this[k, y].Multiply(right[x, k]));
                }
                result[x, y] = sum;
            }
        }

        return result;
    }

    public override Matrix<T> Clone()
    {
        var clonedData = new T[data.Length];
        Array.Copy(data, clonedData, data.Length);
        return new SequentialMatrix<T>(Width, Height, clonedData);
    }

    public override IEnumerator<T> GetEnumerator() =>
        data.AsEnumerable().GetEnumerator();

    public override Matrix<R> Map<R>(Func<T, R> func) =>
        new SequentialMatrix<R>(Width, Height, data.AsParallel().Select(pixel => func(pixel)).ToArray());
}