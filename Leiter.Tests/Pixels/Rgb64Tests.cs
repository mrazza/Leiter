namespace Leiter.Tests.Pixels;

using System;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Rgb64" />.
/// </summary>
public class Rgb64Tests : PixelContractTestsBase<Rgb64, double>
{
    protected override Rgb64 ArithmeticLeft => new(10.0, 20.0, 30.0);

    protected override Rgb64 ArithmeticRight => new(2.0, 2.0, 2.0);

    protected override (Rgb64 Add, Rgb64 Subtract, Rgb64 Multiply, Rgb64 Divide) ExpectedSelfResults =>
        (new(12.0, 22.0, 32.0), new(8.0, 18.0, 28.0), new(20.0, 40.0, 60.0), new(5.0, 10.0, 15.0));

    protected override (Rgb64 Add, Rgb64 Subtract, Rgb64 Multiply, Rgb64 Divide) ExpectedScaledByTwoResults =>
        ExpectedSelfResults;

    protected override Rgb64 VectorSample => new(1.0, 2.0, 3.0);

    protected override double ExpectedVectorFirstComponent => 1.0;

    protected override Rgb64 EnumeratorSample => new(1.0, 2.0, 3.0);

    protected override double[] ExpectedComponents => [1.0, 2.0, 3.0];

    protected override Func<double, double> ComponentDoubleFunc => static b => b * 2.0;

    protected override Rgb64 ExpectedComponentMapped => new(20.0, 40.0, 60.0);

    protected override Rgb64 ExpectedColorComponentMapped => new(20.0, 40.0, 60.0);

    protected override Rgb64 DistanceFirst => new(1.0, 2.0, 3.0);

    protected override Rgb64 DistanceSecond => new(4.0, 6.0, 3.0);

    protected override double ExpectedDistance => 5.0;

    protected override Rgb64 ExpectedLerpMidpoint => new(6.0, 11.0, 16.0);

    [Fact]
    public void Properties_ShouldWork()
    {
        var p = new Rgb64(0.1, 0.2, 0.3);
        Assert.Equal(0.1, p.R);
        Assert.Equal(0.2, p.G);
        Assert.Equal(0.3, p.B);
        Assert.Equal(0.0, Rgb64.Zero.R);
    }

    [Fact]
    public void DistanceSquared_ShouldWork()
    {
        var p1 = new Rgb64(1.0, 2.0, 3.0);
        var p2 = new Rgb64(4.0, 6.0, 3.0);
        Assert.Equal(25.0, p1.DistanceSquared(p2));
    }

    [Fact]
    public void Lerp_ShouldInterpolateChannels()
    {
        var from = new Rgb64(0.2, 0.4, 0.8);
        var to = new Rgb64(0.6, 0.0, 1.0);

        Assert.Equal(from, Rgb64.Lerp(from, to, 0.0));
        Assert.Equal(to, Rgb64.Lerp(from, to, 1.0));

        Rgb64 mid = Rgb64.Lerp(from, to, 0.25);
        Assert.Equal(0.3, mid.R, 10);
        Assert.Equal(0.3, mid.G, 10);
        Assert.Equal(0.85, mid.B, 10);
    }

    [Fact]
    public void NormAndNormSquared_ShouldComputeEuclideanChannelMagnitude()
    {
        var positive = new Rgb64(2.0, 3.0, 6.0);
        Assert.Equal(49.0, positive.NormSquared(), 12);
        Assert.Equal(7.0, positive.Norm(), 12);

        var negative = new Rgb64(-2.0, -3.0, -6.0);
        Assert.Equal(49.0, negative.NormSquared(), 12);
        Assert.Equal(7.0, negative.Norm(), 12);

        Assert.Equal(0.0, Rgb64.Zero.NormSquared());
        Assert.Equal(0.0, Rgb64.Zero.Norm());
    }

    [Fact]
    public void ToVector3D_ShouldReturnChannelVector()
    {
        var pixel = new Rgb64(0.25, 0.5, 0.75);
        Assert.Equal(new Vector3D(0.25, 0.5, 0.75), pixel.ToVector3D());
    }
}
