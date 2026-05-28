namespace Leiter.Tests.TestUtils;

using Leiter.Core;

/// <summary>
/// A reusable, simple implementation of <see cref="IUntypedMatrix"/> for testing.
/// </summary>
public class DummyUntypedMatrix : IUntypedMatrix
{
    /// <summary>
    /// Gets the width property.
    /// </summary>
    public int Width { get; set; }
    /// <summary>
    /// Gets the height property.
    /// </summary>
    public int Height { get; set; }
    /// <summary>
    /// Gets the size property.
    /// </summary>
    public Size Size => new(Width, Height);
    /// <summary>
    /// Gets the count property.
    /// </summary>
    public int Count => Width * Height;

    /// <summary>
    /// Initializes a new instance of the <see cref="DummyUntypedMatrix" /> class.
    /// </summary>
    public DummyUntypedMatrix(int width = 0, int height = 0)
    {
        Width = width;
        Height = height;
    }
}
