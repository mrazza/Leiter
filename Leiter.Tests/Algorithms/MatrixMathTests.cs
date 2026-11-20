namespace Leiter.Tests.Algorithms;

using System;
using Leiter.Algorithms;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MatrixMath"/>.
/// </summary>
public class MatrixMathTests
{
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

    [Fact]
    public void Multiply_DimensionMismatch_ShouldThrow()
    {
        var m1 = new SequentialMatrix<DoublePixel>(2, 2);
        var m2 = new SequentialMatrix<DoublePixel>(3, 3);
        Assert.Throws<ArgumentException>(() => m1.Multiply(m2));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).Multiply(m2));
        Assert.Throws<ArgumentNullException>(() => m1.Multiply(null!));
    }

    [Fact]
    public void HadamardProduct_SameDimensions_ShouldMultiplyElementWise()
    {
        var m1 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 2.0, 3.0, 4.0, 5.0 });
        var m2 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 3.0, 4.0, 5.0, 6.0 });

        // Force first overload by providing only 1 generic argument
        var result = m1.HadamardProduct<DoublePixel>(m2);
        Assert.Equal(6.0, result[0].Value);
        Assert.Equal(12.0, result[1].Value);
        Assert.Equal(20.0, result[2].Value);
        Assert.Equal(30.0, result[3].Value);
    }

    [Fact]
    public void HadamardProduct_WithScalarMatrix_ShouldMultiplyElementWise()
    {
        var m1 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 2.0, 3.0, 4.0, 5.0 });
        var m2 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 3.0, 4.0, 5.0, 6.0 });

        // Force second overload by providing both generic arguments
        var result = m1.HadamardProduct<DoublePixel, DoublePixel>(m2);
        Assert.Equal(6.0, result[0].Value);
        Assert.Equal(12.0, result[1].Value);
    }

    [Fact]
    public void HadamardProduct_DimensionMismatch_ShouldThrow()
    {
        var m1 = new SequentialMatrix<DoublePixel>(2, 2);
        var m2 = new SequentialMatrix<DoublePixel>(3, 3);

        Assert.Throws<ArgumentException>(() => m1.HadamardProduct<DoublePixel>(m2));
        Assert.Throws<ArgumentException>(() => m1.HadamardProduct<DoublePixel, DoublePixel>(m2));
    }

    [Fact]
    public void FrobeniusProduct_ShouldReturnInnerProduct()
    {
        var m1 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 1.0, 2.0, 3.0, 4.0 });
        var m2 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 2.0, 3.0, 4.0, 5.0 });

        // Force first overload
        var result = m1.FrobeniusProduct<DoublePixel>(m2);
        Assert.Equal(40.0, result.Value);

        // Force second overload
        var result2 = m1.FrobeniusProduct<DoublePixel, DoublePixel>(m2);
        Assert.Equal(40.0, result2.Value);
    }

    [Fact]
    public void FrobeniusProduct_DimensionMismatch_ShouldThrow()
    {
        var m1 = new SequentialMatrix<DoublePixel>(2, 2);
        var m2 = new SequentialMatrix<DoublePixel>(3, 3);

        Assert.Throws<ArgumentException>(() => m1.FrobeniusProduct<DoublePixel>(m2));
        Assert.Throws<ArgumentException>(() => m1.FrobeniusProduct<DoublePixel, DoublePixel>(m2));
    }

    [Fact]
    public void FrobeniusProduct_MatrixViewOverloads_ShouldMatchAndThrowOnMismatch()
    {
        var m1 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 1.0, 2.0, 3.0, 4.0 });
        var m2 = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 2.0, 3.0, 4.0, 5.0 });
        var view = new MatrixView<DoublePixel>(m1, 0, 0, 2, 2, EdgeHandling.Extend);

        var sameTypeResult = view.FrobeniusProduct<DoublePixel>(m2);
        Assert.Equal(40.0, sameTypeResult.Value);

        var scalarExplicitResult = view.FrobeniusProduct<DoublePixel, DoublePixel>(m2);
        Assert.Equal(40.0, scalarExplicitResult.Value);

        var rgbImg = new SequentialMatrix<Rgb8>(3, 3);
        rgbImg[1, 1] = new Rgb8(255, 0, 0);
        rgbImg[2, 2] = new Rgb8(0, 0, 255);
        var rgbView = new MatrixView<Rgb8>(rgbImg, 1, 1, 2, 2, EdgeHandling.Extend);
        var scalarKernel = new SequentialMatrix<DoublePixel>(new Size(2, 2), new DoublePixel[] { 1.0, 0.0, 0.0, 1.0 });

        Rgb8 inferredResult = rgbView.FrobeniusProduct(scalarKernel);
        Assert.Equal(new Rgb8(255, 0, 255), inferredResult);

        var m3 = new SequentialMatrix<DoublePixel>(3, 3);
        Assert.Throws<ArgumentException>(() => view.FrobeniusProduct<DoublePixel>(m3));
        Assert.Throws<ArgumentException>(() => view.FrobeniusProduct<DoublePixel, DoublePixel>(m3));
        Assert.Throws<ArgumentException>(() => rgbView.FrobeniusProduct(m3));
    }

    [Fact]
    public void Convolve_ShouldPerformConvolution()
    {
        // 3x3 image
        var img = new SequentialMatrix<DoublePixel>(new Size(3, 3), new DoublePixel[] {
            1.0, 2.0, 3.0,
            4.0, 5.0, 6.0,
            7.0, 8.0, 9.0
        });

        // 3x3 kernel
        var kernel = new SequentialMatrix<DoublePixel>(new Size(3, 3), new DoublePixel[] {
            0.0, 0.0, 0.0,
            0.0, 1.0, 0.0,
            0.0, 0.0, 0.0
        });

        var result = img.Convolve(kernel);
        // Using identity kernel should keep image identical
        Assert.Equal(1.0, result[0, 0].Value);
        Assert.Equal(5.0, result[1, 1].Value);
        Assert.Equal(9.0, result[2, 2].Value);
    }

    [Fact]
    public void ConvolveSeparable_ShouldMatch2DConvolution()
    {
        SequentialMatrix<DoublePixel> img = TestMatrices.Create(
            7,
            6,
            (x, y) => new DoublePixel(x * 1.5 + y * y + ((x + y) % 3) * 2.0));

        double[] hKernel = [0.2, 0.5, 0.3];
        double[] vKernel = [0.1, 0.3, 0.4, 0.2];
        SequentialMatrix<DoublePixel> kernel2D = TestMatrices.Create(
            hKernel.Length,
            vKernel.Length,
            (kx, ky) => new DoublePixel(hKernel[kx] * vKernel[ky]));

        var expected = img.Convolve(kernel2D);
        var actual = img.ConvolveSeparable(hKernel, vKernel);

        MatrixAssert.ApproxEqual(expected, actual, 1e-10);
    }

    [Fact]
    public void Convolve_And_ConvolveSeparable_EdgeCases_ShouldValidateAndHandleEmptyAndLargeKernels()
    {
        SequentialMatrix<DoublePixel> img = TestMatrices.Constant(2, 2, new DoublePixel(5.0));

        Assert.Throws<ArgumentException>(() => img.Convolve(new SequentialMatrix<DoublePixel>(0, 3)));
        Assert.Throws<ArgumentException>(() => img.Convolve(new SequentialMatrix<DoublePixel>(3, 0)));

        var validKernel = new SequentialMatrix<DoublePixel>(1, 1);
        validKernel[0, 0] = 1.0;

        var emptyW = new SequentialMatrix<DoublePixel>(0, 3);
        var convolvedEmptyW = emptyW.Convolve(validKernel);
        Assert.Equal(0, convolvedEmptyW.Width);
        Assert.Equal(3, convolvedEmptyW.Height);

        var emptyH = new SequentialMatrix<DoublePixel>(3, 0);
        var convolvedEmptyH = emptyH.Convolve(validKernel);
        Assert.Equal(3, convolvedEmptyH.Width);
        Assert.Equal(0, convolvedEmptyH.Height);

        Assert.Throws<ArgumentException>(() => img.ConvolveSeparable(ReadOnlySpan<double>.Empty, [1.0]));
        Assert.Throws<ArgumentException>(() => img.ConvolveSeparable([1.0], ReadOnlySpan<double>.Empty));

        var sepEmptyW = emptyW.ConvolveSeparable([1.0], [1.0]);
        Assert.Equal(0, sepEmptyW.Width);
        Assert.Equal(3, sepEmptyW.Height);

        var sepEmptyH = emptyH.ConvolveSeparable([1.0], [1.0]);
        Assert.Equal(3, sepEmptyH.Width);
        Assert.Equal(0, sepEmptyH.Height);

        double[] largeVertical = new double[257];
        Array.Fill(largeVertical, 1.0 / largeVertical.Length);
        var convolvedLargeV = img.ConvolveSeparable([1.0], largeVertical);
        MatrixAssert.ApproxEqual(img, convolvedLargeV, 1e-10);
    }

    [Fact]
    public void MatrixMath_NullArgumentsAndMatrixViewInputs_ShouldWork()
    {
        var img = new SequentialMatrix<DoublePixel>(new Size(2, 2), [1.0, 2.0, 3.0, 4.0]);
        var view = new MatrixView<DoublePixel>(img, 0, 0, 2, 2, EdgeHandling.Extend);
        var identityKernel = new SequentialMatrix<DoublePixel>(new Size(1, 1), [1.0]);

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).HadamardProduct<DoublePixel>(img));
        Assert.Throws<ArgumentNullException>(() => img.HadamardProduct<DoublePixel>(null!));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).HadamardProduct<DoublePixel, DoublePixel>(img));
        Assert.Throws<ArgumentNullException>(() => img.HadamardProduct<DoublePixel, DoublePixel>(null!));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).FrobeniusProduct<DoublePixel>(img));
        Assert.Throws<ArgumentNullException>(() => img.FrobeniusProduct<DoublePixel>(null!));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).FrobeniusProduct<DoublePixel, DoublePixel>(img));
        Assert.Throws<ArgumentNullException>(() => img.FrobeniusProduct<DoublePixel, DoublePixel>(null!));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).Convolve(identityKernel));
        Assert.Throws<ArgumentNullException>(() => img.Convolve<DoublePixel, DoublePixel>(null!));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).ConvolveSeparable([1.0], [1.0]));

        Matrix<DoublePixel> hadamardView = view.HadamardProduct<DoublePixel>(img);
        Assert.Equal(1.0, hadamardView[0, 0].Value);
        Assert.Equal(16.0, hadamardView[1, 1].Value);

        Matrix<DoublePixel> hadamardScalarView = view.HadamardProduct<DoublePixel, DoublePixel>(img);
        Assert.Equal(4.0, hadamardScalarView[1, 0].Value);

        var longScalars = new SequentialMatrix<LongPixel>(new Size(2, 2), [2L, 3L, 4L, 5L]);
        var longView = new MatrixView<LongPixel>(longScalars, 0, 0, 2, 2, EdgeHandling.Extend);
        Matrix<DoublePixel> hadamardLongScalar = view.HadamardProduct(longView);
        Assert.Equal(2.0, hadamardLongScalar[0, 0].Value);
        Assert.Equal(6.0, hadamardLongScalar[1, 0].Value);
        Assert.Equal(12.0, hadamardLongScalar[0, 1].Value);
        Assert.Equal(20.0, hadamardLongScalar[1, 1].Value);

        var emptyLeft = new SequentialMatrix<DoublePixel>(0, 2);
        var emptyRight = new SequentialMatrix<LongPixel>(0, 2);
        Assert.Empty(emptyLeft.HadamardProduct<DoublePixel>(new SequentialMatrix<DoublePixel>(0, 2)));
        Assert.Empty(emptyLeft.HadamardProduct(emptyRight));

        Matrix<DoublePixel> convolvedView = view.Convolve(identityKernel);
        Assert.Equal(3.0, convolvedView[0, 1].Value);

        Matrix<DoublePixel> convolvedSepView = view.ConvolveSeparable([1.0], [1.0]);
        Assert.Equal(2.0, convolvedSepView[1, 0].Value);

        var leftMismatch = new SequentialMatrix<DoublePixel>(2, 3);
        var rightMismatch = new SequentialMatrix<DoublePixel>(2, 4);
        var mulMismatchEx = Assert.Throws<ArgumentException>(() => leftMismatch.Multiply(rightMismatch));
        Assert.Equal("right", mulMismatchEx.ParamName);
    }

    [Fact]
    public void Add_And_Scale_ShouldComputeElementWiseResultsAndValidateInputs()
    {
        var left = new SequentialMatrix<DoublePixel>(new Size(2, 2), [1.0, 2.0, 3.0, 4.0]);
        var right = new SequentialMatrix<DoublePixel>(new Size(2, 2), [10.0, 20.0, 30.0, 40.0]);
        var rightView = new MatrixView<DoublePixel>(right, 0, 0, 2, 2, EdgeHandling.Extend);

        Matrix<DoublePixel> sum = left.Add(right);
        MatrixAssert.ApproxEqual(new SequentialMatrix<DoublePixel>(new Size(2, 2), [11.0, 22.0, 33.0, 44.0]), sum, 1e-12);
        MatrixAssert.ApproxEqual(sum, left.Add(rightView), 1e-12);

        Matrix<DoublePixel> scaled = left.Scale(2.5);
        MatrixAssert.ApproxEqual(new SequentialMatrix<DoublePixel>(new Size(2, 2), [2.5, 5.0, 7.5, 10.0]), scaled, 1e-12);
        MatrixAssert.ApproxEqual(new SequentialMatrix<DoublePixel>(new Size(2, 2), [25.0, 50.0, 75.0, 100.0]), rightView.Scale(2.5), 1e-12);

        var emptyA = new SequentialMatrix<DoublePixel>(0, 2);
        var emptyB = new SequentialMatrix<DoublePixel>(0, 2);
        Assert.Empty(emptyA.Add(emptyB));
        Assert.Empty(emptyA.Scale(3.0));

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).Add(right));
        Assert.Throws<ArgumentNullException>(() => left.Add(null!));
        Assert.Throws<ArgumentException>(() => left.Add(new SequentialMatrix<DoublePixel>(3, 2)));

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).Scale(2.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => left.Scale(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => left.Scale(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => left.Scale(double.NegativeInfinity));
    }

    [Fact]
    public void AddInPlace_And_ScaleInPlace_ShouldMutateSequentialAndNonSequentialMatrices()
    {
        var seqTarget = new SequentialMatrix<DoublePixel>(new Size(2, 2), [1.0, 2.0, 3.0, 4.0]);
        var addend = new SequentialMatrix<DoublePixel>(new Size(2, 2), [0.5, 1.5, 2.5, 3.5]);

        seqTarget.AddInPlace(addend);
        MatrixAssert.ApproxEqual(new SequentialMatrix<DoublePixel>(new Size(2, 2), [1.5, 3.5, 5.5, 7.5]), seqTarget, 1e-12);

        seqTarget.ScaleInPlace(2.0);
        MatrixAssert.ApproxEqual(new SequentialMatrix<DoublePixel>(new Size(2, 2), [3.0, 7.0, 11.0, 15.0]), seqTarget, 1e-12);

        var nonSeqTarget = new NonSequentialDoubleMatrix(2, 2, [1.0, 2.0, 3.0, 4.0]);
        var viewAddend = new MatrixView<DoublePixel>(addend, 0, 0, 2, 2, EdgeHandling.Extend);
        nonSeqTarget.AddInPlace(viewAddend);
        MatrixAssert.ApproxEqual(new SequentialMatrix<DoublePixel>(new Size(2, 2), [1.5, 3.5, 5.5, 7.5]), nonSeqTarget, 1e-12);

        nonSeqTarget.ScaleInPlace(0.5);
        MatrixAssert.ApproxEqual(new SequentialMatrix<DoublePixel>(new Size(2, 2), [0.75, 1.75, 2.75, 3.75]), nonSeqTarget, 1e-12);

        var empty = new SequentialMatrix<DoublePixel>(0, 2);
        empty.AddInPlace(new SequentialMatrix<DoublePixel>(0, 2));
        empty.ScaleInPlace(4.0);

        Assert.Throws<ArgumentNullException>(() => ((Matrix<DoublePixel>)null!).AddInPlace(addend));
        Assert.Throws<ArgumentNullException>(() => seqTarget.AddInPlace(null!));
        Assert.Throws<ArgumentException>(() => seqTarget.AddInPlace(new SequentialMatrix<DoublePixel>(3, 2)));
        Assert.Throws<ArgumentNullException>(() => ((Matrix<DoublePixel>)null!).ScaleInPlace(1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => seqTarget.ScaleInPlace(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => seqTarget.ScaleInPlace(double.PositiveInfinity));
    }

    private sealed class NonSequentialDoubleMatrix(int width, int height, DoublePixel[] backing)
        : Matrix<DoublePixel>(width, height)
    {
        public override DoublePixel GetElement(int index) => backing[index];
        public override DoublePixel GetElement(int x, int y) => backing[Size.IndexOf(x, y)];
        public override void SetElement(int index, DoublePixel value) => backing[index] = value;
        public override void SetElement(int x, int y, DoublePixel value) => backing[Size.IndexOf(x, y)] = value;
        public override void SetAll(DoublePixel value) => Array.Fill(backing, value);
        public override Matrix<R> Map<R>(Func<DoublePixel, R> func) =>
            new SequentialMatrix<R>(Size, Array.ConvertAll(backing, p => func(p)));
        public override Matrix<DoublePixel> Clone() =>
            new NonSequentialDoubleMatrix(Width, Height, backing.ToArray());
    }
}
