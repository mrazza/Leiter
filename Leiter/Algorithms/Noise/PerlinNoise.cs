namespace Leiter.Algorithms.Noise;

using System;
using System.Threading.Tasks;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Generates deterministic Ken Perlin improved 2D gradient noise, fractal multi-octave fields, and displacement fields.
/// </summary>
public sealed class PerlinNoise
{
    private static readonly double Sqrt2 = Math.Sqrt(2.0);
    private static readonly double InvSqrt2 = 1.0 / Math.Sqrt(2.0);

    private static readonly (double X, double Y)[] Gradients =
    [
        (1.0, 0.0),
        (-1.0, 0.0),
        (0.0, 1.0),
        (0.0, -1.0),
        (InvSqrt2, InvSqrt2),
        (-InvSqrt2, InvSqrt2),
        (InvSqrt2, -InvSqrt2),
        (-InvSqrt2, -InvSqrt2)
    ];

    private readonly byte[] _permutation;

    /// <summary>
    /// Gets the pseudo-random seed used to initialize this noise generator.
    /// </summary>
    public int Seed { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PerlinNoise"/> class with the specified seed.
    /// </summary>
    /// <param name="seed">The pseudo-random seed used to shuffle the permutation table (default 1337).</param>
    public PerlinNoise(int seed = 1337)
    {
        Seed = seed;
        _permutation = new byte[512];
        Span<byte> p = stackalloc byte[256];
        for (int i = 0; i < 256; i++)
        {
            p[i] = (byte)i;
        }

        ulong state = unchecked((ulong)seed);
        for (int i = 255; i > 0; i--)
        {
            ulong rnd = NextSplitMix64(ref state);
            int j = (int)(rnd % (ulong)(i + 1));
            (p[i], p[j]) = (p[j], p[i]);
        }

        for (int i = 0; i < 256; i++)
        {
            _permutation[i] = p[i];
            _permutation[i + 256] = p[i];
        }
    }

    /// <summary>
    /// Evaluates Ken Perlin's improved 2D gradient noise at the specified continuous coordinates.
    /// </summary>
    /// <param name="x">The continuous X coordinate.</param>
    /// <param name="y">The continuous Y coordinate.</param>
    /// <returns>A continuous noise value clamped to [-1.0, 1.0].</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="x"/> or <paramref name="y"/> is non-finite.</exception>
    public double Sample(double x, double y)
    {
        if (!double.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x), x, "Coordinate x must be finite.");
        if (!double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(y), y, "Coordinate y must be finite.");

        int x0 = (int)Math.Floor(x);
        int y0 = (int)Math.Floor(y);

        double fx = x - x0;
        double fy = y - y0;

        double u = Fade(fx);
        double v = Fade(fy);

        int xi = x0 & 255;
        int yi = y0 & 255;

        int g00 = _permutation[_permutation[xi] + yi] & 7;
        int g10 = _permutation[_permutation[xi + 1] + yi] & 7;
        int g01 = _permutation[_permutation[xi] + yi + 1] & 7;
        int g11 = _permutation[_permutation[xi + 1] + yi + 1] & 7;

        double n00 = Grad(g00, fx, fy);
        double n10 = Grad(g10, fx - 1.0, fy);
        double n01 = Grad(g01, fx, fy - 1.0);
        double n11 = Grad(g11, fx - 1.0, fy - 1.0);

        double nx0 = Lerp(n00, n10, u);
        double nx1 = Lerp(n01, n11, u);
        double n = Lerp(nx0, nx1, v);

        return Math.Clamp(n * Sqrt2, -1.0, 1.0);
    }

