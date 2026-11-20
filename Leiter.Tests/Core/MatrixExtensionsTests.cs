namespace Leiter.Tests.Core;

using System;
using System.Collections.Generic;
using Leiter.Algorithms;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MatrixExtensions" />.
/// </summary>
public class MatrixExtensionsTests
{
    [Fact]
    public void ToSequentialMatrix_ShouldPopulateCorrectly()
    {
        var source = new List<DoublePixel> { 1.0, 2.0, 3.0, 4.0 };
        var matrix = source.ToSequentialMatrix(2, 2);

        Assert.Equal(2, matrix.Width);
        Assert.Equal(2, matrix.Height);
        Assert.Equal(1.0, matrix[0, 0].Value);
        Assert.Equal(2.0, matrix[1, 0].Value);
        Assert.Equal(3.0, matrix[0, 1].Value);
        Assert.Equal(4.0, matrix[1, 1].Value);
    }

    [Fact]
    public void ToSequentialMatrix_TooSmallSource_ShouldThrow()
    {
        var source = new List<DoublePixel> { 1.0, 2.0, 3.0 };
        var lengthEx = Assert.Throws<ArgumentException>(() => source.ToSequentialMatrix(2, 2));
        Assert.Equal("values", lengthEx.ParamName);
        Assert.Throws<ArgumentNullException>(() => ((IEnumerable<DoublePixel>)null!).ToSequentialMatrix(2, 2));
    }

