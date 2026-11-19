namespace Leiter.Tests.Core;

using System;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MaskMatrix"/>.
/// </summary>
public class MaskMatrixTests
{
    [Fact]
    public void Constructors_ShouldInitializeCorrectly()
    {
        var byDimensions = new MaskMatrix(3, 2);
        Assert.Equal(3, byDimensions.Width);
        Assert.Equal(2, byDimensions.Height);
        Assert.False(byDimensions[0, 0]);

        var bySize = new MaskMatrix(new Size(2, 3));
        Assert.Equal(new Size(2, 3), bySize.Size);
        Assert.Equal(6, bySize.Count);

        var grid = new bool[,]
        {
            { true, false },
            { false, true },
        };
        var byGrid = new MaskMatrix(grid);
        Assert.Equal(2, byGrid.Width);
        Assert.Equal(2, byGrid.Height);
        Assert.True(byGrid[0, 0]);
        Assert.False(byGrid[1, 0]);
        Assert.False(byGrid[0, 1]);
        Assert.True(byGrid[1, 1]);

        var byFlat = new MaskMatrix(new Size(2, 2), [false, true, true, false]);
        Assert.False(byFlat[0, 0]);
        Assert.True(byFlat[1, 0]);
        Assert.True(byFlat[0, 1]);
        Assert.False(byFlat[1, 1]);
    }

    [Fact]
    public void Clone_And_From_ShouldReturnIndependentMaskMatrix()
    {
        var original = new MaskMatrix(2, 2);
        original[0, 0] = true;
        original[1, 1] = true;

        MaskMatrix cloned = original.Clone();
        cloned[0, 0] = false;
        Assert.True(original[0, 0]);
        Assert.False(cloned[0, 0]);
        Assert.True(cloned[1, 1]);

        MaskMatrix fromMask = MaskMatrix.From(original);
        fromMask[1, 1] = false;
        Assert.True(original[1, 1]);
        Assert.False(fromMask[1, 1]);

        var view = new MatrixView<bool>(original, 0, 0, 2, 1, EdgeHandling.Extend);
        MaskMatrix materialized = MaskMatrix.From(view);
        Assert.Equal(new Size(2, 1), materialized.Size);
        Assert.True(materialized[0, 0]);
        Assert.False(materialized[1, 0]);

        Assert.Throws<ArgumentNullException>(() => MaskMatrix.From(null!));
    }

    [Fact]
    public void LogicalOperators_ShouldComputeComplementAndIntersectionAndUnion()
    {
        var left = new MaskMatrix(new Size(2, 2), [true, true, false, false]);
        var right = new MaskMatrix(new Size(2, 2), [false, true, true, false]);

        MaskMatrix complement = !left;
        Assert.False(complement[0, 0]);
        Assert.False(complement[1, 0]);
        Assert.True(complement[0, 1]);
        Assert.True(complement[1, 1]);

        MaskMatrix intersection = left & right;
        Assert.False(intersection[0, 0]);
        Assert.True(intersection[1, 0]);
        Assert.False(intersection[0, 1]);
        Assert.False(intersection[1, 1]);

        MaskMatrix union = left | right;
        Assert.True(union[0, 0]);
        Assert.True(union[1, 0]);
        Assert.True(union[0, 1]);
        Assert.False(union[1, 1]);

        var mismatched = new MaskMatrix(3, 2);
        Assert.Throws<ArgumentNullException>(() => !(MaskMatrix)null!);
        Assert.Throws<ArgumentNullException>(() => ((MaskMatrix)null!) & right);
        Assert.Throws<ArgumentNullException>(() => left & null!);
        Assert.Throws<ArgumentNullException>(() => ((MaskMatrix)null!) | right);
        Assert.Throws<ArgumentNullException>(() => left | null!);
        Assert.Throws<ArgumentException>(() => left & mismatched);
        Assert.Throws<ArgumentException>(() => left | mismatched);
    }
}
