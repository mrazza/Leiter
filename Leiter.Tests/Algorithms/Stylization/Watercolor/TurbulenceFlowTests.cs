namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using System.Collections;
using System.Collections.Generic;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
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
        var image = new SequentialMatrix<Rgb8>(4, 4);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                image[x, y] = new Rgb8((byte)(x * 50), (byte)(y * 60), 128);
            }
        }

        var options = new TurbulenceFlowOptions(Intensity: 0.0);
        Matrix<Rgb8> result = TurbulenceFlow.Apply(image, options);

        Assert.Equal(image.Size, result.Size);
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                Assert.Equal(image[x, y], result[x, y]);
            }
        }
    }

    /// <summary>
    /// Verifies that specifying an intensity of 0.0 with a non-matrix read-only source returns an exact copy.
    /// </summary>
    [Fact]
    public void Apply_ZeroIntensity_CustomReadOnlyMatrix_ShouldReturnExactCopy()
    {
        var custom = new TestReadOnlyMatrix<Rgb8>(3, 3, new Rgb8(100, 110, 120));
        var options = new TurbulenceFlowOptions(Intensity: 0.0);

        Matrix<Rgb8> result = TurbulenceFlow.Apply(custom, options);

        Assert.Equal(custom.Size, result.Size);
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                Assert.Equal(custom[x, y], result[x, y]);
            }
        }
    }

    /// <summary>
    /// Verifies that output is strictly deterministic for a fixed seed.
    /// </summary>
    [Fact]
    public void Apply_DeterministicPerSeed_ShouldProduceIdenticalOutput()
    {
        var image = new SequentialMatrix<Rgb8>(16, 16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                image[x, y] = new Rgb8(128, 128, 128);
            }
        }

        var options = new TurbulenceFlowOptions(Intensity: 0.4);
        Matrix<Rgb8> run1 = TurbulenceFlow.Apply(image, options);
        Matrix<Rgb8> run2 = TurbulenceFlow.Apply(image, options);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Assert.Equal(run1[x, y], run2[x, y]);
            }
        }
    }

    /// <summary>
    /// Verifies that distinct random seeds produce different output matrices.
    /// </summary>
    [Fact]
    public void Apply_DistinctSeeds_ShouldProduceDifferentOutput()
    {
        var image = new SequentialMatrix<Rgb8>(16, 16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                image[x, y] = new Rgb8(128, 128, 128);
            }
        }

        var optsA = new TurbulenceFlowOptions(
            Intensity: 0.4,
            NoiseOptions: new PerlinNoiseOptions(Seed: 101, Frequency: 0.05));

        var optsB = new TurbulenceFlowOptions(
            Intensity: 0.4,
            NoiseOptions: new PerlinNoiseOptions(Seed: 999, Frequency: 0.05));

        Matrix<Rgb8> resA = TurbulenceFlow.Apply(image, optsA);
        Matrix<Rgb8> resB = TurbulenceFlow.Apply(image, optsB);

        bool hasDifference = false;
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                if (resA[x, y] != resB[x, y])
                {
                    hasDifference = true;
                    break;
                }
            }
        }

        Assert.True(hasDifference);
    }

    /// <summary>
    /// Verifies that paper white (255) and pure black (0) remain invariant under turbulence flow density adjustments.
    /// </summary>
    [Fact]
    public void Apply_PaperWhiteAndPureBlack_RemainInvariant()
    {
        var image = new SequentialMatrix<Rgb8>(16, 16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                image[x, y] = (x + y) % 2 == 0
                    ? new Rgb8(255, 255, 255)
                    : new Rgb8(0, 0, 0);
            }
        }

        var options = new TurbulenceFlowOptions(Intensity: 0.5);
        Matrix<Rgb8> result = TurbulenceFlow.Apply(image, options);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Rgb8 original = image[x, y];
                Rgb8 actual = result[x, y];

                Assert.Equal(original, actual);
            }
        }
    }

    /// <summary>
    /// Verifies that midtones show spatially smooth, low-frequency variation.
    /// </summary>
    [Fact]
    public void Apply_Midtones_ShowsSpatiallySmoothLowFrequencyVariation()
    {
        const int width = 32;
        const int height = 32;
        var image = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                image[x, y] = new Rgb8(128, 128, 128);
            }
        }

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
    public void Apply_CustomNoiseOptions_ShouldBeRespected()
    {
        var image = new SequentialMatrix<Rgb8>(16, 16);
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                image[x, y] = new Rgb8(128, 128, 128);
            }
        }

        var defaultOpts = new TurbulenceFlowOptions(Intensity: 0.5);
        var customOpts = new TurbulenceFlowOptions(
            Intensity: 0.5,
            NoiseOptions: new PerlinNoiseOptions(Seed: 888, Frequency: 0.1, Octaves: 2));

        Matrix<Rgb8> resDefault = TurbulenceFlow.Apply(image, defaultOpts);
        Matrix<Rgb8> resCustom = TurbulenceFlow.Apply(image, customOpts);

        bool differs = false;
        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                if (resDefault[x, y] != resCustom[x, y])
                {
                    differs = true;
                    break;
                }
            }
        }

        Assert.True(differs);
    }

    /// <summary>
    /// Verifies that a custom read-only matrix implementation processes properly.
    /// </summary>
    [Fact]
    public void Apply_CustomReadOnlyMatrix_ShouldWork()
    {
        var custom = new TestReadOnlyMatrix<Rgb8>(8, 8, new Rgb8(128, 128, 128));
        Matrix<Rgb8> result = TurbulenceFlow.Apply(custom);

        Assert.Equal(custom.Size, result.Size);
    }

    private sealed class TestReadOnlyMatrix<T> : IReadOnlyMatrix<T>
        where T : struct
    {
        private readonly T[,] _data;

        public TestReadOnlyMatrix(int width, int height, T initial)
        {
            Width = width;
            Height = height;
            _data = new T[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    _data[x, y] = initial;
                }
            }
        }

        public int Width { get; }

        public int Height { get; }

        public Size Size => new(Width, Height);

        public int Count => Width * Height;

        public T this[int x, int y] => _data[x, y];

        public T this[Coord coord] => _data[coord.X, coord.Y];

        public T this[int index] => _data[index % Width, index / Width];

        public T GetElement(int index) => this[index];

        public T GetElement(int width, int height) => _data[width, height];

        public IEnumerator<T> GetEnumerator()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    yield return _data[x, y];
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
