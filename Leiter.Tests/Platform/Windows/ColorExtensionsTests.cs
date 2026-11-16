namespace Leiter.Tests.Platform.Windows;

using System;
using System.Runtime.Versioning;
using Leiter.Platform.Windows;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Leiter.Platform.Windows.ColorExtensions" />.
/// </summary>
[SupportedOSPlatform("windows")]
public class ColorExtensionsTests
{
    [Fact]
    public void ColorExtensions_ShouldConvertCorrectly()
    {
        if (!OperatingSystem.IsWindows()) return;

        RunColorExtensionsTest();
    }

    private void RunColorExtensionsTest()
    {
        var original = System.Drawing.Color.FromArgb(10, 20, 30);
        var leiterPixel = original.ToRgb8();
        Assert.Equal(10, leiterPixel.R);
        Assert.Equal(20, leiterPixel.G);
        Assert.Equal(30, leiterPixel.B);

        var convertedBack = leiterPixel.ToColor();
        Assert.Equal(original.R, convertedBack.R);
        Assert.Equal(original.G, convertedBack.G);
        Assert.Equal(original.B, convertedBack.B);
    }
}
