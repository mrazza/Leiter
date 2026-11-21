namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Threading;
using Leiter.Algorithms;
using Leiter.Algorithms.Internal;
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
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{DoublePixel}"/> containing normalized surface heights in [0.0, 1.0].</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="grainOptions"/> are invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<DoublePixel> GeneratePaperHeightMap(
        Size size,
        PerlinNoiseOptions? grainOptions = null,
        CancellationToken cancellationToken = default)
    {
        Guard.ThrowIfNegative(size);

        PerlinNoiseOptions opts = OptionsResolver.Resolve(grainOptions, PaperGranulationOptions.DefaultGrainNoise);
        cancellationToken.ThrowIfCancellationRequested();

        if (size.Width == 0 || size.Height == 0)
        {
            return new SequentialMatrix<DoublePixel>(size);
        }

        var noise = new PerlinNoise(opts.Seed);
        var result = new SequentialMatrix<DoublePixel>(size);

        void ProcessRow(int y)
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
                double combined = ScalarMath.ClampUnit((0.65 * f) + (0.35 * t));
                result[x, y] = new DoublePixel(combined);
            }
        }

        ParallelRunner.For(size.Height, cancellationToken, ProcessRow);

        return result;
    }

    /// <summary>
    /// Applies paper granulation, micro-warp surface displacement, and valley pigment darkening to an RGB image.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="paperHeightMap">Optional precomputed or custom paper height map. If <see langword="null"/>, a procedural map is synthesized.</param>
    /// <param name="options">Optional granulation configuration parameters. If <see langword="null"/>, default options are used.</param>
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the granulated image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> are invalid.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="paperHeightMap"/> dimensions do not match <paramref name="image"/> dimensions.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel>? paperHeightMap = null,
        PaperGranulationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        PaperGranulationOptions opts = OptionsResolver.Resolve(options);
        Guard.ThrowIfSizeMismatchIfNotNull(image, paperHeightMap);
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0 || (opts.DensityStrength == 0.0 && opts.WarpAmplitude == 0.0))
        {
            return image.Clone();
        }

        PerlinNoiseOptions grainOpts = opts.GrainNoise;
        bool isProcedural = paperHeightMap is null;
        IReadOnlyMatrix<DoublePixel> heightMap = paperHeightMap ?? GeneratePaperHeightMap(image.Size, grainOpts, cancellationToken);

        return ApplyCore(image, heightMap, isProcedural, opts, cancellationToken);
    }

    internal static Matrix<Rgb8> ApplyCore(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> heightMap,
        bool isProcedural,
        PaperGranulationOptions opts,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0 || (opts.DensityStrength == 0.0 && opts.WarpAmplitude == 0.0))
        {
            return image.Clone();
        }

        PerlinNoiseOptions grainOpts = opts.GrainNoise;
        double warpScale = Math.Clamp(PaperGranulationOptions.DefaultGrainNoise.Frequency / grainOpts.Frequency, 0.25, 16.0);
        double effectiveWarpAmplitude = isProcedural
            ? (opts.WarpAmplitude > double.MaxValue / warpScale ? double.MaxValue : opts.WarpAmplitude * warpScale)
            : opts.WarpAmplitude;

        Matrix<Rgb8>? warped = null;
        if (effectiveWarpAmplitude > 0.0)
        {
            Matrix<Vector2D> gradients = SpatialGradients.Compute(heightMap);
            cancellationToken.ThrowIfCancellationRequested();
            warped = image.WarpBilinear(gradients, effectiveWarpAmplitude, EdgeHandling.Extend);
            cancellationToken.ThrowIfCancellationRequested();
        }

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
            },
            cancellationToken);
    }
}
