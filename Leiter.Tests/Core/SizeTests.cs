
using Xunit;
using Leiter.Core;

namespace Leiter.Tests.Core;

public class SizeTests
{
    [Fact]
    public void Constructor_AndProperties_ShouldWork()
    {
        var size = new Size(10, 20);
        Assert.Equal(10, size.Width);
        Assert.Equal(20, size.Height);
    }
}
