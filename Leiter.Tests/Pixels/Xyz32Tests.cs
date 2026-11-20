namespace Leiter.Tests.Pixels;

using System;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Xyz32" />.
/// </summary>
public class Xyz32Tests : PixelContractTestsBase<Xyz32, float>
{
    protected override Xyz32 ArithmeticLeft => new(10.0f, 20.0f, 30.0f);

    protected override Xyz32 ArithmeticRight => new(2.0f, 2.0f, 2.0f);

    protected override (Xyz32 Add, Xyz32 Subtract, Xyz32 Multiply, Xyz32 Divide) ExpectedSelfResults =>
        (new(12.0f, 22.0f, 32.0f), new(8.0f, 18.0f, 28.0f), new(20.0f, 40.0f, 60.0f), new(5.0f, 10.0f, 15.0f));

    protected override (Xyz32 Add, Xyz32 Subtract, Xyz32 Multiply, Xyz32 Divide) ExpectedScaledByTwoResults =>
        ExpectedSelfResults;

    protected override Xyz32 VectorSample => new(1.0f, 2.0f, 3.0f);

    protected override double ExpectedVectorFirstComponent => 1.0;

    protected override Xyz32 EnumeratorSample => new(1.0f, 2.0f, 3.0f);

    protected override float[] ExpectedComponents => [1.0f, 2.0f, 3.0f];

    protected override Func<float, float> ComponentDoubleFunc => static b => b * 2.0f;

    protected override Xyz32 ExpectedComponentMapped => new(20.0f, 40.0f, 60.0f);

    protected override Xyz32 ExpectedColorComponentMapped => new(20.0f, 20.0f, 60.0f);

    protected override Xyz32 DistanceFirst => new(1.0f, 2.0f, 3.0f);

    protected override Xyz32 DistanceSecond => new(4.0f, 2.0f, 3.0f);

    protected override double ExpectedDistance => 3.0;

    protected override Xyz32 ExpectedLerpMidpoint => new(6.0f, 11.0f, 16.0f);

    [Fact]
    public void Properties_ShouldWork()
    {
        var p = new Xyz32(1.0f, 2.0f, 3.0f);
        Assert.Equal(1.0f, p.X);
        Assert.Equal(2.0f, p.Y);
        Assert.Equal(3.0f, p.Z);
        Assert.Equal(0.0f, Xyz32.Zero.X);
    }

    [Fact]
    public void DistanceSquared_ShouldComputeSquaredEuclideanDistance()
    {
        var a = new Xyz32(1.0f, 2.0f, 3.0f);
        var b = new Xyz32(4.0f, 6.0f, 3.0f);
        Assert.Equal(25.0, a.DistanceSquared(b), 6);

        var c = new Xyz32(4.0f, 6.0f, 15.0f);
        Assert.Equal(169.0, a.DistanceSquared(c), 6);
        Assert.Equal(13.0, a.Distance(c), 6);
    }

    [Fact]
    public void Distance_ExtremeFloatValues_ShouldRemainFinite()
    {
        var a = new Xyz32(3e38f, 0.0f, 0.0f);
        var b = new Xyz32(-3e38f, 0.0f, 0.0f);

        double distance = a.Distance(b);
        Assert.True(double.IsFinite(distance));
        Assert.Equal((double)a.X - b.X, distance, 1e25);
    }
}
