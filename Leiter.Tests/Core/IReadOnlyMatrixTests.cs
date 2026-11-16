using Leiter.Tests.TestUtils;
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using System.Collections.Generic;

namespace Leiter.Tests.Core;

/// <summary>
/// Provides unit tests for <see cref="IReadOnlyMatrix{T}" />.
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
        Assert.Equal(4, utm.Count);
        Assert.Equal(4, roc.Count);
    }

    [Fact]
    public void ExplicitOnlyMatrix_And_BaseMatrixEnumerator_ShouldWork()
    {
        var explicitOnly = new ExplicitOnlyReadOnlyMatrix();
        IReadOnlyMatrix<DoublePixel> rom = explicitOnly;
        IUntypedMatrix utm = explicitOnly;
        IReadOnlyCollection<DoublePixel> roc = explicitOnly;

        Assert.Equal(3, rom.Count);
        Assert.Equal(3, utm.Count);
        Assert.Equal(3, roc.Count);

        var minimal = new MinimalMatrix(2, 2);
        minimal[0, 0] = 1.0;
        minimal[1, 0] = 2.0;
        minimal[0, 1] = 3.0;
        minimal[1, 1] = 4.0;

        var values = new List<double>();
        foreach (DoublePixel pixel in minimal)
            values.Add(pixel.Value);
        Assert.Equal([1.0, 2.0, 3.0, 4.0], values);

        var nonGeneric = ((System.Collections.IEnumerable)minimal).GetEnumerator();
        Assert.True(nonGeneric.MoveNext());
        Assert.Equal(new DoublePixel(1.0), nonGeneric.Current);
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
        public override Matrix<DoublePixel> Multiply(IReadOnlyMatrix<DoublePixel> right) => Clone();
        public override Matrix<R> Map<R>(Func<DoublePixel, R> func) => new SequentialMatrix<R>(Width, Height);
        public override Matrix<DoublePixel> Clone() => new MinimalMatrix(Width, Height);
    }
}
