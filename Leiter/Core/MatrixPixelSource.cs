namespace Leiter.Core;

using System.Runtime.CompilerServices;

internal readonly struct MatrixPixelSource<T>(IReadOnlyMatrix<T> matrix) : IPixelSource<T>
    where T : struct
{
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => matrix[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetAt(int x, int y, int linearIndex) => matrix[x, y];
}
