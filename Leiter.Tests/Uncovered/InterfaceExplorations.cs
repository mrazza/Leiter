
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using System.Collections.Generic;
using System.Linq;

namespace Leiter.Tests.Uncovered;

public class InterfaceExplorations
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
}
