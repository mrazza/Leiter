namespace Leiter.Tests.Algorithms.Stylization;

using System;
using System.Threading;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;
using static Leiter.Tests.TestUtils.TestConstants;

/// <summary>
/// Provides unit tests for <see cref="PiecewiseToneCurve"/>.
/// </summary>
public class PiecewiseToneCurveTests
{
    /// <summary>
    /// Verifies that Evaluate correctly maps the domain endpoints to MinTone and MaxTone.
    /// </summary>
    [Fact]
    public void Evaluate_Endpoints_ShouldMapToMinAndMaxTone()
    {
        // Default options: MinTone = 0.05, MaxTone = 0.96
        double minResult = PiecewiseToneCurve.Evaluate(0.0);
        double maxResult = PiecewiseToneCurve.Evaluate(1.0);

        Assert.Equal(0.05, minResult, PrecisionTolerance);
        Assert.Equal(0.96, maxResult, PrecisionTolerance);

        // Custom options
        var custom = new PiecewiseToneCurveOptions(MinTone: 0.12, MaxTone: 0.88, ShadowKnee: 0.2, HighlightKnee: 0.8);
        Assert.Equal(0.12, PiecewiseToneCurve.Evaluate(0.0, custom), PrecisionTolerance);
        Assert.Equal(0.88, PiecewiseToneCurve.Evaluate(1.0, custom), PrecisionTolerance);
    }

    /// <summary>
    /// Verifies that scalar inputs outside [0.0, 1.0] are clamped to the unit interval.
    /// </summary>
    /// <param name="input">The out-of-range input value.</param>
    /// <param name="expected">The expected clamped curve value.</param>
    [Theory]
    [InlineData(-0.5, 0.05)]
    [InlineData(-100.0, 0.05)]
    [InlineData(1.5, 0.96)]
    [InlineData(50.0, 0.96)]
    public void Evaluate_OutOfRangeInputs_ShouldClamp(double input, double expected)
    {
        double result = PiecewiseToneCurve.Evaluate(input);
        Assert.Equal(expected, result, PrecisionTolerance);
    }