    [Fact]
    public void ToSequentialMatrix_ToArray_MapToArray_AndGetClamped_ShouldWork()
    {
        var source = new SequentialMatrix<DoublePixel>(new Size(2, 2), [1.0, 2.0, 3.0, 4.0]);
        var view = new MatrixView<DoublePixel>(source, 1, 0, 2, 2, EdgeHandling.Extend);

        SequentialMatrix<DoublePixel> materialized = view.ToSequentialMatrix();
        Assert.Equal(new Size(2, 2), materialized.Size);
        Assert.Equal(2.0, materialized[0, 0].Value);
        Assert.Equal(2.0, materialized[1, 0].Value);
        Assert.Equal(4.0, materialized[0, 1].Value);
        Assert.Equal(4.0, materialized[1, 1].Value);

        DoublePixel[] rawArray = source.ToArray();
        rawArray[0] = 99.0;
        Assert.Equal(1.0, source[0, 0].Value);

        DoublePixel[] viewArray = view.ToArray();
        Assert.Equal([2.0, 2.0, 4.0, 4.0], Array.ConvertAll(viewArray, static p => p.Value));

        double[] seqMapped = source.MapToArray(static p => p.Value * 10.0);
        Assert.Equal([10.0, 20.0, 30.0, 40.0], seqMapped);

        double[] viewMapped = view.MapToArray(static p => p.Value * 10.0);
        Assert.Equal([20.0, 20.0, 40.0, 40.0], viewMapped);

        Assert.Empty(new SequentialMatrix<DoublePixel>(0, 0).MapToArray(static p => p.Value));

        Assert.Equal(1.0, source.GetClamped(-5, -3).Value);
        Assert.Equal(4.0, source.GetClamped(10, 10).Value);
        Assert.Equal(2.0, source.GetClamped(new Coord(5, -1)).Value);

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).ToSequentialMatrix());
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).ToArray());
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).MapToArray(static p => p.Value));
        Assert.Throws<ArgumentNullException>(() => source.MapToArray<DoublePixel, double>(null!));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).GetClamped(0, 0));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).GetClamped(new Coord(0, 0)));
        Assert.Throws<ArgumentException>(() => new SequentialMatrix<DoublePixel>(0, 0).GetClamped(0, 0));
    }

    [Fact]
    public void CoordFromIndex_ShouldComputeCorrectly()
    {
        var matrix = new DummyUntypedMatrix { Width = 5, Height = 5 };

        var c0 = matrix.CoordFromIndex(0);
        Assert.Equal(new Coord(0, 0), c0);

        var c7 = matrix.CoordFromIndex(7);
        Assert.Equal(new Coord(2, 1), c7);
    }

    [Fact]
    public void IndexFromCoord_ShouldComputeCorrectly()
    {
        var matrix = new DummyUntypedMatrix { Width = 5, Height = 5 };

        var index = matrix.IndexFromCoord(new Coord(2, 1));
        Assert.Equal(7, index);
    }

    [Fact]
    public void NormalizeAndDenormalizeCoord_ShouldWork()
    {
        var matrix = new DummyUntypedMatrix { Width = 10, Height = 20 };

        var coord = new Coord(5, 10);
        var norm = matrix.NormalizeCoord(coord);
        Assert.Equal(0.5, norm.X);
        Assert.Equal(0.5, norm.Y);

        var denorm = matrix.DenormalizeCoord(norm);
        Assert.Equal(coord, denorm);
    }

    [Fact]
    public void Map_OnConcreteMatrix_ShouldTransformElements()
    {
        var matrix = new SequentialMatrix<DoublePixel>(2, 2);
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
    public void Map_OnNonMatrixReadOnlyMatrix_ShouldTransformElements()
    {
        IReadOnlyMatrix<DoublePixel> dummy = new DummyReadOnlyMatrix<DoublePixel>(2, 2, (x, y) => new DoublePixel(x + y));
        Matrix<bool> mapped = dummy.Map(p => p.Value > 0.5);

        Assert.Equal(2, mapped.Width);
        Assert.Equal(2, mapped.Height);
        Assert.False(mapped[0, 0]);
        Assert.True(mapped[1, 0]);
        Assert.True(mapped[0, 1]);
        Assert.True(mapped[1, 1]);
    }

    [Fact]
    public void Map_NullArguments_ShouldThrowArgumentNullException()
    {
        IReadOnlyMatrix<DoublePixel> dummy = new DummyReadOnlyMatrix<DoublePixel>(2, 2, (x, y) => new DoublePixel(x + y));

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).Map(p => p.Value > 0.0));
        Assert.Throws<ArgumentNullException>(() => dummy.Map<DoublePixel, bool>(null!));
    }

    [Fact]
    public void Contains_ShouldCheckMatrixBoundsAndNull()
    {
        var matrix = new DummyUntypedMatrix { Width = 4, Height = 3 };

        Assert.True(matrix.Contains(0, 0));
        Assert.True(matrix.Contains(3, 2));
        Assert.True(matrix.Contains(new Coord(2, 1)));

        Assert.False(matrix.Contains(-1, 0));
        Assert.False(matrix.Contains(0, -1));
        Assert.False(matrix.Contains(4, 2));
        Assert.False(matrix.Contains(new Coord(1, 3)));

        Assert.Throws<ArgumentNullException>(() => ((IUntypedMatrix)null!).Contains(0, 0));
        Assert.Throws<ArgumentNullException>(() => ((IUntypedMatrix)null!).Contains(new Coord(0, 0)));
    }

    [Fact]
    public void ContentEquals_ShouldCompareDimensionsAndElements()
    {
        var left = new MaskMatrix(2, 2);
        left[0, 0] = true;
        left[1, 1] = true;

        var rightEqual = new MaskMatrix(2, 2);
        rightEqual[0, 0] = true;
        rightEqual[1, 1] = true;

        var rightDiffValue = new MaskMatrix(2, 2);
        rightDiffValue[0, 0] = true;
        rightDiffValue[0, 1] = true;

        var rightDiffSize = new MaskMatrix(2, 3);

        Assert.True(left.ContentEquals(rightEqual));
        Assert.False(left.ContentEquals(rightDiffValue));
        Assert.False(left.ContentEquals(rightDiffSize));
        Assert.True(new MaskMatrix(0, 0).ContentEquals(new MaskMatrix(0, 0)));

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<bool>)null!).ContentEquals(left));
        Assert.Throws<ArgumentNullException>(() => left.ContentEquals(null!));
    }

    [Fact]
    public void Clone_OnReadOnlyMatrix_ShouldPreserveSubtypesAndMaterializeViews()
    {
        var mask = new MaskMatrix(2, 2);
        mask[0, 0] = true;
        mask[1, 1] = true;
        IReadOnlyMatrix<bool> maskReadOnly = mask;

        Matrix<bool> maskClone = maskReadOnly.Clone();
        Assert.IsType<MaskMatrix>(maskClone);
        Assert.NotSame(mask, maskClone);
        Assert.True(maskClone.ContentEquals(mask));
        Assert.IsType<SequentialMatrix<bool>>(maskReadOnly.ToSequentialMatrix());

        var trimap = new TrimapMatrix(2, 2);
        trimap[0, 0] = TrimapLabel.Foreground;
        trimap[1, 0] = TrimapLabel.ProbableForeground;
        trimap[0, 1] = TrimapLabel.ProbableBackground;
        IReadOnlyMatrix<TrimapLabel> trimapReadOnly = trimap;

        Matrix<TrimapLabel> trimapClone = trimapReadOnly.Clone();
        Assert.IsType<TrimapMatrix>(trimapClone);
        Assert.NotSame(trimap, trimapClone);
        Assert.True(trimapClone.ContentEquals(trimap));

        var custom = new CustomMatrix(2, 2);
        custom[0, 0] = 1.0;
        custom[1, 0] = 2.0;
        custom[0, 1] = 3.0;
        custom[1, 1] = 4.0;
        IReadOnlyMatrix<DoublePixel> customReadOnly = custom;

        Matrix<DoublePixel> customClone = customReadOnly.Clone();
        Assert.IsType<CustomMatrix>(customClone);
        Assert.NotSame(custom, customClone);
        Assert.True(customClone.ContentEquals(custom));

        Matrix<DoublePixel> blurredCustom = Blur.GaussianBlur(customReadOnly, 1.0, 0);
        Assert.IsType<CustomMatrix>(blurredCustom);
        Assert.NotSame(custom, blurredCustom);
        Assert.True(blurredCustom.ContentEquals(custom));

        var source = new SequentialMatrix<DoublePixel>(new Size(2, 2), [1.0, 2.0, 3.0, 4.0]);
        var view = new MatrixView<DoublePixel>(source, 1, 0, 2, 2, EdgeHandling.Extend);
        Matrix<DoublePixel> viewClone = view.Clone();
        Assert.IsType<SequentialMatrix<DoublePixel>>(viewClone);
        Assert.Equal(new Size(2, 2), viewClone.Size);
        Assert.Equal([2.0, 2.0, 4.0, 4.0], Array.ConvertAll(viewClone.ToArray(), static p => p.Value));

        IReadOnlyMatrix<DoublePixel> dummy = new DummyReadOnlyMatrix<DoublePixel>(2, 2, (x, y) => new DoublePixel(x + y));
        Matrix<DoublePixel> dummyClone = dummy.Clone();
        Assert.IsType<SequentialMatrix<DoublePixel>>(dummyClone);
        Assert.True(dummyClone.ContentEquals(dummy));

        SequentialMatrix<DoublePixel> covariantSeq = source.Clone();
        MaskMatrix covariantMask = mask.Clone();
        TrimapMatrix covariantTrimap = trimap.Clone();
        Assert.NotSame(source, covariantSeq);
        Assert.NotSame(mask, covariantMask);
        Assert.NotSame(trimap, covariantTrimap);

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).Clone());
    }

    [Fact]
    public void MapIndexed_ShouldTransformWithCoordinatesAndValidateArguments()
    {
        var source = new SequentialMatrix<DoublePixel>(new Size(2, 2), [1.0, 2.0, 3.0, 4.0]);
        Matrix<DoublePixel> mapped = source.MapIndexed(static (x, y, p) => new DoublePixel((p.Value * 10.0) + (y * 2) + x));

        Assert.Equal(10.0, mapped[0, 0].Value);
        Assert.Equal(21.0, mapped[1, 0].Value);
        Assert.Equal(32.0, mapped[0, 1].Value);
        Assert.Equal(43.0, mapped[1, 1].Value);

        var custom = new CustomMatrix(2, 2);
        custom[1, 1] = new DoublePixel(5.0);
        Matrix<DoublePixel> mappedCustom = custom.MapIndexed(static (x, y, p) => new DoublePixel(p.Value + x + y));
        Assert.Equal(7.0, mappedCustom[1, 1].Value);

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).MapIndexed(static (x, y, p) => p));
        Assert.Throws<ArgumentNullException>(() => source.MapIndexed<DoublePixel, DoublePixel>(null!));
    }

    private sealed class CustomMatrix : Matrix<DoublePixel>
    {
        private readonly DoublePixel[,] _elements;

        public CustomMatrix(int width, int height)
            : base(width, height)
        {
            _elements = new DoublePixel[width, height];
        }

        private CustomMatrix(int width, int height, DoublePixel[,] elements)
            : base(width, height)
        {
            _elements = elements;
        }

        public override DoublePixel GetElement(int index) =>
            GetElement(index % Width, index / Width);

        public override DoublePixel GetElement(int x, int y) => _elements[x, y];

        public override void SetElement(int index, DoublePixel value) =>
            SetElement(index % Width, index / Width, value);

        public override void SetElement(int x, int y, DoublePixel value) =>
            _elements[x, y] = value;

        public override void SetAll(DoublePixel value)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                    _elements[x, y] = value;
            }
        }

        public override Matrix<R> Map<R>(Func<DoublePixel, R> func)
        {
            var mapped = new SequentialMatrix<R>(Width, Height);
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                    mapped[x, y] = func(_elements[x, y]);
            }

            return mapped;
        }

        public override Matrix<DoublePixel> Clone() =>
            new CustomMatrix(Width, Height, (DoublePixel[,])_elements.Clone());
    }
}
