namespace Leiter.Algorithms.DataStructures;

/// <summary>
/// Represents a region (either connected or disconnected) of a matrix.
///
/// Usually a collection of pixels in an image.
/// </summary>
/// <typeparam name="T">The type to use when representing elements in the region.</typeparam>
public class Region<T>
{
    public HashSet<T> Pixels { get; } = [];

    public Region() { }

    public Region(T initialElement)
        : this()
    {
        Pixels.Add(initialElement);
    }
}