namespace Leiter.Core;

using System.Text;

public interface IReadOnlyMatrix<T> : IUntypedMatrix, IReadOnlyCollection<T>, IReadOnlyList<T>, IEnumerable<T>
    where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
{
    T this[int x, int y]
    {
        get;
    }

    T this[Coord coord]
    {
        get;
    }

    T GetElement(int index);

    T GetElement(int width, int height);

    int IUntypedMatrix.Count => Count;

    int IReadOnlyCollection<T>.Count => Count;

    new int Count { get; }

    protected static string ToString(IReadOnlyMatrix<T> matrix)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(matrix.GetType());
        builder.AppendLine(" {");

        for (int y = 0; y < matrix.Height; y++)
        {
            builder.Append('\t');

            for (int x = 0; x < matrix.Width; x++)
            {
                if (x > 0)
                    builder.Append(", ");
                builder.Append(matrix[x, y].ToString());
            }
            builder.AppendLine();
        }

        builder.Append('}');
        return builder.ToString();
    }
}