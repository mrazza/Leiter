
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using System;
using System.Collections.Generic;

namespace Leiter.Tests.Core;

public class MatrixViewTests
{
    [Fact]
    public void Constructor_AndProperties_ShouldInitialize()
    {
        var baseMat = new SequentialMatrix<DoublePixel>(4, 4);
        var view = new MatrixView<DoublePixel>(baseMat, 1, 1, 2, 2, EdgeHandling.EXTEND);

        Assert.Equal(baseMat, view.Matrix);
        Assert.Equal(1, view.OffsetX);
        Assert.Equal(1, view.OffsetY);
        Assert.Equal(2, view.Width);
        Assert.Equal(2, view.Height);
        Assert.Equal(EdgeHandling.EXTEND, view.EdgeHandling);
        Assert.Equal(new Size(2, 2), view.Size);
        Assert.Equal(4, view.Count);
    }

    [Fact]
    public void Indexers_AndGetElement_WithBoundaryHandling()
    {
        // Setup 3x3 base matrix:
        // [1, 2, 3]
        // [4, 5, 6]
        // [7, 8, 9]
        var baseMat = new SequentialMatrix<DoublePixel>(new Size(3, 3), new DoublePixel[] {
            1.0, 2.0, 3.0,
            4.0, 5.0, 6.0,
            7.0, 8.0, 9.0
        });

        // View at Offset (1,1) size 2x2.
        // This corresponds to:
        // [5, 6]
        // [8, 9]
        var view = new MatrixView<DoublePixel>(baseMat, 1, 1, 2, 2, EdgeHandling.EXTEND);

        Assert.Equal(5.0, view[0, 0].Value);
        Assert.Equal(6.0, view[1, 0].Value);
        Assert.Equal(8.0, view[0, 1].Value);
        Assert.Equal(9.0, view[1, 1].Value);

        // Test single indexer:
        Assert.Equal(5.0, view[0].Value);
        Assert.Equal(6.0, view[1].Value);
        Assert.Equal(8.0, view[2].Value);
        Assert.Equal(9.0, view[3].Value);

        // Test Coord indexer:
        Assert.Equal(5.0, view[new Coord(0, 0)].Value);

        // Test IndexOutOfRangeException:
        Assert.Throws<IndexOutOfRangeException>(() => view[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => view[4]);
    }

    [Fact]
    public void BoundaryHandling_Extend_ShouldClamp()
    {
        // Setup 2x2 matrix:
        // [1, 2]
        // [3, 4]
        var baseMat = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] {
            1.0, 2.0,
            3.0, 4.0
        });

        // Create a view with an offset that goes out of bounds (-1, -1) and size 4x4
        var view = new MatrixView<DoublePixel>(baseMat, -1, -1, 4, 4, EdgeHandling.EXTEND);

        // Coordinates in View and corresponding clamped coords in baseMat:
        // view[0, 0] -> offsets to (-1, -1) -> clamped to (0, 0) in base -> value 1.0
        // view[1, 0] -> offsets to (0, -1) -> clamped to (0, 0) in base -> value 1.0
        // view[2, 0] -> offsets to (1, -1) -> clamped to (1, 0) in base -> value 2.0
        // view[3, 0] -> offsets to (2, -1) -> clamped to (1, 0) in base -> value 2.0
        Assert.Equal(1.0, view[0, 0].Value);
        Assert.Equal(1.0, view[1, 0].Value);
        Assert.Equal(2.0, view[2, 0].Value);
        Assert.Equal(2.0, view[3, 0].Value);

        // view[0, 1] -> offsets to (-1, 0) -> clamped to (0, 0) in base -> value 1.0
        // view[1, 1] -> offsets to (0, 0) -> clamped to (0, 0) in base -> value 1.0
        // view[2, 1] -> offsets to (1, 0) -> clamped to (1, 0) in base -> value 2.0
        // view[3, 1] -> offsets to (2, 0) -> clamped to (1, 0) in base -> value 2.0
        Assert.Equal(1.0, view[0, 1].Value);
        Assert.Equal(1.0, view[1, 1].Value);
        Assert.Equal(2.0, view[2, 1].Value);
        Assert.Equal(2.0, view[3, 1].Value);

        // view[0, 2] -> offsets to (-1, 1) -> clamped to (0, 1) in base -> value 3.0
        // view[1, 2] -> offsets to (0, 1) -> clamped to (0, 1) in base -> value 3.0
        // view[2, 2] -> offsets to (1, 1) -> clamped to (1, 1) in base -> value 4.0
        // view[3, 2] -> offsets to (2, 1) -> clamped to (1, 1) in base -> value 4.0
        Assert.Equal(3.0, view[0, 2].Value);
        Assert.Equal(3.0, view[1, 2].Value);
        Assert.Equal(4.0, view[2, 2].Value);
        Assert.Equal(4.0, view[3, 2].Value);
    }

    [Fact]
    public void Enumerator_ShouldYieldCorrectElements()
    {
        var baseMat = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] {
            1.0, 2.0,
            3.0, 4.0
        });
        var view = new MatrixView<DoublePixel>(baseMat, 0, 0, 2, 2, EdgeHandling.EXTEND);

        var list = new List<double>();
        foreach (var p in view)
        {
            list.Add(p.Value);
        }

        Assert.Equal(new double[] { 1.0, 2.0, 3.0, 4.0 }, list);
    }

    [Fact]
    public void ToString_ShouldWork()
    {
        var baseMat = new SequentialMatrix<DoublePixel>(2, 2);
        var view = new MatrixView<DoublePixel>(baseMat, 0, 0, 2, 2, EdgeHandling.EXTEND);
        var str = view.ToString();
        Assert.Contains("MatrixView", str);
    }
}
