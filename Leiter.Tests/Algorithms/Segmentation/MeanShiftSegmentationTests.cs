namespace Leiter.Tests.Algorithms.Segmentation;

using System;
using System.Linq;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MeanShiftSegmentation"/>.
/// </summary>
public class MeanShiftSegmentationTests
{
    [Fact]
    public void Filter_Lab32_PreservesSharpEdgesWhileSmoothingJitter()
    {
        const int width = 12;
        const int height = 12;
        var img = new SequentialMatrix<Lab32>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x < 6)
                {
                    // Left half: base L=30 with minor ±0.4 jitter
                    float jitter = ((x + y) % 2 == 0) ? 0.4f : -0.4f;
                    img[x, y] = new Lab32(30.0f + jitter, 0.0f, 0.0f);
                }
                else
                {
                    // Right half: base L=85 with minor ±0.4 jitter
                    float jitter = ((x + y) % 2 == 0) ? -0.4f : 0.4f;
                    img[x, y] = new Lab32(85.0f + jitter, 0.0f, 0.0f);
                }
            }
        }

        var options = new MeanShiftOptions(
            SpatialRadius: 3,
            RangeRadius: 5.0,
            MinRegionSize: 1,
            MaxIterations: 10,
            ConvergenceEpsilon: 0.01);

        Matrix<Lab32> filtered = MeanShiftSegmentation.Filter(img, options);

        Assert.Equal(width, filtered.Width);
        Assert.Equal(height, filtered.Height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x < 6)
                {
                    // Jitter smoothed toward 30, strictly away from right half
                    Assert.True(Math.Abs(filtered[x, y].L - 30.0f) < 0.2f);
                }
                else
                {
                    // Jitter smoothed toward 85, strictly away from left half
                    Assert.True(Math.Abs(filtered[x, y].L - 85.0f) < 0.2f);
                }
            }

            // Sharp edge preserved across border
            Assert.True(filtered[5, y].L < 35.0f);
            Assert.True(filtered[6, y].L > 80.0f);
        }
    }

    [Fact]
    public void Filter_Rgb8_PreservesSharpEdgesWhileSmoothingJitter()
    {
        const int width = 10;
        const int height = 10;
        var img = new SequentialMatrix<Rgb8>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte jitter = (byte)(((x + y) % 2 == 0) ? 2 : 0);
                if (y < 5)
                {
                    img[x, y] = new Rgb8((byte)(220 + jitter), 20, 20);
                }
                else
                {
                    img[x, y] = new Rgb8(20, 20, (byte)(220 + jitter));
                }
            }
        }

        var options = new MeanShiftOptions(
            SpatialRadius: 3,
            RangeRadius: 10.0,
            MinRegionSize: 1,
            MaxIterations: 10);

        Matrix<Rgb8> filtered = MeanShiftSegmentation.Filter(img, options);

        Assert.Equal(width, filtered.Width);
        Assert.Equal(height, filtered.Height);

        // Top half should be predominantly red
        Assert.True(filtered[2, 2].R > 200);
        Assert.True(filtered[2, 2].B < 40);

        // Bottom half should be predominantly blue
        Assert.True(filtered[2, 8].B > 200);
        Assert.True(filtered[2, 8].R < 40);
    }

    [Fact]
    public void Segment_Lab32_SeparatesDistinctColoredRegionsCleanly()
    {
        const int width = 12;
        const int height = 12;
        var img = new SequentialMatrix<Lab32>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x < 4)
                {
                    img[x, y] = new Lab32(50.0f, 40.0f, 20.0f);
                }
                else if (x < 8)
                {
                    img[x, y] = new Lab32(50.0f, -40.0f, 20.0f);
                }
                else
                {
                    img[x, y] = new Lab32(50.0f, 0.0f, -40.0f);
                }
            }
        }

        var options = new MeanShiftOptions(
            SpatialRadius: 3,
            RangeRadius: 10.0,
            MinRegionSize: 10);

        IDisjointSet segmentation = MeanShiftSegmentation.Segment(img, options);

        Assert.Equal(width, segmentation.Width);
        Assert.Equal(height, segmentation.Height);

        var regions = segmentation.ToRegions();
        Assert.Equal(3, regions.Count);

        // Check internal consistency within each region
        LongPixel rootA = segmentation[0, 0];
        LongPixel rootB = segmentation[5, 5];
        LongPixel rootC = segmentation[10, 10];

        Assert.NotEqual(rootA, rootB);
        Assert.NotEqual(rootB, rootC);
        Assert.NotEqual(rootA, rootC);

        Assert.Equal(rootA, segmentation[3, 11]);
        Assert.Equal(rootB, segmentation[7, 11]);
        Assert.Equal(rootC, segmentation[11, 11]);
    }

    [Fact]
    public void Segment_Rgb8_SeparatesDistinctColoredRegionsCleanly()
    {
        const int width = 10;
        const int height = 10;
        var img = new SequentialMatrix<Rgb8>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                img[x, y] = x < 5 ? new Rgb8(255, 0, 0) : new Rgb8(0, 0, 255);
            }
        }

        var options = new MeanShiftOptions(
            SpatialRadius: 4,
            RangeRadius: 15.0,
            MinRegionSize: 10);

        IDisjointSet segmentation = MeanShiftSegmentation.Segment(img, options);

        var regions = segmentation.ToRegions();
        Assert.Equal(2, regions.Count);

        LongPixel leftRoot = segmentation[0, 0];
        LongPixel rightRoot = segmentation[9, 9];

        Assert.NotEqual(leftRoot, rightRoot);
        Assert.Equal(leftRoot, segmentation[4, 9]);
        Assert.Equal(rightRoot, segmentation[5, 0]);
    }

    [Fact]
    public void SmallRegionPruning_MinRegionSize_MergesNoisyPixelsIntoClosestAdjacentRegion()
    {
        // 10x10 image: Left half is base color A (L=30), right half is base color B (L=80)
        // Two pixels touching the boundary inside the right half have color L=31 (diff=1 to A, diff=49 to B)
        const int width = 10;
        const int height = 10;
        var img = new SequentialMatrix<Lab32>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                img[x, y] = x < 5 ? new Lab32(30.0f, 0.0f, 0.0f) : new Lab32(80.0f, 0.0f, 0.0f);
            }
        }

        // Two speck pixels adjacent to the left half
        img[5, 4] = new Lab32(31.0f, 0.0f, 0.0f);
        img[5, 5] = new Lab32(31.0f, 0.0f, 0.0f);

        // Without pruning (MinRegionSize = 1, RangeRadius = 0.5), speck forms its own region
        var noPruneOptions = new MeanShiftOptions(
            SpatialRadius: 2,
            RangeRadius: 0.5,
            MinRegionSize: 1,
            MaxIterations: 5);
        IDisjointSet noPrune = MeanShiftSegmentation.Segment(img, noPruneOptions);
        Assert.Equal(3, noPrune.ToRegions().Count);

        // With pruning (MinRegionSize = 10), the speck of size 2 must merge into Region A (diff=1) rather than Region B (diff=49)
        var pruneOptions = new MeanShiftOptions(
            SpatialRadius: 2,
            RangeRadius: 0.5,
            MinRegionSize: 10,
            MaxIterations: 5);
        IDisjointSet pruned = MeanShiftSegmentation.Segment(img, pruneOptions);

        var prunedRegions = pruned.ToRegions();
        Assert.Equal(2, prunedRegions.Count);

        LongPixel regionARoot = pruned[0, 0];
        Assert.Equal(regionARoot, pruned[5, 4]);
        Assert.Equal(regionARoot, pruned[5, 5]);
    }

    [Fact]
    public void SmallRegionPruning_ChainedMultiPassMerges_MergesConsecutiveSmallRegions()
    {
        // 2x10 strip: Left side is 8 pixels of color 10, right side is 8 pixels of color 90
        // Middle has 4 isolated small pixels of colors 12, 14, 16, 18
        const int width = 10;
        const int height = 2;
        var img = new SequentialMatrix<Lab32>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float l = x switch
                {
                    < 3 => 10.0f,
                    3 => 13.0f,
                    4 => 16.0f,
                    5 => 19.0f,
                    6 => 22.0f,
                    _ => 90.0f,
                };
                img[x, y] = new Lab32(l, 0.0f, 0.0f);
            }
        }

        // MinRegionSize = 6: The middle pixels (each size 2) must cascade and merge into the left region
        var options = new MeanShiftOptions(
            SpatialRadius: 2,
            RangeRadius: 1.0,
            MinRegionSize: 6,
            MaxIterations: 5);

        IDisjointSet segmentation = MeanShiftSegmentation.Segment(img, options);
        var regions = segmentation.ToRegions();

        Assert.Equal(2, regions.Count);
        LongPixel leftRoot = segmentation[0, 0];
        Assert.Equal(leftRoot, segmentation[3, 0]);
        Assert.Equal(leftRoot, segmentation[4, 0]);
        Assert.Equal(leftRoot, segmentation[5, 0]);
    }

    [Fact]
    public void SpatialRadiusGreaterThan16_LatticePathMatchesExpectedSegmentation()
    {
        // SpatialRadius = 18 > LatticeRadiusThreshold (16)
        const int width = 36;
        const int height = 36;
        var img = new SequentialMatrix<Lab32>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                img[x, y] = x < 18 ? new Lab32(20.0f, 0.0f, 0.0f) : new Lab32(80.0f, 0.0f, 0.0f);
            }
        }

        var options = new MeanShiftOptions(
            SpatialRadius: 18,
            RangeRadius: 10.0,
            MinRegionSize: 50,
            MaxIterations: 10);

        IDisjointSet segmentation = MeanShiftSegmentation.Segment(img, options);
        var regions = segmentation.ToRegions();

        Assert.Equal(2, regions.Count);
        Assert.NotEqual(segmentation[0, 0], segmentation[35, 35]);
        Assert.Equal(segmentation[0, 0], segmentation[17, 17]);
        Assert.Equal(segmentation[35, 35], segmentation[18, 18]);
    }

    [Fact]
    public void Connectivity_FourConnectedVsEightConnected_DiagonalDifference()
    {
        // 3x3 image with background=0, and two diagonal pixels at (0, 0) and (1, 1) with color L=90
        const int width = 3;
        const int height = 3;
        var img = new SequentialMatrix<Lab32>(width, height);
        img.SetAll(new Lab32(0.0f, 0.0f, 0.0f));
        img[0, 0] = new Lab32(90.0f, 0.0f, 0.0f);
        img[1, 1] = new Lab32(90.0f, 0.0f, 0.0f);

        var eightOptions = new MeanShiftOptions(
            SpatialRadius: 2,
            RangeRadius: 5.0,
            MinRegionSize: 1,
            MaxIterations: 5,
            Connectivity: GridConnectivity.EightConnected);

        IDisjointSet eightSet = MeanShiftSegmentation.Segment(img, eightOptions);

        var fourOptions = new MeanShiftOptions(
            SpatialRadius: 2,
            RangeRadius: 5.0,
            MinRegionSize: 1,
            MaxIterations: 5,
            Connectivity: GridConnectivity.FourConnected);

        IDisjointSet fourSet = MeanShiftSegmentation.Segment(img, fourOptions);

        // Under 8-connected, (0, 0) and (1, 1) are forward diagonal neighbors and merge
        Assert.Equal(eightSet[0, 0], eightSet[1, 1]);

        // Under 4-connected, diagonal neighbors are not connected, so (0, 0) and (1, 1) remain separate
        Assert.NotEqual(fourSet[0, 0], fourSet[1, 1]);
    }

    [Fact]
    public void Segment_DeterminismAcrossRepeatedRuns()
    {
        const int width = 20;
        const int height = 20;
        var img = new SequentialMatrix<Rgb8>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte val = (byte)((x * 12) + (y * 8));
                img[x, y] = new Rgb8(val, (byte)(255 - val), (byte)((val * 2) % 256));
            }
        }

        var options = new MeanShiftOptions(
            SpatialRadius: 5,
            RangeRadius: 15.0,
            MinRegionSize: 15,
            MaxIterations: 10);

        Matrix<LongPixel> baseline = MeanShiftSegmentation.Segment(img, options).ToMatrix();

        for (int run = 0; run < 4; run++)
        {
            Matrix<LongPixel> current = MeanShiftSegmentation.Segment(img, options).ToMatrix();
            Assert.Equal(baseline.Width, current.Width);
            Assert.Equal(baseline.Height, current.Height);
            for (int i = 0; i < baseline.Count; i++)
            {
                Assert.Equal(baseline[i].Value, current[i].Value);
            }
        }
    }

    [Fact]
    public void EmptyImage_FilterAndSegment_ShouldHandleCleanly()
    {
        var emptyLab = new SequentialMatrix<Lab32>(0, 0);
        var emptyRgb = new SequentialMatrix<Rgb8>(0, 0);

        Matrix<Lab32> filteredLab = MeanShiftSegmentation.Filter(emptyLab);
        Assert.Equal(0, filteredLab.Width);
        Assert.Equal(0, filteredLab.Height);
        Assert.Empty(filteredLab);

        Matrix<Rgb8> filteredRgb = MeanShiftSegmentation.Filter(emptyRgb);
        Assert.Equal(0, filteredRgb.Width);
        Assert.Equal(0, filteredRgb.Height);
        Assert.Empty(filteredRgb);

        IDisjointSet segmentLab = MeanShiftSegmentation.Segment(emptyLab);
        Assert.Equal(0, segmentLab.Width);
        Assert.Equal(0, segmentLab.Height);
        Assert.Empty(segmentLab);
        Assert.Empty(segmentLab.ToRegions());

        IDisjointSet segmentRgb = MeanShiftSegmentation.Segment(emptyRgb);
        Assert.Equal(0, segmentRgb.Width);
        Assert.Equal(0, segmentRgb.Height);
        Assert.Empty(segmentRgb);
        Assert.Empty(segmentRgb.ToRegions());
    }

    [Fact]
    public void SinglePixel_FilterAndSegment_ShouldHandleCleanly()
    {
        var singleLab = new SequentialMatrix<Lab32>(1, 1);
        singleLab[0, 0] = new Lab32(65.0f, 12.0f, -8.0f);

        Matrix<Lab32> filtered = MeanShiftSegmentation.Filter(singleLab);
        Assert.Equal(1, filtered.Width);
        Assert.Equal(1, filtered.Height);
        Assert.Equal(singleLab[0, 0], filtered[0, 0]);

        IDisjointSet segment = MeanShiftSegmentation.Segment(singleLab);
        Assert.Equal(1, segment.Width);
        Assert.Equal(1, segment.Height);
        Assert.Equal(1, segment.Count);
        Assert.Single(segment.ToRegions());
    }

    [Fact]
    public void Validation_NullAndOutOfRange_ShouldThrowExpectedExceptions()
    {
        Assert.Throws<ArgumentNullException>(() => MeanShiftSegmentation.Filter((IReadOnlyMatrix<Lab32>)null!));
        Assert.Throws<ArgumentNullException>(() => MeanShiftSegmentation.Filter((IReadOnlyMatrix<Rgb8>)null!));
        Assert.Throws<ArgumentNullException>(() => MeanShiftSegmentation.Segment((IReadOnlyMatrix<Lab32>)null!));
        Assert.Throws<ArgumentNullException>(() => MeanShiftSegmentation.Segment((IReadOnlyMatrix<Rgb8>)null!));
        Assert.Throws<ArgumentNullException>(() => MeanShiftSegmentation.FilterAndSegment((IReadOnlyMatrix<Lab32>)null!));
        Assert.Throws<ArgumentNullException>(() => MeanShiftSegmentation.FilterAndSegment((IReadOnlyMatrix<Rgb8>)null!));

        var sample = new SequentialMatrix<Lab32>(4, 4);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MeanShiftSegmentation.Filter(sample, new MeanShiftOptions(SpatialRadius: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MeanShiftSegmentation.Filter(sample, new MeanShiftOptions(RangeRadius: -1.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MeanShiftSegmentation.Segment(sample, new MeanShiftOptions(MinRegionSize: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MeanShiftSegmentation.Segment(sample, new MeanShiftOptions(MaxIterations: 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MeanShiftSegmentation.Segment(sample, new MeanShiftOptions(ConvergenceEpsilon: double.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MeanShiftSegmentation.Segment(sample, new MeanShiftOptions(Connectivity: (GridConnectivity)99)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MeanShiftSegmentation.FilterAndSegment(sample, new MeanShiftOptions(MaxIterations: 0)));
    }

    [Fact]
    public void Segment_SmoothLinearGradient_ShouldNotChainIntoSingleRegion()
    {
        const int width = 60;
        const int height = 10;
        var ramp = new SequentialMatrix<Lab32>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float l = 20.0f + ((float)x / (width - 1) * 60.0f);
                ramp[x, y] = new Lab32(l, 0.0f, 0.0f);
            }
        }

        var options = new MeanShiftOptions(SpatialRadius: 8, RangeRadius: 5.5, MinRegionSize: 10);
        IDisjointSet segmentation = MeanShiftSegmentation.Segment(ramp, options);
        var regions = segmentation.ToRegions();

        Assert.True(regions.Count > 1, $"Expected gradient ramp to split into multiple regions, got {regions.Count}.");
        Assert.NotEqual(segmentation[0, 5], segmentation[width - 1, 5]);
    }

    [Fact]
    public void FilterAndSegment_ShouldMatchSeparateFilterAndSegmentOutputs()
    {
        var emptyLab = new SequentialMatrix<Lab32>(0, 0);
        var emptyRgb = new SequentialMatrix<Rgb8>(0, 0);

        (Matrix<Lab32> emptyFilteredLab, IDisjointSet emptySegLab) = MeanShiftSegmentation.FilterAndSegment(emptyLab);
        Assert.Empty(emptyFilteredLab);
        Assert.Empty(emptySegLab);

        (Matrix<Rgb8> emptyFilteredRgb, IDisjointSet emptySegRgb) = MeanShiftSegmentation.FilterAndSegment(emptyRgb);
        Assert.Empty(emptyFilteredRgb);
        Assert.Empty(emptySegRgb);

        var rgb = new SequentialMatrix<Rgb8>(12, 12);
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                rgb[x, y] = x < 6 ? new Rgb8(210, 40, 40) : new Rgb8(40, 80, 210);
            }
        }

        var options = new MeanShiftOptions(SpatialRadius: 4, RangeRadius: 10.0, MinRegionSize: 10);

        Matrix<Rgb8> expectedRgb = MeanShiftSegmentation.Filter(rgb, options);
        IDisjointSet expectedRgbSeg = MeanShiftSegmentation.Segment(rgb, options);
        (Matrix<Rgb8> actualRgb, IDisjointSet actualRgbSeg) = MeanShiftSegmentation.FilterAndSegment(rgb, options);

        Assert.True(expectedRgb.ContentEquals(actualRgb));
        Assert.True(expectedRgbSeg.ToMatrix().ContentEquals(actualRgbSeg.ToMatrix()));

        Matrix<Lab32> lab = rgb.Map(static p => p.ToLab32(Leiter.Pixels.ColorSpaces.RgbColorSpace.SRgb));
        Matrix<Lab32> expectedLab = MeanShiftSegmentation.Filter(lab, options);
        IDisjointSet expectedLabSeg = MeanShiftSegmentation.Segment(lab, options);
        (Matrix<Lab32> actualLab, IDisjointSet actualLabSeg) = MeanShiftSegmentation.FilterAndSegment(lab, options);

        Assert.True(expectedLab.ContentEquals(actualLab));
        Assert.True(expectedLabSeg.ToMatrix().ContentEquals(actualLabSeg.ToMatrix()));
    }
}
