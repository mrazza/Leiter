namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyCut"/>.
/// </summary>
public class SaliencyCutTests
{
    [Fact]
    public void Extract_PartialSaliencySeed_ShouldExpandAndExtractObjects()
    {
        var image = new SequentialMatrix<Rgb8>(32, 32);
        var saliency = new SequentialMatrix<DoublePixel>(32, 32);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                bool isObject = x >= 10 && x <= 21 && y >= 10 && y <= 21;
                image[x, y] = isObject ? new Rgb8(220, 35, 30) : new Rgb8(20, 45, 195);

                bool isSeed = x >= 12 && x <= 19 && y >= 12 && y <= 16;
                saliency[x, y] = isSeed ? 0.90 : 0.05;
            }
        }

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
        var image = new SequentialMatrix<Rgb8>(32, 32);
        var saliency = new SequentialMatrix<DoublePixel>(32, 32);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                bool isObject = x >= 10 && x <= 21 && y >= 10 && y <= 21;
                image[x, y] = isObject ? new Rgb8(220, 35, 30) : new Rgb8(20, 45, 195);

                bool isSeed = x >= 12 && x <= 19 && y >= 12 && y <= 16;
                saliency[x, y] = isSeed ? 0.90 : 0.05;
            }
        }

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
        Assert.Equal(downsampledMask.Size, viewDownsampledMask.Size);
        Assert.True(downsampledMask.ContentEquals(viewDownsampledMask));
    }

    [Fact]
    public void Extract_OverspillSaliency_ShouldRefineMaskToActualObject()
    {
        var image = new SequentialMatrix<Rgb8>(32, 32);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                bool isObject = x >= 10 && x <= 21 && y >= 10 && y <= 21;
                image[x, y] = isObject ? new Rgb8(220, 35, 30) : new Rgb8(20, 45, 195);
            }
        }

        var overspillSaliency = new SequentialMatrix<DoublePixel>(32, 32);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                overspillSaliency[x, y] = (x >= 8 && x <= 23 && y >= 8 && y <= 18) ? 0.90 : 0.05;
            }
        }

        MaskMatrix refinedMask = SaliencyCut.Extract(image, overspillSaliency, new SaliencyCutOptions(TrimapBandRadius: 6));
        Assert.False(refinedMask[8, 14]);
        Assert.True(refinedMask[15, 20]);
    }

    [Fact]
    public void Extract_LargeTrimapBandRadius_ShouldScaleErosionAndRefineOverspill()
    {
        var image = new SequentialMatrix<Rgb8>(96, 96);
        var overspillSaliency = new SequentialMatrix<DoublePixel>(96, 96);

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                bool isObject = x >= 24 && x <= 71 && y >= 24 && y <= 71;
                image[x, y] = isObject ? new Rgb8(220, 40, 35) : new Rgb8(25, 55, 195);

                bool isOverspillSeed = x >= 20 && x <= 75 && y >= 20 && y <= 60;
                overspillSaliency[x, y] = isOverspillSeed ? 0.90 : 0.05;
            }
        }

        MaskMatrix mask = SaliencyCut.Extract(
            image,
            overspillSaliency,
            new SaliencyCutOptions(MaxOuterIterations: 4, TrimapBandRadius: 20));

        Assert.True(mask[48, 66]);
        Assert.False(mask[22, 40]);
        Assert.False(mask[4, 4]);
    }

    [Fact]
    public void Extract_FallbackEdgeCases_ShouldHandleVaryingIterations()
    {
        var image = new SequentialMatrix<Rgb8>(6, 6);
        for (int y = 0; y < 6; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                image[x, y] = (x >= 2 && x <= 3 && y >= 2 && y <= 3) ? new Rgb8(240, 20, 20) : new Rgb8(10, 10, 180);
            }
        }

        var lowContrastSaliency = new SequentialMatrix<DoublePixel>(6, 6);
        for (int y = 0; y < 6; y++)
        {
            for (int x = 0; x < 6; x++)
            {
                lowContrastSaliency[x, y] = (x >= 2 && x <= 3 && y >= 2 && y <= 3) ? 0.20 : 0.02;
            }
        }

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
        for (int i = 0; i < zeroResult.Count; i++)
        {
            Assert.False(zeroResult[i]);
        }

        Assert.Empty(SaliencyCut.Extract(new SequentialMatrix<Rgb8>(0, 0), new SequentialMatrix<DoublePixel>(0, 0)));
    }

    [Fact]
    public void Extract_Validation_ShouldThrowExpectedExceptions()
    {
        var image = new SequentialMatrix<Rgb8>(6, 6);
        var allZero = new SequentialMatrix<DoublePixel>(6, 6);

        Assert.Throws<ArgumentNullException>(() => SaliencyCut.Extract(null!, allZero));
        Assert.Throws<ArgumentNullException>(() => SaliencyCut.Extract(image, null!));
        Assert.Throws<ArgumentException>(() => SaliencyCut.Extract(image, new SequentialMatrix<DoublePixel>(2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(Mask: new SaliencyMaskOptions(FixedThreshold: -0.1))));
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
