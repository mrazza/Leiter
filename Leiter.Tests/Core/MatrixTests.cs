namespace Leiter.Tests.Core;

using System;
using System.Collections;
using System.Collections.Generic;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Matrix{T}"/>.
/// </summary>
public class MatrixTests
{
    [Fact]
    public void Properties_ShouldReturnExpectedDimensions()
    {
        var matrix = new MinimalMatrix(3, 2);
        Assert.Equal(3, matrix.Width);
        Assert.Equal(2, matrix.Height);
        Assert.Equal(new Size(3, 2), matrix.Size);
        Assert.Equal(6, matrix.Count);
    }

    [Fact]
    public void Indexers_GetAndSet_ShouldAccessUnderlyingData()
    {
        var matrix = new MinimalMatrix(2, 2);

        // Linear indexer setter and getter
        matrix[0] = 10.0;
        Assert.Equal(10.0, matrix[0].Value);

        // 2D coordinate indexer setter and getter
        matrix[1, 0] = 20.0;
        Assert.Equal(20.0, matrix[1, 0].Value);

        // Coord struct indexer setter and getter
        matrix[new Coord(0, 1)] = 30.0;
        Assert.Equal(30.0, matrix[new Coord(0, 1)].Value);
        Assert.Equal(30.0, matrix[0, 1].Value);

        matrix[1, 1] = 40.0;
        Assert.Equal(40.0, matrix[1, 1].Value);
        Assert.Equal(40.0, matrix[3].Value);
        Assert.Equal(40.0, matrix[new Coord(1, 1)].Value);
    }

    [Fact]
    public void SetAll_ShouldPopulateAllElements()
    {
        var matrix = new MinimalMatrix(2, 2);
        matrix.SetAll(7.5);

        Assert.Equal(7.5, matrix[0].Value);
        Assert.Equal(7.5, matrix[1].Value);
        Assert.Equal(7.5, matrix[2].Value);
        Assert.Equal(7.5, matrix[3].Value);
    }

    [Fact]
    public void Clone_ShouldReturnNewInstanceWithIdenticalDimensions()
    {
        var matrix = new MinimalMatrix(2, 3);
        matrix[0] = 5.0;
        var clone = matrix.Clone();

        Assert.Equal(matrix.Width, clone.Width);
        Assert.Equal(matrix.Height, clone.Height);
        Assert.Equal(matrix.Size, clone.Size);
        Assert.Equal(matrix.Count, clone.Count);
        Assert.Equal(5.0, clone[0].Value);

        clone[0] = 99.0;
        Assert.Equal(5.0, matrix[0].Value);
        Assert.Equal(99.0, clone[0].Value);
    }

    [Fact]
    public void Map_ShouldTransformElements()
    {
        var matrix = new MinimalMatrix(2, 2);
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
    public void ToString_ShouldFormatMatrixContents()
    {
        var matrix = new MinimalMatrix(2, 2);
        matrix[0, 0] = 1.0;
        matrix[1, 0] = 2.0;
        matrix[0, 1] = 3.0;
        matrix[1, 1] = 4.0;

        string? str = matrix.ToString();
        Assert.NotNull(str);
        Assert.Contains(nameof(MinimalMatrix), str);
        Assert.Contains("1", str);
        Assert.Contains("4", str);
    }

    [Fact]
    public void GetEnumerator_GenericAndNonGeneric_ShouldIterateAllElements()
    {
        var matrix = new MinimalMatrix(2, 2);
        matrix[0, 0] = 1.0;
        matrix[1, 0] = 2.0;
        matrix[0, 1] = 3.0;
        matrix[1, 1] = 4.0;

        var values = new List<double>();
        foreach (DoublePixel pixel in matrix)
        {
            values.Add(pixel.Value);
        }
        Assert.Equal(new double[] { 1.0, 2.0, 3.0, 4.0 }, values);

        var nonGeneric = ((IEnumerable)matrix).GetEnumerator();
        Assert.True(nonGeneric.MoveNext());
        Assert.Equal(new DoublePixel(1.0), nonGeneric.Current);
        Assert.True(nonGeneric.MoveNext());
        Assert.Equal(new DoublePixel(2.0), nonGeneric.Current);
    }

    private sealed class MinimalMatrix : Matrix<DoublePixel>
    {
        private readonly DoublePixel[] data;

        public MinimalMatrix(int width, int height) : base(width, height)
        {
            data = new DoublePixel[width * height];
        }

        public override DoublePixel GetElement(int index) => data[index];
        public override DoublePixel GetElement(int x, int y) => data[x + (y * Width)];
        public override void SetElement(int index, DoublePixel value) => data[index] = value;
        public override void SetElement(int x, int y, DoublePixel value) => data[x + (y * Width)] = value;
        public override void SetAll(DoublePixel value) => Array.Fill(data, value);
        public override Matrix<R> Map<R>(Func<DoublePixel, R> func)
        {
            var result = new SequentialMatrix<R>(Width, Height);
            for (int i = 0; i < data.Length; i++)
                result[i] = func(data[i]);
            return result;
        }
        public override Matrix<DoublePixel> Clone()
        {
            var clone = new MinimalMatrix(Width, Height);
            Array.Copy(data, clone.data, data.Length);
            return clone;
        }
    }
}
