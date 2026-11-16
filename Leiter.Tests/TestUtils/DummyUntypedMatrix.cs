namespace Leiter.Tests.TestUtils;

using Leiter.Core;

/// <summary>
/// A reusable, simple implementation of <see cref="IUntypedMatrix"/> for testing.
/// </summary>
public class DummyUntypedMatrix : IUntypedMatrix
{
    /// <summary>
    /// Gets or sets the width of the mock matrix.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the height of the mock matrix.
    /// </summary>
    public int Height { get; set; }

    /// <inheritdoc/>
    public Size Size => new(Width, Height);

    /// <inheritdoc/>
    public int Count => Width * Height;

    /// <summary>
    /// Initializes a new instance of the <see cref="DummyUntypedMatrix"/> class.
    /// </summary>
    /// <param name="width">The initial width of the matrix.</param>
    /// <param name="height">The initial height of the matrix.</param>
    public DummyUntypedMatrix(int width = 0, int height = 0)
    {
        Width = width;
        Height = height;
    }
}
