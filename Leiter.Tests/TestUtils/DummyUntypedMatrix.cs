namespace Leiter.Tests.TestUtils;

using Leiter.Core;

/// <summary>
/// A reusable, simple implementation of <see cref="IUntypedMatrix"/> for testing.
/// </summary>
public class DummyUntypedMatrix : IUntypedMatrix
{
    public int Width { get; set; }
    public int Height { get; set; }
    public Size Size => new(Width, Height);
    public int Count => Width * Height;

    public DummyUntypedMatrix(int width = 0, int height = 0)
    {
        Width = width;
        Height = height;
    }
}
