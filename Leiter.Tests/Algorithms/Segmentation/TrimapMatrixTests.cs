namespace Leiter.Tests.Algorithms.Segmentation;

using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="TrimapMatrix"/>.
/// </summary>
public class TrimapMatrixTests
{
    [Fact]
    public void Constructors_ShouldInitializeDefaultsToBackground()
    {
        var byDimensions = new TrimapMatrix(3, 2);
        Assert.Equal(3, byDimensions.Width);
        Assert.Equal(2, byDimensions.Height);
        for (int i = 0; i < byDimensions.Count; i++)
            Assert.Equal(TrimapLabel.Background, byDimensions[i]);

        var bySize = new TrimapMatrix(new Size(2, 2));
        Assert.Equal(new Size(2, 2), bySize.Size);
        Assert.Equal(TrimapLabel.Background, bySize[1, 1]);

        var grid = new TrimapLabel[,]
        {
            { TrimapLabel.Foreground, TrimapLabel.ProbableForeground },
            { TrimapLabel.ProbableBackground, TrimapLabel.Background },
        };
        var byGrid = new TrimapMatrix(grid);
        Assert.Equal(TrimapLabel.Foreground, byGrid[0, 0]);
        Assert.Equal(TrimapLabel.ProbableForeground, byGrid[1, 0]);
        Assert.Equal(TrimapLabel.ProbableBackground, byGrid[0, 1]);
        Assert.Equal(TrimapLabel.Background, byGrid[1, 1]);

        var byFlat = new TrimapMatrix(
            new Size(2, 2),
            [TrimapLabel.Background, TrimapLabel.Foreground, TrimapLabel.ProbableBackground, TrimapLabel.ProbableForeground]);
        Assert.Equal(TrimapLabel.Background, byFlat[0, 0]);
        Assert.Equal(TrimapLabel.Foreground, byFlat[1, 0]);
        Assert.Equal(TrimapLabel.ProbableBackground, byFlat[0, 1]);
        Assert.Equal(TrimapLabel.ProbableForeground, byFlat[1, 1]);
    }

    [Fact]
    public void Clone_And_From_ShouldReturnIndependentTrimapMatrix()
    {
        var original = new TrimapMatrix(2, 2);
        original[0, 0] = TrimapLabel.Foreground;
        original[1, 1] = TrimapLabel.ProbableBackground;

        TrimapMatrix cloned = original.Clone();
        cloned[0, 0] = TrimapLabel.Background;
        Assert.Equal(TrimapLabel.Foreground, original[0, 0]);
        Assert.Equal(TrimapLabel.Background, cloned[0, 0]);
        Assert.Equal(TrimapLabel.ProbableBackground, cloned[1, 1]);

        TrimapMatrix copied = TrimapMatrix.From(original);
        copied[1, 1] = TrimapLabel.Foreground;
        Assert.Equal(TrimapLabel.ProbableBackground, original[1, 1]);
        Assert.Equal(TrimapLabel.Foreground, copied[1, 1]);

        var view = new MatrixView<TrimapLabel>(original, 0, 0, 2, 1, EdgeHandling.Extend);
        TrimapMatrix materialized = TrimapMatrix.From(view);
        Assert.Equal(new Size(2, 1), materialized.Size);
        Assert.Equal(TrimapLabel.Foreground, materialized[0, 0]);
        Assert.Equal(TrimapLabel.Background, materialized[1, 0]);

        Assert.Throws<ArgumentNullException>(() => TrimapMatrix.From(null!));
    }

