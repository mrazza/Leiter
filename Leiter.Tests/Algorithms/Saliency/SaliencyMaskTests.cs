namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyMask"/>.
/// </summary>
public class SaliencyMaskTests
{
    [Fact]
    public void ComputeThreshold_And_Binarize_AcrossMethodsAndRetentionPolicies_ShouldWork()
    {
        SequentialMatrix<DoublePixel> map = TestMatrices.Create<DoublePixel>(
            10,
            10,
            (x, y) =>
                x >= 1 && x <= 4 && y >= 1 && y <= 4 ? 0.85 :
                x >= 7 && x <= 8 && y >= 7 && y <= 8 ? 0.80 :
                0.0);

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
    public void Binarize_ConnectivityOption_ShouldControlDiagonalComponentMerging()
    {
        // Two 2x2 blocks touching only diagonally at (2, 2)-(3, 3): 4 pixels (4%) each, 8 pixels (8%) combined
        SequentialMatrix<DoublePixel> map = TestMatrices.Create<DoublePixel>(
            10,
            10,
            (x, y) =>
                (x >= 1 && x <= 2 && y >= 1 && y <= 2) || (x >= 3 && x <= 4 && y >= 3 && y <= 4)
                    ? 0.9
                    : 0.0);

        MaskMatrix eightConnected = SaliencyMask.Binarize(
            map,
            new SaliencyMaskOptions(
                Method: SaliencyThresholdMethod.Fixed,
                FixedThreshold: 0.5,
                RetentionPolicy: ComponentRetentionPolicy.MinimumAreaFraction,
                MinAreaFraction: 0.06,
                Connectivity: GridConnectivity.EightConnected));

        Assert.True(eightConnected[1, 1]);
        Assert.True(eightConnected[4, 4]);

        MaskMatrix fourConnected = SaliencyMask.Binarize(
            map,
            new SaliencyMaskOptions(
                Method: SaliencyThresholdMethod.Fixed,
                FixedThreshold: 0.5,
                RetentionPolicy: ComponentRetentionPolicy.MinimumAreaFraction,
                MinAreaFraction: 0.06,
                Connectivity: GridConnectivity.FourConnected));

        MatrixAssert.Equal(TestMatrices.Constant(10, 10, false), fourConnected);

        // Under LargestComponentOnly, an asymmetric diagonal bridge preserves both blocks for 8-connected but only the larger for 4-connected
        map[5, 3] = 0.9;
        map[5, 4] = 0.9;
        MaskMatrix largestFourConnected = SaliencyMask.Binarize(
            map,
            new SaliencyMaskOptions(
                Method: SaliencyThresholdMethod.Fixed,
                FixedThreshold: 0.5,
                RetentionPolicy: ComponentRetentionPolicy.LargestComponentOnly,
                Connectivity: GridConnectivity.FourConnected));

        Assert.False(largestFourConnected[1, 1]);
        Assert.True(largestFourConnected[4, 4]);
        Assert.True(largestFourConnected[5, 3]);
    }

    [Fact]
    public void ComputeThreshold_OtsuLargeSymmetricMultilevel_ShouldPreserveExactTieAcrossSplitModes()
    {
        // 300x300 = 90,000 pixels: 40% at bin 50, 20% at bin 125, 40% at bin 200.
        // Both split boundaries (bin 50..124 and bin 125..199) share identical theoretical between-class variance.
        const int width = 300;
        const int height = 300;
        SequentialMatrix<DoublePixel> map = TestMatrices.Create<DoublePixel>(
            width,
            height,
            (_, y) =>
                y < 120 ? 50.0 / 255.0 :
                y < 180 ? 125.0 / 255.0 :
                200.0 / 255.0);

        double threshold = SaliencyMask.ComputeThreshold(map, SaliencyThresholdMethod.Otsu);
        Assert.Equal(125.0 / 255.0, threshold, 9);
    }

    [Fact]
    public void UniformAndEdgeInputs_ShouldBeHandledCleanly()
    {
        var allZero = new SequentialMatrix<DoublePixel>(4, 4);
        Assert.Equal(1.0, SaliencyMask.ComputeThreshold(allZero, SaliencyThresholdMethod.Otsu), 6);
        MaskMatrix zeroMask = SaliencyMask.Binarize(allZero, new SaliencyMaskOptions(Method: SaliencyThresholdMethod.Otsu));
        MatrixAssert.Equal(TestMatrices.Constant(4, 4, false), zeroMask);

        SequentialMatrix<DoublePixel> allHigh = TestMatrices.Constant<DoublePixel>(4, 4, 0.8);
        MaskMatrix highMask = SaliencyMask.Binarize(allHigh, new SaliencyMaskOptions(Method: SaliencyThresholdMethod.Otsu));
        MatrixAssert.Equal(TestMatrices.Constant(4, 4, true), highMask);

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
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.Binarize(allZero, new SaliencyMaskOptions(Connectivity: (GridConnectivity)99)));
    }

    [Fact]
    public void NonSequentialMatrixView_ShouldMatchMaterializedThresholdsAndMasks()
    {
        SequentialMatrix<DoublePixel> padded = TestMatrices.Create<DoublePixel>(
            12,
            12,
            (x, y) =>
                x >= 3 && x <= 6 && y >= 3 && y <= 6 ? 0.85 :
                x >= 8 && x <= 9 && y >= 8 && y <= 9 ? 0.75 :
                0.0);

        var view = new MatrixView<DoublePixel>(padded, 2, 2, 8, 8, EdgeHandling.Extend);
        SequentialMatrix<DoublePixel> materialized = view.ToSequentialMatrix();

        foreach (SaliencyThresholdMethod method in new[]
        {
            SaliencyThresholdMethod.Otsu,
            SaliencyThresholdMethod.AdaptiveMean,
            SaliencyThresholdMethod.Fixed
        })
        {
            Assert.Equal(
                SaliencyMask.ComputeThreshold(materialized, method, 0.5),
                SaliencyMask.ComputeThreshold(view, method, 0.5),
                12);

            var options = new SaliencyMaskOptions(Method: method, FixedThreshold: 0.5);
            MatrixAssert.Equal(SaliencyMask.Binarize(materialized, options), SaliencyMask.Binarize(view, options));
        }
    }

    [Fact]
    public void ComputeThreshold_LargeGrid_AdaptiveMeanAndOtsu_ShouldMatchExpected()
    {
        const int width = 280;
        const int height = 260;

        SequentialMatrix<DoublePixel> map = TestMatrices.Create<DoublePixel>(
            width,
            height,
            static (x, y) => (x < 140 && y < 130) ? 1.0 : 0.0);

        double adaptive = SaliencyMask.ComputeThreshold(map, SaliencyThresholdMethod.AdaptiveMean);
        double otsu = SaliencyMask.ComputeThreshold(map, SaliencyThresholdMethod.Otsu);

        // 25% of pixels are 1.0 and 75% are 0.0: mean = 0.25 -> AdaptiveMean = 2 * 0.25 = 0.50;
        // Otsu maximizes between-class variance equally across all bins in [0, 254], giving (127.0 + 0.5) / 255.0 = 0.50.
        Assert.Equal(0.50, adaptive, 12);
        Assert.Equal(0.50, otsu, 12);
    }
}
