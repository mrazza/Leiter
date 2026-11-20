namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MeanShiftRegionClusterer"/>.
/// </summary>
public class MeanShiftRegionClustererTests
{
    [Fact]
    public void ClusterModes_ValidationAndDegenerateSizes_ShouldBehaveCorrectly()
    {
        var img = new SequentialMatrix<Lab32>(2, 2);
        var validModes = new MeanShiftMode[4];
        var validPixels = new Lab32[4];

        Assert.Throws<ArgumentNullException>("targetImage", () =>
            MeanShiftRegionClusterer.ClusterModes(null!, validPixels, validModes, MeanShiftOptions.Default));
        Assert.Throws<ArgumentNullException>("sourcePixels", () =>
            MeanShiftRegionClusterer.ClusterModes(img, null!, validModes, MeanShiftOptions.Default));
        Assert.Throws<ArgumentNullException>("modes", () =>
            MeanShiftRegionClusterer.ClusterModes(img, validPixels, null!, MeanShiftOptions.Default));
        Assert.Throws<ArgumentNullException>("options", () =>
            MeanShiftRegionClusterer.ClusterModes(img, validPixels, validModes, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MeanShiftRegionClusterer.ClusterModes(img, validPixels, validModes, new MeanShiftOptions(SpatialRadius: 0)));
        Assert.Throws<ArgumentException>("sourcePixels", () =>
            MeanShiftRegionClusterer.ClusterModes(img, new Lab32[2], validModes, MeanShiftOptions.Default));
        Assert.Throws<ArgumentException>("modes", () =>
            MeanShiftRegionClusterer.ClusterModes(img, validPixels, new MeanShiftMode[2], MeanShiftOptions.Default));

        var empty = new SequentialMatrix<Lab32>(0, 0);
        IDisjointSet emptySeg = MeanShiftRegionClusterer.ClusterModes(empty, [], [], MeanShiftOptions.Default);
        Assert.Empty(emptySeg.ToRegions());

        var single = TestMatrices.Constant(1, 1, new Lab32(50f, 0f, 0f));
        IDisjointSet singleSeg = MeanShiftRegionClusterer.ClusterModes(
            single,
            [single[0, 0]],
            [new MeanShiftMode(0.0, 0.0, single[0, 0])],
            MeanShiftOptions.Default);
        Assert.Single(singleSeg.ToRegions());
    }

    [Fact]
    public void ClusterModes_RespectsConnectivityAndMinRegionSizePruning()
    {
        // 3x3 image: diagonal foreground pixels at (0,0) and (1,1) on L=0 background.
        SequentialMatrix<Lab32> img = TestMatrices.Constant(3, 3, new Lab32(0f, 0f, 0f));
        img[0, 0] = new Lab32(90f, 0f, 0f);
        img[1, 1] = new Lab32(90f, 0f, 0f);

        Lab32[] sourcePixels = img.ToArray();
        var modes = new MeanShiftMode[sourcePixels.Length];
        for (int y = 0; y < img.Height; y++)
        {
            for (int x = 0; x < img.Width; x++)
            {
                int idx = img.Size.IndexOf(x, y);
                modes[idx] = new MeanShiftMode(x, y, sourcePixels[idx]);
            }
        }

        // 8-connected merges the diagonal pair into one 2-pixel region; MinRegionSize = 0 disables pruning.
        IDisjointSet eightNoPrune = MeanShiftRegionClusterer.ClusterModes(
            img,
            sourcePixels,
            modes,
            new MeanShiftOptions(SpatialRadius: 2, RangeRadius: 5.0, MinRegionSize: 0, Connectivity: GridConnectivity.EightConnected));
        Assert.Equal(eightNoPrune[0, 0], eightNoPrune[1, 1]);
        Assert.Equal(2, eightNoPrune.ToRegions().Count);

        // 4-connected keeps (0,0) and (1,1) separate when MinRegionSize = 0.
        IDisjointSet fourNoPrune = MeanShiftRegionClusterer.ClusterModes(
            img,
            sourcePixels,
            modes,
            new MeanShiftOptions(SpatialRadius: 2, RangeRadius: 5.0, MinRegionSize: 0, Connectivity: GridConnectivity.FourConnected));
        Assert.NotEqual(fourNoPrune[0, 0], fourNoPrune[1, 1]);
        Assert.Equal(3, fourNoPrune.ToRegions().Count);

        // MinRegionSize = 4 prunes the small diagonal components into the background.
        IDisjointSet pruned = MeanShiftRegionClusterer.ClusterModes(
            img,
            sourcePixels,
            modes,
            new MeanShiftOptions(SpatialRadius: 2, RangeRadius: 5.0, MinRegionSize: 4, Connectivity: GridConnectivity.FourConnected));
        Assert.Single(pruned.ToRegions());
    }

    [Fact]
    public void ClusterModes_ChainedSmallRegions_ShouldCascadeIntoClosestLargeNeighbor()
    {
        // 8x2 strip: 6px left region (L=10), two 2px small regions (L=12, L=14), 6px right region (L=90).
        SequentialMatrix<Lab32> img = TestMatrices.Create(
            8,
            2,
            (x, _) => new Lab32(
                x switch
                {
                    < 3 => 10f,
                    3 => 12f,
                    4 => 14f,
                    _ => 90f,
                },
                0f,
                0f));

        Lab32[] sourcePixels = img.ToArray();
        var modes = new MeanShiftMode[sourcePixels.Length];
        for (int y = 0; y < img.Height; y++)
        {
            for (int x = 0; x < img.Width; x++)
            {
                int idx = img.Size.IndexOf(x, y);
                modes[idx] = new MeanShiftMode(x, y, sourcePixels[idx]);
            }
        }

        IDisjointSet seg = MeanShiftRegionClusterer.ClusterModes(
            img,
            sourcePixels,
            modes,
            new MeanShiftOptions(SpatialRadius: 2, RangeRadius: 1.0, MinRegionSize: 6));

        Assert.Equal(2, seg.ToRegions().Count);
        Assert.Equal(seg[0, 0], seg[3, 0]);
        Assert.Equal(seg[0, 0], seg[4, 0]);
        Assert.NotEqual(seg[0, 0], seg[5, 0]);
    }
}
