namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using System;
using System.Threading;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MeanShiftModeSeeker"/>.
/// </summary>
public class MeanShiftModeSeekerTests
{
    [Fact]
    public void SeekModes_ValidationAndEmptyImage_ShouldBehaveCorrectly()
    {
        var empty = new SequentialMatrix<Lab32>(0, 0);
        Assert.Throws<ArgumentNullException>("labImage", () => MeanShiftModeSeeker.SeekModes(null!, MeanShiftOptions.Default));
        Assert.Throws<ArgumentNullException>("options", () => MeanShiftModeSeeker.SeekModes(empty, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => MeanShiftModeSeeker.SeekModes(empty, new MeanShiftOptions(SpatialRadius: 0)));

        (MeanShiftMode[] modes, Lab32[] sourcePixels) = MeanShiftModeSeeker.SeekModes(empty, MeanShiftOptions.Default);
        Assert.Empty(modes);
        Assert.Empty(sourcePixels);
    }

    [Fact]
    public void SeekModes_DirectAndLatticePaths_ShouldConvergeConsistentlyAcrossSharpEdge()
    {
        SequentialMatrix<Lab32> img = TestMatrices.Create(
            24,
            16,
            (x, _) => x < 12 ? new Lab32(25f, -10f, 5f) : new Lab32(80f, 15f, -12f));

        var options = new MeanShiftOptions(
            SpatialRadius: 4,
            RangeRadius: 6.0,
            MinRegionSize: 0,
            MaxIterations: 15,
            ConvergenceEpsilon: 0.01);

        (MeanShiftMode[] directModes, Lab32[] directPixels) = MeanShiftModeSeeker.SeekModes(
            img,
            options,
            latticeRadiusThreshold: int.MaxValue);
        (MeanShiftMode[] latticeModes, Lab32[] latticePixels) = MeanShiftModeSeeker.SeekModes(
            img,
            options,
            latticeRadiusThreshold: 2);

        Assert.Equal(img.Count, directModes.Length);
        Assert.Equal(img.Count, directPixels.Length);
        Assert.Equal(directPixels, latticePixels);

        for (int i = 0; i < directModes.Length; i++)
        {
            Assert.Equal(directModes[i].X, latticeModes[i].X, 4);
            Assert.Equal(directModes[i].Y, latticeModes[i].Y, 4);
            Assert.Equal(directModes[i].Color.L, latticeModes[i].Color.L, 3);
            Assert.Equal(directModes[i].Color.A, latticeModes[i].Color.A, 3);
            Assert.Equal(directModes[i].Color.B, latticeModes[i].Color.B, 3);
        }

        Assert.Equal(25f, directModes[img.Size.IndexOf(4, 8)].Color.L, 3);
        Assert.Equal(80f, directModes[img.Size.IndexOf(18, 8)].Color.L, 3);
    }

    [Fact]
    public void SeekModes_CoarseToFineAndStridedPaths_ShouldAgreeWithDirectOnUniformRegions()
    {
        SequentialMatrix<Lab32> img = TestMatrices.Create(
            32,
            32,
            (x, y) => (x < 16 && y < 16)
                ? new Lab32(20f, 0f, 0f)
                : new Lab32(85f, 10f, -10f));

        // SpatialRadius = 20 triggers both coarse grid (>=12) and strided lattice (>=20).
        var options = new MeanShiftOptions(SpatialRadius: 20, RangeRadius: 8.0, MinRegionSize: 0);
        (MeanShiftMode[] modes, _) = MeanShiftModeSeeker.SeekModes(img, options);

        Assert.Equal(20f, modes[img.Size.IndexOf(5, 5)].Color.L, 2);
        Assert.Equal(85f, modes[img.Size.IndexOf(24, 24)].Color.L, 2);
    }

    [Fact]
    public void CoarseCornersAgree_And_TryInterpolateAllFourCorners_ShouldSkipZeroWeightCornersAndGuardCorner00()
    {
        var agreeModeA = new MeanShiftMode(4.0, 4.0, new Lab32(50f, 0f, 0f));
        var agreeModeB = new MeanShiftMode(4.5, 4.5, new Lab32(50.5f, 0.2f, -0.2f));
        var disagreeMode = new MeanShiftMode(20.0, 20.0, new Lab32(95f, 40f, -40f));

        var src = new Lab32(50f, 0f, 0f);
        var disagreeSrc = new Lab32(95f, 40f, -40f);

        // Grid-edge case: corner10 and corner11 have weight 0 with disagreeing modes; must still agree using corner00 and corner01.
        var c00 = new MeanShiftModeSeeker.CoarseCornerSample(src, agreeModeA, 0.4);
        var c10Zero = new MeanShiftModeSeeker.CoarseCornerSample(disagreeSrc, disagreeMode, 0.0);
        var c01 = new MeanShiftModeSeeker.CoarseCornerSample(src, agreeModeB, 0.6);
        var c11Zero = new MeanShiftModeSeeker.CoarseCornerSample(disagreeSrc, disagreeMode, 0.0);

        Assert.True(MeanShiftModeSeeker.CoarseCornersAgree(
            in c00,
            in c10Zero,
            in c01,
            in c11Zero,
            modeColorToleranceSquared: 4.0,
            spatialRadiusSquared: 9.0,
            out MeanShiftMode refMode));
        Assert.Equal(agreeModeA, refMode);

        Assert.True(MeanShiftModeSeeker.TryInterpolateAllFourCorners(
            src,
            refMode,
            in c00,
            in c10Zero,
            in c01,
            in c11Zero,
            sourceColorToleranceSquared: 4.0,
            rangeRadiusSquared: 16.0,
            out MeanShiftMode edgeInterpolated));
        Assert.Equal(4.3, edgeInterpolated.X, 9);
        Assert.Equal(4.3, edgeInterpolated.Y, 9);

        // Guard against corner00 having weight 0 (and even NaN payload): must pick first positive-weight corner as referenceMode.
        var badCorner00 = new MeanShiftModeSeeker.CoarseCornerSample(
            new Lab32(float.NaN, 0f, 0f),
            new MeanShiftMode(0.0, 0.0, new Lab32(float.NaN, 0f, 0f)),
            0.0);
        var active10 = new MeanShiftModeSeeker.CoarseCornerSample(src, agreeModeA, 0.25);
        var c01Zero = c01 with { BilinearWeight = 0.0 };
        var active11 = new MeanShiftModeSeeker.CoarseCornerSample(src, agreeModeB, 0.75);

        Assert.True(MeanShiftModeSeeker.CoarseCornersAgree(
            in badCorner00,
            in active10,
            in c01Zero,
            in active11,
            modeColorToleranceSquared: 4.0,
            spatialRadiusSquared: 9.0,
            out MeanShiftMode nonZeroRef));
        Assert.Equal(agreeModeA, nonZeroRef);

        Assert.True(MeanShiftModeSeeker.TryInterpolateAllFourCorners(
            src,
            nonZeroRef,
            in badCorner00,
            in active10,
            in c01Zero,
            in active11,
            sourceColorToleranceSquared: 4.0,
            rangeRadiusSquared: 16.0,
            out MeanShiftMode guardedInterpolated));
        Assert.Equal(4.375, guardedInterpolated.X, 9);
        Assert.Equal(4.375, guardedInterpolated.Y, 9);

        // All zero weights must return false.
        Assert.False(MeanShiftModeSeeker.CoarseCornersAgree(
            in badCorner00,
            in c10Zero,
            in c01Zero,
            in c11Zero,
            modeColorToleranceSquared: 4.0,
            spatialRadiusSquared: 9.0,
            out _));
    }

    [Fact]
    public void SeekModes_CancellationToken_ShouldHonorCancellationAndMatchUncanceledResults()
    {
        SequentialMatrix<Lab32> img = TestMatrices.Create(
            32,
            32,
            (x, _) => x < 16 ? new Lab32(25f, 0f, 0f) : new Lab32(80f, 0f, 0f));

        var directOptions = new MeanShiftOptions(SpatialRadius: 4, RangeRadius: 8.0, MinRegionSize: 0);
        var coarseOptions = new MeanShiftOptions(SpatialRadius: 20, RangeRadius: 8.0, MinRegionSize: 0);

        using var canceledCts = new CancellationTokenSource();
        canceledCts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            MeanShiftModeSeeker.SeekModes(img, directOptions, cancellationToken: canceledCts.Token));
        Assert.Throws<OperationCanceledException>(() =>
            MeanShiftModeSeeker.SeekModes(img, coarseOptions, cancellationToken: canceledCts.Token));

        using var activeCts = new CancellationTokenSource();
        (MeanShiftMode[] baseDirect, _) = MeanShiftModeSeeker.SeekModes(img, directOptions);
        (MeanShiftMode[] tokenDirect, _) = MeanShiftModeSeeker.SeekModes(img, directOptions, cancellationToken: activeCts.Token);
        Assert.Equal(baseDirect, tokenDirect);

        (MeanShiftMode[] baseCoarse, _) = MeanShiftModeSeeker.SeekModes(img, coarseOptions);
        (MeanShiftMode[] tokenCoarse, _) = MeanShiftModeSeeker.SeekModes(img, coarseOptions, cancellationToken: activeCts.Token);
        Assert.Equal(baseCoarse, tokenCoarse);
    }
}
