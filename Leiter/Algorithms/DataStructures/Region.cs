namespace Leiter.Algorithms.DataStructures;

/// <summary>
/// Represents a region (either connected or disconnected) of a matrix.
///
/// Usually a collection of pixels in an image.
/// </summary>
/// <typeparam name="T">The type to use when representing elements in the region.</typeparam>
public class Region<T>
{
    /// <summary>
    /// Gets the unique identifier of the region.
    /// </summary>
    public int Id { get; init; } = -1;

    /// <summary>
    /// Gets the set of pixels contained within this region.
    /// </summary>
    public HashSet<T> Pixels { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Region{T}"/> class that is initially empty.
    /// </summary>
    public Region()
    {
        Pixels = [];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Region{T}"/> class containing a single initial element.
    /// </summary>
    /// <param name="initialElement">The initial element to add to the region.</param>
    public Region(T initialElement)
        : this()
    {
        Pixels.Add(initialElement);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Region{T}"/> class populated from an existing collection of pixels.
    /// </summary>
    /// <param name="pixels">The collection of pixels to populate the region with.</param>
    public Region(IEnumerable<T> pixels)
    {
        Pixels = [.. pixels];
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Region{T}"/> class with the specified initial capacity.
    /// </summary>
    /// <param name="size">The initial capacity of the underlying pixel set.</param>
    public Region(int size)
    {
        Pixels = new HashSet<T>(size);
    }
}