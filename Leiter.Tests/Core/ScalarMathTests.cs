namespace Leiter.Tests.Core;

using System;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="ScalarMath"/>.
/// </summary>
public class ScalarMathTests
{
    [Fact]
    public void Lerp_ShouldInterpolateAndExtrapolate()
    {
        Assert.Equal(10.0, ScalarMath.Lerp(10.0, 20.0, 0.0), 10);
        Assert.Equal(15.0, ScalarMath.Lerp(10.0, 20.0, 0.5), 10);
        Assert.Equal(20.0, ScalarMath.Lerp(10.0, 20.0, 1.0), 10);
        Assert.Equal(30.0, ScalarMath.Lerp(10.0, 20.0, 2.0), 10);
        Assert.Equal(0.0, ScalarMath.Lerp(10.0, 20.0, -1.0), 10);
    }

    [Fact]
    public void Smoothstep_ShouldClampAndEvaluateCubicHermiteCurve()
    {
        Assert.Equal(0.0, ScalarMath.Smoothstep(-0.5), 10);
        Assert.Equal(0.0, ScalarMath.Smoothstep(0.0), 10);
        Assert.Equal(0.15625, ScalarMath.Smoothstep(0.25), 10);
        Assert.Equal(0.5, ScalarMath.Smoothstep(0.5), 10);
        Assert.Equal(0.84375, ScalarMath.Smoothstep(0.75), 10);
        Assert.Equal(1.0, ScalarMath.Smoothstep(1.0), 10);
        Assert.Equal(1.0, ScalarMath.Smoothstep(1.5), 10);
        Assert.True(double.IsNaN(ScalarMath.Smoothstep(double.NaN)));
    }

    [Fact]
    public void OuterBandSmoothstepWeight_ShouldTaperSmoothlyAcrossBand()
    {
        Assert.Equal(1.0, ScalarMath.OuterBandSmoothstepWeight(0, innerRadius: 3, outerRadius: 6), 10);
        Assert.Equal(1.0, ScalarMath.OuterBandSmoothstepWeight(3, innerRadius: 3, outerRadius: 6), 10);

        double w4 = ScalarMath.OuterBandSmoothstepWeight(4, innerRadius: 3, outerRadius: 6);
        double w5 = ScalarMath.OuterBandSmoothstepWeight(5, innerRadius: 3, outerRadius: 6);
        double w6 = ScalarMath.OuterBandSmoothstepWeight(6, innerRadius: 3, outerRadius: 6);
        Assert.InRange(w4, 0.0, 1.0);
        Assert.True(w4 > w5 && w5 > w6 && w6 > 0.0);

        Assert.Equal(0.0, ScalarMath.OuterBandSmoothstepWeight(7, innerRadius: 3, outerRadius: 6), 10);
        Assert.Equal(1.0, ScalarMath.OuterBandSmoothstepWeight(2, innerRadius: 2, outerRadius: 2), 10);
        Assert.Equal(0.0, ScalarMath.OuterBandSmoothstepWeight(3, innerRadius: 2, outerRadius: 2), 10);

        Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.OuterBandSmoothstepWeight(0, innerRadius: 5, outerRadius: 4));
    }

    [Fact]
    public void ClampUnitOrDefault_ShouldClampFiniteValuesAndHandleNonFinite()
    {
        Assert.Equal(0.0, ScalarMath.ClampUnitOrDefault(-0.5), 10);
        Assert.Equal(0.35, ScalarMath.ClampUnitOrDefault(0.35), 10);
        Assert.Equal(1.0, ScalarMath.ClampUnitOrDefault(2.5), 10);
        Assert.Equal(0.0, ScalarMath.ClampUnitOrDefault(double.NaN), 10);
        Assert.Equal(0.5, ScalarMath.ClampUnitOrDefault(double.NaN, fallback: 0.5), 10);
        Assert.Equal(0.25, ScalarMath.ClampUnitOrDefault(double.PositiveInfinity, fallback: 0.25), 10);
    }

    [Fact]
    public void PackUnorderedPairKey_ShouldBeOrderIndependentAndUnique()
    {
        Assert.Equal(ScalarMath.PackUnorderedPairKey(3, 17), ScalarMath.PackUnorderedPairKey(17, 3));
        Assert.Equal(ScalarMath.PackUnorderedPairKey(-1, 5), ScalarMath.PackUnorderedPairKey(5, -1));
        Assert.NotEqual(ScalarMath.PackUnorderedPairKey(1, 2), ScalarMath.PackUnorderedPairKey(1, 3));
        Assert.NotEqual(ScalarMath.PackUnorderedPairKey(1, 100), ScalarMath.PackUnorderedPairKey(2, 100));
    }

    [Fact]
    public void Sqrt2Constants_ShouldMatchMathSqrt2()
    {
        double sqrt2 = ScalarMath.Sqrt2;
        double invSqrt2 = ScalarMath.InvSqrt2;
        Assert.Equal(Math.Sqrt(2.0), sqrt2, 15);
        Assert.Equal(1.0 / Math.Sqrt(2.0), invSqrt2, 15);
    }
}
