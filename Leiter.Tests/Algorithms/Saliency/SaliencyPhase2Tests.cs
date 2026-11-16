namespace Leiter.Tests.Algorithms.Saliency;

using Leiter.Algorithms.Saliency;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for Phase 2 saliency algorithms: <see cref="SaliencyMask"/>,
/// <see cref="SaliencyCut"/>, and <see cref="SaliencyDistanceField"/>.
/// </summary>
public class SaliencyPhase2Tests
{
    [Fact]
    public void SaliencyMask_ThresholdsAndRetentionPolicies_ShouldWork()
    {
        var map = new SequentialMatrix<DoublePixel>(10, 10);
        for (int y = 1; y <= 4; y++)
        {
            for (int x = 1; x <= 4; x++)
                map[x, y] = 0.85;
        }
        for (int y = 7; y <= 8; y++)
        {
            for (int x = 7; x <= 8; x++)
                map[x, y] = 0.80;
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
            SaliencyThresholdMethod.Otsu,
            retentionPolicy: ComponentRetentionPolicy.MinimumAreaFraction,
            minAreaFraction: 0.02);
        Assert.True(multiObjectMask[2, 2]);
        Assert.True(multiObjectMask[7, 7]);
        Assert.False(multiObjectMask[0, 9]);
        Assert.False(multiObjectMask[9, 0]);

        MaskMatrix largestOnly = SaliencyMask.Binarize(
            map,
            SaliencyThresholdMethod.Fixed,
            fixedThreshold: 0.5,
            retentionPolicy: ComponentRetentionPolicy.LargestComponentOnly);
        Assert.True(largestOnly[2, 2]);
        Assert.False(largestOnly[7, 7]);
        Assert.False(largestOnly[0, 9]);

        MaskMatrix keepAll = SaliencyMask.Binarize(
            map,
            SaliencyThresholdMethod.Fixed,
            fixedThreshold: 0.5,
            retentionPolicy: ComponentRetentionPolicy.KeepAll);
        Assert.True(keepAll[0, 9]);
        Assert.True(keepAll[7, 7]);
    }

