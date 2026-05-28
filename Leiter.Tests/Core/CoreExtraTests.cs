using Leiter.Tests.TestUtils;
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
    

    [Fact]
    public void TestExplicitInterfaceCounts()
    {
        var m = new DummyReadOnlyMatrix();
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
