namespace Leiter.Algorithms.Stylization;

using System;
using System.Threading.Tasks;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Provides pigment density adjustments and subtractive-like pigment blending operations for watercolor stylization (Bousseau et al. 2006).
/// </summary>
public static class PigmentBlend
{
    /// <summary>
    /// Adjusts an individual color channel density using the Bousseau et al. quadratic pigment density formula.
    /// </summary>
    /// <remarks>
    /// Leaves pure black (0.0) and paper white (1.0) invariant, while darkening midtones when <paramref name="densityOffset"/> is positive
    /// and lightening midtones when <paramref name="densityOffset"/> is negative. Values are clamped to [0.0, 1.0].
    /// </remarks>
    /// <param name="channel">The channel intensity in [0.0, 1.0].</param>
    /// <param name="densityOffset">The additive density offset to apply.</param>
    /// <returns>The density-adjusted channel value clamped to [0.0, 1.0].</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="channel"/> or <paramref name="densityOffset"/> is not finite.</exception>
    public static double AdjustChannelDensity(double channel, double densityOffset)
    {
        Guard.ThrowIfNotFinite(channel);
        Guard.ThrowIfNotFinite(densityOffset);

        double c = Math.Clamp(channel, 0.0, 1.0);
        return Math.Clamp(c - (c * (1.0 - c) * densityOffset), 0.0, 1.0);
    }

    /// <summary>
    /// Adjusts the pigment density of a 64-bit floating-point RGB color.
    /// </summary>
    /// <param name="color">The RGB color to adjust.</param>
    /// <param name="densityOffset">The additive density offset to apply to all channels.</param>
    /// <returns>A new <see cref="Rgb64"/> color with density-adjusted channels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any color channel or <paramref name="densityOffset"/> is not finite.</exception>
    public static Rgb64 AdjustDensity(Rgb64 color, double densityOffset)
    {
        ValidateFiniteColor(color, nameof(color));
        Guard.ThrowIfNotFinite(densityOffset);

        return new Rgb64(
            AdjustChannelDensity(color.R, densityOffset),
            AdjustChannelDensity(color.G, densityOffset),
            AdjustChannelDensity(color.B, densityOffset));
    }

    /// <summary>
    /// Adjusts the pigment density of an 8-bit RGB color.
    /// </summary>
    /// <param name="color">The 8-bit RGB color to adjust.</param>
    /// <param name="densityOffset">The additive density offset to apply.</param>
    /// <returns>A new <see cref="Rgb8"/> color with density-adjusted channels.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="densityOffset"/> is not finite.</exception>
    public static Rgb8 AdjustDensity(Rgb8 color, double densityOffset)
    {
        Guard.ThrowIfNotFinite(densityOffset);

        return AdjustDensity(color.ToRgb64(), densityOffset).ToRgb8();
    }

    /// <summary>
    /// Blends two pigment colors using a subtractive-like multiplicative glaze model with an overlap darkening factor.
    /// </summary>
    /// <remarks>
    /// The darker of the two colors (evaluated by intensity) acts as the base pigment layer, and the lighter layer
    /// forms a multiplicative glaze over it. When <paramref name="overlapDarkening"/> is 0, the darker pigment is returned invariant;
    /// when 1, the result equals the multiplicative glaze.
    /// </remarks>
    /// <param name="first">The first pigment color.</param>
    /// <param name="second">The second pigment color.</param>
    /// <param name="overlapDarkening">The overlap darkening blend weight in [0.0, 1.0] (default 0.5).</param>
    /// <returns>The resulting mixed pigment color in <see cref="Rgb64"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any channel of <paramref name="first"/> or <paramref name="second"/> is not finite, or when <paramref name="overlapDarkening"/> is not finite or outside [0.0, 1.0].</exception>
    public static Rgb64 MixPigments(Rgb64 first, Rgb64 second, double overlapDarkening = 0.5)
    {
        ValidateFiniteColor(first, nameof(first));
        ValidateFiniteColor(second, nameof(second));
        Guard.ThrowIfNotFiniteInRange(overlapDarkening, 0.0, 1.0);

        Rgb64 c1 = new(
            Math.Clamp(first.R, 0.0, 1.0),
            Math.Clamp(first.G, 0.0, 1.0),
            Math.Clamp(first.B, 0.0, 1.0));
        Rgb64 c2 = new(
            Math.Clamp(second.R, 0.0, 1.0),
            Math.Clamp(second.G, 0.0, 1.0),
            Math.Clamp(second.B, 0.0, 1.0));

        Rgb64 darker;
        Rgb64 lighter;
        if (c1.GetIntensity() <= c2.GetIntensity())
        {
            darker = c1;
            lighter = c2;
        }
        else
        {
            darker = c2;
            lighter = c1;
        }

        Rgb64 glaze = new(darker.R * lighter.R, darker.G * lighter.G, darker.B * lighter.B);
        return Rgb64.Lerp(darker, glaze, overlapDarkening);
    }

