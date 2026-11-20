
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using System;
using System.Collections.Generic;

namespace Leiter.Tests.Core;

/// <summary>
/// Provides unit tests for <see cref="MatrixView{T}" />.
/// </summary>
public class MatrixViewTests
{
    [Fact]
    public void Constructor_AndProperties_ShouldInitialize()
    {
        var baseMat = new SequentialMatrix<DoublePixel>(4, 4);
        var view = new MatrixView<DoublePixel>(baseMat, 1, 1, 2, 2, EdgeHandling.Extend);

        Assert.Equal(baseMat, view.Matrix);
        Assert.Equal(1, view.OffsetX);
        Assert.Equal(1, view.OffsetY);
        Assert.Equal(2, view.Width);
        Assert.Equal(2, view.Height);
        Assert.Equal(EdgeHandling.Extend, view.EdgeHandling);
        Assert.Equal(new Size(2, 2), view.Size);
        Assert.Equal(4, view.Count);
    }

    [Fact]
    public void Constructor_InvalidArguments_ShouldThrow()
    {
        var baseMat = new SequentialMatrix<DoublePixel>(2, 2);

        ArgumentNullException nullEx = Assert.Throws<ArgumentNullException>(() => new MatrixView<DoublePixel>(null!, 0, 0, 2, 2, EdgeHandling.Extend));
        Assert.Equal("matrix", nullEx.ParamName);

        ArgumentOutOfRangeException enumEx = Assert.Throws<ArgumentOutOfRangeException>(() => new MatrixView<DoublePixel>(baseMat, 0, 0, 2, 2, (EdgeHandling)999));
        Assert.Equal("edgeHandling", enumEx.ParamName);

        ArgumentOutOfRangeException negWidth = Assert.Throws<ArgumentOutOfRangeException>(() => new MatrixView<DoublePixel>(baseMat, 0, 0, -1, 2, EdgeHandling.Extend));
        Assert.Equal("width", negWidth.ParamName);

        ArgumentOutOfRangeException negHeight = Assert.Throws<ArgumentOutOfRangeException>(() => new MatrixView<DoublePixel>(baseMat, 0, 0, 2, -1, EdgeHandling.Extend));
        Assert.Equal("height", negHeight.ParamName);

        // Negative offsets with non-negative dimensions must remain valid
        baseMat[0, 0] = 7.0;
        var negativeOffsetView = new MatrixView<DoublePixel>(baseMat, -2, -3, 2, 2, EdgeHandling.Extend);
        Assert.Equal(-2, negativeOffsetView.OffsetX);
        Assert.Equal(-3, negativeOffsetView.OffsetY);
        Assert.Equal(7.0, negativeOffsetView[0, 0].Value);

        var emptyView = new MatrixView<DoublePixel>(new SequentialMatrix<DoublePixel>(0, 0), 0, 0, 1, 1, EdgeHandling.Extend);
        Assert.Throws<ArgumentOutOfRangeException>(() => emptyView[0, 0]);
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
        var view = new MatrixView<DoublePixel>(baseMat, 1, 1, 2, 2, EdgeHandling.Extend);

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
        var view = new MatrixView<DoublePixel>(baseMat, -1, -1, 4, 4, EdgeHandling.Extend);

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
        var view = new MatrixView<DoublePixel>(baseMat, 0, 0, 2, 2, EdgeHandling.Extend);

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
        var view = new MatrixView<DoublePixel>(baseMat, 0, 0, 2, 2, EdgeHandling.Extend);
        var str = view.ToString();
        Assert.Contains("MatrixView", str);
    }

    [Fact]
    public void TestMatrixView_UncoveredLines()
    {
        var baseMat = new SequentialMatrix<DoublePixel>(2, 2);
        var view = new MatrixView<DoublePixel>(baseMat, 0, 0, 2, 2, EdgeHandling.Extend);

        // Call ToString
        Assert.NotNull(view.ToString());

        // Call IEnumerable.GetEnumerator on MatrixView
        var enumerable = (System.Collections.IEnumerable)view;
        var enumerator = enumerable.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);
    }
}
