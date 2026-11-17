namespace Leiter.Tests.Algorithms.Noise;

using System;
using Leiter.Algorithms.Noise;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="PerlinNoise"/>.
/// </summary>
public class PerlinNoiseTests
{
    /// <summary>
    /// Verifies that instances initialized with the same seed generate bit-identical samples.
    /// </summary>
    [Fact]
    public void SeedDeterminism_IdenticalSeed_ShouldProduceIdenticalSamples()
    {
        var noise1 = new PerlinNoise(42);
        var noise2 = new PerlinNoise(42);

        Assert.Equal(42, noise1.Seed);
        Assert.Equal(42, noise2.Seed);

        double sample1 = noise1.Sample(1.23, 4.56);
        double sample2 = noise2.Sample(1.23, 4.56);
        Assert.Equal(sample1, sample2);

        double fractal1 = noise1.SampleFractal(1.23, 4.56, octaves: 4, lacunarity: 2.0, persistence: 0.5);
        double fractal2 = noise2.SampleFractal(1.23, 4.56, octaves: 4, lacunarity: 2.0, persistence: 0.5);
        Assert.Equal(fractal1, fractal2);

        double turb1 = noise1.SampleTurbulence(1.23, 4.56, octaves: 4, lacunarity: 2.0, persistence: 0.5);
        double turb2 = noise2.SampleTurbulence(1.23, 4.56, octaves: 4, lacunarity: 2.0, persistence: 0.5);
        Assert.Equal(turb1, turb2);
    }

    /// <summary>
    /// Verifies that instances with different seeds produce distinct sample values.
    /// </summary>
    [Fact]
    public void DistinctSeeds_ShouldProduceDifferentSamples()
    {
        var noiseA = new PerlinNoise(100);
        var noiseB = new PerlinNoise(200);

        Assert.NotEqual(noiseA.Sample(3.14, 2.71), noiseB.Sample(3.14, 2.71));
    }

    /// <summary>
    /// Verifies that raw Perlin noise samples are bounded within [-1.0, 1.0].
    /// </summary>
    [Fact]
    public void Sample_RangeBounds_ShouldBeWithinNegativeOneAndPositiveOne()
    {
        var noise = new PerlinNoise(1337);

        for (double y = -5.0; y <= 5.0; y += 0.35)
        {
            for (double x = -5.0; x <= 5.0; x += 0.35)
            {
                double val = noise.Sample(x, y);
                Assert.True(val is >= -1.0 and <= 1.0, $"Sample out of range: {val} at ({x}, {y})");
            }
        }
    }

    /// <summary>
    /// Verifies that fractal Perlin noise samples are bounded within [-1.0, 1.0].
    /// </summary>
    [Fact]
    public void SampleFractal_RangeBounds_ShouldBeWithinNegativeOneAndPositiveOne()
    {
        var noise = new PerlinNoise(1337);

        for (double y = -3.0; y <= 3.0; y += 0.5)
        {
            for (double x = -3.0; x <= 3.0; x += 0.5)
            {
                double val = noise.SampleFractal(x, y);
                Assert.True(val is >= -1.0 and <= 1.0, $"SampleFractal out of range: {val}");
            }
        }
    }

    /// <summary>
    /// Verifies that turbulence noise samples are bounded within [0.0, 1.0].
    /// </summary>
    [Fact]
    public void SampleTurbulence_RangeBounds_ShouldBeWithinZeroAndOne()
    {
        var noise = new PerlinNoise(1337);

        for (double y = -3.0; y <= 3.0; y += 0.5)
        {
            for (double x = -3.0; x <= 3.0; x += 0.5)
            {
                double val = noise.SampleTurbulence(x, y);
                Assert.True(val is >= 0.0 and <= 1.0, $"SampleTurbulence out of range: {val}");
            }
        }
    }

    /// <summary>
    /// Verifies spatial continuity: very close spatial coordinates produce very close sample values.
    /// </summary>
    [Fact]
    public void Sample_SpatialContinuity_ShouldBeSmooth()
    {
        var noise = new PerlinNoise(1337);
        double x = 2.456;
        double y = 7.891;
        double epsilon = 1e-4;

        double baseSample = noise.Sample(x, y);
        double steppedSampleX = noise.Sample(x + epsilon, y);
        double steppedSampleY = noise.Sample(x, y + epsilon);

        Assert.True(Math.Abs(steppedSampleX - baseSample) < 0.005);
        Assert.True(Math.Abs(steppedSampleY - baseSample) < 0.005);
    }

