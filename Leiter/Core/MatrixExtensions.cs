namespace Leiter.Core;

public static class MatrixExtensions
{
    public static SequentialMatrix<T> ToSequentialMatrix<T>(this IEnumerable<T> values, int width, int height)
        where T : struct, ISelfOperable<T>, INumericOperable<T>, IScalarOperable<T>
    {
        var result = new SequentialMatrix<T>(width, height);
        var enumerator = values.GetEnumerator();

        for (int index = 0; index < result.Count; index++)
        {
            if (!enumerator.MoveNext())
            {
                throw new ArgumentException($"IEnumerable of type {values.GetType()} is smaller than matrix of size {{{width}, {height}}}.");
            }
            result[index] = enumerator.Current;
        }

        return result;
    }

    public static Coord CoordFromIndex(this IUntypedMatrix matrix, int index) => new(index % matrix.Width, index / matrix.Width);

    public static int IndexFromCoord(this IUntypedMatrix matrix, Coord coord) => coord.X + coord.Y * matrix.Width;

    public static NormalizedCoord NormalizeCoord(this IUntypedMatrix matrix, Coord coord) => new(coord.X / (double)matrix.Width, coord.Y / (double)matrix.Height);

    public static Coord DenormalizeCoord(this IUntypedMatrix matrix, NormalizedCoord normalizedCoord) => new((int)(normalizedCoord.X * matrix.Width), (int)(normalizedCoord.Y * matrix.Height));
}