namespace Leiter.Tests.Algorithms.Morphology;

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
        var view = new MatrixView<bool>(mixed, 0, 0, 2, 2, EdgeHandling.EXTEND);
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
}