    /// <summary>
    /// Verifies that non-finite coordinates passed to <see cref="PerlinNoise.Sample"/> throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Sample_NonFiniteCoordinates_ShouldThrowArgumentOutOfRangeException()
    {
        var noise = new PerlinNoise();

        Assert.Throws<ArgumentOutOfRangeException>(() => noise.Sample(double.NaN, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.Sample(double.PositiveInfinity, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.Sample(0.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.Sample(0.0, double.NegativeInfinity));
    }

    /// <summary>
    /// Verifies that invalid parameters passed to <see cref="PerlinNoise.SampleFractal"/> throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void SampleFractal_InvalidParameters_ShouldThrowArgumentOutOfRangeException()
    {
        var noise = new PerlinNoise();

        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleFractal(double.NaN, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleFractal(0.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleFractal(0.0, 0.0, octaves: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleFractal(0.0, 0.0, octaves: 17));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleFractal(0.0, 0.0, lacunarity: 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleFractal(0.0, 0.0, lacunarity: -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleFractal(0.0, 0.0, lacunarity: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleFractal(0.0, 0.0, persistence: -0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleFractal(0.0, 0.0, persistence: double.NaN));

        // Persistence = 0.0 is valid boundary
        double val = noise.SampleFractal(0.5, 0.5, persistence: 0.0);
        Assert.True(val is >= -1.0 and <= 1.0);
    }

    /// <summary>
    /// Verifies that invalid parameters passed to <see cref="PerlinNoise.SampleTurbulence"/> throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void SampleTurbulence_InvalidParameters_ShouldThrowArgumentOutOfRangeException()
    {
        var noise = new PerlinNoise();

        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleTurbulence(double.NaN, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleTurbulence(0.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleTurbulence(0.0, 0.0, octaves: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleTurbulence(0.0, 0.0, octaves: 17));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleTurbulence(0.0, 0.0, lacunarity: -1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => noise.SampleTurbulence(0.0, 0.0, persistence: -0.5));
    }

    /// <summary>
    /// Verifies that <see cref="PerlinNoise.GenerateFractalField"/> generates expected dimensions, values, and handles options.
    /// </summary>
    [Fact]
    public void GenerateFractalField_ValidInputs_ShouldGenerateCorrectMatrix()
    {
        var size = new Size(16, 16);
        var options = new PerlinNoiseOptions(Seed: 1234, Frequency: 0.1, Octaves: 3);

        Matrix<DoublePixel> field = PerlinNoise.GenerateFractalField(size, options);

        Assert.Equal(16, field.Width);
        Assert.Equal(16, field.Height);
        Assert.Equal(256, field.Count);

        for (int y = 0; y < field.Height; y++)
        {
            for (int x = 0; x < field.Width; x++)
            {
                double val = field[x, y].Value;
                Assert.True(val is >= -1.0 and <= 1.0);
            }
        }

        // Test with default options (null)
        Matrix<DoublePixel> defaultField = PerlinNoise.GenerateFractalField(new Size(4, 4));
        Assert.Equal(4, defaultField.Width);
    }

    /// <summary>
    /// Verifies that <see cref="PerlinNoise.GenerateFractalField"/> handles empty sizes and invalid inputs appropriately.
    /// </summary>
    [Fact]
    public void GenerateFractalField_EmptyOrInvalidInputs_ShouldHandleCorrectly()
    {
        Assert.Empty(PerlinNoise.GenerateFractalField(new Size(0, 0)));
        Assert.Empty(PerlinNoise.GenerateFractalField(new Size(0, 10)));
        Assert.Empty(PerlinNoise.GenerateFractalField(new Size(10, 0)));

        Assert.Throws<ArgumentOutOfRangeException>(() => PerlinNoise.GenerateFractalField(new Size(-1, 5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PerlinNoise.GenerateFractalField(new Size(5, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PerlinNoise.GenerateFractalField(new Size(5, 5), new PerlinNoiseOptions(Frequency: -1.0)));
    }

    /// <summary>
    /// Verifies that <see cref="PerlinNoise.GenerateTurbulenceField"/> generates expected dimensions and values in [0, 1].
    /// </summary>
    [Fact]
    public void GenerateTurbulenceField_ValidInputs_ShouldGenerateCorrectMatrix()
    {
        var size = new Size(8, 8);
        Matrix<DoublePixel> field = PerlinNoise.GenerateTurbulenceField(size);

        Assert.Equal(8, field.Width);
        Assert.Equal(8, field.Height);

        for (int y = 0; y < field.Height; y++)
        {
            for (int x = 0; x < field.Width; x++)
            {
                double val = field[x, y].Value;
                Assert.True(val is >= 0.0 and <= 1.0);
            }
        }
    }

    /// <summary>
    /// Verifies that <see cref="PerlinNoise.GenerateTurbulenceField"/> handles empty sizes and invalid inputs appropriately.
    /// </summary>
    [Fact]
    public void GenerateTurbulenceField_EmptyOrInvalidInputs_ShouldHandleCorrectly()
    {
        Assert.Empty(PerlinNoise.GenerateTurbulenceField(new Size(0, 0)));
        Assert.Empty(PerlinNoise.GenerateTurbulenceField(new Size(0, 5)));
        Assert.Empty(PerlinNoise.GenerateTurbulenceField(new Size(5, 0)));

        Assert.Throws<ArgumentOutOfRangeException>(() => PerlinNoise.GenerateTurbulenceField(new Size(-1, 5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PerlinNoise.GenerateTurbulenceField(new Size(5, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PerlinNoise.GenerateTurbulenceField(new Size(5, 5), new PerlinNoiseOptions(Octaves: 0)));
    }

    /// <summary>
    /// Verifies that <see cref="PerlinNoise.GenerateDisplacementField"/> scales vectors by amplitude and generates distinct X and Y displacements.
    /// </summary>
    [Fact]
    public void GenerateDisplacementField_ValidInputs_ShouldScaleAndGenerateVectors()
    {
        var size = new Size(8, 8);
        double amplitude = 4.5;
        var options = new PerlinNoiseOptions(Seed: 42, Frequency: 0.1);

        Matrix<Vector2D> displacement = PerlinNoise.GenerateDisplacementField(size, amplitude, options);

        Assert.Equal(8, displacement.Width);
        Assert.Equal(8, displacement.Height);

        bool foundDifferentXAndY = false;

        for (int y = 0; y < displacement.Height; y++)
        {
            for (int x = 0; x < displacement.Width; x++)
            {
                Vector2D vec = displacement[x, y];
                Assert.True(Math.Abs(vec.X) <= amplitude + 1e-6);
                Assert.True(Math.Abs(vec.Y) <= amplitude + 1e-6);

                if (Math.Abs(vec.X - vec.Y) > 1e-4)
                {
                    foundDifferentXAndY = true;
                }
            }
        }

        Assert.True(foundDifferentXAndY, "X and Y displacements should be uncorrelated.");

        // Zero amplitude produces zero vectors
        Matrix<Vector2D> zeroField = PerlinNoise.GenerateDisplacementField(size, 0.0);
        for (int i = 0; i < zeroField.Count; i++)
        {
            Assert.Equal(0.0, zeroField[i].X);
            Assert.Equal(0.0, zeroField[i].Y);
        }
    }

    /// <summary>
    /// Verifies that <see cref="PerlinNoise.GenerateDisplacementField"/> handles empty sizes and invalid inputs appropriately.
    /// </summary>
    [Fact]
    public void GenerateDisplacementField_EmptyOrInvalidInputs_ShouldHandleCorrectly()
    {
        Assert.Empty(PerlinNoise.GenerateDisplacementField(new Size(0, 0), 1.0));
        Assert.Empty(PerlinNoise.GenerateDisplacementField(new Size(0, 5), 1.0));
        Assert.Empty(PerlinNoise.GenerateDisplacementField(new Size(5, 0), 1.0));

        Assert.Throws<ArgumentOutOfRangeException>(() => PerlinNoise.GenerateDisplacementField(new Size(-1, 5), 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PerlinNoise.GenerateDisplacementField(new Size(5, -1), 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PerlinNoise.GenerateDisplacementField(new Size(5, 5), -0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => PerlinNoise.GenerateDisplacementField(new Size(5, 5), double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => PerlinNoise.GenerateDisplacementField(new Size(5, 5), 1.0, new PerlinNoiseOptions(Lacunarity: -2.0)));
    }
}
