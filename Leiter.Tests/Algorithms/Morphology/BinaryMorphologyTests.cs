namespace Leiter.Tests.Algorithms.Morphology;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Core;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="BinaryMorphology"/>.
/// </summary>
public class BinaryMorphologyTests
{
    [Fact]
    public void Dilate_And_Erode_DiskAndSquare_ShouldMatchExactDefinition()
    {
        var impulse = new MaskMatrix(9, 9);
        impulse[4, 4] = true;

        MaskMatrix diskDilated = BinaryMorphology.Dilate(impulse, 2, StructuringElementShape.Disk);
        MaskMatrix squareDilated = BinaryMorphology.Dilate(impulse, 2, StructuringElementShape.Square);

        SequentialMatrix<bool> expectedDisk = TestMatrices.Create(
            9,
            9,
            static (x, y) => ((x - 4) * (x - 4)) + ((y - 4) * (y - 4)) <= 4);
        SequentialMatrix<bool> expectedSquare = TestMatrices.Create(
            9,
            9,
            static (x, y) => Math.Max(Math.Abs(x - 4), Math.Abs(y - 4)) <= 2);
        MatrixAssert.Equal(expectedDisk, diskDilated);
        MatrixAssert.Equal(expectedSquare, squareDilated);

        MaskMatrix erodedBack = BinaryMorphology.Erode(diskDilated, 2, StructuringElementShape.Disk);
        Assert.True(erodedBack[4, 4]);
        Assert.False(erodedBack[4, 5]);
        Assert.False(erodedBack[5, 4]);
    }

    [Fact]
    public void Erode_BorderHandling_ShouldRespectBorderIsBackgroundSetting()
    {
        SequentialMatrix<bool> block = TestMatrices.Constant(5, 5, true);

        MaskMatrix clamped = BinaryMorphology.Erode(block, 1, StructuringElementShape.Square, borderIsBackground: false);
        MatrixAssert.Equal(block, clamped);

        MaskMatrix zeroPadded = BinaryMorphology.Erode(block, 1, StructuringElementShape.Square, borderIsBackground: true);
        MatrixAssert.Equal(TestMatrices.CenteredBox(5, 5, 1, 1, 3, 3, true, false), zeroPadded);

        Assert.False(zeroPadded[0, 2]);
        Assert.False(zeroPadded[4, 2]);
    }

    [Fact]
    public void Dilate_Open_And_Close_BorderIsBackground_ShouldBehaveConsistently()
    {
        var cornerImpulse = new MaskMatrix(5, 5);
        cornerImpulse[0, 0] = true;

        foreach (StructuringElementShape shape in new[] { StructuringElementShape.Disk, StructuringElementShape.Square })
        {
            MaskMatrix dilatedClamped = BinaryMorphology.Dilate(cornerImpulse, 2, shape, borderIsBackground: false);
            MaskMatrix dilatedBackground = BinaryMorphology.Dilate(cornerImpulse, 2, shape, borderIsBackground: true);
            MatrixAssert.Equal(dilatedClamped, dilatedBackground);
            Assert.True(dilatedBackground[0, 0]);
            Assert.True(dilatedBackground[2, 0]);
            Assert.True(dilatedBackground[0, 2]);
            Assert.Equal(shape == StructuringElementShape.Square, dilatedBackground[2, 2]);
        }

        var singleFalse = new MaskMatrix(1, 1);
        Assert.False(BinaryMorphology.Dilate(singleFalse, 1, StructuringElementShape.Disk, borderIsBackground: false)[0, 0]);
        Assert.False(BinaryMorphology.Dilate(singleFalse, 1, StructuringElementShape.Disk, borderIsBackground: true)[0, 0]);

        // Verify Open and Close forward borderIsBackground consistently.
        // A 2x2 top-left corner block survives Erode+Dilate when clamped, but vanishes completely when borderIsBackground=true.
        var cornerBlock = new MaskMatrix(5, 5);
        cornerBlock[0, 0] = true;
        cornerBlock[1, 0] = true;
        cornerBlock[0, 1] = true;
        cornerBlock[1, 1] = true;

        MaskMatrix openedClamped = BinaryMorphology.Open(cornerBlock, 1, StructuringElementShape.Square, borderIsBackground: false);
        MaskMatrix openedBackground = BinaryMorphology.Open(cornerBlock, 1, StructuringElementShape.Square, borderIsBackground: true);
        Assert.True(openedClamped[0, 0]);
        Assert.False(openedBackground[0, 0]);
        Assert.False(openedBackground[1, 1]);

        SequentialMatrix<bool> fullBlock = TestMatrices.Constant(5, 5, true);

        MaskMatrix closedClamped = BinaryMorphology.Close(fullBlock, 1, StructuringElementShape.Square, borderIsBackground: false);
        MaskMatrix closedBackground = BinaryMorphology.Close(fullBlock, 1, StructuringElementShape.Square, borderIsBackground: true);
        Assert.True(closedClamped[0, 0]);
        Assert.False(closedBackground[0, 0]);
        Assert.True(closedBackground[1, 1]);
    }

