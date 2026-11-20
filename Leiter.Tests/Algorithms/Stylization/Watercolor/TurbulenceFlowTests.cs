namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using System.Threading;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="TurbulenceFlow"/>.
/// </summary>
public class TurbulenceFlowTests
{
    /// <summary>
    /// Verifies that passing a null image to Apply throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Apply_NullImage_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => TurbulenceFlow.Apply(null!));
    }

    /// <summary>
    /// Verifies that passing invalid options to Apply throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Apply_InvalidOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var invalidOptions = new TurbulenceFlowOptions(Intensity: -0.5);

        Assert.Throws<ArgumentOutOfRangeException>(() => TurbulenceFlow.Apply(image, invalidOptions));
    }

    /// <summary>
    /// Verifies that empty images of various zero dimensions return an empty matrix.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 8)]
    [InlineData(8, 0)]
    public void Apply_EmptyImage_ShouldReturnEmptyMatrix(int width, int height)
    {
        var empty = new SequentialMatrix<Rgb8>(width, height);
        Matrix<Rgb8> result = TurbulenceFlow.Apply(empty);

        Assert.Empty(result);
        Assert.Equal(width, result.Width);
        Assert.Equal(height, result.Height);
    }

    /// <summary>
    /// Verifies that a single-pixel image executes without errors.
    /// </summary>
    [Fact]
    public void Apply_SinglePixel_ShouldExecuteSuccessfully()
    {
        var image = new SequentialMatrix<Rgb8>(1, 1);
        image[0, 0] = new Rgb8(128, 128, 128);

        Matrix<Rgb8> result = TurbulenceFlow.Apply(image);

        Assert.Single(result);
        Assert.True(result[0, 0].R >= 0 && result[0, 0].R <= 255);
    }

    /// <summary>
    /// Verifies that specifying an intensity of 0.0 returns an exact copy without modification.
    /// </summary>
    [Fact]
    public void Apply_ZeroIntensity_ShouldReturnExactClone()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Create(4, 4, (x, y) => new Rgb8((byte)(x * 50), (byte)(y * 60), 128));

        var options = new TurbulenceFlowOptions(Intensity: 0.0);
        Matrix<Rgb8> result = TurbulenceFlow.Apply(image, options);

        MatrixAssert.Equal(image, result);
    }

    /// <summary>
    /// Verifies that specifying an intensity of 0.0 with a non-matrix read-only source returns an exact copy.
    /// </summary>
    [Fact]
    public void Apply_ZeroIntensity_CustomReadOnlyMatrix_ShouldReturnExactCopy()
    {
        var custom = new DummyReadOnlyMatrix<Rgb8>(3, 3, new Rgb8(100, 110, 120));
        var options = new TurbulenceFlowOptions(Intensity: 0.0);

        Matrix<Rgb8> result = TurbulenceFlow.Apply(custom, options);

        MatrixAssert.Equal(custom, result);
    }

    /// <summary>
    /// Verifies that output is strictly deterministic for a fixed seed.
    /// </summary>
    [Fact]
    public void Apply_DeterministicPerSeed_ShouldProduceIdenticalOutput()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(16, 16, new Rgb8(128, 128, 128));

        var options = new TurbulenceFlowOptions(Intensity: 0.4);
        Matrix<Rgb8> run1 = TurbulenceFlow.Apply(image, options);
        Matrix<Rgb8> run2 = TurbulenceFlow.Apply(image, options);

        MatrixAssert.Equal(run1, run2);
    }

    /// <summary>
    /// Verifies that distinct random seeds produce different output matrices.
    /// </summary>
    [Fact]
    public void Apply_DistinctSeeds_ShouldProduceDifferentOutput()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(16, 16, new Rgb8(128, 128, 128));

        var optsA = new TurbulenceFlowOptions(
            Intensity: 0.4,
            Noise: new PerlinNoiseOptions(Seed: 101, Frequency: 0.05));

        var optsB = new TurbulenceFlowOptions(
            Intensity: 0.4,
            Noise: new PerlinNoiseOptions(Seed: 999, Frequency: 0.05));

        Matrix<Rgb8> resA = TurbulenceFlow.Apply(image, optsA);
        Matrix<Rgb8> resB = TurbulenceFlow.Apply(image, optsB);

        MatrixAssert.NotEqual(resA, resB);
    }

    /// <summary>
    /// Verifies that paper white (255) and pure black (0) remain invariant under turbulence flow density adjustments.
    /// </summary>
    [Fact]
    public void Apply_PaperWhiteAndPureBlack_RemainInvariant()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Checkerboard(16, 16, new Rgb8(255, 255, 255), new Rgb8(0, 0, 0));

        var options = new TurbulenceFlowOptions(Intensity: 0.5);
        Matrix<Rgb8> result = TurbulenceFlow.Apply(image, options);

        MatrixAssert.Equal(image, result);
    }

    /// <summary>
    /// Verifies that midtones show spatially smooth, low-frequency variation.
    /// </summary>
    [Fact]
    public void Apply_Midtones_ShowsSpatiallySmoothLowFrequencyVariation()
    {
        const int width = 32;
        const int height = 32;
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(width, height, new Rgb8(128, 128, 128));

        var options = new TurbulenceFlowOptions(Intensity: 0.4);
        Matrix<Rgb8> result = TurbulenceFlow.Apply(image, options);

        byte minVal = byte.MaxValue;
        byte maxVal = byte.MinValue;
        int maxAdjacentDiff = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte val = result[x, y].R;
                if (val < minVal) minVal = val;
                if (val > maxVal) maxVal = val;

                if (x + 1 < width)
                {
                    int diffX = Math.Abs(result[x + 1, y].R - val);
                    if (diffX > maxAdjacentDiff) maxAdjacentDiff = diffX;
                }

                if (y + 1 < height)
                {
                    int diffY = Math.Abs(result[x, y + 1].R - val);
                    if (diffY > maxAdjacentDiff) maxAdjacentDiff = diffY;
                }
            }
        }

        // Midtone dispersion must vary both above and below 128.
        Assert.True(minVal < 128);
        Assert.True(maxVal > 128);

        // Low base frequency (0.015) ensures adjacent pixels vary smoothly with tiny step differences.
        Assert.True(maxAdjacentDiff <= 4);
    }

    /// <summary>
    /// Verifies that custom PerlinNoiseOptions are properly utilized.
    /// </summary>
    [Fact]
    public void Apply_CustomNoise_ShouldBeRespected()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(16, 16, new Rgb8(128, 128, 128));

        var defaultOpts = new TurbulenceFlowOptions(Intensity: 0.5);
        var customOpts = new TurbulenceFlowOptions(
            Intensity: 0.5,
            Noise: new PerlinNoiseOptions(Seed: 888, Frequency: 0.1, Octaves: 2));

        Matrix<Rgb8> resDefault = TurbulenceFlow.Apply(image, defaultOpts);
        Matrix<Rgb8> resCustom = TurbulenceFlow.Apply(image, customOpts);

        MatrixAssert.NotEqual(resDefault, resCustom);
    }

    /// <summary>
    /// Verifies that a custom read-only matrix implementation processes properly.
    /// </summary>
    [Fact]
    public void Apply_CustomReadOnlyMatrix_ShouldWork()
    {
        var custom = new DummyReadOnlyMatrix<Rgb8>(8, 8, new Rgb8(128, 128, 128));
        Matrix<Rgb8> result = TurbulenceFlow.Apply(custom);

        Assert.Equal(custom.Size, result.Size);
    }

    /// <summary>
    /// Verifies that <see cref="TurbulenceFlow.ComputeDensityField"/> validates negative sizes and invalid options.
    /// </summary>
    [Fact]
    public void ComputeDensityField_InvalidInputs_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TurbulenceFlow.ComputeDensityField(new Size(-1, 8)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TurbulenceFlow.ComputeDensityField(new Size(8, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TurbulenceFlow.ComputeDensityField(new Size(8, 8), new TurbulenceFlowOptions(Intensity: -0.2)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TurbulenceFlow.ComputeDensityField(
                new Size(8, 8),
                new TurbulenceFlowOptions(Intensity: 0.0, Noise: new PerlinNoiseOptions(Frequency: -1.0))));
    }

    /// <summary>
    /// Verifies that <see cref="TurbulenceFlow.ComputeDensityField"/> returns an empty matrix for zero dimensions.
    /// </summary>
    /// <param name="width">The width dimension.</param>
    /// <param name="height">The height dimension.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 8)]
    [InlineData(8, 0)]
    public void ComputeDensityField_ZeroDimensions_ShouldReturnEmptyMatrix(int width, int height)
    {
        Matrix<DoublePixel> field = TurbulenceFlow.ComputeDensityField(new Size(width, height));

        Assert.Empty(field);
        Assert.Equal(width, field.Width);
        Assert.Equal(height, field.Height);
    }

    /// <summary>
    /// Verifies that <see cref="TurbulenceFlow.ComputeDensityField"/> returns an all-zero field when intensity is zero.
    /// </summary>
    [Fact]
    public void ComputeDensityField_ZeroIntensity_ShouldReturnZeroField()
    {
        Matrix<DoublePixel> field = TurbulenceFlow.ComputeDensityField(new Size(12, 12), new TurbulenceFlowOptions(Intensity: 0.0));

        Assert.Equal(new Size(12, 12), field.Size);
        MatrixAssert.AllInRange(field, 0.0, 0.0);
    }

    /// <summary>
    /// Verifies that <see cref="TurbulenceFlow.ComputeDensityField"/> matches scaled Perlin fractal noise
    /// and reproduces <see cref="TurbulenceFlow.Apply"/> bit-for-bit when applied via <see cref="PigmentBlend"/>.
    /// </summary>
    [Fact]
    public void ComputeDensityField_ShouldMatchPerlinFormulaAndApplyBitForBit()
    {
        var size = new Size(24, 24);
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(size.Width, size.Height, new Rgb8(128, 140, 152));

        // Default options (null)
        Matrix<DoublePixel> defaultField = TurbulenceFlow.ComputeDensityField(size);
        Matrix<DoublePixel> expectedFractal = PerlinNoise.GenerateFractalField(size, TurbulenceFlowOptions.DefaultNoise);
        double defaultIntensity = TurbulenceFlowOptions.Default.Intensity;
        for (int y = 0; y < size.Height; y++)
        {
            for (int x = 0; x < size.Width; x++)
            {
                Assert.Equal(defaultIntensity * expectedFractal[x, y].Value, defaultField[x, y].Value, 12);
            }
        }

        MatrixAssert.Equal(
            TurbulenceFlow.Apply(image),
            PigmentBlend.ApplyDensityField(image, defaultField));

        // Custom options
        var customOptions = new TurbulenceFlowOptions(
            Intensity: 0.55,
            Noise: new PerlinNoiseOptions(Seed: 777, Frequency: 0.06, Octaves: 3));
        Matrix<DoublePixel> customField = TurbulenceFlow.ComputeDensityField(size, customOptions);
        MatrixAssert.Equal(
            TurbulenceFlow.Apply(image, customOptions),
            PigmentBlend.ApplyDensityField(image, customField));
    }

    /// <summary>
    /// Verifies that a pre-canceled <see cref="CancellationToken"/> throws <see cref="OperationCanceledException"/>
    /// for both <see cref="TurbulenceFlow.Apply"/> and <see cref="TurbulenceFlow.ComputeDensityField"/>,
    /// and that an active uncanceled token matches default execution bit-for-bit.
    /// </summary>
    [Fact]
    public void Apply_And_ComputeDensityField_CanceledToken_ShouldThrowOperationCanceledException()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(16, 16, new Rgb8(128, 128, 128));
        var canceledToken = new CancellationToken(canceled: true);

        Assert.Throws<OperationCanceledException>(() => TurbulenceFlow.Apply(image, cancellationToken: canceledToken));
        Assert.Throws<OperationCanceledException>(() => TurbulenceFlow.Apply(image, new TurbulenceFlowOptions(Intensity: 0.0), canceledToken));
        Assert.Throws<OperationCanceledException>(() => TurbulenceFlow.ComputeDensityField(image.Size, cancellationToken: canceledToken));
        Assert.Throws<OperationCanceledException>(() => TurbulenceFlow.ComputeDensityField(image.Size, new TurbulenceFlowOptions(Intensity: 0.0), canceledToken));

        using var cts = new CancellationTokenSource();
        MatrixAssert.Equal(TurbulenceFlow.ComputeDensityField(image.Size), TurbulenceFlow.ComputeDensityField(image.Size, cancellationToken: cts.Token));
        MatrixAssert.Equal(TurbulenceFlow.Apply(image), TurbulenceFlow.Apply(image, cancellationToken: cts.Token));
    }
}
