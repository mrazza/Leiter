namespace Leiter.Core;

using System.Runtime.CompilerServices;

/// <summary>
/// Provides indexed pixel access over either a raw backing array or an arbitrary <see cref="IReadOnlyMatrix{T}"/>.
/// </summary>
internal interface IPixelSource<T>
    where T : struct
{
    T this[int index] { get; }

    T GetAt(int x, int y, int linearIndex);
}

internal readonly struct ArrayPixelSource<T>(T[] raw) : IPixelSource<T>
    where T : struct
{
    public T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => raw[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetAt(int x, int y, int linearIndex) => raw[linearIndex];
}

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
