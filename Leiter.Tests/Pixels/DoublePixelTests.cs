namespace Leiter.Tests.Pixels;

using System;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="DoublePixel" />.
/// </summary>
public class DoublePixelTests : PixelContractTestsBase<DoublePixel, double>
{
    protected override DoublePixel ArithmeticLeft => new(10.0);

    protected override DoublePixel ArithmeticRight => new(2.0);

    protected override (DoublePixel Add, DoublePixel Subtract, DoublePixel Multiply, DoublePixel Divide) ExpectedSelfResults =>
        (new(12.0), new(8.0), new(20.0), new(5.0));

    protected override (DoublePixel Add, DoublePixel Subtract, DoublePixel Multiply, DoublePixel Divide) ExpectedScaledByTwoResults =>
        ExpectedSelfResults;

    protected override DoublePixel VectorSample => new(5.5);

    protected override double ExpectedVectorFirstComponent => 5.5;

    protected override DoublePixel EnumeratorSample => new(5.5);

    protected override double[] ExpectedComponents => [5.5];

    protected override Func<double, double> ComponentDoubleFunc => static v => v * 2.0;

    protected override DoublePixel ExpectedComponentMapped => new(20.0);

    protected override DoublePixel ExpectedColorComponentMapped => new(20.0);

    protected override DoublePixel DistanceFirst => new(1.5);

    protected override DoublePixel DistanceSecond => new(4.0);

    protected override double ExpectedDistance => 2.5;

    protected override DoublePixel ExpectedLerpMidpoint => new(6.0);

    [Fact]
    public void Properties_AndImplicitOperators_ShouldWork()
    {
        var p = new DoublePixel(2.5);
        Assert.Equal(2.5, p.Value);
        Assert.Equal(2.5, p.AsDouble());
        Assert.Equal(0.0, DoublePixel.Zero.Value);

        double d = p;
        Assert.Equal(2.5, d);

        DoublePixel p2 = 3.5;
        Assert.Equal(3.5, p2.Value);
    }
}