    [Fact]
    public void FromBoundingBox_ShouldClipRectanglesAndValidateArguments()
    {
        TrimapMatrix interior = TrimapMatrix.FromBoundingBox(new Size(6, 5), new Coord(1, 1), new Size(3, 2));
        for (int y = 0; y < interior.Height; y++)
        {
            for (int x = 0; x < interior.Width; x++)
            {
                TrimapLabel expected = (x >= 1 && x < 4 && y >= 1 && y < 3)
                    ? TrimapLabel.ProbableForeground
                    : TrimapLabel.Background;
                Assert.Equal(expected, interior[x, y]);
            }
        }

        // Clipped partially outside image bounds.
        TrimapMatrix clipped = TrimapMatrix.FromBoundingBox(new Size(5, 5), new Coord(-2, 3), new Size(4, 5));
        Assert.Equal(TrimapLabel.ProbableForeground, clipped[0, 3]);
        Assert.Equal(TrimapLabel.ProbableForeground, clipped[1, 4]);
        Assert.Equal(TrimapLabel.Background, clipped[2, 3]);
        Assert.Equal(TrimapLabel.Background, clipped[0, 2]);

        // Empty image sizes return empty TrimapMatrix without error.
        Assert.Empty(TrimapMatrix.FromBoundingBox(new Size(0, 0), new Coord(0, 0), new Size(2, 2)));
        Assert.Empty(TrimapMatrix.FromBoundingBox(new Size(0, 4), new Coord(0, 0), new Size(2, 2)));
        Assert.Empty(TrimapMatrix.FromBoundingBox(new Size(4, 0), new Coord(0, 0), new Size(2, 2)));

        // Validation errors.
        Assert.Throws<ArgumentOutOfRangeException>(() => TrimapMatrix.FromBoundingBox(new Size(-1, 4), new Coord(0, 0), new Size(2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrimapMatrix.FromBoundingBox(new Size(4, -1), new Coord(0, 0), new Size(2, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrimapMatrix.FromBoundingBox(new Size(4, 4), new Coord(0, 0), new Size(0, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrimapMatrix.FromBoundingBox(new Size(4, 4), new Coord(0, 0), new Size(2, 0)));

        // Non-overlapping rectangles (on both axes).
        Assert.Throws<ArgumentException>(() => TrimapMatrix.FromBoundingBox(new Size(4, 4), new Coord(4, 1), new Size(2, 2)));
        Assert.Throws<ArgumentException>(() => TrimapMatrix.FromBoundingBox(new Size(4, 4), new Coord(-3, 1), new Size(2, 2)));
        Assert.Throws<ArgumentException>(() => TrimapMatrix.FromBoundingBox(new Size(4, 4), new Coord(1, 4), new Size(2, 2)));
        Assert.Throws<ArgumentException>(() => TrimapMatrix.FromBoundingBox(new Size(4, 4), new Coord(1, -3), new Size(2, 2)));

        // Full-image coverage leaving zero background pixels.
        Assert.Throws<ArgumentException>(() => TrimapMatrix.FromBoundingBox(new Size(4, 4), new Coord(0, 0), new Size(4, 4)));
        Assert.Throws<ArgumentException>(() => TrimapMatrix.FromBoundingBox(new Size(4, 4), new Coord(-1, -1), new Size(10, 10)));
    }

    [Fact]
    public void FromCandidateMask_ShouldMapLabelsAndValidateArguments()
    {
        var mask = new MaskMatrix(3, 2);
        mask[0, 0] = true;
        mask[2, 1] = true;

        TrimapMatrix defaultMapped = TrimapMatrix.FromCandidateMask(mask);
        Assert.Equal(TrimapLabel.ProbableForeground, defaultMapped[0, 0]);
        Assert.Equal(TrimapLabel.Background, defaultMapped[1, 0]);
        Assert.Equal(TrimapLabel.ProbableForeground, defaultMapped[2, 1]);

        TrimapMatrix customMapped = TrimapMatrix.FromCandidateMask(
            mask,
            foregroundLabel: TrimapLabel.Foreground,
            backgroundLabel: TrimapLabel.ProbableBackground);
        Assert.Equal(TrimapLabel.Foreground, customMapped[0, 0]);
        Assert.Equal(TrimapLabel.ProbableBackground, customMapped[1, 0]);

        Assert.Empty(TrimapMatrix.FromCandidateMask(new MaskMatrix(0, 0)));

        Assert.Throws<ArgumentNullException>(() => TrimapMatrix.FromCandidateMask(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrimapMatrix.FromCandidateMask(mask, foregroundLabel: (TrimapLabel)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => TrimapMatrix.FromCandidateMask(mask, backgroundLabel: (TrimapLabel)99));
    }

    [Fact]
    public void FromConfidenceBands_ShouldClassifyAllFourRegionsAndValidateArguments()
    {
        var current = new MaskMatrix(4, 1);
        current[0, 0] = true;
        current[1, 0] = true;

        var sureForeground = new MaskMatrix(4, 1);
        sureForeground[0, 0] = true;

        var possibleRegion = new MaskMatrix(4, 1);
        possibleRegion[0, 0] = true;
        possibleRegion[1, 0] = true;
        possibleRegion[2, 0] = true;

        TrimapMatrix trimap = TrimapMatrix.FromConfidenceBands(current, sureForeground, possibleRegion);
        Assert.Equal(TrimapLabel.Foreground, trimap[0, 0]);
        Assert.Equal(TrimapLabel.ProbableForeground, trimap[1, 0]);
        Assert.Equal(TrimapLabel.ProbableBackground, trimap[2, 0]);
        Assert.Equal(TrimapLabel.Background, trimap[3, 0]);

        var empty = new MaskMatrix(0, 0);
        Assert.Empty(TrimapMatrix.FromConfidenceBands(empty, empty, empty));

        var wrongSize = new MaskMatrix(2, 2);
        Assert.Throws<ArgumentNullException>(() => TrimapMatrix.FromConfidenceBands(null!, sureForeground, possibleRegion));
        Assert.Throws<ArgumentNullException>(() => TrimapMatrix.FromConfidenceBands(current, null!, possibleRegion));
        Assert.Throws<ArgumentNullException>(() => TrimapMatrix.FromConfidenceBands(current, sureForeground, null!));
        Assert.Throws<ArgumentException>(() => TrimapMatrix.FromConfidenceBands(current, wrongSize, possibleRegion));
        Assert.Throws<ArgumentException>(() => TrimapMatrix.FromConfidenceBands(current, sureForeground, wrongSize));
    }
}
