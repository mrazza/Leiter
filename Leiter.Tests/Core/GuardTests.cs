namespace Leiter.Tests.Core;

using System;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for internal validation helpers in <see cref="Guard"/>.
/// </summary>
public class GuardTests
{
    [Fact]
    public void ThrowIfNotFinite_ShouldAcceptFiniteAndRejectNonFinite()
    {
        Guard.ThrowIfNotFinite(0.0);
        Guard.ThrowIfNotFinite(-42.5);

        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFinite(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFinite(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFinite(double.NegativeInfinity));
    }

    [Fact]
    public void ThrowIfNotFinitePositive_ShouldValidatePositiveDomain()
    {
        Guard.ThrowIfNotFinitePositive(1e-9);
        Guard.ThrowIfNotFinitePositive(42.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFinitePositive(0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFinitePositive(-1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFinitePositive(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFinitePositive(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFinitePositive(double.NegativeInfinity));
    }

    [Fact]
    public void ThrowIfNotFiniteNonNegative_ShouldValidateNonNegativeDomain()
    {
        Guard.ThrowIfNotFiniteNonNegative(0.0);
        Guard.ThrowIfNotFiniteNonNegative(10.5);

        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFiniteNonNegative(-0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFiniteNonNegative(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFiniteNonNegative(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFiniteNonNegative(double.NegativeInfinity));
    }

    [Fact]
    public void ThrowIfNotFiniteInRange_ShouldValidateClosedInterval()
    {
        Guard.ThrowIfNotFiniteInRange(0.0, 0.0, 1.0);
        Guard.ThrowIfNotFiniteInRange(0.5, 0.0, 1.0);
        Guard.ThrowIfNotFiniteInRange(1.0, 0.0, 1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFiniteInRange(-0.01, 0.0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFiniteInRange(1.01, 0.0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFiniteInRange(double.NaN, 0.0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFiniteInRange(double.PositiveInfinity, 0.0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfNotFiniteInRange(double.NegativeInfinity, 0.0, 1.0));
    }

    [Fact]
    public void ThrowIfUndefinedEnum_ShouldAcceptDefinedAndRejectUndefined()
    {
        Guard.ThrowIfUndefinedEnum(EdgeHandling.Extend);
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.ThrowIfUndefinedEnum((EdgeHandling)999));
    }

    [Fact]
    public void ThrowIfSizeMismatch_ShouldValidateNullAndDimensions()
    {
        var a = new SequentialMatrix<DoublePixel>(2, 3);
        var b = new SequentialMatrix<Rgb8>(2, 3);
        var c = new SequentialMatrix<Rgb8>(3, 2);

        Guard.ThrowIfSizeMismatch(a, b);

        Assert.Throws<ArgumentNullException>(() => Guard.ThrowIfSizeMismatch(null!, b));
        Assert.Throws<ArgumentNullException>(() => Guard.ThrowIfSizeMismatch(a, null!));
        Assert.Throws<ArgumentException>(() => Guard.ThrowIfSizeMismatch(a, c));

        Guard.ThrowIfSizeMismatch(new Size(2, 3), new Size(2, 3));
        Assert.Throws<ArgumentException>(() => Guard.ThrowIfSizeMismatch(new Size(2, 3), new Size(3, 2)));

        Guard.ThrowIfSizeMismatchIfNotNull(a, null);
        Guard.ThrowIfSizeMismatchIfNotNull(a, b);
        Assert.Throws<ArgumentNullException>(() => Guard.ThrowIfSizeMismatchIfNotNull(null!, b));
        Assert.Throws<ArgumentException>(() => Guard.ThrowIfSizeMismatchIfNotNull(a, c));
    }
}
