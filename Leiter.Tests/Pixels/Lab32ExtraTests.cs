
using Xunit;
using Leiter.Pixels;

namespace Leiter.Tests.Pixels;

/// <summary>
/// Provides unit tests or helpers for <see cref="Lab32ExtraTests" />.
/// </summary>
public class Lab32ExtraTests
{
    /// <summary>
    /// Executes the test e2000 special cases operation.
    /// </summary>
    [Fact]
    public void TestCIEDE2000_SpecialCases()
    {
        var gray1 = new Lab32(50, 0, 0);
        var gray2 = new Lab32(50, 0, 0);
        var color1 = new Lab32(50, 10, 20);
        var color2 = new Lab32(50, -10, -20);
        var color3 = new Lab32(50, 10, -20);

        Assert.Equal(0.0, gray1.Distance(gray2), 5);
        Assert.True(gray1.Distance(color1) > 0);
        Assert.True(color1.Distance(gray2) > 0);
        Assert.True(color1.Distance(color2) > 0);
        Assert.True(color1.Distance(color3) > 0);
    }
}
