namespace Leiter.Tests.Pixels;

using System;
using Leiter.Pixels;
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

    protected override int RequiredVectorWidth => 1;

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
}