    [Fact]
    public void Open_And_Close_ShouldRemoveSpecksAndFillHoles()
    {
        SequentialMatrix<bool> mask = TestMatrices.CenteredBox(11, 11, 2, 2, 8, 8, true, false);
        mask[5, 5] = false;
        mask[0, 0] = true;

        MaskMatrix opened = BinaryMorphology.Open(mask, 1, StructuringElementShape.Square);
        Assert.False(opened[0, 0]);
        Assert.True(opened[3, 3]);

        MaskMatrix closed = BinaryMorphology.Close(mask, 1, StructuringElementShape.Disk);
        Assert.True(closed[5, 5]);
    }

    [Fact]
    public void ZeroRadius_EmptyMatrix_AndInvalidArgs_ShouldBeHandledCleanly()
    {
        var mixed = new MaskMatrix(3, 2);
        mixed[0, 0] = true;
        mixed[2, 1] = true;

        MaskMatrix zeroDilate = BinaryMorphology.Dilate(mixed, 0);
        MaskMatrix zeroErode = BinaryMorphology.Erode(mixed, 0);
        Assert.True(zeroDilate[0, 0]);
        Assert.True(zeroDilate[2, 1]);
        Assert.False(zeroDilate[1, 0]);
        Assert.True(zeroErode[0, 0]);

        // Also exercise zero-radius cloning on a MatrixView<bool> (non-Matrix IReadOnlyMatrix).
        var view = new MatrixView<bool>(mixed, 0, 0, 2, 2, EdgeHandling.Extend);
        MaskMatrix viewZeroDilate = BinaryMorphology.Dilate(view, 0);
        Assert.True(viewZeroDilate[0, 0]);
        Assert.False(viewZeroDilate[1, 0]);

        var empty = new MaskMatrix(0, 0);
        Assert.Empty(BinaryMorphology.Dilate(empty, 2));
        Assert.Empty(BinaryMorphology.Erode(empty, 2));

        Assert.Throws<ArgumentNullException>(() => BinaryMorphology.Dilate(null!, 1));
        Assert.Throws<ArgumentNullException>(() => BinaryMorphology.Erode(null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMorphology.Dilate(mixed, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMorphology.Erode(mixed, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMorphology.Dilate(mixed, 1, (StructuringElementShape)999));
    }

    [Fact]
    public void FilterConnectedComponents_AllRetentionPolicies_ShouldRetainExpectedComponents()
    {
        // 10x10 mask with a 4x4 component (16 px), a 2x2 component (4 px), and a 1x1 component (1 px)
        SequentialMatrix<bool> mask = TestMatrices.Create(
            10,
            10,
            static (x, y) => (x >= 1 && x <= 4 && y >= 1 && y <= 4) || (x >= 7 && x <= 8 && y >= 7 && y <= 8));
        mask[0, 9] = true;

        // MinimumAreaFraction: 0.03 requires at least 3 pixels. 4x4 and 2x2 survive, 1x1 is discarded.
        MaskMatrix areaFiltered = BinaryMorphology.FilterConnectedComponents(
            mask,
            ComponentRetentionPolicy.MinimumAreaFraction,
            minAreaFraction: 0.03);
        Assert.True(areaFiltered[2, 2]);
        Assert.True(areaFiltered[7, 7]);
        Assert.False(areaFiltered[0, 9]);

        // LargestComponentOnly: only the 4x4 component survives.
        MaskMatrix largestOnly = BinaryMorphology.FilterConnectedComponents(
            mask,
            ComponentRetentionPolicy.LargestComponentOnly);
        Assert.True(largestOnly[2, 2]);
        Assert.False(largestOnly[7, 7]);
        Assert.False(largestOnly[0, 9]);

        // KeepAll: all components survive.
        MaskMatrix keepAll = BinaryMorphology.FilterConnectedComponents(
            mask,
            ComponentRetentionPolicy.KeepAll);
        Assert.True(keepAll[2, 2]);
        Assert.True(keepAll[7, 7]);
        Assert.True(keepAll[0, 9]);

        Assert.Empty(BinaryMorphology.FilterConnectedComponents(new MaskMatrix(0, 0)));
        MaskMatrix allFalse = BinaryMorphology.FilterConnectedComponents(new MaskMatrix(3, 3));
        Assert.False(allFalse[0, 0]);
    }

    [Fact]
    public void FilterConnectedComponents_FourVsEightConnected_ShouldSeparateOrMergeDiagonalPixels()
    {
        // One 2x1 horizontal bar (2 px) plus two diagonally touching 1x1 pixels ((0,3)-(1,4) -> 2 px only when 8-connected)
        var mask = new MaskMatrix(5, 5);
        mask[3, 0] = true;
        mask[4, 0] = true;
        mask[0, 3] = true;
        mask[1, 4] = true;

        // Threshold 2/25 = 0.08: under 4-connected, (0,3) and (1,4) are size-1 components -> filtered out!
        MaskMatrix fourConnected = BinaryMorphology.FilterConnectedComponents(
            mask,
            ComponentRetentionPolicy.MinimumAreaFraction,
            minAreaFraction: 0.08,
            connectivity: GridConnectivity.FourConnected);
        Assert.True(fourConnected[3, 0]);
        Assert.False(fourConnected[0, 3]);
        Assert.False(fourConnected[1, 4]);

        // Under 8-connected, (0,3) and (1,4) merge into a size-2 component -> retained!
        MaskMatrix eightConnected = BinaryMorphology.FilterConnectedComponents(
            mask,
            ComponentRetentionPolicy.MinimumAreaFraction,
            minAreaFraction: 0.08,
            connectivity: GridConnectivity.EightConnected);
        Assert.True(eightConnected[3, 0]);
        Assert.True(eightConnected[0, 3]);
        Assert.True(eightConnected[1, 4]);

        // Validation
        Assert.Throws<ArgumentNullException>(() => BinaryMorphology.FilterConnectedComponents(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMorphology.FilterConnectedComponents(mask, (ComponentRetentionPolicy)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMorphology.FilterConnectedComponents(mask, minAreaFraction: -0.05));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMorphology.FilterConnectedComponents(mask, minAreaFraction: 1.05));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMorphology.FilterConnectedComponents(mask, minAreaFraction: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMorphology.FilterConnectedComponents(mask, connectivity: (GridConnectivity)99));
    }

    [Fact]
    public void Erode_BorderIsBackgroundWithDisk_ShouldErodeBordersWithDiskShape()
    {
        SequentialMatrix<bool> mask = TestMatrices.Constant(5, 5, true);
        // Clearing (0, 0) removes a corner outside the radius-2 disk centered at (2, 2),
        // distinguishing Disk from Square while verifying borderIsBackground: true behavior.
        mask[0, 0] = false;

        MaskMatrix erodedDisk = BinaryMorphology.Erode(
            mask,
            radius: 2,
            shape: StructuringElementShape.Disk,
            borderIsBackground: true);

        MatrixAssert.Equal(TestMatrices.Create(5, 5, static (x, y) => x == 2 && y == 2), erodedDisk);
    }

    [Fact]
    public void Dilate_And_Erode_SinglePixelMask_ShouldHandleAllModes()
    {
        var singleTrue = new MaskMatrix(1, 1);
        singleTrue[0, 0] = true;

        var singleFalse = new MaskMatrix(1, 1);

        Assert.True(BinaryMorphology.Dilate(singleTrue, 1, StructuringElementShape.Disk)[0, 0]);
        Assert.True(BinaryMorphology.Dilate(singleTrue, 2, StructuringElementShape.Square)[0, 0]);
        Assert.False(BinaryMorphology.Dilate(singleFalse, 1, StructuringElementShape.Disk)[0, 0]);

        Assert.True(BinaryMorphology.Erode(singleTrue, 1, StructuringElementShape.Disk, borderIsBackground: false)[0, 0]);
        Assert.True(BinaryMorphology.Erode(singleTrue, 2, StructuringElementShape.Square, borderIsBackground: false)[0, 0]);
        Assert.False(BinaryMorphology.Erode(singleTrue, 1, StructuringElementShape.Disk, borderIsBackground: true)[0, 0]);
        Assert.False(BinaryMorphology.Erode(singleTrue, 1, StructuringElementShape.Square, borderIsBackground: true)[0, 0]);
        Assert.True(BinaryMorphology.Erode(singleTrue, 0, StructuringElementShape.Disk, borderIsBackground: true)[0, 0]);
        Assert.False(BinaryMorphology.Erode(singleFalse, 1, StructuringElementShape.Disk, borderIsBackground: false)[0, 0]);
    }

    [Fact]
    public void NonSequentialMatrixView_ShouldMatchMaterializedMorphologyAndConnectedComponents()
    {
        SequentialMatrix<bool> source = TestMatrices.CenteredBox(12, 12, 2, 2, 5, 5, true, false);
        source[3, 3] = false;
        source[8, 8] = true;
        source[9, 8] = true;
        source[8, 9] = true;
        source[1, 10] = true;

        var view = new MatrixView<bool>(source, 1, 1, 10, 10, EdgeHandling.Extend);
        MaskMatrix materialized = view.ToMaskMatrix();

        foreach (StructuringElementShape shape in new[] { StructuringElementShape.Disk, StructuringElementShape.Square })
        {
            MatrixAssert.Equal(BinaryMorphology.Dilate(materialized, 2, shape, borderIsBackground: false), BinaryMorphology.Dilate(view, 2, shape, borderIsBackground: false));
            MatrixAssert.Equal(BinaryMorphology.Dilate(materialized, 2, shape, borderIsBackground: true), BinaryMorphology.Dilate(view, 2, shape, borderIsBackground: true));
            MatrixAssert.Equal(BinaryMorphology.Erode(materialized, 1, shape, borderIsBackground: false), BinaryMorphology.Erode(view, 1, shape, borderIsBackground: false));
            MatrixAssert.Equal(BinaryMorphology.Erode(materialized, 1, shape, borderIsBackground: true), BinaryMorphology.Erode(view, 1, shape, borderIsBackground: true));
            MatrixAssert.Equal(BinaryMorphology.Open(materialized, 1, shape), BinaryMorphology.Open(view, 1, shape));
            MatrixAssert.Equal(BinaryMorphology.Close(materialized, 1, shape), BinaryMorphology.Close(view, 1, shape));
        }

        foreach (ComponentRetentionPolicy policy in new[]
        {
            ComponentRetentionPolicy.MinimumAreaFraction,
            ComponentRetentionPolicy.LargestComponentOnly,
            ComponentRetentionPolicy.KeepAll
        })
        {
            MatrixAssert.Equal(
                BinaryMorphology.FilterConnectedComponents(materialized, policy, 0.02, GridConnectivity.EightConnected),
                BinaryMorphology.FilterConnectedComponents(view, policy, 0.02, GridConnectivity.EightConnected));
            MatrixAssert.Equal(
                BinaryMorphology.FilterConnectedComponents(materialized, policy, 0.02, GridConnectivity.FourConnected),
                BinaryMorphology.FilterConnectedComponents(view, policy, 0.02, GridConnectivity.FourConnected));
        }

        var emptyMask = new MaskMatrix(4, 4);
        var allFalseView = new MatrixView<bool>(emptyMask, 0, 0, 3, 3, EdgeHandling.Extend);
        MatrixAssert.Equal(new MaskMatrix(3, 3), BinaryMorphology.FilterConnectedComponents(allFalseView));
    }

    [Fact]
    public void FilterConnectedComponents_LargeGridWithStripSeams_ShouldStitchAllStripsAccurately()
    {
        const int width = 320;
        const int height = 256; // 81,920 pixels (> ParallelRunner threshold)
        var mask = new MaskMatrix(width, height);

        // Component A: Thin continuous 2px-wide vertical ribbon across ALL 256 rows (512 px).
        for (int y = 0; y < height; y++)
        {
            mask[20, y] = true;
            mask[21, y] = true;
        }

        // Component B: Diagonal staircase stepping 1px per row (256 px connected only under 8-connectivity).
        for (int y = 0; y < height; y++)
            mask[40 + y, y] = true;

        // Component C: Solid 10x10 block (100 px) safely away from A and B.
        for (int y = 100; y < 110; y++)
        {
            for (int x = 300; x < 310; x++)
                mask[x, y] = true;
        }

        // Under 8-connectivity and MinAreaFraction keeping >= 200 px, both A (512 px) and B (256 px) survive while C (100 px) is pruned.
        double fractionFor200 = 200.0 / (width * height);
        MaskMatrix eightKeepLarge = BinaryMorphology.FilterConnectedComponents(
            mask,
            ComponentRetentionPolicy.MinimumAreaFraction,
            fractionFor200,
            GridConnectivity.EightConnected);
        Assert.True(eightKeepLarge[20, 0]);
        Assert.True(eightKeepLarge[20, height - 1]);
        Assert.True(eightKeepLarge[40, 0]);
        Assert.True(eightKeepLarge[40 + height - 1, height - 1]);
        Assert.False(eightKeepLarge[305, 105]);

        // Under 4-connectivity, diagonal staircase B breaks into singletons (size 1) and is completely filtered out!
        MaskMatrix fourKeepLarge = BinaryMorphology.FilterConnectedComponents(
            mask,
            ComponentRetentionPolicy.MinimumAreaFraction,
            fractionFor200,
            GridConnectivity.FourConnected);
        Assert.True(fourKeepLarge[20, 0]);
        Assert.True(fourKeepLarge[20, height - 1]);
        Assert.False(fourKeepLarge[40, 0]);
        Assert.False(fourKeepLarge[40 + 128, 128]);
        Assert.False(fourKeepLarge[305, 105]);

        // LargestComponentOnly retains only the 512px vertical ribbon across all strips.
        MaskMatrix largest = BinaryMorphology.FilterConnectedComponents(
            mask,
            ComponentRetentionPolicy.LargestComponentOnly,
            connectivity: GridConnectivity.EightConnected);
        Assert.True(largest[20, 0]);
        Assert.True(largest[20, height - 1]);
        Assert.False(largest[40, 0]);
        Assert.False(largest[305, 105]);
    }
}