    /// <summary>
    /// Evaluates multi-octave fractal Perlin noise (fractional Brownian motion) at the specified coordinates.
    /// </summary>
    /// <param name="x">The continuous X coordinate.</param>
    /// <param name="y">The continuous Y coordinate.</param>
    /// <param name="octaves">The number of fractal octaves to accumulate (default 4; must be between 1 and 16).</param>
    /// <param name="lacunarity">The frequency scaling factor per octave step (default 2.0; must be finite and greater than 0.0).</param>
    /// <param name="persistence">The amplitude decay factor per octave step (default 0.5; must be finite and non-negative).</param>
    /// <returns>A normalized fractal noise sample clamped to [-1.0, 1.0].</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when coordinates or octave parameters are outside valid ranges.</exception>
    public double SampleFractal(double x, double y, int octaves = 4, double lacunarity = 2.0, double persistence = 0.5)
    {
        ValidateOctaveParameters(x, y, octaves, lacunarity, persistence);

        double total = 0.0;
        double frequency = 1.0;
        double amplitude = 1.0;
        double maxAmplitude = 0.0;

        for (int i = 0; i < octaves; i++)
        {
            total += Sample(x * frequency, y * frequency) * amplitude;
            maxAmplitude += amplitude;
            frequency *= lacunarity;
            amplitude *= persistence;
        }

        return Math.Clamp(total / maxAmplitude, -1.0, 1.0);
    }

    /// <summary>
    /// Evaluates multi-octave absolute turbulence noise at the specified coordinates.
    /// </summary>
    /// <param name="x">The continuous X coordinate.</param>
    /// <param name="y">The continuous Y coordinate.</param>
    /// <param name="octaves">The number of fractal octaves to accumulate (default 4; must be between 1 and 16).</param>
    /// <param name="lacunarity">The frequency scaling factor per octave step (default 2.0; must be finite and greater than 0.0).</param>
    /// <param name="persistence">The amplitude decay factor per octave step (default 0.5; must be finite and non-negative).</param>
    /// <returns>A normalized turbulence noise sample clamped to [0.0, 1.0].</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when coordinates or octave parameters are outside valid ranges.</exception>
    public double SampleTurbulence(double x, double y, int octaves = 4, double lacunarity = 2.0, double persistence = 0.5)
    {
        ValidateOctaveParameters(x, y, octaves, lacunarity, persistence);

        double total = 0.0;
        double frequency = 1.0;
        double amplitude = 1.0;
        double maxAmplitude = 0.0;

        for (int i = 0; i < octaves; i++)
        {
            total += Math.Abs(Sample(x * frequency, y * frequency)) * amplitude;
            maxAmplitude += amplitude;
            frequency *= lacunarity;
            amplitude *= persistence;
        }

        return Math.Clamp(total / maxAmplitude, 0.0, 1.0);
    }

    /// <summary>
    /// Generates a two-dimensional matrix of fractal Perlin noise values in [-1.0, 1.0].
    /// </summary>
    /// <param name="size">The dimensions of the output matrix.</param>
    /// <param name="options">Optional configuration options for frequency, octaves, and seed.</param>
    /// <returns>A new <see cref="Matrix{DoublePixel}"/> containing normalized fractal noise samples.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when matrix dimensions or options are invalid.</exception>
    public static Matrix<DoublePixel> GenerateFractalField(Size size, PerlinNoiseOptions? options = null) =>
        GenerateScalarField(
            size,
            options,
            static (noise, sx, sy, opts) => noise.SampleFractal(sx, sy, opts.Octaves, opts.Lacunarity, opts.Persistence));

    /// <summary>
    /// Generates a two-dimensional matrix of absolute turbulence noise values in [0.0, 1.0].
    /// </summary>
    /// <param name="size">The dimensions of the output matrix.</param>
    /// <param name="options">Optional configuration options for frequency, octaves, and seed.</param>
    /// <returns>A new <see cref="Matrix{DoublePixel}"/> containing normalized turbulence noise samples.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when matrix dimensions or options are invalid.</exception>
    public static Matrix<DoublePixel> GenerateTurbulenceField(Size size, PerlinNoiseOptions? options = null) =>
        GenerateScalarField(
            size,
            options,
            static (noise, sx, sy, opts) => noise.SampleTurbulence(sx, sy, opts.Octaves, opts.Lacunarity, opts.Persistence));

