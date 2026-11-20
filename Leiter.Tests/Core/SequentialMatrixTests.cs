namespace Leiter.Tests.Core;

using System;
using System.Collections.Generic;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SequentialMatrix{T}" />.
/// </summary>
public class SequentialMatrixTests
{
    [Fact]
    public void Constructors_ShouldInitializeCorrectly()
    {
        // 1. Width/Height constructor
        var m1 = new SequentialMatrix<DoublePixel>(3, 4);
        Assert.Equal(3, m1.Width);
        Assert.Equal(4, m1.Height);
        Assert.Equal(12, m1.Count);
        Assert.Equal(new Size(3, 4), m1.Size);
        Assert.Equal(0.0, m1[0].Value);

        // 2. Size constructor
        var m2 = new SequentialMatrix<DoublePixel>(new Size(2, 3));
        Assert.Equal(2, m2.Width);
        Assert.Equal(3, m2.Height);

        // 3. T[,] constructor
        var values = new DoublePixel[,] {
            { 1.0, 2.0 },
            { 3.0, 4.0 }
        };
        var m3 = new SequentialMatrix<DoublePixel>(values);
        Assert.Equal(2, m3.Width);
        Assert.Equal(2, m3.Height);
        Assert.Equal(1.0, m3[0, 0].Value);
        Assert.Equal(2.0, m3[1, 0].Value);
        Assert.Equal(3.0, m3[0, 1].Value);
        Assert.Equal(4.0, m3[1, 1].Value);

        // 4. Size and T[] constructor
        var m4 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 10.0, 20.0, 30.0, 40.0 });
        Assert.Equal(10.0, m4[0].Value);
        Assert.Equal(40.0, m4[3].Value);
    }

    [Fact]
    public void Constructor_DataMismatch_ShouldThrow()
    {
        var values = new DoublePixel[] { 1.0, 2.0 };
        ArgumentException ex2 = Assert.Throws<ArgumentException>(() => new SequentialMatrix<DoublePixel>(new Size(2, 2), values));
        Assert.Equal("values", ex2.ParamName);

        ArgumentException ex3 = Assert.Throws<ArgumentException>(() => new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[3]));
        Assert.Equal("values", ex3.ParamName);

        ArgumentNullException nullEx = Assert.Throws<ArgumentNullException>(() => new SequentialMatrix<DoublePixel>(new Size(2, 2), null!));
        Assert.Equal("values", nullEx.ParamName);

        ArgumentOutOfRangeException negW = Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<DoublePixel>(-1, 2));
        Assert.Equal("width", negW.ParamName);

        ArgumentOutOfRangeException negH = Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<DoublePixel>(2, -1));
        Assert.Equal("height", negH.ParamName);

        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<DoublePixel>(new Size(-1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<DoublePixel>(new Size(2, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<DoublePixel>(new Size(-1, 2), values));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<DoublePixel>(new Size(2, -1), values));
    }

    [Fact]
    public void Indexer_OutOfBounds_ShouldThrow()
    {
        var matrix = new SequentialMatrix<DoublePixel>(2, 2);

        Assert.Throws<IndexOutOfRangeException>(() => matrix[-1]);
        Assert.Throws<IndexOutOfRangeException>(() => matrix[4]);

        Assert.Throws<IndexOutOfRangeException>(() => matrix[-1, 0]);
        Assert.Throws<IndexOutOfRangeException>(() => matrix[2, 0]);
        Assert.Throws<IndexOutOfRangeException>(() => matrix[0, -1]);
        Assert.Throws<IndexOutOfRangeException>(() => matrix[0, 2]);

        Assert.Throws<IndexOutOfRangeException>(() => matrix[-1] = 1.0);
        Assert.Throws<IndexOutOfRangeException>(() => matrix[4] = 1.0);

        Assert.Throws<IndexOutOfRangeException>(() => matrix[-1, 0] = 1.0);
        Assert.Throws<IndexOutOfRangeException>(() => matrix[2, 0] = 1.0);
    }

    [Fact]
    public void SetAll_ShouldFillMatrix()
    {
        var m = new SequentialMatrix<DoublePixel>(2, 2);
        m.SetAll(5.0);
        Assert.Equal(5.0, m[0].Value);
        Assert.Equal(5.0, m[1].Value);
        Assert.Equal(5.0, m[2].Value);
        Assert.Equal(5.0, m[3].Value);
    }

    [Fact]
    public void Clone_ShouldCreateDistinctCopy()
    {
        var m = new SequentialMatrix<DoublePixel>(2, 2);
        m[0] = 10.0;
        var clone = m.Clone();
        Assert.Equal(10.0, clone[0].Value);

        // Modifying clone shouldn't modify original
        clone[0] = 20.0;
        Assert.Equal(10.0, m[0].Value);
    }

    [Fact]
    public void Map_ShouldApplyFunction()
    {
        var m = new SequentialMatrix<DoublePixel>(2, 2);
        m[0] = 2.0;
        m[1] = 3.0;
        m[2] = 4.0;
        m[3] = 5.0;

        var result = m.Map(p => new DoublePixel(p.Value * 2));
        Assert.Equal(4.0, result[0].Value);
        Assert.Equal(10.0, result[3].Value);
    }

    /// <summary>
    /// Verifies that mapping a large matrix preserves exact element ordering.
    /// </summary>
    [Fact]
    public void Map_LargeMatrix_ShouldPreserveExactElementOrdering()
    {
        const int width = 100;
        const int height = 100;
        var m = new SequentialMatrix<DoublePixel>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                m[x, y] = x + (y * width);
            }
        }

        var result = m.Map(p => new DoublePixel(p.Value * 3 + 1));

        Assert.Equal(width, result.Width);
        Assert.Equal(height, result.Height);
        for (int i = 0; i < width * height; i++)
        {
            double expected = (i * 3) + 1;
            Assert.Equal(expected, result[i].Value);
        }
    }

    [Fact]
    public void GetEnumerator_ShouldReturnAllElements()
    {
        var m = new SequentialMatrix<DoublePixel>(2, 2);
        m[0] = 1.0;
        m[1] = 2.0;
        m[2] = 3.0;
        m[3] = 4.0;

        var list = new List<double>();
        foreach (var p in m)
        {
            list.Add(p.Value);
        }
        Assert.Equal(new double[] { 1.0, 2.0, 3.0, 4.0 }, list);
    }

    [Fact]
    public void SequentialMatrix_ArbitraryValueTypes_ShouldWork()
    {
        var boolMask = new SequentialMatrix<bool>(2, 2);
        boolMask[0, 0] = true;
        boolMask[1, 1] = true;

        Matrix<int> intGrid = boolMask.Map(static flag => flag ? 42 : -1);
        Assert.Equal(42, intGrid[0, 0]);
        Assert.Equal(-1, intGrid[1, 0]);
        Assert.Equal(42, intGrid[1, 1]);
    }

    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        var m = new SequentialMatrix<DoublePixel>(2, 2);
        m[0] = 1.0;
        m[1] = 2.0;
        m[2] = 3.0;
        m[3] = 4.0;

        var str = m.ToString();
        Assert.NotNull(str);
        Assert.Contains("SequentialMatrix", str);
        Assert.Contains("1", str);
    }

    [Fact]
    public void TestExplicitInterfaceImplementations_AndToString()
    {
        var m = new SequentialMatrix<DoublePixel>(2, 2);
        m.SetAll(1.0);

        // Test explicit interface implementation on IReadOnlyMatrix/IUntypedMatrix
        IReadOnlyMatrix<DoublePixel> rom = m;
        IUntypedMatrix utm = rom;
        IReadOnlyCollection<DoublePixel> roc = rom;

        Assert.Equal(4, utm.Count);
        Assert.Equal(4, roc.Count);

        // Test index setter on Matrix
        m[0] = 5.0;
        Assert.Equal(5.0, m[0].Value);

        m[1, 0] = 6.0;
        Assert.Equal(6.0, m[1, 0].Value);

        m[new Coord(0, 1)] = 7.0;
        Assert.Equal(7.0, m[0, 1].Value);

        // Test IEnumerable.GetEnumerator on Matrix
        var enumerable = (System.Collections.IEnumerable)m;
        var enumerator = enumerable.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);
    }

    [Fact]
    public void From_AndAsReadOnlySpan_ShouldCopyAndViewCorrectly()
    {
        var original = new SequentialMatrix<DoublePixel>(2, 2);
        original[0, 0] = 1.0;
        original[1, 0] = 2.0;
        original[0, 1] = 3.0;
        original[1, 1] = 4.0;

        ReadOnlySpan<DoublePixel> span = original.AsReadOnlySpan();
        Assert.Equal(4, span.Length);
        Assert.Equal(2.0, span[1].Value);
        Assert.Equal(3.0, span[2].Value);

        Span<DoublePixel> writableSpan = original.AsSpan();
        writableSpan[1] = 42.0;
        Assert.Equal(42.0, original[1, 0].Value);
        writableSpan[1] = 2.0;

        Assert.True(new SequentialMatrix<DoublePixel>(0, 0).AsReadOnlySpan().IsEmpty);

        SequentialMatrix<DoublePixel> copied = SequentialMatrix<DoublePixel>.From(original);
        copied[0, 0] = 99.0;
        Assert.Equal(1.0, original[0, 0].Value);
        Assert.Equal(99.0, copied[0, 0].Value);

        var view = new MatrixView<DoublePixel>(original, 1, 0, 2, 2, EdgeHandling.Extend);
        SequentialMatrix<DoublePixel> fromView = SequentialMatrix<DoublePixel>.From(view);
        Assert.Equal(new Size(2, 2), fromView.Size);
        Assert.Equal(2.0, fromView[0, 0].Value);
        Assert.Equal(2.0, fromView[1, 0].Value);
        Assert.Equal(4.0, fromView[0, 1].Value);
        Assert.Equal(4.0, fromView[1, 1].Value);

        SequentialMatrix<DoublePixel> empty = SequentialMatrix<DoublePixel>.From(new SequentialMatrix<DoublePixel>(0, 0));
        Assert.Empty(empty);

        Assert.Throws<ArgumentNullException>(() => SequentialMatrix<DoublePixel>.From(null!));
    }
}
