namespace Leiter.Algorithms.Stylization;

using System;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms.Internal;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Leiter.Pixels.Internal;

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

        return AdjustChannelDensityUnchecked(channel, densityOffset);
    }

    internal static double AdjustChannelDensityUnchecked(double channel, double densityOffset)
    {
        double c = ScalarMath.ClampUnit(channel);
        return ScalarMath.ClampUnit(c - (c * (1.0 - c) * densityOffset));
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
        PixelGuard.ThrowIfNotFinite(color);
        Guard.ThrowIfNotFinite(densityOffset);

        return AdjustDensityUnchecked(color, densityOffset);
    }

    internal static Rgb64 AdjustDensityUnchecked(Rgb64 color, double densityOffset) =>
        new(
            AdjustChannelDensityUnchecked(color.R, densityOffset),
            AdjustChannelDensityUnchecked(color.G, densityOffset),
            AdjustChannelDensityUnchecked(color.B, densityOffset));

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

        return AdjustDensityUnchecked(color, densityOffset);
    }

    internal static Rgb8 AdjustDensityUnchecked(Rgb8 color, double densityOffset) =>
        AdjustDensityUnchecked(color.ToRgb64(), densityOffset).ToRgb8();

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
        PixelGuard.ThrowIfNotFinite(first);
        PixelGuard.ThrowIfNotFinite(second);
        Guard.ThrowIfNotFiniteInRange(overlapDarkening, 0.0, 1.0);

        return MixPigmentsUnchecked(first, second, overlapDarkening);
    }

    internal static Rgb64 MixPigmentsUnchecked(Rgb64 first, Rgb64 second, double overlapDarkening = 0.5)
    {
        Rgb64 c1 = new(
            ScalarMath.ClampUnit(first.R),
            ScalarMath.ClampUnit(first.G),
            ScalarMath.ClampUnit(first.B));
        Rgb64 c2 = new(
            ScalarMath.ClampUnit(second.R),
            ScalarMath.ClampUnit(second.G),
            ScalarMath.ClampUnit(second.B));

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

        return MixPigmentsUnchecked(first.ToRgb64(), second.ToRgb64(), overlapDarkening).ToRgb8();
    }

    /// <summary>
    /// Applies per-pixel pigment density adjustments across <paramref name="image"/> scaled by <paramref name="intensity"/>.
    /// </summary>
    /// <param name="image">The input 8-bit RGB image.</param>
    /// <param name="densityField">The scalar density field matching <paramref name="image"/> dimensions.</param>
    /// <param name="intensity">The scalar multiplier applied to each field sample (default <c>1.0</c>).</param>
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing density-adjusted pixels.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or <paramref name="densityField"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="densityField"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="intensity"/>, any sample in <paramref name="densityField"/>, or any scaled density offset is non-finite.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<Rgb8> ApplyDensityField(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> densityField,
        double intensity = 1.0,
        CancellationToken cancellationToken = default)
    {
        Guard.ThrowIfSizeMismatch(image, densityField);
        Guard.ThrowIfNotFinite(intensity);
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0)
        {
            return image.Clone();
        }

        double[]? densityOffsets = ValidateAndBuildDensityOffsets(densityField, intensity, cancellationToken);
        if (densityOffsets is null)
        {
            return image.Clone();
        }

        return ApplyDensityOffsetsUnchecked(image, densityOffsets, cancellationToken);
    }

    /// <summary>
    /// Applies per-pixel pigment density adjustments across <paramref name="image"/> using a custom scalar mapper.
    /// </summary>
    /// <param name="image">The input 8-bit RGB image.</param>
    /// <param name="field">The scalar field matching <paramref name="image"/> dimensions.</param>
    /// <param name="densityOffsetSelector">Maps each raw field sample to the density offset passed to <see cref="AdjustDensity(Rgb8, double)"/>.</param>
    /// <param name="cancellationToken">Optional token used to cancel the operation.</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing density-adjusted pixels.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="image"/>, <paramref name="field"/>, or <paramref name="densityOffsetSelector"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="field"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="densityOffsetSelector"/> returns a non-finite density offset.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public static Matrix<Rgb8> ApplyDensityField(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel> field,
        Func<double, double> densityOffsetSelector,
        CancellationToken cancellationToken = default)
    {
        Guard.ThrowIfSizeMismatch(image, field);
        ArgumentNullException.ThrowIfNull(densityOffsetSelector);
        cancellationToken.ThrowIfCancellationRequested();

        if (image.Count == 0)
        {
            return image.Clone();
        }

        double[] densityOffsets = EvaluateAndValidateDensityOffsets(field, densityOffsetSelector, cancellationToken);
        return ApplyDensityOffsetsUnchecked(image, densityOffsets, cancellationToken);
    }

    private readonly record struct OffsetValidationError(bool HasError, double InvalidValue);

    private static double[]? ValidateAndBuildDensityOffsets(
        IReadOnlyMatrix<DoublePixel> densityField,
        double intensity,
        CancellationToken cancellationToken)
    {
        int count = densityField.Count;
        DoublePixel[]? fieldRaw = densityField is SequentialMatrix<DoublePixel> sequential ? sequential.RawData : null;

        if (intensity == 0.0)
        {
            OffsetValidationError[] errors = fieldRaw is not null
                ? ParallelRunner.MapChunks(
                    fieldRaw.Length,
                    cancellationToken,
                    (start, end) =>
                    {
                        for (int i = start; i < end; i++)
                        {
                            double sample = fieldRaw[i].Value;
                            if (!double.IsFinite(sample))
                                return new OffsetValidationError(true, sample);
                        }

                        return default;
                    })
                : ParallelRunner.MapChunks(
                    densityField.Height,
                    cancellationToken,
                    (startRow, endRow) =>
                    {
                        int width = densityField.Width;
                        for (int y = startRow; y < endRow; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                double sample = densityField[x, y].Value;
                                if (!double.IsFinite(sample))
                                    return new OffsetValidationError(true, sample);
                            }
                        }

                        return default;
                    },
                    minParallelCount: count >= ParallelRunner.DefaultMinParallelCount && densityField.Height >= 8 ? 2 : densityField.Height + 1);

            ThrowFirstNonFiniteError(errors, nameof(densityField));
            return null;
        }

        var offsets = new double[count];
        OffsetValidationError[] buildErrors = fieldRaw is not null
            ? ParallelRunner.MapChunks(
                fieldRaw.Length,
                cancellationToken,
                (start, end) =>
                {
                    for (int i = start; i < end; i++)
                    {
                        if (!TryComputeScaledOffset(fieldRaw[i].Value, intensity, out double offset, out double invalidValue))
                            return new OffsetValidationError(true, invalidValue);

                        offsets[i] = offset;
                    }

                    return default;
                })
            : ParallelRunner.MapChunks(
                densityField.Height,
                cancellationToken,
                (startRow, endRow) =>
                {
                    int width = densityField.Width;
                    for (int y = startRow; y < endRow; y++)
                    {
                        int rowOffset = y * width;
                        for (int x = 0; x < width; x++)
                        {
                            if (!TryComputeScaledOffset(densityField[x, y].Value, intensity, out double offset, out double invalidValue))
                                return new OffsetValidationError(true, invalidValue);

                            offsets[rowOffset + x] = offset;
                        }
                    }

                    return default;
                },
                minParallelCount: count >= ParallelRunner.DefaultMinParallelCount && densityField.Height >= 8 ? 2 : densityField.Height + 1);

        ThrowFirstNonFiniteError(buildErrors, nameof(densityField));
        return offsets;
    }

    private static bool TryComputeScaledOffset(double sample, double intensity, out double offset, out double invalidValue)
    {
        if (!double.IsFinite(sample))
        {
            offset = 0.0;
            invalidValue = sample;
            return false;
        }

        offset = intensity * sample;
        if (!double.IsFinite(offset))
        {
            invalidValue = offset;
            return false;
        }

        invalidValue = 0.0;
        return true;
    }

    private static double[] EvaluateAndValidateDensityOffsets(
        IReadOnlyMatrix<DoublePixel> field,
        Func<double, double> densityOffsetSelector,
        CancellationToken cancellationToken)
    {
        int count = field.Count;
        var offsets = new double[count];
        DoublePixel[]? fieldRaw = field is SequentialMatrix<DoublePixel> sequential ? sequential.RawData : null;

        OffsetValidationError[] errors = fieldRaw is not null
            ? ParallelRunner.MapChunks(
                fieldRaw.Length,
                cancellationToken,
                (start, end) =>
                {
                    for (int i = start; i < end; i++)
                    {
                        double densityOffset = densityOffsetSelector(fieldRaw[i].Value);
                        if (!double.IsFinite(densityOffset))
                            return new OffsetValidationError(true, densityOffset);

                        offsets[i] = densityOffset;
                    }

                    return default;
                })
            : ParallelRunner.MapChunks(
                field.Height,
                cancellationToken,
                (startRow, endRow) =>
                {
                    int width = field.Width;
                    for (int y = startRow; y < endRow; y++)
                    {
                        int rowOffset = y * width;
                        for (int x = 0; x < width; x++)
                        {
                            double densityOffset = densityOffsetSelector(field[x, y].Value);
                            if (!double.IsFinite(densityOffset))
                                return new OffsetValidationError(true, densityOffset);

                            offsets[rowOffset + x] = densityOffset;
                        }
                    }

                    return default;
                },
                minParallelCount: count >= ParallelRunner.DefaultMinParallelCount && field.Height >= 8 ? 2 : field.Height + 1);

        ThrowFirstNonFiniteError(errors, "densityOffset");
        return offsets;
    }

    private static void ThrowFirstNonFiniteError(OffsetValidationError[] errors, string paramName)
    {
        for (int i = 0; i < errors.Length; i++)
        {
            if (errors[i].HasError)
                Guard.ThrowIfNotFinite(errors[i].InvalidValue, paramName);
        }
    }

    private static Matrix<Rgb8> ApplyDensityOffsetsUnchecked(
        IReadOnlyMatrix<Rgb8> image,
        double[] densityOffsets,
        CancellationToken cancellationToken)
    {
        int width = image.Width;
        int height = image.Height;
        var result = new SequentialMatrix<Rgb8>(width, height);
        Rgb8[] destRaw = result.RawData;

        if (image is SequentialMatrix<Rgb8> sequentialImage)
        {
            Rgb8[] srcRaw = sequentialImage.RawData;
            ParallelFor(destRaw.Length, cancellationToken, i => destRaw[i] = AdjustDensityUnchecked(srcRaw[i], densityOffsets[i]));
            return result;
        }

        Size size = image.Size;
        ParallelFor(height, cancellationToken, y =>
        {
            int rowOffset = size.IndexOf(0, y);
            for (int x = 0; x < width; x++)
            {
                int idx = rowOffset + x;
                destRaw[idx] = AdjustDensityUnchecked(image[x, y], densityOffsets[idx]);
            }
        });

        return result;
    }

    private static void ParallelFor(int toExclusive, CancellationToken cancellationToken, Action<int> body)
    {
        if (cancellationToken.CanBeCanceled)
        {
            Parallel.For(0, toExclusive, new ParallelOptions { CancellationToken = cancellationToken }, body);
        }
        else
        {
            Parallel.For(0, toExclusive, body);
        }
    }
}