    /// <summary>
    /// Verifies that non-finite scalar values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="input">The non-finite input value.</param>
    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Evaluate_NonFiniteInput_ShouldThrowArgumentOutOfRangeException(double input)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PiecewiseToneCurve.Evaluate(input));
    }

    /// <summary>
    /// Verifies that passing invalid options to Evaluate throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Evaluate_InvalidOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var invalid = new PiecewiseToneCurveOptions(MinTone: 0.9, MaxTone: 0.1);
        Assert.Throws<ArgumentOutOfRangeException>(() => PiecewiseToneCurve.Evaluate(0.5, invalid));
    }

    /// <summary>
    /// Verifies that the midtone segment between ShadowKnee and HighlightKnee is strictly linear.
    /// </summary>
    [Fact]
    public void Evaluate_MidtoneSegment_ShouldBeStrictlyLinear()
    {
        var options = new PiecewiseToneCurveOptions(MinTone: 0.05, MaxTone: 0.96, ShadowKnee: 0.15, HighlightKnee: 0.85);
        double range = options.MaxTone - options.MinTone; // 0.91

        // Exact midpoint x = 0.5
        double midExpected = options.MinTone + (0.5 * range);
        Assert.Equal(midExpected, PiecewiseToneCurve.Evaluate(0.5, options), PrecisionTolerance);

        // Constant slope verification across multiple points within [0.15, 0.85]
        double yA = PiecewiseToneCurve.Evaluate(0.3, options);
        double yB = PiecewiseToneCurve.Evaluate(0.7, options);
        double calculatedSlope = (yB - yA) / (0.7 - 0.3);

        Assert.Equal(range, calculatedSlope, PrecisionTolerance);
    }

    /// <summary>
    /// Verifies that the curve is strictly monotonic across 256 uniform samples in [0.0, 1.0].
    /// </summary>
    [Fact]
    public void Evaluate_StrictMonotonicityAcross256Samples_ShouldHold()
    {
        double previous = PiecewiseToneCurve.Evaluate(0.0);
        for (int i = 1; i < 256; i++)
        {
            double x = i / 255.0;
            double current = PiecewiseToneCurve.Evaluate(x);

            Assert.True(current > previous, $"Expected strictly increasing curve at step {i} (x={x:F4}).");
            previous = current;
        }
    }

    /// <summary>
    /// Verifies C¹-continuous tangent matching at the ShadowKnee and HighlightKnee transition points.
    /// </summary>
    [Fact]
    public void Evaluate_C1ContinuityAtKnees_ShouldMatchDerivatives()
    {
        var options = new PiecewiseToneCurveOptions(MinTone: 0.05, MaxTone: 0.96, ShadowKnee: 0.15, HighlightKnee: 0.85);
        double h = 1e-5;

        // At ShadowKnee (0.15)
        double leftShadowSlope = (PiecewiseToneCurve.Evaluate(options.ShadowKnee, options) - PiecewiseToneCurve.Evaluate(options.ShadowKnee - h, options)) / h;
        double rightShadowSlope = (PiecewiseToneCurve.Evaluate(options.ShadowKnee + h, options) - PiecewiseToneCurve.Evaluate(options.ShadowKnee, options)) / h;
        Assert.Equal(leftShadowSlope, rightShadowSlope, 1e-3);

        // At HighlightKnee (0.85)
        double leftHighlightSlope = (PiecewiseToneCurve.Evaluate(options.HighlightKnee, options) - PiecewiseToneCurve.Evaluate(options.HighlightKnee - h, options)) / h;
        double rightHighlightSlope = (PiecewiseToneCurve.Evaluate(options.HighlightKnee + h, options) - PiecewiseToneCurve.Evaluate(options.HighlightKnee, options)) / h;
        Assert.Equal(leftHighlightSlope, rightHighlightSlope, 1e-3);
    }

    /// <summary>
    /// Verifies that passing a null image to Apply throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Apply_NullImage_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PiecewiseToneCurve.Apply(null!));
    }

    /// <summary>
    /// Verifies that passing invalid options to Apply throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Apply_InvalidOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var image = new SequentialMatrix<Rgb8>(2, 2);
        var invalid = new PiecewiseToneCurveOptions(ShadowKnee: 0.9, HighlightKnee: 0.2);
        Assert.Throws<ArgumentOutOfRangeException>(() => PiecewiseToneCurve.Apply(image, invalid));
    }

    /// <summary>
    /// Verifies that applying the curve to an empty (0x0) image returns an empty matrix.
    /// </summary>
    [Fact]
    public void Apply_EmptyImage_ShouldReturnEmptyMatrix()
    {
        var empty = new SequentialMatrix<Rgb8>(0, 0);
        Matrix<Rgb8> result = PiecewiseToneCurve.Apply(empty);

        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that pure black (0) and pure white (255) pixels are eliminated by the tone curve.
    /// </summary>
    [Fact]
    public void Apply_EliminatesPureBlackAndPureWhite()
    {
        var image = new SequentialMatrix<Rgb8>(2, 1);
        image[0, 0] = new Rgb8(0, 0, 0);
        image[1, 0] = new Rgb8(255, 255, 255);

        Matrix<Rgb8> result = PiecewiseToneCurve.Apply(image);

        // Default: MinTone = 0.05 => round(0.05 * 255) = 13 > 0
        Assert.True(result[0, 0].R > 0);
        Assert.Equal((byte)13, result[0, 0].R);
        Assert.Equal((byte)13, result[0, 0].G);
        Assert.Equal((byte)13, result[0, 0].B);

        // Default: MaxTone = 0.96 => round(0.96 * 255) = 245 < 255
        Assert.True(result[1, 0].R < 255);
        Assert.Equal((byte)245, result[1, 0].R);
        Assert.Equal((byte)245, result[1, 0].G);
        Assert.Equal((byte)245, result[1, 0].B);
    }

    /// <summary>
    /// Verifies that Apply correctly maps channels across a multi-pixel image with custom options.
    /// </summary>
    [Fact]
    public void Apply_MultiPixelImageWithCustomOptions_ShouldMapAllChannels()
    {
        var custom = new PiecewiseToneCurveOptions(MinTone: 0.1, MaxTone: 0.9, ShadowKnee: 0.2, HighlightKnee: 0.8);
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            3,
            2,
            (x, y) =>
            {
                byte val = (byte)(((y * 3) + x) * 40);
                return new Rgb8(val, val, val);
            });

        Matrix<Rgb8> result = PiecewiseToneCurve.Apply(image, custom);

        Assert.Equal(image.Width, result.Width);
        Assert.Equal(image.Height, result.Height);

        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                byte inputVal = image[x, y].R;
                double expectedEval = PiecewiseToneCurve.Evaluate(inputVal / 255.0, custom);
                byte expectedByte = (byte)Math.Clamp((int)Math.Round(expectedEval * 255.0, MidpointRounding.AwayFromZero), 0, 255);

                Assert.Equal(expectedByte, result[x, y].R);
                Assert.Equal(expectedByte, result[x, y].G);
                Assert.Equal(expectedByte, result[x, y].B);
            }
        }
    }

    /// <summary>
    /// Verifies that a pre-canceled <see cref="CancellationToken"/> throws <see cref="OperationCanceledException"/>
    /// and that an active uncanceled token produces bit-for-bit identical output.
    /// </summary>
    [Fact]
    public void Apply_CanceledToken_ShouldThrowOperationCanceledException()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            8,
            8,
            (x, y) => new Rgb8((byte)(x * 32), (byte)(y * 32), (byte)((x + y) * 16)));
        var empty = new SequentialMatrix<Rgb8>(0, 0);
        var canceledToken = new CancellationToken(canceled: true);

        Assert.Throws<OperationCanceledException>(() => PiecewiseToneCurve.Apply(image, cancellationToken: canceledToken));
        Assert.Throws<OperationCanceledException>(() => PiecewiseToneCurve.Apply(empty, cancellationToken: canceledToken));

        using var cts = new CancellationTokenSource();
        Matrix<Rgb8> expected = PiecewiseToneCurve.Apply(image);
        Matrix<Rgb8> actual = PiecewiseToneCurve.Apply(image, cancellationToken: cts.Token);
        MatrixAssert.Equal(expected, actual);
    }
}
