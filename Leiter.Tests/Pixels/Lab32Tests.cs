namespace Leiter.Tests.Pixels;

using System;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Lab32" />.
/// </summary>
public class Lab32Tests : PixelContractTestsBase<Lab32, float>
{
    protected override Lab32 ArithmeticLeft => new(10.0f, 20.0f, 30.0f);

    protected override Lab32 ArithmeticRight => new(2.0f, 2.0f, 2.0f);

    protected override (Lab32 Add, Lab32 Subtract, Lab32 Multiply, Lab32 Divide) ExpectedSelfResults =>
        (new(12.0f, 22.0f, 32.0f), new(8.0f, 18.0f, 28.0f), new(20.0f, 40.0f, 60.0f), new(5.0f, 10.0f, 15.0f));

    protected override (Lab32 Add, Lab32 Subtract, Lab32 Multiply, Lab32 Divide) ExpectedScaledByTwoResults =>
        ExpectedSelfResults;

    protected override Lab32 VectorSample => new(1.0f, 2.0f, 3.0f);

    protected override double ExpectedVectorFirstComponent => 1.0;

    protected override Lab32 EnumeratorSample => new(1.0f, 2.0f, 3.0f);

    protected override float[] ExpectedComponents => [1.0f, 2.0f, 3.0f];

    protected override Func<float, float> ComponentDoubleFunc => static b => b * 2.0f;

    protected override Lab32 ExpectedComponentMapped => new(20.0f, 40.0f, 60.0f);

    protected override Lab32 ExpectedColorComponentMapped => new(10.0f, 40.0f, 60.0f);

    protected override Lab32 DistanceFirst => new(1.0f, 2.0f, 3.0f);

    protected override Lab32 DistanceSecond => new(4.0f, 2.0f, 3.0f);

    protected override double ExpectedDistance => 1.7550397986383963;

    protected override Lab32 ExpectedLerpMidpoint => new(6.0f, 11.0f, 16.0f);

    [Fact]
    public void Properties_ShouldWork()
    {
        var p = new Lab32(1.0f, 2.0f, 3.0f);
        Assert.Equal(1.0f, p.L);
        Assert.Equal(2.0f, p.A);
        Assert.Equal(3.0f, p.B);
        Assert.Equal(0.0f, Lab32.Zero.L);
    }

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

    [Fact]
    public void EuclideanDistance_ShouldComputeCIE76Distance()
    {
        var a = new Lab32(50, 10, 20);
        var b = new Lab32(53, 14, 32);

        Assert.Equal(0.0, a.EuclideanDistance(a), 12);
        Assert.Equal(13.0, a.EuclideanDistance(b), 12);
        Assert.Equal(a.EuclideanDistance(b), b.EuclideanDistance(a), 12);
    }

    /// <summary>
    /// Verifies that <see cref="Lab32.EuclideanDistanceSquared"/> computes exact squared CIE76 Euclidean distance.
    /// </summary>
    [Fact]
    public void EuclideanDistanceSquared_ShouldComputeSquaredCIE76Distance()
    {
        var a = new Lab32(50.0f, 10.0f, 20.0f);
        var b = new Lab32(53.0f, 14.0f, 32.0f);

        Assert.Equal(0.0, a.EuclideanDistanceSquared(a), 12);
        Assert.Equal(169.0, a.EuclideanDistanceSquared(b), 12);
        Assert.Equal(a.EuclideanDistanceSquared(b), b.EuclideanDistanceSquared(a), 12);
    }

    [Fact]
    public void TestCIEDE2000_HueWraparoundBranches()
    {
        // Straddle 0°/360° with sumH' < 360 and |diffH'| > 180.
        var lowHue1 = new Lab32(50f, 30f, 2f);
        var highHue1 = new Lab32(50f, 30f, -10f);
        double d12 = lowHue1.Distance(highHue1);
        double d21 = highHue1.Distance(lowHue1);
        Assert.True(d12 > 0);
        Assert.Equal(d12, d21, 10);

        // Straddle 0°/360° with sumH' >= 360 and |diffH'| > 180.
        var lowHue2 = new Lab32(50f, 30f, 15f);
        var highHue2 = new Lab32(50f, 30f, -2f);
        double d34 = lowHue2.Distance(highHue2);
        Assert.True(d34 > 0);
        Assert.Equal(d34, highHue2.Distance(lowHue2), 10);
    }

    [Fact]
    public void DeltaE2000_DistanceSquared_ToVector3D_AndExtremeEuclidean_ShouldWork()
    {
        var a = new Lab32(50f, 10f, 20f);
        var b = new Lab32(53f, 14f, 32f);

        Assert.Equal(a.Distance(b), a.DeltaE2000(b));
        Assert.Equal(a.EuclideanDistanceSquared(b), a.DistanceSquared(b));
        Assert.Equal(new Vector3D(50.0, 10.0, 20.0), a.ToVector3D());

        var posExtreme = new Lab32(3e38f, 0.0f, 0.0f);
        var negExtreme = new Lab32(-3e38f, 0.0f, 0.0f);
        double extremeDistance = posExtreme.EuclideanDistance(negExtreme);
        Assert.True(double.IsFinite(extremeDistance));
        Assert.Equal((double)posExtreme.L - negExtreme.L, extremeDistance, 1e25);
    }
}
