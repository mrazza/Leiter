namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using System.Threading;
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
                double t = 0.5 + (PaperGranulation.ExpectedMeanTurbulence - turbulence[x, y].Value);
                double expected = Math.Clamp((0.65 * f) + (0.35 * t), 0.0, 1.0);
                Assert.Equal(expected, map1[x, y].Value, 12);
            }
        }

        MatrixAssert.Equal(map1, map2);
        MatrixAssert.NotEqual(map1, mapOther);
    }

    /// <summary>
    /// Verifies that procedural paper height maps are centered near 0.5 so granulation does not globally lighten midtones.
    /// </summary>
    [Fact]
    public void GeneratePaperHeightMap_DefaultGrainNoise_ShouldBeCenteredNearHalf()
    {
        Matrix<DoublePixel> heightMap = PaperGranulation.GeneratePaperHeightMap(new Size(256, 256));
        double sum = 0.0;
        for (int i = 0; i < heightMap.Count; i++)
        {
            sum += heightMap[i].Value;
        }

        double mean = sum / heightMap.Count;
        Assert.InRange(mean, 0.48, 0.52);
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

        Assert.Throws<ArgumentOutOfRangeException>(() => PaperGranulation.Apply(image, options: invalidOptions));
    }

    /// <summary>
    /// Verifies that passing a height map with mismatched dimensions throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Apply_MismatchedHeightMap_ShouldThrowArgumentException()
    {
        var image = new SequentialMatrix<Rgb8>(8, 8);
        var mismatchedMap = new SequentialMatrix<DoublePixel>(4, 4);

        Assert.Throws<ArgumentException>(() => PaperGranulation.Apply(image, mismatchedMap));
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
        Matrix<Rgb8> result = PaperGranulation.Apply(image, options: options);

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

        Matrix<Rgb8> result = PaperGranulation.Apply(custom, options: options);

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
        Matrix<Rgb8> result = PaperGranulation.Apply(image, heightMap, options);

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
        Matrix<Rgb8> result = PaperGranulation.Apply(image, options: options);

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
        Matrix<Rgb8> result = PaperGranulation.Apply(image, heightMap, options);

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
        Matrix<Rgb8> result = PaperGranulation.Apply(image, heightMap, options);

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
        Matrix<Rgb8> result = PaperGranulation.Apply(image, options: options);

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

    /// <summary>
    /// Verifies that <see cref="PaperGranulationOptions.ForSize"/> scales warp amplitude linearly for custom height maps
    /// while compensating procedural height maps cleanly at high resolutions.
    /// </summary>
    [Fact]
    public void Apply_ForSize_ShouldScaleLinearlyForCustomHeightMapAndCompensateProcedural()
    {
        PaperGranulationOptions refOpts = PaperGranulationOptions.ForSize(new Size(800, 800));
        PaperGranulationOptions bigOpts = PaperGranulationOptions.ForSize(new Size(1600, 1600));

        // Linear scaling (2x linear dimension -> 2x WarpAmplitude, not 4x area scale)
        Assert.Equal(refOpts.WarpAmplitude * 2.0, bigOpts.WarpAmplitude, 6);

        // Apply with a custom step height map and bigOpts.WarpAmplitude: pixel displacement must stay bounded linearly (< 3 px)
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(16, 16, new Rgb8(220, 220, 220), new Rgb8(40, 40, 40), splitX: 8);
        SequentialMatrix<DoublePixel> stepMap = TestMatrices.Create(16, 16, (x, _) => new DoublePixel(x < 8 ? 0.0 : 1.0));

        var customOpts = new PaperGranulationOptions(DensityStrength: 0.0, WarpAmplitude: bigOpts.WarpAmplitude);
        Matrix<Rgb8> warpedCustom = PaperGranulation.Apply(image, stepMap, customOpts);

        // At 3px away from step edge (x=5), Sobel gradient is 0, so pixel remains unwarped
        Assert.Equal(image[5, 8], warpedCustom[5, 8]);

        // Procedural height map application executes cleanly under ForSize options
        var midtone = TestMatrices.Constant(32, 32, new Rgb8(128, 128, 128));
        Matrix<Rgb8> warpedProcedural = PaperGranulation.Apply(midtone, options: PaperGranulationOptions.ForSize(midtone.Size));
        Assert.Equal(midtone.Size, warpedProcedural.Size);
    }

    /// <summary>
    /// Verifies that <see cref="PaperGranulation.ApplyCore"/> with a cached procedural height map and <c>isProcedural: true</c>
    /// matches <see cref="PaperGranulation.Apply"/> bit-for-bit even when <see cref="PaperGranulationOptions.GrainNoise"/>
    /// frequency differs from <see cref="PaperGranulationOptions.DefaultGrainNoise"/>, and preserves procedural warp scaling
    /// distinct from <c>isProcedural: false</c>.
    /// </summary>
    [Fact]
    public void ApplyCore_CachedProceduralHeightMap_WithNonDefaultFrequency_ShouldMatchApplyBitForBit()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(
            32,
            32,
            new Rgb8(220, 180, 140),
            new Rgb8(40, 80, 130),
            splitX: 16);

        var nonDefaultGrain = new PerlinNoiseOptions(
            Seed: 707,
            Frequency: 0.02875, // 4x lower than DefaultGrainNoise.Frequency (0.115) => warpScale = 4.0
            Octaves: 2,
            Lacunarity: 2.0,
            Persistence: 0.5);
        Assert.NotEqual(PaperGranulationOptions.DefaultGrainNoise.Frequency, nonDefaultGrain.Frequency);

        var options = new PaperGranulationOptions(
            DensityStrength: 0.35,
            WarpAmplitude: 1.5,
            GrainNoise: nonDefaultGrain);

        Matrix<DoublePixel> cachedHeightMap = PaperGranulation.GeneratePaperHeightMap(image.Size, nonDefaultGrain);

        // Full granulation (warp + density) with procedural flag matches Apply(..., paperHeightMap: null) bit-for-bit
        Matrix<Rgb8> expectedProcedural = PaperGranulation.Apply(image, paperHeightMap: null, options: options);
        Matrix<Rgb8> actualProcedural = PaperGranulation.ApplyCore(image, cachedHeightMap, isProcedural: true, options);
        MatrixAssert.Equal(expectedProcedural, actualProcedural);

        // Custom flag matches Apply(..., paperHeightMap: cachedHeightMap) bit-for-bit and differs from procedural warp scaling
        Matrix<Rgb8> expectedCustom = PaperGranulation.Apply(image, paperHeightMap: cachedHeightMap, options: options);
        Matrix<Rgb8> actualCustom = PaperGranulation.ApplyCore(image, cachedHeightMap, isProcedural: false, options);
        MatrixAssert.Equal(expectedCustom, actualCustom);
        MatrixAssert.NotEqual(actualProcedural, actualCustom);

        // Warp-only path (DensityStrength == 0) also matches bit-for-bit
        PaperGranulationOptions warpOnlyOptions = options with { DensityStrength = 0.0 };
        MatrixAssert.Equal(
            PaperGranulation.Apply(image, paperHeightMap: null, options: warpOnlyOptions),
            PaperGranulation.ApplyCore(image, cachedHeightMap, isProcedural: true, warpOnlyOptions));

        // No-op path (DensityStrength == 0 && WarpAmplitude == 0) returns exact clone
        PaperGranulationOptions noOpOptions = options with { DensityStrength = 0.0, WarpAmplitude = 0.0 };
        MatrixAssert.Equal(image, PaperGranulation.ApplyCore(image, cachedHeightMap, isProcedural: true, noOpOptions));
    }

    /// <summary>
    /// Verifies that a pre-canceled <see cref="CancellationToken"/> throws <see cref="OperationCanceledException"/>
    /// across <see cref="PaperGranulation.GeneratePaperHeightMap"/>, <see cref="PaperGranulation.Apply"/>, and <see cref="PaperGranulation.ApplyCore"/>,
    /// and that an active uncanceled token produces bit-for-bit identical output.
    /// </summary>
    [Fact]
    public void GeneratePaperHeightMap_And_Apply_CanceledToken_ShouldThrowOperationCanceledException()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(16, 16, new Rgb8(200, 180, 160), new Rgb8(60, 80, 100), splitX: 8);
        Matrix<DoublePixel> heightMap = PaperGranulation.GeneratePaperHeightMap(image.Size);
        var canceledToken = new CancellationToken(canceled: true);

        Assert.Throws<OperationCanceledException>(() =>
            PaperGranulation.GeneratePaperHeightMap(image.Size, cancellationToken: canceledToken));
        Assert.Throws<OperationCanceledException>(() =>
            PaperGranulation.Apply(image, cancellationToken: canceledToken));
        Assert.Throws<OperationCanceledException>(() =>
            PaperGranulation.Apply(image, heightMap, cancellationToken: canceledToken));
        Assert.Throws<OperationCanceledException>(() =>
            PaperGranulation.ApplyCore(image, heightMap, isProcedural: true, PaperGranulationOptions.Default, canceledToken));

        using var cts = new CancellationTokenSource();
        MatrixAssert.Equal(
            heightMap,
            PaperGranulation.GeneratePaperHeightMap(image.Size, cancellationToken: cts.Token));
        MatrixAssert.Equal(
            PaperGranulation.Apply(image),
            PaperGranulation.Apply(image, cancellationToken: cts.Token));
        MatrixAssert.Equal(
            PaperGranulation.ApplyCore(image, heightMap, isProcedural: true, PaperGranulationOptions.Default),
            PaperGranulation.ApplyCore(image, heightMap, isProcedural: true, PaperGranulationOptions.Default, cts.Token));
    }
}
