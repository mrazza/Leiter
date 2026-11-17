namespace Leiter.Algorithms.Stylization;

using System;
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
        if (!double.IsFinite(channel))
        {
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "Channel value must be finite.");
        }

        if (!double.IsFinite(densityOffset))
        {
            throw new ArgumentOutOfRangeException(nameof(densityOffset), densityOffset, "Density offset must be finite.");
        }

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

        if (!double.IsFinite(densityOffset))
        {
            throw new ArgumentOutOfRangeException(nameof(densityOffset), densityOffset, "Density offset must be finite.");
        }

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
        if (!double.IsFinite(densityOffset))
        {
            throw new ArgumentOutOfRangeException(nameof(densityOffset), densityOffset, "Density offset must be finite.");
        }

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

        if (!double.IsFinite(overlapDarkening) || overlapDarkening < 0.0 || overlapDarkening > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapDarkening), overlapDarkening, "Overlap darkening factor must be finite and within [0, 1].");
        }

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
        double darkerWeight = 1.0 - overlapDarkening;
        return new Rgb64(
            (darkerWeight * darker.R) + (overlapDarkening * glaze.R),
            (darkerWeight * darker.G) + (overlapDarkening * glaze.G),
            (darkerWeight * darker.B) + (overlapDarkening * glaze.B));
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
        if (!double.IsFinite(overlapDarkening) || overlapDarkening < 0.0 || overlapDarkening > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(overlapDarkening), overlapDarkening, "Overlap darkening factor must be finite and within [0, 1].");
        }

        return MixPigments(first.ToRgb64(), second.ToRgb64(), overlapDarkening).ToRgb8();
    }

    private static void ValidateFiniteColor(Rgb64 color, string paramName)
    {
        if (!double.IsFinite(color.R) || !double.IsFinite(color.G) || !double.IsFinite(color.B))
        {
            throw new ArgumentOutOfRangeException(paramName, color, "All RGB channels must be finite.");
        }
    }
}
