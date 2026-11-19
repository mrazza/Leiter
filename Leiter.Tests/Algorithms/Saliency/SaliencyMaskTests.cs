namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyMask"/>.
/// </summary>
public class SaliencyMaskTests
{
    [Fact]
    public void ComputeThreshold_And_Binarize_AcrossMethodsAndRetentionPolicies_ShouldWork()
    {
        var map = new SequentialMatrix<DoublePixel>(10, 10);
        for (int y = 1; y <= 4; y++)
        {
            for (int x = 1; x <= 4; x++)
            {
                map[x, y] = 0.85;
            }
        }

        for (int y = 7; y <= 8; y++)
        {
            for (int x = 7; x <= 8; x++)
            {
                map[x, y] = 0.80;
            }
        }

        map[0, 9] = 0.90;
        map[9, 0] = double.NaN;

        double otsuThreshold = SaliencyMask.ComputeThreshold(map, SaliencyThresholdMethod.Otsu);
        Assert.InRange(otsuThreshold, 0.2, 0.7);

        double adaptiveThreshold = SaliencyMask.ComputeThreshold(map, SaliencyThresholdMethod.AdaptiveMean);
        Assert.True(adaptiveThreshold > 0.0 && adaptiveThreshold <= 0.95);

        Assert.Equal(0.5, SaliencyMask.ComputeThreshold(map, SaliencyThresholdMethod.Fixed, 0.5), 6);

        MaskMatrix multiObjectMask = SaliencyMask.Binarize(
            map,
            new SaliencyMaskOptions(
                Method: SaliencyThresholdMethod.Otsu,
                RetentionPolicy: ComponentRetentionPolicy.MinimumAreaFraction,
                MinAreaFraction: 0.02));
        Assert.True(multiObjectMask[2, 2]);
        Assert.True(multiObjectMask[7, 7]);
        Assert.False(multiObjectMask[0, 9]);
        Assert.False(multiObjectMask[9, 0]);

        MaskMatrix largestOnly = SaliencyMask.Binarize(
            map,
            new SaliencyMaskOptions(
                Method: SaliencyThresholdMethod.Fixed,
                FixedThreshold: 0.5,
                RetentionPolicy: ComponentRetentionPolicy.LargestComponentOnly));
        Assert.True(largestOnly[2, 2]);
        Assert.False(largestOnly[7, 7]);
        Assert.False(largestOnly[0, 9]);

        MaskMatrix keepAll = SaliencyMask.Binarize(
            map,
            new SaliencyMaskOptions(
                Method: SaliencyThresholdMethod.Fixed,
                FixedThreshold: 0.5,
                RetentionPolicy: ComponentRetentionPolicy.KeepAll));
        Assert.True(keepAll[0, 9]);
        Assert.True(keepAll[7, 7]);
    }

    [Fact]
    public void UniformAndEdgeInputs_ShouldBeHandledCleanly()
    {
        var allZero = new SequentialMatrix<DoublePixel>(4, 4);
        Assert.Equal(1.0, SaliencyMask.ComputeThreshold(allZero, SaliencyThresholdMethod.Otsu), 6);
        MaskMatrix zeroMask = SaliencyMask.Binarize(allZero, new SaliencyMaskOptions(Method: SaliencyThresholdMethod.Otsu));
        for (int i = 0; i < zeroMask.Count; i++)
        {
            Assert.False(zeroMask[i]);
        }

        var allHigh = new SequentialMatrix<DoublePixel>(4, 4);
        allHigh.SetAll(0.8);
        MaskMatrix highMask = SaliencyMask.Binarize(allHigh, new SaliencyMaskOptions(Method: SaliencyThresholdMethod.Otsu));
        for (int i = 0; i < highMask.Count; i++)
        {
            Assert.True(highMask[i]);
        }

        var emptyDouble = new SequentialMatrix<DoublePixel>(0, 0);
        Assert.Equal(0.0, SaliencyMask.ComputeThreshold(emptyDouble));
        Assert.Empty(SaliencyMask.Binarize(emptyDouble));
    }

    [Fact]
    public void Validation_InvalidArguments_ShouldThrowExpectedExceptions()
    {
        var allZero = new SequentialMatrix<DoublePixel>(4, 4);

        Assert.Throws<ArgumentNullException>(() => SaliencyMask.ComputeThreshold(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.ComputeThreshold(allZero, (SaliencyThresholdMethod)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.ComputeThreshold(allZero, SaliencyThresholdMethod.Fixed, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.ComputeThreshold(allZero, SaliencyThresholdMethod.Fixed, 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.ComputeThreshold(allZero, SaliencyThresholdMethod.Fixed, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.Binarize(allZero, new SaliencyMaskOptions(RetentionPolicy: (ComponentRetentionPolicy)99)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.Binarize(allZero, new SaliencyMaskOptions(MinAreaFraction: -0.05)));
    }
}
