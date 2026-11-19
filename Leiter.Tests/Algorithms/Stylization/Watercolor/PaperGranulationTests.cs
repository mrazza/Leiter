namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="PaperGranulation"/>.
/// </summary>
public class PaperGranulationTests
{
    /// <summary>
    /// Verifies that negative width dimensions throw <see cref="ArgumentOutOfRangeException"/> when generating a paper height map.
    /// </summary>
    [Fact]
    public void GeneratePaperHeightMap_NegativeWidth_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaperGranulation.GeneratePaperHeightMap(new Size(-1, 10)));
    }

    /// <summary>
    /// Verifies that negative height dimensions throw <see cref="ArgumentOutOfRangeException"/> when generating a paper height map.
    /// </summary>
    [Fact]
    public void GeneratePaperHeightMap_NegativeHeight_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PaperGranulation.GeneratePaperHeightMap(new Size(10, -1)));
    }

    /// <summary>
    /// Verifies that invalid noise options throw <see cref="ArgumentOutOfRangeException"/> when generating a paper height map.
    /// </summary>
    [Fact]
    public void GeneratePaperHeightMap_InvalidOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var invalidOptions = new PerlinNoiseOptions(Frequency: -1.0);
        Assert.Throws<ArgumentOutOfRangeException>(() => PaperGranulation.GeneratePaperHeightMap(new Size(8, 8), invalidOptions));
    }

    /// <summary>
    /// Verifies that zero-sized dimensions produce an empty height map matrix.
    /// </summary>
    /// <param name="width">The width dimension.</param>
    /// <param name="height">The height dimension.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 8)]
    [InlineData(8, 0)]
    public void GeneratePaperHeightMap_ZeroDimensions_ShouldReturnEmptyMatrix(int width, int height)
    {
        Matrix<DoublePixel> heightMap = PaperGranulation.GeneratePaperHeightMap(new Size(width, height));

        Assert.Empty(heightMap);
        Assert.Equal(width, heightMap.Width);
        Assert.Equal(height, heightMap.Height);
    }

    /// <summary>
    /// Verifies that all generated paper height map values are within the unit interval [0.0, 1.0].
    /// </summary>
    [Fact]
    public void GeneratePaperHeightMap_Values_ShouldBeWithinUnitInterval()
    {
        Matrix<DoublePixel> heightMap = PaperGranulation.GeneratePaperHeightMap(new Size(16, 16));

        MatrixAssert.AllInRange(heightMap, 0.0, 1.0);
    }

    /// <summary>
    /// Verifies that procedural paper height map generation is deterministic for identical seeds and distinct for different seeds.
    /// </summary>
    [Fact]
    public void GeneratePaperHeightMap_SeedDeterminism_ShouldProduceExpectedOutputs()
    {
        var size = new Size(12, 12);
        var optsA = new PerlinNoiseOptions(Seed: 707);
        var optsB = new PerlinNoiseOptions(Seed: 999);

        Matrix<DoublePixel> map1 = PaperGranulation.GeneratePaperHeightMap(size, optsA);
        Matrix<DoublePixel> map2 = PaperGranulation.GeneratePaperHeightMap(size, optsA);
        Matrix<DoublePixel> mapOther = PaperGranulation.GeneratePaperHeightMap(size, optsB);

        // Verify fused single-pass output matches separate GenerateFractalField + GenerateTurbulenceField formula
        Matrix<DoublePixel> fractal = PerlinNoise.GenerateFractalField(size, optsA);
        Matrix<DoublePixel> turbulence = PerlinNoise.GenerateTurbulenceField(size, optsA);
        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                double f = (fractal[x, y].Value + 1.0) * 0.5;
                double t = 1.0 - turbulence[x, y].Value;
                double expected = Math.Clamp((0.65 * f) + (0.35 * t), 0.0, 1.0);
                Assert.Equal(expected, map1[x, y].Value, 12);
            }
        }

        MatrixAssert.Equal(map1, map2);
        MatrixAssert.NotEqual(map1, mapOther);
    }

    /// <summary>
    /// Verifies that passing a null image to Apply throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Apply_NullImage_ShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PaperGranulation.Apply(null!));
    }

    /// <summary>
    /// Verifies that passing invalid options to Apply throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Apply_InvalidOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var invalidOptions = new PaperGranulationOptions(DensityStrength: -0.5);

        Assert.Throws<ArgumentOutOfRangeException>(() => PaperGranulation.Apply(image, invalidOptions));
    }

    /// <summary>
    /// Verifies that passing a height map with mismatched dimensions throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Apply_MismatchedHeightMap_ShouldThrowArgumentException()
    {
        var image = new SequentialMatrix<Rgb8>(8, 8);
        var mismatchedMap = new SequentialMatrix<DoublePixel>(4, 4);

        Assert.Throws<ArgumentException>(() => PaperGranulation.Apply(image, null, mismatchedMap));
    }

    /// <summary>
    /// Verifies that empty images of various zero dimensions return an empty matrix.
    /// </summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 6)]
    [InlineData(6, 0)]
    public void Apply_EmptyImage_ShouldReturnEmptyMatrix(int width, int height)
    {
        var empty = new SequentialMatrix<Rgb8>(width, height);
        Matrix<Rgb8> result = PaperGranulation.Apply(empty);

        Assert.Empty(result);
        Assert.Equal(width, result.Width);
        Assert.Equal(height, result.Height);
    }

    /// <summary>
    /// Verifies that specifying density strength and warp amplitude of 0.0 returns an exact copy without modification.
    /// </summary>
    [Fact]
    public void Apply_ZeroStrengths_ShouldReturnExactClone()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Create(4, 4, (x, y) => new Rgb8((byte)(x * 50), (byte)(y * 60), 128));

        var options = new PaperGranulationOptions(DensityStrength: 0.0, WarpAmplitude: 0.0);
        Matrix<Rgb8> result = PaperGranulation.Apply(image, options);

        MatrixAssert.Equal(image, result);
    }

    /// <summary>
    /// Verifies that specifying zero strengths with a non-matrix read-only source returns an exact copy.
    /// </summary>
    [Fact]
    public void Apply_ZeroStrengths_CustomReadOnlyMatrix_ShouldReturnExactCopy()
    {
        var custom = new DummyReadOnlyMatrix<Rgb8>(3, 3, new Rgb8(110, 120, 130));
        var options = new PaperGranulationOptions(DensityStrength: 0.0, WarpAmplitude: 0.0);

        Matrix<Rgb8> result = PaperGranulation.Apply(custom, options);

        MatrixAssert.Equal(custom, result);
    }

    /// <summary>
    /// Verifies that custom height maps cause valleys to darken and peaks to lighten relative to midtones.
    /// </summary>
    [Fact]
    public void Apply_CustomHeightMap_ValleysDarkenRelativeToPeaks()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(4, 4, new Rgb8(128, 128, 128));
        // Left half is valley (h = 0.1), right half is peak (h = 0.9)
        SequentialMatrix<DoublePixel> heightMap = TestMatrices.Create(4, 4, (x, _) => new DoublePixel(x < 2 ? 0.1 : 0.9));

        var options = new PaperGranulationOptions(DensityStrength: 0.5, WarpAmplitude: 0.0);
        Matrix<Rgb8> result = PaperGranulation.Apply(image, options, heightMap);

        // Valley pixels (x < 2) must be darker than original 128.
        Assert.True(result[0, 0].R < 128);

        // Peak pixels (x >= 2) must be lighter than original 128.
        Assert.True(result[3, 0].R > 128);

        // Valley pixels must be strictly darker than peak pixels.
        Assert.True(result[0, 0].R < result[3, 0].R);
    }

    /// <summary>
    /// Verifies that procedural height maps introduce granulation across midtone images.
    /// </summary>
    [Fact]
    public void Apply_ProceduralHeightMap_MidtonesShowGranulation()
    {
        const int width = 16;
        const int height = 16;
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(width, height, new Rgb8(128, 128, 128));

        var options = new PaperGranulationOptions(DensityStrength: 0.35, WarpAmplitude: 0.0);
        Matrix<Rgb8> result = PaperGranulation.Apply(image, options);

        byte minVal = byte.MaxValue;
        byte maxVal = byte.MinValue;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte val = result[x, y].R;
                if (val < minVal) minVal = val;
                if (val > maxVal) maxVal = val;
            }
        }

        Assert.True(minVal < 128);
        Assert.True(maxVal > 128);
    }

    /// <summary>
    /// Verifies that micro-warp displacement shifts sampled coordinates near edges even when density strength is zero.
    /// </summary>
    [Fact]
    public void Apply_MicroWarpDisplacementNearEdges_DensityStrengthZeroWarpPositive()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(16, 16, new Rgb8(220, 220, 220), new Rgb8(40, 40, 40), splitX: 8);
        // Step height map creating a strong gradient at x = 7/8
        SequentialMatrix<DoublePixel> heightMap = TestMatrices.Create(16, 16, (x, _) => new DoublePixel(x < 8 ? 0.0 : 1.0));

        var options = new PaperGranulationOptions(DensityStrength: 0.0, WarpAmplitude: 2.0);
        Matrix<Rgb8> result = PaperGranulation.Apply(image, options, heightMap);

        // Near the edge, the gradient displaces sampling, altering the pixel.
        Assert.NotEqual(image[7, 8], result[7, 8]);

        // Far from the edge, gradient is zero so pixel is unchanged.
        Assert.Equal(image[0, 8], result[0, 8]);
    }

    /// <summary>
    /// Verifies that non-finite NaN values in custom height maps are handled gracefully without throwing exceptions.
    /// </summary>
    [Fact]
    public void Apply_NaNHeightMapValues_ShouldHandleGracefullyWithoutThrowing()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Constant(4, 4, new Rgb8(128, 128, 128));
        SequentialMatrix<DoublePixel> heightMap = TestMatrices.Constant(4, 4, new DoublePixel(double.NaN));

        var options = new PaperGranulationOptions(DensityStrength: 0.4, WarpAmplitude: 1.0);
        Matrix<Rgb8> result = PaperGranulation.Apply(image, options, heightMap);

        // NaN height defaults to 0.5 (neutral density offset = 0) and NaN gradients are bypassed
        MatrixAssert.Equal(image, result);
    }

    /// <summary>
    /// Verifies that paper white and pure black remain invariant under density adjustments.
    /// </summary>
    [Fact]
    public void Apply_PaperWhiteAndPureBlack_RemainInvariantUnderDensityAdjustment()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Checkerboard(8, 8, new Rgb8(255, 255, 255), new Rgb8(0, 0, 0));

        var options = new PaperGranulationOptions(DensityStrength: 0.5, WarpAmplitude: 0.0);
        Matrix<Rgb8> result = PaperGranulation.Apply(image, options);

        MatrixAssert.Equal(image, result);
    }

    /// <summary>
    /// Verifies that a single-pixel image executes successfully.
    /// </summary>
    [Fact]
    public void Apply_SinglePixel_ShouldExecuteSuccessfully()
    {
        var image = new SequentialMatrix<Rgb8>(1, 1);
        image[0, 0] = new Rgb8(120, 130, 140);

        Matrix<Rgb8> result = PaperGranulation.Apply(image);

        Assert.Single(result);
    }

    /// <summary>
    /// Verifies that a custom read-only matrix implementation processes properly.
    /// </summary>
    [Fact]
    public void Apply_CustomReadOnlyMatrix_ShouldWork()
    {
        var custom = new DummyReadOnlyMatrix<Rgb8>(8, 8, new Rgb8(128, 128, 128));
        Matrix<Rgb8> result = PaperGranulation.Apply(custom);

        Assert.Equal(custom.Size, result.Size);
    }
}
