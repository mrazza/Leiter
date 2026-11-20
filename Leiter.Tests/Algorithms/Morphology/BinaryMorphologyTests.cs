namespace Leiter.Tests.Algorithms.Morphology;

using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Core;
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

        for (int y = 0; y < 9; y++)
        {
            for (int x = 0; x < 9; x++)
            {
                int dx = x - 4;
                int dy = y - 4;
                bool expectedDisk = (dx * dx) + (dy * dy) <= 4;
                bool expectedSquare = Math.Max(Math.Abs(dx), Math.Abs(dy)) <= 2;

                Assert.Equal(expectedDisk, diskDilated[x, y]);
                Assert.Equal(expectedSquare, squareDilated[x, y]);
            }
        }

        MaskMatrix erodedBack = BinaryMorphology.Erode(diskDilated, 2, StructuringElementShape.Disk);
        Assert.True(erodedBack[4, 4]);
        Assert.False(erodedBack[4, 5]);
        Assert.False(erodedBack[5, 4]);
    }

    [Fact]
    public void Erode_BorderHandling_ShouldRespectBorderIsBackgroundSetting()
    {
        var block = new MaskMatrix(5, 5);
        block.SetAll(true);

        MaskMatrix clamped = BinaryMorphology.Erode(block, 1, StructuringElementShape.Square, borderIsBackground: false);
        for (int i = 0; i < clamped.Count; i++)
            Assert.True(clamped[i]);

        MaskMatrix zeroPadded = BinaryMorphology.Erode(block, 1, StructuringElementShape.Square, borderIsBackground: true);
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                bool expected = x >= 1 && x <= 3 && y >= 1 && y <= 3;
                Assert.Equal(expected, zeroPadded[x, y]);
            }
        }

        Assert.False(zeroPadded[0, 2]);
        Assert.False(zeroPadded[4, 2]);
    }

    [Fact]
    public void Open_And_Close_ShouldRemoveSpecksAndFillHoles()
    {
        var mask = new MaskMatrix(11, 11);
        for (int y = 2; y <= 8; y++)
        {
            for (int x = 2; x <= 8; x++)
                mask[x, y] = true;
        }

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
        var mask = new MaskMatrix(10, 10);
        for (int y = 1; y <= 4; y++)
        {
            for (int x = 1; x <= 4; x++)
                mask[x, y] = true;
        }

        for (int y = 7; y <= 8; y++)
        {
            for (int x = 7; x <= 8; x++)
                mask[x, y] = true;
        }

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
        var mask = new MaskMatrix(5, 5);
        mask.SetAll(true);
        // Clearing (0, 0) removes a corner outside the radius-2 disk centered at (2, 2),
        // distinguishing Disk from Square while verifying borderIsBackground: true behavior.
        mask[0, 0] = false;

        MaskMatrix erodedDisk = BinaryMorphology.Erode(
            mask,
            radius: 2,
            shape: StructuringElementShape.Disk,
            borderIsBackground: true);

        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                bool expected = x == 2 && y == 2;
                Assert.Equal(expected, erodedDisk[x, y]);
            }
        }
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
}
