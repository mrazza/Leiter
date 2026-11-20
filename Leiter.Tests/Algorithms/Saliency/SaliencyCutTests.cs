namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyCut"/>.
/// </summary>
public class SaliencyCutTests
{
    [Fact]
    public void Extract_PartialSaliencySeed_ShouldExpandAndExtractObjects()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.CenteredBox(
            32,
            32,
            10,
            10,
            21,
            21,
            new Rgb8(220, 35, 30),
            new Rgb8(20, 45, 195));
        SequentialMatrix<DoublePixel> saliency = TestMatrices.CenteredBox<DoublePixel>(
            32,
            32,
            12,
            12,
            19,
            16,
            0.90,
            0.05);

        MaskMatrix mask = SaliencyCut.Extract(image, saliency, new SaliencyCutOptions(TrimapBandRadius: 6));

        Assert.True(mask[15, 19]);
        Assert.True(mask[10, 21]);
        Assert.True(mask[11, 11]);
        Assert.False(mask[9, 15]);
        Assert.False(mask[15, 22]);
        Assert.False(mask[2, 2]);
        Assert.False(mask[29, 29]);
    }

    [Fact]
    public void Extract_MaxWorkingDimension_ShouldDownsampleAndUpsampleToMatchImageSize()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.CenteredBox(
            32,
            32,
            10,
            10,
            21,
            21,
            new Rgb8(220, 35, 30),
            new Rgb8(20, 45, 195));
        SequentialMatrix<DoublePixel> saliency = TestMatrices.CenteredBox<DoublePixel>(
            32,
            32,
            12,
            12,
            19,
            16,
            0.90,
            0.05);

        MaskMatrix downsampledMask = SaliencyCut.Extract(
            image,
            saliency,
            new SaliencyCutOptions(MaxWorkingDimension: 16, TrimapBandRadius: 6));

        Assert.Equal(image.Size, downsampledMask.Size);
        Assert.True(downsampledMask[15, 14]);
        Assert.False(downsampledMask[1, 1]);

        // Also exercise non-SequentialMatrix (MatrixView) inputs with MaxWorkingDimension
        var imageView = new MatrixView<Rgb8>(image, 0, 0, 32, 32, EdgeHandling.Extend);
        var saliencyView = new MatrixView<DoublePixel>(saliency, 0, 0, 32, 32, EdgeHandling.Extend);
        MaskMatrix viewDownsampledMask = SaliencyCut.Extract(
            imageView,
            saliencyView,
            new SaliencyCutOptions(MaxWorkingDimension: 16, TrimapBandRadius: 6));
        MatrixAssert.Equal(downsampledMask, viewDownsampledMask);
    }

    [Fact]
    public void Extract_MaxWorkingDimension_ShouldRescaleGrabCutSmoothnessWeight()
    {
        // 64x64 image scaled to MaxWorkingDimension=16 (scale = 0.25) with realistic multi-tone variance
        // and a narrow 4px (1px working-res) protruding arm where unscaled SmoothnessWeight over-smooths
        // and trims the arm, while rescaled SmoothnessWeight matches direct working-resolution execution.
        static bool IsForeground(int x, int y) =>
            (x >= 12 && x <= 31 && y >= 16 && y <= 47) || (x >= 32 && x <= 55 && y >= 28 && y <= 31);

        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            64,
            64,
            (x, y) =>
            {
                int tone = ((x / 4) + (y / 4)) % 3;
                return IsForeground(x, y)
                    ? new Rgb8((byte)(165 + (tone * 20)), (byte)(75 + (tone * 10)), 85)
                    : new Rgb8(85, (byte)(95 + (tone * 10)), (byte)(135 + (tone * 20)));
            });
        SequentialMatrix<DoublePixel> saliency = TestMatrices.CenteredBox<DoublePixel>(
            64,
            64,
            12,
            16,
            31,
            47,
            0.90,
            0.05);

        var scaledOptions = new SaliencyCutOptions(
            MaxOuterIterations: 3,
            TrimapBandRadius: 8,
            GrabCut: new GrabCutOptions(Iterations: 2, MixtureComponentCount: 2, SmoothnessWeight: 40.0),
            MaxWorkingDimension: 16);

        MaskMatrix actual = SaliencyCut.Extract(image, saliency, scaledOptions);

        Matrix<Rgb8> smallImage = image.ResizeBox(new Size(16, 16));
        Matrix<DoublePixel> smallSaliency = saliency.ResizeBox(new Size(16, 16));
        MaskMatrix expectedRescaled = SaliencyCut.Extract(
            smallImage,
            smallSaliency,
            new SaliencyCutOptions(
                MaxOuterIterations: 3,
                TrimapBandRadius: 2,
                GrabCut: new GrabCutOptions(Iterations: 2, MixtureComponentCount: 2, SmoothnessWeight: 10.0)))
            .ResizeMaskNearest(image.Size);

        MatrixAssert.Equal(expectedRescaled, actual);

        MaskMatrix unrescaledSmoothness = SaliencyCut.Extract(
            smallImage,
            smallSaliency,
            new SaliencyCutOptions(
                MaxOuterIterations: 3,
                TrimapBandRadius: 2,
                GrabCut: new GrabCutOptions(Iterations: 2, MixtureComponentCount: 2, SmoothnessWeight: 40.0)))
            .ResizeMaskNearest(image.Size);

        MatrixAssert.NotEqual(unrescaledSmoothness, actual);
    }

    [Fact]
    public void Extract_OverspillSaliency_ShouldRefineMaskToActualObject()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.CenteredBox(
            32,
            32,
            10,
            10,
            21,
            21,
            new Rgb8(220, 35, 30),
            new Rgb8(20, 45, 195));
        SequentialMatrix<DoublePixel> overspillSaliency = TestMatrices.CenteredBox<DoublePixel>(
            32,
            32,
            8,
            8,
            23,
            18,
            0.90,
            0.05);

        MaskMatrix refinedMask = SaliencyCut.Extract(image, overspillSaliency, new SaliencyCutOptions(TrimapBandRadius: 6));
        Assert.False(refinedMask[8, 14]);
        Assert.True(refinedMask[15, 20]);
    }

    [Fact]
    public void Extract_LargeTrimapBandRadius_ShouldScaleErosionAndRefineOverspill()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.CenteredBox(
            96,
            96,
            24,
            24,
            71,
            71,
            new Rgb8(220, 40, 35),
            new Rgb8(25, 55, 195));
        SequentialMatrix<DoublePixel> overspillSaliency = TestMatrices.CenteredBox<DoublePixel>(
            96,
            96,
            20,
            20,
            75,
            60,
            0.90,
            0.05);

        MaskMatrix mask = SaliencyCut.Extract(
            image,
            overspillSaliency,
            new SaliencyCutOptions(MaxOuterIterations: 4, TrimapBandRadius: 20));

        Assert.True(mask[48, 66]);
        Assert.False(mask[22, 40]);
        Assert.False(mask[4, 4]);
    }

    [Fact]
    public void Extract_MaskConnectivity_ShouldControlDiagonalComponentFiltering()
    {
        // Two diagonally touching blocks: 4x4 (16px, 16%) and 2x2 (4px, 4%) touching at (4,4)-(5,5) on 10x10 (100px)
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            10,
            10,
            (x, y) =>
                (x >= 1 && x <= 4 && y >= 1 && y <= 4) || (x >= 5 && x <= 6 && y >= 5 && y <= 6)
                    ? new Rgb8(220, 30, 30)
                    : new Rgb8(20, 45, 195));
        SequentialMatrix<DoublePixel> saliency = TestMatrices.Create<DoublePixel>(
            10,
            10,
            (x, y) =>
                (x >= 1 && x <= 4 && y >= 1 && y <= 4) || (x >= 5 && x <= 6 && y >= 5 && y <= 6)
                    ? 0.90
                    : 0.05);

        MaskMatrix eightConnected = SaliencyCut.Extract(
            image,
            saliency,
            new SaliencyCutOptions(
                MaxOuterIterations: 1,
                TrimapBandRadius: 1,
                Mask: new SaliencyMaskOptions(
                    Method: SaliencyThresholdMethod.Fixed,
                    RetentionPolicy: ComponentRetentionPolicy.LargestComponentOnly,
                    Connectivity: GridConnectivity.EightConnected)));

        MaskMatrix fourConnected = SaliencyCut.Extract(
            image,
            saliency,
            new SaliencyCutOptions(
                MaxOuterIterations: 1,
                TrimapBandRadius: 1,
                Mask: new SaliencyMaskOptions(
                    Method: SaliencyThresholdMethod.Fixed,
                    RetentionPolicy: ComponentRetentionPolicy.LargestComponentOnly,
                    Connectivity: GridConnectivity.FourConnected)));

        Assert.True(eightConnected[6, 6]);
        Assert.False(fourConnected[6, 6]);
        Assert.True(fourConnected[2, 2]);
    }

    [Fact]
    public void Extract_FallbackEdgeCases_ShouldHandleVaryingIterations()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.CenteredBox(
            6,
            6,
            2,
            2,
            3,
            3,
            new Rgb8(240, 20, 20),
            new Rgb8(10, 10, 180));
        SequentialMatrix<DoublePixel> lowContrastSaliency = TestMatrices.CenteredBox<DoublePixel>(
            6,
            6,
            2,
            2,
            3,
            3,
            0.20,
            0.02);

        MaskMatrix otsuFallbackMask = SaliencyCut.Extract(
            image,
            lowContrastSaliency,
            new SaliencyCutOptions(MaxOuterIterations: 2, TrimapBandRadius: 1));
        Assert.True(otsuFallbackMask[2, 2]);
        Assert.False(otsuFallbackMask[0, 0]);

        MaskMatrix hugeRadiusMask = SaliencyCut.Extract(
            image,
            lowContrastSaliency,
            new SaliencyCutOptions(MaxOuterIterations: 3, TrimapBandRadius: 10));
        Assert.True(hugeRadiusMask[2, 2]);

        MaskMatrix singleOuter = SaliencyCut.Extract(
            image,
            lowContrastSaliency,
            new SaliencyCutOptions(MaxOuterIterations: 1, TrimapBandRadius: 1));
        Assert.True(singleOuter[2, 2]);

        var allZero = new SequentialMatrix<DoublePixel>(6, 6);
        MaskMatrix zeroResult = SaliencyCut.Extract(image, allZero);
        MatrixAssert.Equal(TestMatrices.Constant(6, 6, false), zeroResult);

        Assert.Empty(SaliencyCut.Extract(new SequentialMatrix<Rgb8>(0, 0), new SequentialMatrix<DoublePixel>(0, 0)));
    }

    [Fact]
    public void Extract_Validation_ShouldThrowExpectedExceptions()
    {
        var image = new SequentialMatrix<Rgb8>(6, 6);
        var allZero = new SequentialMatrix<DoublePixel>(6, 6);

        Assert.Throws<ArgumentNullException>("image", () => SaliencyCut.Extract(null!, allZero));
        Assert.Throws<ArgumentNullException>("saliencyMap", () => SaliencyCut.Extract(image, null!));
        Assert.Throws<ArgumentException>("saliencyMap", () => SaliencyCut.Extract(image, new SequentialMatrix<DoublePixel>(2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(Mask: new SaliencyMaskOptions(FixedThreshold: -0.1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(Mask: new SaliencyMaskOptions(Connectivity: (GridConnectivity)99))));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(MaxOuterIterations: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(GrabCut: new GrabCutOptions(Iterations: 0))));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(TrimapBandRadius: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(GrabCut: new GrabCutOptions(MixtureComponentCount: 0))));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(GrabCut: new GrabCutOptions(SmoothnessWeight: -1.0))));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(Mask: new SaliencyMaskOptions(RetentionPolicy: (ComponentRetentionPolicy)99))));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(Mask: new SaliencyMaskOptions(MinAreaFraction: 1.5))));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(MaxWorkingDimension: 1)));
    }
}
