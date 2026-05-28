
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Algorithms.DataStructures;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Leiter.Tests.Core;

public class CoreExtraTests
{
    private class DumbMatrix : IReadOnlyMatrix<DoublePixel>
    {
        public int Width => 2;
        public int Height => 2;
        public Size Size => new(2, 2);
        public int Count => 4;

        public DoublePixel this[int x, int y] => new(x + y);
        public DoublePixel this[Coord coord] => this[coord.X, coord.Y];
        public DoublePixel this[int index] => new(index);

        public DoublePixel GetElement(int index) => new(index);
        public DoublePixel GetElement(int width, int height) => new(width * height);

        public IEnumerator<DoublePixel> GetEnumerator() => Enumerable.Range(0, 4).Select(i => new DoublePixel(i)).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void TestExplicitInterfaceCounts()
    {
        var m = new DumbMatrix();
        IReadOnlyMatrix<DoublePixel> rom = m;
        IUntypedMatrix utm = rom;
        IReadOnlyCollection<DoublePixel> roc = rom;

        // Force call explicit interface default implementations in IReadOnlyMatrix
        Assert.Equal(4, utm.Count);
        Assert.Equal(4, roc.Count);
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
    public void TestMatrixView_UncoveredLines()
    {
        var baseMat = new SequentialMatrix<DoublePixel>(2, 2);
        var view = new MatrixView<DoublePixel>(baseMat, 0, 0, 2, 2, EdgeHandling.EXTEND);

        // Call ToString
        Assert.NotNull(view.ToString());

        // Call IEnumerable.GetEnumerator on MatrixView
        var enumerable = (System.Collections.IEnumerable)view;
        var enumerator = enumerable.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);
    }
}
