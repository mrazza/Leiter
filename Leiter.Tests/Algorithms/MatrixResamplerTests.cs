namespace Leiter.Tests.Algorithms;

using System;
using Leiter.Algorithms;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="MatrixResampler"/>.
/// </summary>
public class MatrixResamplerTests
{
    [Fact]
    public void ResizeBilinear_ShouldInterpolateAndPreserveIdentity()
    {
        var source = new SequentialMatrix<DoublePixel>(2, 2);
        source[0, 0] = 0.0;
        source[1, 0] = 1.0;
        source[0, 1] = 0.0;
        source[1, 1] = 1.0;

        Matrix<DoublePixel> sameSize = source.ResizeBilinear(source.Size);
        Assert.Equal(source.Size, sameSize.Size);
        Assert.Equal(0.0, sameSize[0, 0].Value, 9);
        Assert.Equal(1.0, sameSize[1, 0].Value, 9);

        Matrix<DoublePixel> upsampled = source.ResizeBilinear(new Size(4, 4));
        Assert.Equal(new Size(4, 4), upsampled.Size);
        Assert.Equal(0.0, upsampled[0, 0].Value, 9);
        Assert.InRange(upsampled[1, 1].Value, 0.2, 0.3);
        Assert.InRange(upsampled[2, 2].Value, 0.7, 0.8);
        Assert.Equal(1.0, upsampled[3, 3].Value, 9);

        var rgb = new SequentialMatrix<Rgb8>(4, 4);
        rgb.SetAll(new Rgb8(100, 150, 200));
        Matrix<Rgb8> downsampled = rgb.ResizeBilinear(new Size(2, 2));
        Assert.Equal(new Size(2, 2), downsampled.Size);
        Assert.Equal(new Rgb8(100, 150, 200), downsampled[0, 0]);
        Assert.Equal(new Rgb8(100, 150, 200), downsampled[1, 1]);
    }

    [Fact]
    public void ResizeNearest_And_ResizeMaskNearest_ShouldResampleCorrectly()
    {
        var mask = new MaskMatrix(2, 2);
        mask[0, 0] = true;
        mask[1, 1] = true;

        MaskMatrix upsampledMask = mask.ResizeMaskNearest(new Size(4, 4));
        Assert.Equal(new Size(4, 4), upsampledMask.Size);
        Assert.True(upsampledMask[0, 0]);
        Assert.True(upsampledMask[1, 1]);
        Assert.False(upsampledMask[3, 0]);
        Assert.False(upsampledMask[0, 3]);
        Assert.True(upsampledMask[2, 2]);
        Assert.True(upsampledMask[3, 3]);

        Matrix<bool> genericUpsampled = ((IReadOnlyMatrix<bool>)mask).ResizeNearest(new Size(4, 4));
        Assert.True(upsampledMask.ContentEquals(genericUpsampled));

        var single = new SequentialMatrix<DoublePixel>(1, 1);
        single[0, 0] = 0.75;
        Matrix<DoublePixel> tiled = single.ResizeNearest(new Size(3, 2));
        Assert.Equal(new Size(3, 2), tiled.Size);
        for (int i = 0; i < tiled.Count; i++)
            Assert.Equal(0.75, tiled[i].Value, 9);
    }

