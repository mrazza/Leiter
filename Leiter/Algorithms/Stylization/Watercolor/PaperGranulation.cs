namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Threading.Tasks;
using Leiter.Algorithms;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Simulates paper granulation and pigment settling in rough paper valleys and peaks (Bousseau et al. 2006).
/// </summary>
public static class PaperGranulation
{
    /// <summary>
    /// Generates a realistic paper surface height map combining fractal fiber noise and inverted turbulence valleys.
    /// </summary>
    /// <param name="size">The dimensions of the output height map.</param>
    /// <param name="grainOptions">Optional Perlin noise options for procedural grain synthesis. If <see langword="null"/>, default grain noise parameters are used.</param>
    /// <returns>A new <see cref="Matrix{DoublePixel}"/> containing normalized surface heights in [0.0, 1.0].</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="grainOptions"/> are invalid.</exception>
    public static Matrix<DoublePixel> GeneratePaperHeightMap(Size size, PerlinNoiseOptions? grainOptions = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size.Width, nameof(size));
        ArgumentOutOfRangeException.ThrowIfNegative(size.Height, nameof(size));

        PerlinNoiseOptions opts = grainOptions ?? PaperGranulationOptions.DefaultGrainNoise;
        opts.Validate();

        if (size.Width == 0 || size.Height == 0)
        {
            return new SequentialMatrix<DoublePixel>(size);
        }

        var noise = new PerlinNoise(opts.Seed);
        var result = new SequentialMatrix<DoublePixel>(size);

        Parallel.For(0, size.Height, y =>
        {
            double sy = y * opts.Frequency;
            for (int x = 0; x < size.Width; x++)
            {
                var (fractal, turbulence) = noise.SampleFractalAndTurbulenceUnchecked(
                    x * opts.Frequency,
                    sy,
                    opts.Octaves,
                    opts.Lacunarity,
                    opts.Persistence);
                double f = (fractal + 1.0) * 0.5;
                double t = 1.0 - turbulence;
                double combined = Math.Clamp((0.65 * f) + (0.35 * t), 0.0, 1.0);
                result[x, y] = new DoublePixel(combined);
            }
        });

        return result;
    }

    /// <summary>
    /// Applies paper granulation, micro-warp surface displacement, and valley pigment darkening to an RGB image.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="options">Optional granulation configuration parameters. If <see langword="null"/>, default options are used.</param>
    /// <param name="paperHeightMap">Optional precomputed or custom paper height map. If <see langword="null"/>, a procedural map is synthesized.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the granulated image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> are invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="paperHeightMap"/> dimensions do not match <paramref name="image"/> dimensions.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        PaperGranulationOptions? options = null,
        IReadOnlyMatrix<DoublePixel>? paperHeightMap = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        PaperGranulationOptions opts = options ?? PaperGranulationOptions.Default;
        opts.Validate();
        Guard.ThrowIfSizeMismatchIfNotNull(image, paperHeightMap);

        if (image.Count == 0 || (opts.DensityStrength == 0.0 && opts.WarpAmplitude == 0.0))
        {
            return image.Clone();
        }

        PerlinNoiseOptions grainOpts = opts.GrainNoiseOptions ?? PaperGranulationOptions.DefaultGrainNoise;
        bool isProcedural = paperHeightMap is null;
        IReadOnlyMatrix<DoublePixel> heightMap = paperHeightMap ?? GeneratePaperHeightMap(image.Size, grainOpts);

        double effectiveWarpAmplitude = isProcedural
            ? opts.WarpAmplitude * Math.Clamp(PaperGranulationOptions.DefaultGrainNoise.Frequency / grainOpts.Frequency, 0.25, 16.0)
            : opts.WarpAmplitude;

        Matrix<Rgb8>? warped = effectiveWarpAmplitude > 0.0
            ? image.WarpBilinear(SpatialGradients.Compute(heightMap), effectiveWarpAmplitude, EdgeHandling.Extend)
            : null;

        if (opts.DensityStrength == 0.0)
        {
            return warped ?? image.Clone();
        }

        return PigmentBlend.ApplyDensityField(
            warped ?? image,
            heightMap,
            rawHeight =>
            {
                double h = ScalarMath.ClampUnitOrDefault(rawHeight, fallback: 0.5);
                return opts.DensityStrength * ((0.5 - h) * 2.0);
            });
    }
}
