namespace Leiter.Core;

/// <summary>
/// Provides indexed pixel access over either a raw backing array or an arbitrary <see cref="IReadOnlyMatrix{T}"/>.
/// </summary>
internal interface IPixelSource<T>
    where T : struct
{
    T this[int index] { get; }

    T GetAt(int x, int y, int linearIndex);
}
