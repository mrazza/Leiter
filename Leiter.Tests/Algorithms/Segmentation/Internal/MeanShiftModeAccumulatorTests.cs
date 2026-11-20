namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MeanShiftModeAccumulator"/>.
/// </summary>
public class MeanShiftModeAccumulatorTests
{
    [Fact]
    public void EmptyAccumulator_ShouldReturnFallbackAndFailTryGetMode()
    {
        var accumulator = default(MeanShiftModeAccumulator);
        var fallback = new MeanShiftMode(3.5, 4.5, new Lab32(55f, -8f, 12f));

        Assert.Equal(0, accumulator.Count);
        Assert.Equal(0.0, accumulator.WeightSum);
        Assert.False(accumulator.TryGetMode(out MeanShiftMode mode));
        Assert.Equal(default, mode);
        Assert.Equal(fallback, accumulator.ToModeOrFallback(fallback));
    }

    [Fact]
    public void Add_UnweightedSamples_ShouldComputeExactArithmeticMean()
    {
        var accumulator = default(MeanShiftModeAccumulator);
        accumulator.Add(1.0, 2.0, new Lab32(20f, -10f, 5f));
        accumulator.Add(5.0, 6.0, new Lab32(60f, 10f, -15f));

        Assert.Equal(2, accumulator.Count);
        Assert.Equal(2.0, accumulator.WeightSum, 12);
        Assert.True(accumulator.TryGetMode(out MeanShiftMode mode));
        Assert.Equal(3.0, mode.X, 12);
        Assert.Equal(4.0, mode.Y, 12);
        Assert.Equal(40.0f, mode.Color.L, 5);
        Assert.Equal(0.0f, mode.Color.A, 5);
        Assert.Equal(-5.0f, mode.Color.B, 5);

        var fallback = new MeanShiftMode(99.0, 99.0, new Lab32(0f, 0f, 0f));
        Assert.Equal(mode, accumulator.ToModeOrFallback(fallback));
    }

    [Fact]
    public void AddWeighted_WeightedSamples_ShouldComputeWeightedMean()
    {
        var accumulator = default(MeanShiftModeAccumulator);
        accumulator.AddWeighted(new MeanShiftMode(0.0, 4.0, new Lab32(20f, 0f, 0f)), weight: 0.25);
        accumulator.AddWeighted(new MeanShiftMode(4.0, 0.0, new Lab32(60f, 12f, -8f)), weight: 0.75);

        Assert.Equal(2, accumulator.Count);
        Assert.Equal(1.0, accumulator.WeightSum, 12);
        Assert.True(accumulator.TryGetMode(out MeanShiftMode mode));
        Assert.Equal(3.0, mode.X, 12);
        Assert.Equal(1.0, mode.Y, 12);
        Assert.Equal(50.0f, mode.Color.L, 5);
        Assert.Equal(9.0f, mode.Color.A, 5);
        Assert.Equal(-6.0f, mode.Color.B, 5);
    }

    [Fact]
    public void AddWeighted_ZeroWeightOnly_ShouldFailTryGetModeAndUseFallback()
    {
        var accumulator = default(MeanShiftModeAccumulator);
        accumulator.AddWeighted(new MeanShiftMode(2.0, 3.0, new Lab32(40f, 5f, -5f)), weight: 0.0);

        var fallback = new MeanShiftMode(7.0, 8.0, new Lab32(70f, 0f, 0f));
        Assert.Equal(1, accumulator.Count);
        Assert.Equal(0.0, accumulator.WeightSum);
        Assert.False(accumulator.TryGetMode(out _));
        Assert.Equal(fallback, accumulator.ToModeOrFallback(fallback));
    }
}
