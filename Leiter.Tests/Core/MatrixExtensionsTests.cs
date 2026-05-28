
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using System;
using System.Collections.Generic;

namespace Leiter.Tests.Core;

public class MatrixExtensionsTests
{
    private class DummyUntypedMatrix : IUntypedMatrix
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public Size Size => new(Width, Height);
        public int Count => Width * Height;
    }

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
}
