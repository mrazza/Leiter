namespace Leiter.Tests.Pixels.Internal;

using System;
using Leiter.Pixels;
using Leiter.Pixels.Internal;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="PixelGuard"/>.
/// </summary>
public class PixelGuardTests
{
    [Fact]
    public void ThrowIfNotFinite_Rgb64_ShouldAcceptFiniteAndRejectNonFiniteWithParamName()
    {
        PixelGuard.ThrowIfNotFinite(Rgb64.Zero);
        PixelGuard.ThrowIfNotFinite(new Rgb64(-0.5, 0.5, 1.5));

        var badRed = new Rgb64(double.NaN, 0.5, 0.5);
        ArgumentOutOfRangeException redEx = Assert.Throws<ArgumentOutOfRangeException>(() => PixelGuard.ThrowIfNotFinite(badRed));
        Assert.Equal(nameof(badRed), redEx.ParamName);

        var badGreen = new Rgb64(0.5, double.PositiveInfinity, 0.5);
        ArgumentOutOfRangeException greenEx = Assert.Throws<ArgumentOutOfRangeException>(() => PixelGuard.ThrowIfNotFinite(badGreen));
        Assert.Equal(nameof(badGreen), greenEx.ParamName);

        var badBlue = new Rgb64(0.5, 0.5, double.NegativeInfinity);
        ArgumentOutOfRangeException blueEx = Assert.Throws<ArgumentOutOfRangeException>(() => PixelGuard.ThrowIfNotFinite(badBlue));
        Assert.Equal(nameof(badBlue), blueEx.ParamName);

        ArgumentOutOfRangeException customEx = Assert.Throws<ArgumentOutOfRangeException>(() => PixelGuard.ThrowIfNotFinite(badRed, "customColor"));
        Assert.Equal("customColor", customEx.ParamName);
    }
}