    /// <summary>
    /// Blends two 8-bit pigment colors using a subtractive-like multiplicative glaze model with an overlap darkening factor.
    /// </summary>
    /// <param name="first">The first pigment color.</param>
    /// <param name="second">The second pigment color.</param>
    /// <param name="overlapDarkening">The overlap darkening blend weight in [0.0, 1.0] (default 0.5).</param>
    /// <returns>The resulting mixed pigment color in <see cref="Rgb8"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="overlapDarkening"/> is not finite or outside [0.0, 1.0].</exception>
    public static Rgb8 MixPigments(Rgb8 first, Rgb8 second, double overlapDarkening = 0.5)
    {
        Guard.ThrowIfNotFiniteInRange(overlapDarkening, 0.0, 1.0);

        return MixPigments(first.ToRgb64(), second.ToRgb64(), overlapDarkening).ToRgb8();
    }

    /// <summary>
    /// Applies per-pixel pigment density adjustments across <paramref name="image"/> scaled by <paramref name="intensity"/>.
    /// </summary>
    /// <param name="image">The input 8-bit RGB image.</param>
    /// <param name="densityField">The scalar density field matching <paramref name="image"/> dimensions.</param>
    /// <param name="intensity">The scalar multiplier applied to each field sample (default <c>1.0</c>).</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing density-adjusted pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="densityField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="densityField"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="intensity"/> or any field sample is non-finite.</exception>
    public static Matrix<Rgb8> ApplyDensityField(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> densityField,
        double intensity = 1.0)
    {
        ArgumentNullException.ThrowIfNull(image);
        Guard.ThrowIfSizeMismatch(image, densityField);
        Guard.ThrowIfNotFinite(intensity);

        if (image.Count == 0 || intensity == 0.0)
        {
            return image.Clone();
        }

        return ApplyDensityField(image, densityField, value => intensity * value);
    }

    /// <summary>
    /// Applies per-pixel pigment density adjustments across <paramref name="image"/> using a custom scalar mapper.
    /// </summary>
    /// <param name="image">The input 8-bit RGB image.</param>
    /// <param name="field">The scalar field matching <paramref name="image"/> dimensions.</param>
    /// <param name="densityOffsetSelector">Maps each raw field sample to the density offset passed to <see cref="AdjustDensity(Rgb8, double)"/>.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing density-adjusted pixels.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="image"/>, <paramref name="field"/>, or <paramref name="densityOffsetSelector"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="field"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="densityOffsetSelector"/> returns a non-finite density offset.</exception>
    public static Matrix<Rgb8> ApplyDensityField(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> field,
        Func<double, double> densityOffsetSelector)
    {
        ArgumentNullException.ThrowIfNull(image);
        Guard.ThrowIfSizeMismatch(image, field);
        ArgumentNullException.ThrowIfNull(densityOffsetSelector);

        if (image.Count == 0)
        {
            return new SequentialMatrix<Rgb8>(image.Size);
        }

        int width = image.Width;
        int height = image.Height;
        var result = new SequentialMatrix<Rgb8>(width, height);

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                result[x, y] = AdjustDensity(image[x, y], densityOffsetSelector(field[x, y].Value));
            }
        });

        return result;
    }

    private static void ValidateFiniteColor(Rgb64 color, string paramName)
    {
        if (!double.IsFinite(color.R) || !double.IsFinite(color.G) || !double.IsFinite(color.B))
        {
            throw new ArgumentOutOfRangeException(paramName, color, "All RGB channels must be finite.");
        }
    }
}
