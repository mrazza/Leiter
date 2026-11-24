namespace Leiter.Core;

using System.Runtime.CompilerServices;

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
