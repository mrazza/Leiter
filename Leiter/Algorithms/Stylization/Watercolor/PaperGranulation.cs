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

        Matrix<DoublePixel> fractal = PerlinNoise.GenerateFractalField(size, opts);
        Matrix<DoublePixel> turbulence = PerlinNoise.GenerateTurbulenceField(size, opts);

        var result = new SequentialMatrix<DoublePixel>(size);

        Parallel.For(0, size.Height, y =>
        {
            for (int x = 0; x < size.Width; x++)
            {
                double f = (fractal[x, y].Value + 1.0) * 0.5;
                double t = 1.0 - turbulence[x, y].Value;
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

        PaperGranulationOptions opts = options ?? new PaperGranulationOptions();
        opts.Validate();

        if (paperHeightMap is not null && paperHeightMap.Size != image.Size)
        {
            throw new ArgumentException("The paper height map dimensions must match the image dimensions.", nameof(paperHeightMap));
        }

        if (image.Count == 0 || (opts.DensityStrength == 0.0 && opts.WarpAmplitude == 0.0))
        {
            return image.Clone();
        }

        IReadOnlyMatrix<DoublePixel> heightMap = paperHeightMap ?? GeneratePaperHeightMap(image.Size, opts.GrainNoiseOptions);

        Matrix<Vector2D>? heightGrad = opts.WarpAmplitude > 0.0
            ? SpatialGradients.Compute(heightMap)
            : null;

        int width = image.Width;
        int height = image.Height;
        var result = new SequentialMatrix<Rgb8>(width, height);

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                double rawH = heightMap[x, y].Value;
                double h = double.IsFinite(rawH) ? Math.Clamp(rawH, 0.0, 1.0) : 0.5;

                Rgb8 sampled;
                if (opts.WarpAmplitude > 0.0)
                {
                    Vector2D grad = heightGrad![x, y];
                    double sampleX = x;
                    double sampleY = y;
                    if (double.IsFinite(grad.X))
                    {
                        sampleX += opts.WarpAmplitude * grad.X;
                    }

                    if (double.IsFinite(grad.Y))
                    {
                        sampleY += opts.WarpAmplitude * grad.Y;
                    }

                    sampled = image.SampleBilinear(sampleX, sampleY, EdgeHandling.EXTEND);
                }
                else
                {
                    sampled = image[x, y];
                }

                double densityOffset = opts.DensityStrength * ((0.5 - h) * 2.0);
                result[x, y] = PigmentBlend.AdjustDensity(sampled, densityOffset);
            }
        });

        return result;
    }
}
