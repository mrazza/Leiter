
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Algorithms.DataStructures;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Leiter.Tests.Uncovered;

public class UncoveredLinesTests
{
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

    [Fact]
    public void TestBucketingHistogram_IEnumerable()
    {
        var hist = new DictionaryHistogram<string>();
        hist.Increment("test");
        var enumerable = (System.Collections.IEnumerable)hist;
        var enumerator = enumerable.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);
    }

    [Fact]
    public void TestRegion_SingleElementConstructor()
    {
        var region = new Region<Coord>(new Coord(1, 1));
        Assert.Single(region.Pixels);
        Assert.Contains(new Coord(1, 1), region.Pixels);
    }

    [Fact]
    public void TestCIEDE2000_SpecialCases()
    {
        // To cover various partialDeltaHPrime and meanHPrime branches in CIEDE2000 (Lab32.Distance):
        // case { selfCPrime: 0.0 }
        // case { otherCPrime: 0.0 }
        // case sumHPrime and absDiffHPrime conditions

        var gray1 = new Lab32(50, 0, 0);
        var gray2 = new Lab32(50, 0, 0);
        var color1 = new Lab32(50, 10, 20);
        var color2 = new Lab32(50, -10, -20);
        var color3 = new Lab32(50, 10, -20);

        Assert.Equal(0.0, gray1.Distance(gray2), 5);
        Assert.True(gray1.Distance(color1) > 0);
        Assert.True(color1.Distance(gray2) > 0);
        Assert.True(color1.Distance(color2) > 0);
        Assert.True(color1.Distance(color3) > 0);
    }
}
