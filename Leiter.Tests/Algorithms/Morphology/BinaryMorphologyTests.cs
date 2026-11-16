namespace Leiter.Tests.Algorithms.Morphology;

using Leiter.Algorithms.Morphology;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="BinaryMorphology"/>.
/// </summary>
public class BinaryMorphologyTests
{
    [Fact]
    public void Dilate_And_Erode_DiskAndSquare_ShouldMatchExactDefinition()
    {
        var impulse = new SequentialMatrix<LongPixel>(9, 9);
        impulse[4, 4] = 5L;

        Matrix<LongPixel> diskDilated = BinaryMorphology.Dilate(impulse, 2, StructuringElementShape.Disk);
        Matrix<LongPixel> squareDilated = BinaryMorphology.Dilate(impulse, 2, StructuringElementShape.Square);

        for (int y = 0; y < 9; y++)
        {
            for (int x = 0; x < 9; x++)
            {
                int dx = x - 4;
                int dy = y - 4;
                long expectedDisk = (dx * dx) + (dy * dy) <= 4 ? 1L : 0L;
                long expectedSquare = Math.Max(Math.Abs(dx), Math.Abs(dy)) <= 2 ? 1L : 0L;

                Assert.Equal(expectedDisk, diskDilated[x, y].Value);
                Assert.Equal(expectedSquare, squareDilated[x, y].Value);
            }
        }

        Matrix<LongPixel> erodedBack = BinaryMorphology.Erode(diskDilated, 2, StructuringElementShape.Disk);
        Assert.Equal(1L, erodedBack[4, 4].Value);
        Assert.Equal(0L, erodedBack[4, 5].Value);
        Assert.Equal(0L, erodedBack[5, 4].Value);
    }

    [Fact]
    public void Erode_BorderHandling_ShouldRespectBorderIsBackgroundSetting()
    {
        var block = new SequentialMatrix<LongPixel>(5, 5);
        block.SetAll(1L);

        Matrix<LongPixel> clamped = BinaryMorphology.Erode(block, 1, StructuringElementShape.Square, borderIsBackground: false);
        for (int i = 0; i < clamped.Count; i++)
            Assert.Equal(1L, clamped[i].Value);

        Matrix<LongPixel> zeroPadded = BinaryMorphology.Erode(block, 1, StructuringElementShape.Square, borderIsBackground: true);
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                long expected = x >= 1 && x <= 3 && y >= 1 && y <= 3 ? 1L : 0L;
                Assert.Equal(expected, zeroPadded[x, y].Value);
            }
        }

        // Also test column border trigger inside borderIsBackground loop when row is in bounds.
        Assert.Equal(0L, zeroPadded[0, 2].Value);
        Assert.Equal(0L, zeroPadded[4, 2].Value);
    }

    [Fact]
    public void Open_And_Close_ShouldRemoveSpecksAndFillHoles()
    {
        var mask = new SequentialMatrix<LongPixel>(11, 11);
        for (int y = 2; y <= 8; y++)
        {
            for (int x = 2; x <= 8; x++)
                mask[x, y] = 1L;
        }

        // Isolated background hole inside foreground block and isolated speck outside.
        mask[5, 5] = 0L;
        mask[0, 0] = 1L;

        Matrix<LongPixel> opened = BinaryMorphology.Open(mask, 1, StructuringElementShape.Square);
        Assert.Equal(0L, opened[0, 0].Value);
        Assert.Equal(1L, opened[3, 3].Value);

        Matrix<LongPixel> closed = BinaryMorphology.Close(mask, 1, StructuringElementShape.Disk);
        Assert.Equal(1L, closed[5, 5].Value);
    }

    [Fact]
    public void ZeroRadius_EmptyMatrix_AndInvalidArgs_ShouldBeHandledCleanly()
    {
        var mixed = new SequentialMatrix<LongPixel>(3, 2);
        mixed[0, 0] = 9L;
        mixed[2, 1] = -3L;

        Matrix<LongPixel> zeroDilate = BinaryMorphology.Dilate(mixed, 0);
        Matrix<LongPixel> zeroErode = BinaryMorphology.Erode(mixed, 0);
        Assert.Equal(1L, zeroDilate[0, 0].Value);
        Assert.Equal(1L, zeroDilate[2, 1].Value);
        Assert.Equal(0L, zeroDilate[1, 0].Value);
        Assert.Equal(1L, zeroErode[0, 0].Value);

        var empty = new SequentialMatrix<LongPixel>(0, 0);
        Assert.Empty(BinaryMorphology.Dilate(empty, 2));
        Assert.Empty(BinaryMorphology.Erode(empty, 2));

        Assert.Throws<ArgumentNullException>(() => BinaryMorphology.Dilate(null!, 1));
        Assert.Throws<ArgumentNullException>(() => BinaryMorphology.Erode(null!, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMorphology.Dilate(mixed, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMorphology.Erode(mixed, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BinaryMorphology.Dilate(mixed, 1, (StructuringElementShape)999));
    }
}
