
using Xunit;
using Leiter.Core;

namespace Leiter.Tests.Core;

/// <summary>
/// Provides unit tests for <see cref="SizeTests" />.
/// </summary>
public class SizeTests
{
    /// <summary>
    /// Verifies that the constructor and properties should work behaves correctly.
    /// </summary>
    [Fact]
    public void Constructor_AndProperties_ShouldWork()
    {
        var size = new Size(10, 20);
        Assert.Equal(10, size.Width);
        Assert.Equal(20, size.Height);
    }
}