    [Fact]
    public void ResampleMethods_EdgeCasesAndValidation_ShouldBehaveCorrectly()
    {
        var nonEmpty = new SequentialMatrix<DoublePixel>(2, 2);
        var emptyDouble = new SequentialMatrix<DoublePixel>(0, 0);
        var emptyMask = new MaskMatrix(0, 0);

        Assert.Empty(nonEmpty.ResizeBilinear(new Size(0, 2)));
        Assert.Empty(nonEmpty.ResizeBilinear(new Size(2, 0)));
        Assert.Empty(emptyDouble.ResizeBilinear(new Size(0, 0)));
        Assert.Empty(emptyMask.ResizeMaskNearest(new Size(0, 0)));
        Assert.Empty(emptyMask.ResizeNearest(new Size(0, 3)));

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).ResizeBilinear(new Size(2, 2)));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).ResizeNearest(new Size(2, 2)));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<bool>)null!).ResizeMaskNearest(new Size(2, 2)));

        Assert.Throws<ArgumentOutOfRangeException>(() => nonEmpty.ResizeBilinear(new Size(-1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => nonEmpty.ResizeBilinear(new Size(2, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => nonEmpty.ResizeNearest(new Size(-1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => emptyMask.ResizeMaskNearest(new Size(2, -1)));

        Assert.Throws<ArgumentException>(() => emptyDouble.ResizeBilinear(new Size(2, 2)));
        Assert.Throws<ArgumentException>(() => emptyDouble.ResizeNearest(new Size(2, 2)));
        Assert.Throws<ArgumentException>(() => emptyMask.ResizeMaskNearest(new Size(2, 2)));
    }

    /// <summary>
    /// Verifies that <see cref="MatrixResampler.SampleBilinear{T}(IReadOnlyMatrix{T}, double, double, EdgeHandling)"/>
    /// and its <see cref="Vector2D"/> overload interpolate values with exact weights.
    /// </summary>
    [Fact]
    public void SampleBilinear_ExactWeights_ShouldInterpolateAccurately()
    {
        var source = new SequentialMatrix<DoublePixel>(2, 2);
        source[0, 0] = 10.0;
        source[1, 0] = 20.0;
        source[0, 1] = 30.0;
        source[1, 1] = 40.0;

        Assert.Equal(10.0, source.SampleBilinear(0.0, 0.0).Value, 9);
        Assert.Equal(20.0, source.SampleBilinear(1.0, 0.0).Value, 9);
        Assert.Equal(30.0, source.SampleBilinear(0.0, 1.0).Value, 9);
        Assert.Equal(40.0, source.SampleBilinear(1.0, 1.0).Value, 9);
        Assert.Equal(25.0, source.SampleBilinear(0.5, 0.5).Value, 9);
        Assert.Equal(27.5, source.SampleBilinear(0.25, 0.75).Value, 9);

        var position = new Vector2D(0.25, 0.75);
        Assert.Equal(27.5, source.SampleBilinear(position).Value, 9);
    }

    /// <summary>
    /// Verifies that bilinear sampling clamps coordinates that fall outside the matrix bounds.
    /// </summary>
    [Fact]
    public void SampleBilinear_ClampingOutOfBounds_ShouldClampToEdge()
    {
        var source = new SequentialMatrix<DoublePixel>(2, 2);
        source[0, 0] = 10.0;
        source[1, 0] = 20.0;
        source[0, 1] = 30.0;
        source[1, 1] = 40.0;

        Assert.Equal(10.0, source.SampleBilinear(-5.0, 0.0).Value, 9);
        Assert.Equal(40.0, source.SampleBilinear(5.0, 1.0).Value, 9);
        Assert.Equal(15.0, source.SampleBilinear(0.5, -2.0).Value, 9);
        Assert.Equal(35.0, source.SampleBilinear(0.5, 10.0).Value, 9);
    }

    /// <summary>
    /// Verifies that bilinear sampling works correctly on a 1x1 matrix.
    /// </summary>
    [Fact]
    public void SampleBilinear_SinglePixelMatrix_ShouldReturnCenterValue()
    {
        var single = new SequentialMatrix<DoublePixel>(1, 1);
        single[0, 0] = 42.0;

        Assert.Equal(42.0, single.SampleBilinear(0.0, 0.0).Value, 9);
        Assert.Equal(42.0, single.SampleBilinear(-2.0, 3.5).Value, 9);
        Assert.Equal(42.0, single.SampleBilinear(new Vector2D(10.0, -10.0)).Value, 9);
    }

    /// <summary>
    /// Verifies that <see cref="MatrixResampler.SampleNearest{T}(IReadOnlyMatrix{T}, double, double, EdgeHandling)"/>
    /// and its <see cref="Vector2D"/> overload round away from zero and clamp coordinates.
    /// </summary>
    [Fact]
    public void SampleNearest_DoubleAndVector_ShouldRoundAwayFromZeroAndClamp()
    {
        var source = new SequentialMatrix<DoublePixel>(2, 2);
        source[0, 0] = 10.0;
        source[1, 0] = 20.0;
        source[0, 1] = 30.0;
        source[1, 1] = 40.0;

        Assert.Equal(10.0, source.SampleNearest(0.0, 0.0).Value, 9);
        Assert.Equal(10.0, source.SampleNearest(0.49, 0.0).Value, 9);
        Assert.Equal(20.0, source.SampleNearest(0.5, 0.0).Value, 9);
        Assert.Equal(30.0, source.SampleNearest(0.0, 0.5).Value, 9);
        Assert.Equal(40.0, source.SampleNearest(0.5, 0.5).Value, 9);
        Assert.Equal(40.0, source.SampleNearest(new Vector2D(0.5, 0.5)).Value, 9);

        Assert.Equal(10.0, source.SampleNearest(-10.0, -10.0).Value, 9);
        Assert.Equal(40.0, source.SampleNearest(10.0, 10.0).Value, 9);
    }

    /// <summary>
    /// Verifies that nearest sampling works correctly on a 1x1 matrix.
    /// </summary>
    [Fact]
    public void SampleNearest_SinglePixelMatrix_ShouldReturnCenterValue()
    {
        var single = new SequentialMatrix<DoublePixel>(1, 1);
        single[0, 0] = 99.0;

        Assert.Equal(99.0, single.SampleNearest(0.0, 0.0).Value, 9);
        Assert.Equal(99.0, single.SampleNearest(5.5, -2.3).Value, 9);
        Assert.Equal(99.0, single.SampleNearest(new Vector2D(-1.0, 4.0)).Value, 9);
    }

    /// <summary>
    /// Verifies argument validation for both bilinear and nearest sampling methods.
    /// </summary>
    [Fact]
    public void SampleMethods_Validation_ShouldThrowCorrectExceptions()
    {
        var validMatrix = new SequentialMatrix<DoublePixel>(2, 2);
        var emptyMatrix = new SequentialMatrix<DoublePixel>(0, 0);

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).SampleBilinear(0.0, 0.0));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).SampleBilinear(Vector2D.Zero));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).SampleNearest(0.0, 0.0));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).SampleNearest(Vector2D.Zero));

        Assert.Throws<ArgumentException>(() => emptyMatrix.SampleBilinear(0.0, 0.0));
        Assert.Throws<ArgumentException>(() => emptyMatrix.SampleBilinear(Vector2D.Zero));
        Assert.Throws<ArgumentException>(() => emptyMatrix.SampleNearest(0.0, 0.0));
        Assert.Throws<ArgumentException>(() => emptyMatrix.SampleNearest(Vector2D.Zero));

        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleBilinear(0.0, 0.0, (EdgeHandling)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleBilinear(Vector2D.Zero, (EdgeHandling)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleNearest(0.0, 0.0, (EdgeHandling)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleNearest(Vector2D.Zero, (EdgeHandling)999));

        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleBilinear(double.NaN, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleBilinear(0.0, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleBilinear(new Vector2D(double.NegativeInfinity, 0.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleBilinear(new Vector2D(0.0, double.NaN)));

        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleNearest(double.NaN, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleNearest(0.0, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleNearest(new Vector2D(double.NegativeInfinity, 0.0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => validMatrix.SampleNearest(new Vector2D(0.0, double.NaN)));
    }

    /// <summary>
    /// Verifies predicated bilinear sampling and fallback behavior across matching and non-matching corners.
    /// </summary>
    [Fact]
    public void SampleBilinearWhere_ShouldRestrictCornersAndHandleFallback()
    {
        var image = new SequentialMatrix<Rgb8>(2, 2);
        image[0, 0] = new Rgb8(200, 0, 0);
        image[1, 0] = new Rgb8(0, 200, 0);
        image[0, 1] = new Rgb8(200, 0, 0);
        image[1, 1] = new Rgb8(0, 200, 0);

        // Only left column passes predicate: midpoint x=0.5, y=0.5 must evaluate purely to (200, 0, 0)
        Rgb8 leftOnly = image.SampleBilinearWhere(0.5, 0.5, static (cx, _) => cx == 0);
        Assert.Equal(new Rgb8(200, 0, 0), leftOnly);

        // Using state avoids closures
        Rgb8 stateOnly = image.SampleBilinearWhere(0.5, 0.5, state: 1, static (cx, _, tx) => cx == tx);
        Assert.Equal(new Rgb8(0, 200, 0), stateOnly);

        // No corner passes -> returns fallback
        var fallback = new Rgb8(42, 43, 44);
        Rgb8 noneMatched = image.SampleBilinearWhere(0.5, 0.5, static (_, _) => false, fallback);
        Assert.Equal(fallback, noneMatched);

        Assert.Throws<ArgumentNullException>(() => image.SampleBilinearWhere(0.0, 0.0, (Func<int, int, bool>)null!));
        Assert.Throws<ArgumentNullException>(() => image.SampleBilinearWhere(0.0, 0.0, 0, (Func<int, int, int, bool>)null!));
    }

    /// <summary>
    /// Verifies bilinear matrix warping for constant shifts, zero scale, non-finite vectors, empty inputs, and invalid arguments.
    /// </summary>
    [Fact]
    public void WarpBilinear_ShouldWarpSmoothlyAndValidateInputs()
    {
        var image = new SequentialMatrix<DoublePixel>(4, 4);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
                image[x, y] = (x * 10.0) + y;
        }

        var field = new SequentialMatrix<Vector2D>(4, 4);
        field.SetAll(new Vector2D(0.5, 1.0));
        field[0, 0] = new Vector2D(double.NaN, double.PositiveInfinity);

        Matrix<DoublePixel> warped = image.WarpBilinear(field, scale: 1.0);
        Assert.Equal(image[0, 0].Value, warped[0, 0].Value, 9); // non-finite Treated as 0 shift
        Assert.Equal(image.SampleBilinear(1.5, 2.0).Value, warped[1, 1].Value, 9);

        Matrix<DoublePixel> unchanged = image.WarpBilinear(field, scale: 0.0);
        Assert.Equal(image[2, 3].Value, unchanged[2, 3].Value, 9);

        var emptyImg = new SequentialMatrix<DoublePixel>(0, 0);
        var emptyField = new SequentialMatrix<Vector2D>(0, 0);
        Assert.Empty(emptyImg.WarpBilinear(emptyField));

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<DoublePixel>)null!).WarpBilinear(field));
        Assert.Throws<ArgumentNullException>(() => image.WarpBilinear(null!));
        Assert.Throws<ArgumentException>(() => image.WarpBilinear(new SequentialMatrix<Vector2D>(3, 4)));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.WarpBilinear(field, scale: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => image.WarpBilinear(field, edgeHandling: (EdgeHandling)99));
    }

    [Fact]
    public void ResizeBox_Rgb8AndLab32AndGeneric_ShouldAverageAndRoundCorrectly()
    {
        // Rgb8 exact integer average + AwayFromZero half-step rounding (20.5 -> 21, 0.5 -> 1)
        var rgbHalfStep = new SequentialMatrix<Rgb8>(2, 2);
        rgbHalfStep[0, 0] = new Rgb8(10, 20, 0);
        rgbHalfStep[1, 0] = new Rgb8(11, 21, 1);
        rgbHalfStep[0, 1] = new Rgb8(10, 20, 0);
        rgbHalfStep[1, 1] = new Rgb8(11, 21, 1);

        Matrix<Rgb8> rgbSingle = rgbHalfStep.ResizeBox(new Size(1, 1));
        Assert.Equal(new Size(1, 1), rgbSingle.Size);
        Assert.Equal(new Rgb8(11, 21, 1), rgbSingle[0, 0]);

        // Non-integer downscale ratio 5x5 -> 2x2 splits axes into [0..2) and [2..5)
        var rgbFiveByFive = new SequentialMatrix<Rgb8>(5, 5);
        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                byte r = (byte)(x < 2 ? 10 : 40);
                byte g = (byte)(y < 2 ? 20 : 80);
                byte b = (byte)(x >= 2 && y >= 2 ? 90 : 30);
                rgbFiveByFive[x, y] = new Rgb8(r, g, b);
            }
        }

        Matrix<Rgb8> rgbTwoByTwo = rgbFiveByFive.ResizeBox(new Size(2, 2));
        Assert.Equal(new Rgb8(10, 20, 30), rgbTwoByTwo[0, 0]);
        Assert.Equal(new Rgb8(40, 20, 30), rgbTwoByTwo[1, 0]);
        Assert.Equal(new Rgb8(10, 80, 30), rgbTwoByTwo[0, 1]);
        Assert.Equal(new Rgb8(40, 80, 90), rgbTwoByTwo[1, 1]);

        // Lab32 fast path
        var lab = new SequentialMatrix<Lab32>(2, 2);
        lab[0, 0] = new Lab32(10f, -20f, 5f);
        lab[1, 0] = new Lab32(30f, 20f, 15f);
        lab[0, 1] = new Lab32(50f, -10f, 25f);
        lab[1, 1] = new Lab32(70f, 10f, 35f);

        Matrix<Lab32> labAvg = lab.ResizeBox(new Size(1, 1));
        Assert.Equal(40.0f, labAvg[0, 0].L, 5);
        Assert.Equal(0.0f, labAvg[0, 0].A, 5);
        Assert.Equal(20.0f, labAvg[0, 0].B, 5);

        // Generic paths: DoublePixel and Rgb64
        var dbl = new SequentialMatrix<DoublePixel>(4, 2);
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 4; x++)
                dbl[x, y] = (y * 100.0) + x;
        }

        Matrix<DoublePixel> dblMixed = dbl.ResizeBox(new Size(2, 4));
        Assert.Equal(new Size(2, 4), dblMixed.Size);
        Assert.Equal(0.5, dblMixed[0, 0].Value, 9);
        Assert.Equal(2.5, dblMixed[1, 0].Value, 9);
        Assert.Equal(0.5, dblMixed[0, 1].Value, 9);
        Assert.Equal(100.5, dblMixed[0, 2].Value, 9);
        Assert.Equal(102.5, dblMixed[1, 3].Value, 9);

        var rgb64 = new SequentialMatrix<Rgb64>(2, 2);
        rgb64[0, 0] = new Rgb64(0.2, 0.4, 0.6);
        rgb64[1, 0] = new Rgb64(0.4, 0.6, 0.8);
        rgb64[0, 1] = new Rgb64(0.2, 0.4, 0.6);
        rgb64[1, 1] = new Rgb64(0.4, 0.6, 0.8);

        Matrix<Rgb64> rgb64Upsampled = rgb64.ResizeBox(new Size(4, 4));
        Assert.Equal(new Size(4, 4), rgb64Upsampled.Size);
        Assert.Equal(0.2, rgb64Upsampled[0, 0].R, 9);
        Assert.Equal(0.2, rgb64Upsampled[1, 1].R, 9);
        Assert.Equal(0.4, rgb64Upsampled[2, 2].R, 9);
        Assert.Equal(0.8, rgb64Upsampled[3, 3].B, 9);

        Matrix<Rgb64> rgb64Down = rgb64.ResizeBox(new Size(1, 1));
        Assert.Equal(0.3, rgb64Down[0, 0].R, 9);
        Assert.Equal(0.5, rgb64Down[0, 0].G, 9);
        Assert.Equal(0.7, rgb64Down[0, 0].B, 9);
    }

    [Fact]
    public void ResizeBox_IdentityAndValidationAndEmpty_ShouldBehaveCorrectly()
    {
        var source = new SequentialMatrix<Rgb8>(2, 2);
        source[0, 0] = new Rgb8(10, 20, 30);
        source[1, 1] = new Rgb8(40, 50, 60);

        Matrix<Rgb8> identity = source.ResizeBox(source.Size);
        Assert.NotSame(source, identity);
        Assert.True(source.ContentEquals(identity));

        Assert.Empty(source.ResizeBox(new Size(0, 2)));
        Assert.Empty(source.ResizeBox(new Size(2, 0)));

        var empty = new SequentialMatrix<DoublePixel>(0, 0);
        Assert.Empty(empty.ResizeBox(new Size(0, 0)));

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Rgb8>)null!).ResizeBox(new Size(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ResizeBox(new Size(-1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.ResizeBox(new Size(2, -1)));
        Assert.Throws<ArgumentException>(() => empty.ResizeBox(new Size(2, 2)));
    }

    /// <summary>
    /// Verifies <see cref="MatrixResampler.SampleBilinearRgb64WhereUnchecked{TState}"/> for normal blending,
    /// fallback when no corners match, and subnormal tiny positive weights without division overflow or NaN.
    /// </summary>
    [Fact]
    public void SampleBilinearRgb64WhereUnchecked_ShouldBlendFallbackAndHandleTinySubnormalWeights()
    {
        var image = new SequentialMatrix<Rgb8>(2, 2);
        image[0, 0] = new Rgb8(51, 102, 153);
        image[1, 0] = new Rgb8(153, 204, 255);
        image[0, 1] = new Rgb8(10, 20, 30);
        image[1, 1] = new Rgb8(0, 153, 255);

        // Normal blend of top row corners (0,0) and (1,0) at x=0.25, y=0.5
        Rgb64 blended = MatrixResampler.SampleBilinearRgb64WhereUnchecked(
            image, 0.25, 0.5, 0, static (_, cy, _) => cy == 0);
        Assert.Equal(((0.75 * 51) + (0.25 * 153)) / 255.0, blended.R, 12);
        Assert.Equal(((0.75 * 102) + (0.25 * 204)) / 255.0, blended.G, 12);
        Assert.Equal(((0.75 * 153) + (0.25 * 255)) / 255.0, blended.B, 12);

        // Fallback when no corner matches
        var fallback = new Rgb64(0.125, 0.375, 0.625);
        Rgb64 noneMatched = MatrixResampler.SampleBilinearRgb64WhereUnchecked(
            image, 0.5, 0.5, 0, static (_, _, _) => false, fallback);
        Assert.Equal(fallback, noneMatched);

        // Subnormal weight (1e-156 * 1e-156 = 1e-312) where only (1, 1) matches
        Rgb64 tinyWeight = MatrixResampler.SampleBilinearRgb64WhereUnchecked(
            image, 1e-156, 1e-156, 0, static (cx, cy, _) => cx == 1 && cy == 1, fallback);
        Assert.True(double.IsFinite(tinyWeight.R) && !double.IsNaN(tinyWeight.R));
        Assert.True(double.IsFinite(tinyWeight.G) && !double.IsNaN(tinyWeight.G));
        Assert.True(double.IsFinite(tinyWeight.B) && !double.IsNaN(tinyWeight.B));
        Assert.Equal(0.0, tinyWeight.R, 12);
        Assert.Equal(153.0 / 255.0, tinyWeight.G, 12);
        Assert.Equal(1.0, tinyWeight.B, 12);
    }

    /// <summary>
    /// Verifies that <see cref="MatrixResampler.SampleNearest{T}(IReadOnlyMatrix{T}, double, double, EdgeHandling)"/>
    /// clamps large finite coordinates outside <see cref="int"/> range without integer overflow.
    /// </summary>
    [Fact]
    public void SampleNearest_LargeOutOfIntRangeCoordinates_ShouldClampWithoutOverflow()
    {
        var source = new SequentialMatrix<DoublePixel>(3, 2);
        source[0, 0] = 1.0;
        source[1, 0] = 2.0;
        source[2, 0] = 3.0;
        source[0, 1] = 4.0;
        source[1, 1] = 5.0;
        source[2, 1] = 6.0;

        Assert.Equal(source[2, 0].Value, source.SampleNearest(3e9, 0.0).Value, 9);
        Assert.Equal(source[0, 0].Value, source.SampleNearest(-3e9, 0.0).Value, 9);
        Assert.Equal(source[2, 1].Value, source.SampleNearest(1e20, 1e20).Value, 9);
        Assert.Equal(source[0, 0].Value, source.SampleNearest(-1e20, -1e20).Value, 9);
        Assert.Equal(source[2, 0].Value, source.SampleNearest(3e9, -3e9).Value, 9);
        Assert.Equal(source[0, 1].Value, source.SampleNearest(-3e9, 1e20).Value, 9);
    }
}
