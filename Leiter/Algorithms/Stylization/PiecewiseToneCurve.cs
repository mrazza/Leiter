namespace Leiter.Algorithms.Stylization;

using System;
using Leiter.Algorithms.Color;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Evaluates and applies a C¹-continuous piecewise tone reproduction curve with shadow toe easing, linear midtones, and highlight shoulder compression.
/// </summary>
public static class PiecewiseToneCurve
{
    private const double EndpointSlopeScale = 0.35;

    /// <summary>
    /// Evaluates the piecewise tone reproduction curve for a single normalized scalar value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The curve maps input values in [0.0, 1.0] monotonically to [MinTone, MaxTone]. The domain is partitioned into:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Shadow toe [0.0, ShadowKnee]: Cubic Hermite spline easing from slope 0.35 * toneRange to toneRange.</description></item>
    /// <item><description>Linear midtones [ShadowKnee, HighlightKnee]: Exact linear segment with constant slope equal to toneRange.</description></item>
    /// <item><description>Highlight shoulder [HighlightKnee, 1.0]: Cubic Hermite spline easing from slope toneRange to 0.35 * toneRange.</description></item>
    /// </list>
    /// <para>
    /// The curve is strictly increasing, C¹-continuous at both knee points, and clamps the output to [MinTone, MaxTone].
    /// </para>
    /// </remarks>
    /// <param name="value">The input scalar value to map.</param>
    /// <param name="options">Optional tone curve options. If <see langword="null"/>, default options are used.</param>
    /// <returns>The tone-mapped scalar value in [MinTone, MaxTone].</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> is non-finite or <paramref name="options"/> are invalid.</exception>
    public static double Evaluate(double value, PiecewiseToneCurveOptions? options = null)
    {
        Guard.ThrowIfNotFinite(value);

        PiecewiseToneCurveOptions resolvedOptions = OptionsResolver.Resolve(options, PiecewiseToneCurveOptions.Default);

        return EvaluateCore(value, resolvedOptions);
    }

    private static double EvaluateCore(double value, PiecewiseToneCurveOptions resolvedOptions)
    {
        double clampedInput = Math.Clamp(value, 0.0, 1.0);
        double minTone = resolvedOptions.MinTone;
        double maxTone = resolvedOptions.MaxTone;
        double toneRange = maxTone - minTone;
        double midtoneSlope = toneRange;
        double endpointSlope = EndpointSlopeScale * midtoneSlope;

        double shadowKnee = resolvedOptions.ShadowKnee;
        double highlightKnee = resolvedOptions.HighlightKnee;
        double shadowKneeTone = minTone + (shadowKnee * toneRange);
        double highlightKneeTone = minTone + (highlightKnee * toneRange);

        double mappedTone;
        if (clampedInput < shadowKnee)
        {
            mappedTone = EvaluateCubicHermite(
                clampedInput,
                intervalStart: 0.0,
                intervalEnd: shadowKnee,
                startValue: minTone,
                startSlope: endpointSlope,
                endValue: shadowKneeTone,
                endSlope: midtoneSlope);
        }
        else if (clampedInput > highlightKnee)
        {
            mappedTone = EvaluateCubicHermite(
                clampedInput,
                intervalStart: highlightKnee,
                intervalEnd: 1.0,
                startValue: highlightKneeTone,
                startSlope: midtoneSlope,
                endValue: maxTone,
                endSlope: endpointSlope);
        }
        else
        {
            mappedTone = minTone + (clampedInput * toneRange);
        }

        return Math.Clamp(mappedTone, minTone, maxTone);
    }

    /// <summary>
    /// Applies the piecewise tone curve to an 8-bit RGB image by mapping channel values through a precomputed 256-entry lookup table.
    /// </summary>
    /// <param name="image">The input RGB image.</param>
    /// <param name="options">Optional tone curve options. If <see langword="null"/>, default options are used.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the tone-mapped image.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> are invalid.</exception>
    public static Matrix<Rgb8> Apply(IReadOnlyMatrix<Rgb8> image, PiecewiseToneCurveOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        PiecewiseToneCurveOptions resolvedOptions = OptionsResolver.Resolve(options, PiecewiseToneCurveOptions.Default);

        if (image.Count == 0)
        {
            return new SequentialMatrix<Rgb8>(image.Size);
        }

        byte[] toneLookupTable = ChannelLut.Build(normalized => EvaluateCore(normalized, resolvedOptions));
        return ChannelLut.Apply(image, toneLookupTable);
    }

    private static double EvaluateCubicHermite(
        double input,
        double intervalStart,
        double intervalEnd,
        double startValue,
        double startSlope,
        double endValue,
        double endSlope)
    {
        double intervalSpan = intervalEnd - intervalStart;
        double normalizedOffset = (input - intervalStart) / intervalSpan;
        double offsetSquared = normalizedOffset * normalizedOffset;
        double offsetCubed = offsetSquared * normalizedOffset;

        // Standard cubic Hermite basis polynomials h00(t), h10(t), h01(t), h11(t) for t = normalizedOffset.
        double startValueWeight = (2.0 * offsetCubed) - (3.0 * offsetSquared) + 1.0;
        double startTangentWeight = offsetCubed - (2.0 * offsetSquared) + normalizedOffset;
        double endValueWeight = (-2.0 * offsetCubed) + (3.0 * offsetSquared);
        double endTangentWeight = offsetCubed - offsetSquared;

        double scaledStartTangent = intervalSpan * startSlope;
        double scaledEndTangent = intervalSpan * endSlope;

        return (startValueWeight * startValue)
             + (startTangentWeight * scaledStartTangent)
             + (endValueWeight * endValue)
             + (endTangentWeight * scaledEndTangent);
    }
}