    [Fact]
    public void SaliencyMask_UniformAndEdgeInputs_ShouldBeHandledCleanly()
    {
        var allZero = new SequentialMatrix<DoublePixel>(4, 4);
        Assert.Equal(1.0, SaliencyMask.ComputeThreshold(allZero, SaliencyThresholdMethod.Otsu), 6);
        MaskMatrix zeroMask = SaliencyMask.Binarize(allZero, SaliencyThresholdMethod.Otsu);
        for (int i = 0; i < zeroMask.Count; i++)
            Assert.False(zeroMask[i]);

        var allHigh = new SequentialMatrix<DoublePixel>(4, 4);
        allHigh.SetAll(0.8);
        MaskMatrix highMask = SaliencyMask.Binarize(allHigh, SaliencyThresholdMethod.Otsu);
        for (int i = 0; i < highMask.Count; i++)
            Assert.True(highMask[i]);

        var emptyDouble = new SequentialMatrix<DoublePixel>(0, 0);
        Assert.Equal(0.0, SaliencyMask.ComputeThreshold(emptyDouble));
        Assert.Empty(SaliencyMask.Binarize(emptyDouble));
        Assert.Empty(SaliencyMask.FilterConnectedComponents(new MaskMatrix(0, 0)));

        var emptyBoolMask = new MaskMatrix(3, 3);
        MaskMatrix filteredEmpty = SaliencyMask.FilterConnectedComponents(emptyBoolMask);
        Assert.False(filteredEmpty[0, 0]);

        Assert.Throws<ArgumentNullException>(() => SaliencyMask.ComputeThreshold(null!));
        Assert.Throws<ArgumentNullException>(() => SaliencyMask.FilterConnectedComponents(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.ComputeThreshold(allZero, (SaliencyThresholdMethod)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.ComputeThreshold(allZero, SaliencyThresholdMethod.Fixed, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.ComputeThreshold(allZero, SaliencyThresholdMethod.Fixed, 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.FilterConnectedComponents(emptyBoolMask, (ComponentRetentionPolicy)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.FilterConnectedComponents(emptyBoolMask, minAreaFraction: -0.05));
    }

    [Fact]
    public void SaliencyCut_PartialSaliencySeed_ShouldExpandAndExtractObjects()
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

        MaskMatrix downsampledMask = SaliencyCut.Extract(
            image,
            saliency,
            new SaliencyCutOptions(MaxWorkingDimension: 16, TrimapBandRadius: 6));
        Assert.Equal(image.Size, downsampledMask.Size);
        Assert.True(downsampledMask[15, 14]);
        Assert.False(downsampledMask[1, 1]);

        var overspillSaliency = new SequentialMatrix<DoublePixel>(32, 32);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
                overspillSaliency[x, y] = (x >= 8 && x <= 23 && y >= 8 && y <= 18) ? 0.90 : 0.05;
        }
        MaskMatrix refinedMask = SaliencyCut.Extract(image, overspillSaliency, new SaliencyCutOptions(TrimapBandRadius: 6));
        Assert.False(refinedMask[8, 14]);
        Assert.True(refinedMask[15, 20]);
    }

    [Fact]
    public void SaliencyCut_FallbackEdgeCasesAndValidation_ShouldBeHandledCleanly()
    {
        var image = new SequentialMatrix<Rgb8>(6, 6);
        for (int y = 0; y < 6; y++)
        {
            for (int x = 0; x < 6; x++)
                image[x, y] = (x >= 2 && x <= 3 && y >= 2 && y <= 3) ? new Rgb8(240, 20, 20) : new Rgb8(10, 10, 180);
        }

        var lowContrastSaliency = new SequentialMatrix<DoublePixel>(6, 6);
        for (int y = 0; y < 6; y++)
        {
            for (int x = 0; x < 6; x++)
                lowContrastSaliency[x, y] = (x >= 2 && x <= 3 && y >= 2 && y <= 3) ? 0.20 : 0.02;
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
            Assert.False(zeroResult[i]);

        Assert.Empty(SaliencyCut.Extract(new SequentialMatrix<Rgb8>(0, 0), new SequentialMatrix<DoublePixel>(0, 0)));

        Assert.Throws<ArgumentNullException>(() => SaliencyCut.Extract(null!, allZero));
        Assert.Throws<ArgumentNullException>(() => SaliencyCut.Extract(image, null!));
        Assert.Throws<ArgumentException>(() => SaliencyCut.Extract(image, new SequentialMatrix<DoublePixel>(2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(InitialThreshold: -0.1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(MaxOuterIterations: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(InnerGrabCutIterations: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(TrimapBandRadius: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(MixtureComponentCount: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(SmoothnessWeight: -1.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(RetentionPolicy: (ComponentRetentionPolicy)99)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(MinAreaFraction: 1.5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyCut.Extract(image, allZero, new SaliencyCutOptions(MaxWorkingDimension: 1)));
    }

    [Fact]
    public void SaliencyDistanceField_NormalizedRayIntersectionAndSmoothing_ShouldMatchGeometry()
    {
        var mask = new MaskMatrix(31, 31);
        for (int y = 10; y <= 20; y++)
        {
            for (int x = 10; x <= 20; x++)
                mask[x, y] = true;
        }

        Matrix<DoublePixel> rawField = SaliencyDistanceField.ComputeNormalizedField(mask);
        Assert.Equal(0.0, rawField[15, 15].Value, 9);
        Assert.Equal(0.0, rawField[10, 10].Value, 9);
        Assert.Equal(0.5, rawField[25, 15].Value, 9);
        Assert.Equal(0.5, rawField[5, 15].Value, 9);
        Assert.Equal(0.5, rawField[15, 25].Value, 9);
        Assert.Equal(0.5, rawField[15, 5].Value, 9);
        Assert.Equal(1.0, rawField[30, 15].Value, 9);
        Assert.Equal(1.0, rawField[0, 15].Value, 9);
        Assert.Equal(1.0, rawField[0, 0].Value, 9);
        Assert.Equal(0.5, rawField[25, 25].Value, 9);

        Matrix<DoublePixel> unblurred = SaliencyDistanceField.Compute(mask, smoothingSigma: 0.0);
        Assert.Equal(0.5, unblurred[25, 15].Value, 9);

        Matrix<DoublePixel> smoothed = SaliencyDistanceField.Compute(mask);
        Assert.InRange(smoothed[10, 15].Value, 0.0, 0.4);
        Assert.True(smoothed[30, 15].Value > smoothed[15, 15].Value);
        for (int i = 0; i < smoothed.Count; i++)
            Assert.InRange(smoothed[i].Value, 0.0, 1.0);
    }

    [Fact]
    public void SaliencyDistanceField_EdgeCasesAndEndToEndOverload_ShouldWork()
    {
        var empty = new MaskMatrix(0, 0);
        Assert.Empty(SaliencyDistanceField.ComputeNormalizedField(empty));
        Assert.Empty(SaliencyDistanceField.Compute(empty));

        var allBg = new MaskMatrix(4, 4);
        Matrix<DoublePixel> allBgField = SaliencyDistanceField.ComputeNormalizedField(allBg);
        for (int i = 0; i < allBgField.Count; i++)
            Assert.Equal(1.0, allBgField[i].Value, 9);

        var allFg = new MaskMatrix(4, 4);
        allFg.SetAll(true);
        Matrix<DoublePixel> allFgField = SaliencyDistanceField.ComputeNormalizedField(allFg);
        for (int i = 0; i < allFgField.Count; i++)
            Assert.Equal(0.0, allFgField[i].Value, 9);

        var image = new SequentialMatrix<Rgb8>(20, 20);
        var saliency = new SequentialMatrix<DoublePixel>(20, 20);
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                bool fg = x >= 6 && x <= 13 && y >= 6 && y <= 13;
                image[x, y] = fg ? new Rgb8(220, 30, 30) : new Rgb8(20, 180, 40);
                saliency[x, y] = fg ? 0.9 : 0.05;
            }
        }

        (MaskMatrix mask, Matrix<DoublePixel> field) = SaliencyDistanceField.Compute(image, saliency);
        Assert.True(mask[10, 10]);
        Assert.False(mask[0, 0]);
        Assert.True(field[0, 0].Value > field[10, 10].Value);

        Assert.Throws<ArgumentNullException>(() => SaliencyDistanceField.ComputeNormalizedField(null!));
        Assert.Throws<ArgumentNullException>(() => SaliencyDistanceField.Compute(null!, smoothingSigma: 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyDistanceField.Compute(allBg, defaultSigmaScale: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyDistanceField.Compute(allBg, smoothingSigma: -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyDistanceField.Compute(allBg, smoothingSigma: double.NaN));
    }
}