    private static Matrix<DoublePixel> GenerateScalarField(
        Size size,
        PerlinNoiseOptions? options,
        Func<PerlinNoise, double, double, PerlinNoiseOptions, double> sampleSelector)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size.Width, nameof(size));
        ArgumentOutOfRangeException.ThrowIfNegative(size.Height, nameof(size));

        PerlinNoiseOptions opts = options ?? new PerlinNoiseOptions();
        opts.Validate();

        if (size.Width == 0 || size.Height == 0)
            return new SequentialMatrix<DoublePixel>(size);

        var noise = new PerlinNoise(opts.Seed);
        var result = new SequentialMatrix<DoublePixel>(size);

        Parallel.For(0, size.Height, y =>
        {
            for (int x = 0; x < size.Width; x++)
            {
                double val = sampleSelector(noise, x * opts.Frequency, y * opts.Frequency, opts);
                result[x, y] = new DoublePixel(val);
            }
        });

        return result;
    }

    /// <summary>
    /// Generates a two-dimensional matrix of uncorrelated 2D displacement vectors scaled by <paramref name="amplitude"/>.
    /// </summary>
    /// <param name="size">The dimensions of the output matrix.</param>
    /// <param name="amplitude">The maximum displacement scaling factor (must be finite and non-negative).</param>
    /// <param name="options">Optional configuration options for frequency, octaves, and seed.</param>
    /// <returns>A new <see cref="Matrix{Vector2D}"/> containing 2D displacement vectors.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when dimensions, amplitude, or options are invalid.</exception>
    public static Matrix<Vector2D> GenerateDisplacementField(Size size, double amplitude, PerlinNoiseOptions? options = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size.Width, nameof(size));
        ArgumentOutOfRangeException.ThrowIfNegative(size.Height, nameof(size));

        if (!double.IsFinite(amplitude) || amplitude < 0.0)
            throw new ArgumentOutOfRangeException(nameof(amplitude), amplitude, "Amplitude must be finite and non-negative.");

        PerlinNoiseOptions opts = options ?? new PerlinNoiseOptions();
        opts.Validate();

        if (size.Width == 0 || size.Height == 0)
            return new SequentialMatrix<Vector2D>(size);

        var noiseX = new PerlinNoise(opts.Seed);
        var noiseY = new PerlinNoise(unchecked(opts.Seed + (int)0x7F4A7C15));
        var result = new SequentialMatrix<Vector2D>(size);

        Parallel.For(0, size.Height, y =>
        {
            for (int x = 0; x < size.Width; x++)
            {
                double dx = noiseX.SampleFractal(x * opts.Frequency, y * opts.Frequency, opts.Octaves, opts.Lacunarity, opts.Persistence) * amplitude;
                double dy = noiseY.SampleFractal(x * opts.Frequency, y * opts.Frequency, opts.Octaves, opts.Lacunarity, opts.Persistence) * amplitude;
                result[x, y] = new Vector2D(dx, dy);
            }
        });

        return result;
    }

    private static ulong NextSplitMix64(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static double Fade(double t) => t * t * t * ((t * ((t * 6.0) - 15.0)) + 10.0);

    private static double Lerp(double a, double b, double t) => a + (t * (b - a));

    private static double Grad(int hash, double dx, double dy)
    {
        var (gx, gy) = Gradients[hash & 7];
        return (gx * dx) + (gy * dy);
    }

    private static void ValidateOctaveParameters(double x, double y, int octaves, double lacunarity, double persistence)
    {
        if (!double.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x), x, "Coordinate x must be finite.");
        if (!double.IsFinite(y))
            throw new ArgumentOutOfRangeException(nameof(y), y, "Coordinate y must be finite.");
        if (octaves is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(octaves), octaves, "Octaves must be between 1 and 16 inclusive.");
        if (!double.IsFinite(lacunarity) || lacunarity <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(lacunarity), lacunarity, "Lacunarity must be finite and greater than 0.0.");
        if (!double.IsFinite(persistence) || persistence < 0.0)
            throw new ArgumentOutOfRangeException(nameof(persistence), persistence, "Persistence must be finite and non-negative.");
    }
}
