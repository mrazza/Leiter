namespace Leiter.Tests.Core;

using System.Collections.Generic;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="IReadOnlyMatrix{T}"/>.
/// </summary>
public class IReadOnlyMatrixTests
{
    [Fact]
    public void TestExplicitInterfaceCounts()
    {
        var m = new DummyReadOnlyMatrix();
        IReadOnlyMatrix<DoublePixel> rom = m;
        IUntypedMatrix utm = rom;
        IReadOnlyCollection<DoublePixel> roc = rom;

        // Force call explicit interface default implementations in IReadOnlyMatrix
        Assert.Equal(4, rom.Count);
        Assert.Equal(4, utm.Count);
        Assert.Equal(4, roc.Count);
    }

    [Fact]
    public void ExplicitOnlyMatrix_And_InterfaceCounts_ShouldWork()
    {
        var explicitOnly = new ExplicitOnlyReadOnlyMatrix();
        IReadOnlyMatrix<DoublePixel> rom = explicitOnly;
        IUntypedMatrix utm = explicitOnly;
        IReadOnlyCollection<DoublePixel> roc = explicitOnly;

        Assert.Equal(3, rom.Count);
        Assert.Equal(3, utm.Count);
        Assert.Equal(3, roc.Count);
    }

    [Fact]
    public void ToString_ShouldFormatMatrixContents()
    {
        var dummy = new DummyReadOnlyMatrix();
        string dummyString = IReadOnlyMatrix<DoublePixel>.ToString(dummy);
        Assert.Contains(nameof(DummyReadOnlyMatrix), dummyString);
        Assert.Contains("{", dummyString);
        Assert.Contains("}", dummyString);

        var explicitOnly = new ExplicitOnlyReadOnlyMatrix();
        string explicitString = IReadOnlyMatrix<DoublePixel>.ToString(explicitOnly);
        Assert.Contains(nameof(ExplicitOnlyReadOnlyMatrix), explicitString);
        Assert.Contains("{", explicitString);
        Assert.Contains("}", explicitString);
    }

    private sealed class ExplicitOnlyReadOnlyMatrix : IReadOnlyMatrix<DoublePixel>
    {
        public int Width => 3;
        public int Height => 1;
        public Size Size => new(3, 1);
        int IReadOnlyMatrix<DoublePixel>.Count => 3;
        public DoublePixel this[int x, int y] => new(x + y);
        public DoublePixel this[Coord coord] => this[coord.X, coord.Y];
        public DoublePixel this[int index] => new(index);
        public DoublePixel GetElement(int index) => new(index);
        public DoublePixel GetElement(int width, int height) => new(width + height);
        public IEnumerator<DoublePixel> GetEnumerator() { yield break; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
