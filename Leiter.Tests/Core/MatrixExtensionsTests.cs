namespace Leiter.Tests.Core;

using System;
using System.Collections.Generic;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MatrixExtensions" />.
/// </summary>
public class MatrixExtensionsTests
{
    [Fact]
    public void ToSequentialMatrix_ShouldPopulateCorrectly()
    {
        var source = new List<DoublePixel> { 1.0, 2.0, 3.0, 4.0 };
        var matrix = source.ToSequentialMatrix(2, 2);

        Assert.Equal(2, matrix.Width);
        Assert.Equal(2, matrix.Height);
        Assert.Equal(1.0, matrix[0, 0].Value);
        Assert.Equal(2.0, matrix[1, 0].Value);
        Assert.Equal(3.0, matrix[0, 1].Value);
        Assert.Equal(4.0, matrix[1, 1].Value);
    }

    [Fact]
    public void ToSequentialMatrix_TooSmallSource_ShouldThrow()
    {
        var source = new List<DoublePixel> { 1.0, 2.0, 3.0 };
        Assert.Throws<ArgumentException>(() => source.ToSequentialMatrix(2, 2));
    }

    [Fact]
    public void CoordFromIndex_ShouldComputeCorrectly()
    {
        var matrix = new DummyUntypedMatrix { Width = 5, Height = 5 };

        var c0 = matrix.CoordFromIndex(0);
        Assert.Equal(new Coord(0, 0), c0);

        var c7 = matrix.CoordFromIndex(7);
        Assert.Equal(new Coord(2, 1), c7);
    }

    [Fact]
    public void IndexFromCoord_ShouldComputeCorrectly()
    {
        var matrix = new DummyUntypedMatrix { Width = 5, Height = 5 };

        var index = matrix.IndexFromCoord(new Coord(2, 1));
        Assert.Equal(7, index);
    }

    [Fact]
    public void NormalizeAndDenormalizeCoord_ShouldWork()
    {
        var matrix = new DummyUntypedMatrix { Width = 10, Height = 20 };

        var coord = new Coord(5, 10);
        var norm = matrix.NormalizeCoord(coord);
        Assert.Equal(0.5, norm.X);
        Assert.Equal(0.5, norm.Y);

        var denorm = matrix.DenormalizeCoord(norm);
        Assert.Equal(coord, denorm);
    }

    [Fact]
    public void Map_OnConcreteMatrix_ShouldTransformElements()
    {
        var matrix = new SequentialMatrix<DoublePixel>(2, 2);
        matrix[0, 0] = 1.0;
        matrix[1, 0] = 2.0;
        matrix[0, 1] = 3.0;
        matrix[1, 1] = 4.0;

        Matrix<bool> mapped = matrix.Map(p => p.Value >= 3.0);
        Assert.Equal(2, mapped.Width);
        Assert.Equal(2, mapped.Height);
        Assert.False(mapped[0, 0]);
        Assert.False(mapped[1, 0]);
        Assert.True(mapped[0, 1]);
        Assert.True(mapped[1, 1]);
    }

    [Fact]
    public void Map_OnNonMatrixReadOnlyMatrix_ShouldTransformElements()
    {
        IReadOnlyMatrix<DoublePixel> dummy = new DummyReadOnlyMatrix();
        Matrix<bool> mapped = dummy.Map(p => p.Value > 0.5);

        Assert.Equal(2, mapped.Width);
        Assert.Equal(2, mapped.Height);
        Assert.False(mapped[0, 0]);
        Assert.True(mapped[1, 0]);
        Assert.True(mapped[0, 1]);
        Assert.True(mapped[1, 1]);
    }

    [Fact]
    public void Map_NullArguments_ShouldThrowArgumentNullException()
    {
        IReadOnlyMatrix<DoublePixel> dummy = new DummyReadOnlyMatrix();

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).Map(p => p.Value > 0.0));
        Assert.Throws<ArgumentNullException>(() => dummy.Map<DoublePixel, bool>(null!));
    }
}
