namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
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
    /// Applies turbulence flow displacement to an RGB image using multi-octave fractal noise.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="options">Optional turbulence flow configuration parameters. If <see langword="null"/>, default options are used.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the stylized image with turbulence flow.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> are invalid.</exception>
    public static Matrix<Rgb8> Apply(IReadOnlyMatrix<Rgb8> image, TurbulenceFlowOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        TurbulenceFlowOptions opts = options ?? new TurbulenceFlowOptions();
        opts.Validate();

        if (image.Count == 0 || opts.Intensity == 0.0)
        {
            return image.Clone();
        }

        PerlinNoiseOptions noiseOpts = opts.NoiseOptions ?? TurbulenceFlowOptions.DefaultNoise;
        Matrix<DoublePixel> noiseField = PerlinNoise.GenerateFractalField(image.Size, noiseOpts);

        int width = image.Width;
        int height = image.Height;
        var result = new SequentialMatrix<Rgb8>(width, height);

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                result[x, y] = PigmentBlend.AdjustDensity(image[x, y], opts.Intensity * noiseField[x, y].Value);
            }
        });

        return result;
    }
}
