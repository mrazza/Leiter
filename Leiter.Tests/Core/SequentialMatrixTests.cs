
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using System;
using System.Collections.Generic;

namespace Leiter.Tests.Core;

/// <summary>
/// Provides unit tests or helpers for <see cref="SequentialMatrixTests" />.
/// </summary>
public class SequentialMatrixTests
{
    /// <summary>
    /// Verifies that the constructors should initialize correctly behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the constructor data mismatch should throw behaves correctly.
    /// </summary>
    [Fact]
    public void Constructor_DataMismatch_ShouldThrow()
    {
        var values = new DoublePixel[] { 1.0, 2.0 };
        Assert.Throws<ArgumentException>(() => new SequentialMatrix<DoublePixel>(new Size(2, 2), values));
    }

    /// <summary>
    /// Verifies that the indexer out of bounds should throw behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the set all should fill matrix behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the clone should create distinct copy behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the map should apply function behaves correctly.
    /// </summary>
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
    /// Verifies that the get enumerator should return all elements behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Verifies that the multiply should perform matrix multiplication behaves correctly.
    /// </summary>
    [Fact]
    public void Multiply_ShouldPerformMatrixMultiplication()
    {
        // 2x3 matrix:
        // [1, 2, 3]
        // [4, 5, 6]
        var m1 = new SequentialMatrix<DoublePixel>(new Size(3, 2), new DoublePixel[] {
            1.0, 2.0, 3.0,
            4.0, 5.0, 6.0
        });

        // 3x2 matrix:
        // [7, 8]
        // [9, 1]
        // [2, 3]
        var m2 = new SequentialMatrix<DoublePixel>(new Size(2, 3), new DoublePixel[] {
            7.0, 8.0,
            9.0, 1.0,
            2.0, 3.0
        });

        // Result: 2x2 matrix
        // [1*7 + 2*9 + 3*2,  1*8 + 2*1 + 3*3] = [7+18+6, 8+2+9] = [31, 19]
        // [4*7 + 5*9 + 6*2,  4*8 + 5*1 + 6*3] = [28+45+12, 32+5+18] = [85, 55]
        var result = m1.Multiply(m2);

        Assert.Equal(2, result.Width);
        Assert.Equal(2, result.Height);
        Assert.Equal(31.0, result[0, 0].Value);
        Assert.Equal(19.0, result[1, 0].Value);
        Assert.Equal(85.0, result[0, 1].Value);
        Assert.Equal(55.0, result[1, 1].Value);
    }

    /// <summary>
    /// Verifies that the multiply dimension mismatch should throw behaves correctly.
    /// </summary>
    [Fact]
    public void Multiply_DimensionMismatch_ShouldThrow()
    {
        var m1 = new SequentialMatrix<DoublePixel>(2, 2);
        var m2 = new SequentialMatrix<DoublePixel>(3, 3);
        Assert.Throws<ArgumentException>(() => m1.Multiply(m2));
    }

    /// <summary>
    /// Verifies that the to string should format correctly behaves correctly.
    /// </summary>
    [Fact]
    public void ToString_ShouldFormatCorrectly()
    {
        var m = new SequentialMatrix<DoublePixel>(2, 2);
        m[0] = 1.0;
        m[1] = 2.0;
        m[2] = 3.0;
        m[3] = 4.0;

        var str = m.ToString();
        Assert.Contains("SequentialMatrix", str);
        Assert.Contains("1", str);
    }

    /// <summary>
    /// Executes the test explicit interface implementations and to string operation.
    /// </summary>
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
}
