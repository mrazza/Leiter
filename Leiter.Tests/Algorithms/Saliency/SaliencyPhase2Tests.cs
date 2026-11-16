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
        // Large primary salient block (16 pixels >= 1% of 100) + medium secondary block (4 pixels) + 1 speck
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

        // MinAreaFraction = 0.02 (minPixels = 2): retains 16-pixel and 4-pixel blocks, drops 1-pixel speck.
        Matrix<LongPixel> multiObjectMask = SaliencyMask.Binarize(
            map,
            SaliencyThresholdMethod.Otsu,
            retentionPolicy: ComponentRetentionPolicy.MinimumAreaFraction,
            minAreaFraction: 0.02);
        Assert.Equal(1L, multiObjectMask[2, 2].Value);
        Assert.Equal(1L, multiObjectMask[7, 7].Value);
        Assert.Equal(0L, multiObjectMask[0, 9].Value);
        Assert.Equal(0L, multiObjectMask[9, 0].Value);

        // LargestComponentOnly retains only the 16-pixel primary block.
        Matrix<LongPixel> largestOnly = SaliencyMask.Binarize(
            map,
            SaliencyThresholdMethod.Fixed,
            fixedThreshold: 0.5,
            retentionPolicy: ComponentRetentionPolicy.LargestComponentOnly);
        Assert.Equal(1L, largestOnly[2, 2].Value);
        Assert.Equal(0L, largestOnly[7, 7].Value);
        Assert.Equal(0L, largestOnly[0, 9].Value);

        // KeepAll keeps even the isolated 1-pixel speck.
        Matrix<LongPixel> keepAll = SaliencyMask.Binarize(
            map,
            SaliencyThresholdMethod.Fixed,
            fixedThreshold: 0.5,
            retentionPolicy: ComponentRetentionPolicy.KeepAll);
        Assert.Equal(1L, keepAll[0, 9].Value);
        Assert.Equal(1L, keepAll[7, 7].Value);
    }

    [Fact]
    public void SaliencyMask_UniformAndEdgeInputs_ShouldBeHandledCleanly()
    {
        var allZero = new SequentialMatrix<DoublePixel>(4, 4);
        Assert.Equal(1.0, SaliencyMask.ComputeThreshold(allZero, SaliencyThresholdMethod.Otsu), 6);
        Matrix<LongPixel> zeroMask = SaliencyMask.Binarize(allZero, SaliencyThresholdMethod.Otsu);
        for (int i = 0; i < zeroMask.Count; i++)
            Assert.Equal(0L, zeroMask[i].Value);

        var allHigh = new SequentialMatrix<DoublePixel>(4, 4);
        allHigh.SetAll(0.8);
        Matrix<LongPixel> highMask = SaliencyMask.Binarize(allHigh, SaliencyThresholdMethod.Otsu);
        for (int i = 0; i < highMask.Count; i++)
            Assert.Equal(1L, highMask[i].Value);

        var emptyDouble = new SequentialMatrix<DoublePixel>(0, 0);
        Assert.Equal(0.0, SaliencyMask.ComputeThreshold(emptyDouble));
        Assert.Empty(SaliencyMask.Binarize(emptyDouble));
        Assert.Empty(SaliencyMask.FilterConnectedComponents(new SequentialMatrix<LongPixel>(0, 0)));

        var emptyLongMask = new SequentialMatrix<LongPixel>(3, 3);
        Matrix<LongPixel> filteredEmpty = SaliencyMask.FilterConnectedComponents(emptyLongMask);
        Assert.Equal(0L, filteredEmpty[0, 0].Value);

        Assert.Throws<ArgumentNullException>(() => SaliencyMask.ComputeThreshold(null!));
        Assert.Throws<ArgumentNullException>(() => SaliencyMask.FilterConnectedComponents(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.ComputeThreshold(allZero, (SaliencyThresholdMethod)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.ComputeThreshold(allZero, SaliencyThresholdMethod.Fixed, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.ComputeThreshold(allZero, SaliencyThresholdMethod.Fixed, 1.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.FilterConnectedComponents(emptyLongMask, (ComponentRetentionPolicy)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyMask.FilterConnectedComponents(emptyLongMask, minAreaFraction: -0.05));
    }

    [Fact]
    public void SaliencyCut_PartialSaliencySeed_ShouldExpandAndExtractObjects()
    {
        // 32x32 scene: distinct warm crimson foreground square [10..21]x[10..21] on deep cobalt background.
        // Saliency map only marks a partial core [12..19]x[12..16] loudly, verifying iterative expansion recovers the full square.
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

        Matrix<LongPixel> mask = SaliencyCut.Extract(image, saliency, new SaliencyCutOptions(TrimapBandRadius: 6));

        Assert.Equal(1L, mask[15, 19].Value); // Recovered lower portion outside initial seed
        Assert.Equal(1L, mask[10, 21].Value);
        Assert.Equal(1L, mask[11, 11].Value);
        Assert.Equal(0L, mask[9, 15].Value);
        Assert.Equal(0L, mask[15, 22].Value);
        Assert.Equal(0L, mask[2, 2].Value);
        Assert.Equal(0L, mask[29, 29].Value);

        // Also verify optional MaxWorkingDimension downsampling/upsampling pipeline.
        Matrix<LongPixel> downsampledMask = SaliencyCut.Extract(
            image,
            saliency,
            new SaliencyCutOptions(MaxWorkingDimension: 16, TrimapBandRadius: 6));
        Assert.Equal(image.Size, downsampledMask.Size);
        Assert.Equal(1L, downsampledMask[15, 14].Value);
        Assert.Equal(0L, downsampledMask[1, 1].Value);

        // Over-spilling + partial seed [8..23]x[8..18]: trims background leaks and grows to y=20.
        var overspillSaliency = new SequentialMatrix<DoublePixel>(32, 32);
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
                overspillSaliency[x, y] = (x >= 8 && x <= 23 && y >= 8 && y <= 18) ? 0.90 : 0.05;
        }
        Matrix<LongPixel> refinedMask = SaliencyCut.Extract(image, overspillSaliency, new SaliencyCutOptions(TrimapBandRadius: 6));
        Assert.Equal(0L, refinedMask[8, 14].Value);
        Assert.Equal(1L, refinedMask[15, 20].Value);
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

        // Low saliency below default InitialThreshold (70/255) triggers Otsu fallback!
        var lowContrastSaliency = new SequentialMatrix<DoublePixel>(6, 6);
        for (int y = 0; y < 6; y++)
        {
            for (int x = 0; x < 6; x++)
                lowContrastSaliency[x, y] = (x >= 2 && x <= 3 && y >= 2 && y <= 3) ? 0.20 : 0.02;
        }
        Matrix<LongPixel> otsuFallbackMask = SaliencyCut.Extract(
            image,
            lowContrastSaliency,
            new SaliencyCutOptions(MaxOuterIterations: 2, TrimapBandRadius: 1));
        Assert.Equal(1L, otsuFallbackMask[2, 2].Value);
        Assert.Equal(0L, otsuFallbackMask[0, 0].Value);

        // Tiny image where dilation fills 100% triggers early return of currentMask after iter 0.
        Matrix<LongPixel> hugeRadiusMask = SaliencyCut.Extract(
            image,
            lowContrastSaliency,
            new SaliencyCutOptions(MaxOuterIterations: 3, TrimapBandRadius: 10));
        Assert.Equal(1L, hugeRadiusMask[2, 2].Value);

        // MaxOuterIterations == 1 runs once and exits cleanly without building second trimap.
        Matrix<LongPixel> singleOuter = SaliencyCut.Extract(
            image,
            lowContrastSaliency,
            new SaliencyCutOptions(MaxOuterIterations: 1, TrimapBandRadius: 1));
        Assert.Equal(1L, singleOuter[2, 2].Value);

        // All-zero saliency cannot split even with Otsu and returns all-zero mask.
        var allZero = new SequentialMatrix<DoublePixel>(6, 6);
        Matrix<LongPixel> zeroResult = SaliencyCut.Extract(image, allZero);
        for (int i = 0; i < zeroResult.Count; i++)
            Assert.Equal(0L, zeroResult[i].Value);

        // Empty 0x0 inputs.
        Assert.Empty(SaliencyCut.Extract(new SequentialMatrix<Rgb8>(0, 0), new SequentialMatrix<DoublePixel>(0, 0)));

        // Validation checks.
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
        // Centered square mask [10..20] x [10..20] in a 31x31 grid (indices 0..30).
        // For outside points directly along horizontal/vertical rays from boundary pixels:
        // e.g. x=(25, 15) has nearest boundary o=(20, 15), border y=(30, 15) -> dx=5, dy=10 -> d = 0.5!
        var mask = new SequentialMatrix<LongPixel>(31, 31);
        for (int y = 10; y <= 20; y++)
        {
            for (int x = 10; x <= 20; x++)
                mask[x, y] = 1L;
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
        Assert.Equal(0.5, rawField[25, 25].Value, 9); // nearest seed (20,20), ray hits (30,30)

        // Smoothing with explicit sigma=0 matches normalized field exactly.
        Matrix<DoublePixel> unblurred = SaliencyDistanceField.Compute(mask, smoothingSigma: 0.0);
        Assert.Equal(0.5, unblurred[25, 15].Value, 9);

        // Smoothed field softens boundaries continuously into [0, 1].
        Matrix<DoublePixel> smoothed = SaliencyDistanceField.Compute(mask);
        Assert.InRange(smoothed[10, 15].Value, 0.0, 0.4);
        Assert.True(smoothed[30, 15].Value > smoothed[15, 15].Value);
        for (int i = 0; i < smoothed.Count; i++)
            Assert.InRange(smoothed[i].Value, 0.0, 1.0);
    }

    [Fact]
    public void SaliencyDistanceField_EdgeCasesAndEndToEndOverload_ShouldWork()
    {
        var empty = new SequentialMatrix<LongPixel>(0, 0);
        Assert.Empty(SaliencyDistanceField.ComputeNormalizedField(empty));
        Assert.Empty(SaliencyDistanceField.Compute(empty));

        var allBg = new SequentialMatrix<LongPixel>(4, 4);
        Matrix<DoublePixel> allBgField = SaliencyDistanceField.ComputeNormalizedField(allBg);
        for (int i = 0; i < allBgField.Count; i++)
            Assert.Equal(1.0, allBgField[i].Value, 9);

        var allFg = new SequentialMatrix<LongPixel>(4, 4);
        allFg.SetAll(1L);
        Matrix<DoublePixel> allFgField = SaliencyDistanceField.ComputeNormalizedField(allFg);
        for (int i = 0; i < allFgField.Count; i++)
            Assert.Equal(0.0, allFgField[i].Value, 9);

        // End-to-end (SalientMask, DistanceField) helper.
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

        (Matrix<LongPixel> mask, Matrix<DoublePixel> field) = SaliencyDistanceField.Compute(image, saliency);
        Assert.Equal(1L, mask[10, 10].Value);
        Assert.Equal(0L, mask[0, 0].Value);
        Assert.True(field[0, 0].Value > field[10, 10].Value);

        Assert.Throws<ArgumentNullException>(() => SaliencyDistanceField.ComputeNormalizedField(null!));
        Assert.Throws<ArgumentNullException>(() => SaliencyDistanceField.Compute(null!, smoothingSigma: 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyDistanceField.Compute(allBg, defaultSigmaScale: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyDistanceField.Compute(allBg, smoothingSigma: -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SaliencyDistanceField.Compute(allBg, smoothingSigma: double.NaN));
    }
}
