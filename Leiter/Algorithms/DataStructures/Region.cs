namespace Leiter.Algorithms.DataStructures;

/// <summary>
/// Represents a region (either connected or disconnected) of a matrix.
///
/// Usually a collection of pixels in an image.
/// </summary>
/// <typeparam name="T">The type to use when representing elements in the region.</typeparam>
public class Region<T>
{
    public long Id { get; init; } = -1;
    public HashSet<T> Pixels { get; init; }

    public Region()
    {
        Pixels = [];
    }

    public Region(T initialElement)
        : this()
    {
        Pixels.Add(initialElement);
    }

    public Region(IEnumerable<T> pixels)
    {
        Pixels = [.. pixels];
    }

    public Region(int size)
    {
        Pixels = new HashSet<T>(size);
    }
}