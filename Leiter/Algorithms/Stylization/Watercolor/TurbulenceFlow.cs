namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Simulates low-frequency turbulence flow and chaotic pigment dispersion across wet watercolor washes (Bousseau et al. 2006).
/// </summary>
public static class TurbulenceFlow
{
    /// <summary>
    /// Applies turbulence flow pigment density variation to an RGB image using multi-octave fractal noise.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="options">Optional turbulence flow configuration parameters. If <see langword="null"/>, default options are used.</param>
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the stylized image with turbulence flow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> are invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<Rgb8> Apply(
        IReadOnlyMatrix<Rgb8> image,
        TurbulenceFlowOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(image);

        TurbulenceFlowOptions opts = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0 || opts.Intensity == 0.0)
        {
            return image.Clone();
        }

        Matrix<DoublePixel> densityField = ComputeDensityFieldCore(image.Size, opts, cancellationToken);
        return PigmentBlend.ApplyDensityField(image, densityField, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Computes the per-pixel signed turbulence flow pigment density offset field for the specified dimensions.
    /// </summary>
    /// <remarks>
    /// The returned field depends only on <paramref name="size"/> and <paramref name="options"/> and can be passed directly
    /// to <see cref="PigmentBlend.ApplyDensityField(IReadOnlyMatrix{Rgb8}, IReadOnlyMatrix{DoublePixel}, double, CancellationToken)"/>
    /// or summed with other pigment density fields before blending.
    /// </remarks>
    /// <param name="size">The dimensions of the output density field.</param>
    /// <param name="options">Optional turbulence flow configuration parameters. If <see langword="null"/>, default options are used.</param>
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{DoublePixel}"/> containing signed turbulence density offsets matching <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="options"/> are invalid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<DoublePixel> ComputeDensityField(
        Size size,
        TurbulenceFlowOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.ThrowIfNegative(size);

        TurbulenceFlowOptions opts = OptionsResolver.Resolve(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (size.Width == 0 || size.Height == 0 || opts.Intensity == 0.0)
        {
            return new SequentialMatrix<DoublePixel>(size);
        }

        return ComputeDensityFieldCore(size, opts, cancellationToken);
    }

    private static Matrix<DoublePixel> ComputeDensityFieldCore(
        Size size,
        TurbulenceFlowOptions opts,
        CancellationToken cancellationToken)
    {
        PerlinNoiseOptions noiseOpts = opts.Noise;
        var noise = new PerlinNoise(noiseOpts.Seed);
        var result = new SequentialMatrix<DoublePixel>(size);
        double intensity = opts.Intensity;

        void ProcessRow(int y)
        {
            double sy = y * noiseOpts.Frequency;
            for (int x = 0; x < size.Width; x++)
            {
                double fractal = noise.SampleFractalAndTurbulenceUnchecked(
                    x * noiseOpts.Frequency,
                    sy,
                    noiseOpts.Octaves,
                    noiseOpts.Lacunarity,
                    noiseOpts.Persistence).Fractal;
                result[x, y] = new DoublePixel(intensity * fractal);
            }
        }

        if (cancellationToken.CanBeCanceled)
        {
            Parallel.For(0, size.Height, new ParallelOptions { CancellationToken = cancellationToken }, ProcessRow);
        }
        else
        {
            Parallel.For(0, size.Height, ProcessRow);
        }

        return result;
    }
}
