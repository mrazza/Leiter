namespace Leiter.Tests.Pixels;

using System;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="LongPixel" />.
/// </summary>
public class LongPixelTests : PixelContractTestsBase<LongPixel, long>
{
    protected override LongPixel ArithmeticLeft => new(10L);

    protected override LongPixel ArithmeticRight => new(2L);

    protected override (LongPixel Add, LongPixel Subtract, LongPixel Multiply, LongPixel Divide) ExpectedSelfResults =>
        (new(12L), new(8L), new(20L), new(5L));

    protected override (LongPixel Add, LongPixel Subtract, LongPixel Multiply, LongPixel Divide) ExpectedScaledByTwoResults =>
        ExpectedSelfResults;

    protected override LongPixel VectorSample => new(123L);

    protected override double ExpectedVectorFirstComponent => 123.0;

    protected override LongPixel EnumeratorSample => new(123L);

    protected override long[] ExpectedComponents => [123L];

    protected override Func<long, long> ComponentDoubleFunc => static v => v * 2L;

    protected override LongPixel ExpectedComponentMapped => new(20L);

    protected override LongPixel ExpectedColorComponentMapped => new(20L);

    protected override LongPixel DistanceFirst => new(100L);

    protected override LongPixel DistanceSecond => new(150L);

    protected override double ExpectedDistance => 50.0;

    protected override LongPixel ExpectedLerpMidpoint => new(6L);

    [Fact]
    public void Properties_AndImplicitOperators_ShouldWork()
    {
        var p = new LongPixel(123L);
        Assert.Equal(123L, p.Value);
        Assert.Equal(123.0, p.AsDouble());
        Assert.Equal(0L, LongPixel.Zero.Value);

        long l = p;
        Assert.Equal(123L, l);

        LongPixel p2 = 456L;
        Assert.Equal(456L, p2.Value);
    }

    [Fact]
    public void Lerp_HalfStepRounding_ShouldRoundAwayFromZero()
    {
        Assert.Equal(new LongPixel(1L), LongPixel.Lerp(new LongPixel(0L), new LongPixel(1L), 0.5));
        Assert.Equal(new LongPixel(3L), LongPixel.Lerp(new LongPixel(2L), new LongPixel(3L), 0.5));
        Assert.Equal(new LongPixel(-1L), LongPixel.Lerp(new LongPixel(0L), new LongPixel(-1L), 0.5));
        Assert.Equal(new LongPixel(-3L), LongPixel.Lerp(new LongPixel(-2L), new LongPixel(-3L), 0.5));
        Assert.Equal(new LongPixel(25L), LongPixel.Lerp(new LongPixel(10L), new LongPixel(20L), 1.5));
    }

    [Fact]
    public void MultiplyAndDivide_FractionalHalf_ShouldRoundAwayFromZero()
    {
        Assert.Equal(new LongPixel(3L), new LongPixel(5L).Multiply(0.5));
        Assert.Equal(new LongPixel(-3L), new LongPixel(-5L).Multiply(0.5));
        Assert.Equal(new LongPixel(2L), new LongPixel(4L).Multiply(0.5));

        Assert.Equal(new LongPixel(3L), new LongPixel(5L).Multiply(0.5m));
        Assert.Equal(new LongPixel(-3L), new LongPixel(-5L).Multiply(0.5m));
        Assert.Equal(new LongPixel(2L), new LongPixel(4L).Multiply(0.5m));

        Assert.Equal(new LongPixel(3L), new LongPixel(5L).Divide(2.0));
        Assert.Equal(new LongPixel(-3L), new LongPixel(-5L).Divide(2.0));
        Assert.Equal(new LongPixel(6L), new LongPixel(3L).Divide(0.5));

        Assert.Equal(new LongPixel(3L), new LongPixel(5L).Divide(2.0m));
        Assert.Equal(new LongPixel(-3L), new LongPixel(-5L).Divide(2.0m));
        Assert.Equal(new LongPixel(6L), new LongPixel(3L).Divide(0.5m));

        var halfScalar = new DummyScalar(0.5);
        Assert.Equal(new LongPixel(3L), new LongPixel(5L).Multiply(halfScalar));
        Assert.Equal(new LongPixel(6L), new LongPixel(3L).Divide(halfScalar));
    }

    [Fact]
    public void DistanceSquared_ShouldComputeSquaredDifferenceWithoutLongOverflow()
    {
        Assert.Equal(2500.0, new LongPixel(100L).DistanceSquared(new LongPixel(150L)));
        Assert.Equal(0.0, new LongPixel(42L).DistanceSquared(new LongPixel(42L)));

        double extremeDistSq = new LongPixel(long.MaxValue).DistanceSquared(new LongPixel(long.MinValue));
        Assert.True(double.IsFinite(extremeDistSq));
        Assert.True(extremeDistSq > 0.0);
    }
}
