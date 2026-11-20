namespace Leiter.Core;

using System.Runtime.CompilerServices;

/// <summary>
/// Provides shared internal argument and domain validation guards.
/// </summary>
internal static class Guard
{
    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is <see cref="double.NaN"/> or infinite.
    /// </summary>
    public static void ThrowIfNotFinite(
        double value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be a finite number.");
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is non-finite or less than or equal to zero.
    /// </summary>
    public static void ThrowIfNotFinitePositive(
        double value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!double.IsFinite(value) || value <= 0.0)
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be a finite positive number.");
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is non-finite or negative.
    /// </summary>
    public static void ThrowIfNotFiniteNonNegative(
        double value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!double.IsFinite(value) || value < 0.0)
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be a finite non-negative number.");
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is non-finite or outside <c>[min, max]</c>.
    /// </summary>
    public static void ThrowIfNotFiniteInRange(
        double value,
        double min,
        double max,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!double.IsFinite(value) || value < min || value > max)
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be finite and within [{min}, {max}].");
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is not a defined member of <typeparamref name="TEnum"/>.
    /// </summary>
    public static void ThrowIfUndefinedEnum<TEnum>(
        TEnum value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
            throw new ArgumentOutOfRangeException(paramName, value, $"Unsupported {typeof(TEnum).Name} value.");
    }

    /// <summary>
    /// Throws if either matrix is <see langword="null"/> or if their dimensions differ.
    /// </summary>
    public static void ThrowIfSizeMismatch(
        IUntypedMatrix expected,
        IUntypedMatrix actual,
        [CallerArgumentExpression(nameof(actual))] string? paramName = null,
        [CallerArgumentExpression(nameof(expected))] string? expectedParamName = null)
    {
        ArgumentNullException.ThrowIfNull(expected, expectedParamName);
        ArgumentNullException.ThrowIfNull(actual, paramName);

        if (expected.Size != actual.Size)
            throw new ArgumentException($"Matrix dimensions {actual.Size} must match expected dimensions {expected.Size}.", paramName);
    }

    /// <summary>
    /// Throws <see cref="ArgumentException"/> if <paramref name="expected"/> and <paramref name="actual"/> dimensions differ.
    /// </summary>
    public static void ThrowIfSizeMismatch(
        Size expected,
        Size actual,
        [CallerArgumentExpression(nameof(actual))] string? paramName = null)
    {
        if (expected != actual)
            throw new ArgumentException($"Dimensions {actual} must match expected dimensions {expected}.", paramName);
    }

    /// <summary>
    /// Throws if <paramref name="expected"/> is <see langword="null"/>, or if non-null <paramref name="actual"/> has different dimensions.
    /// </summary>
    public static void ThrowIfSizeMismatchIfNotNull(
        IUntypedMatrix expected,
        IUntypedMatrix? actual,
        [CallerArgumentExpression(nameof(actual))] string? paramName = null,
        [CallerArgumentExpression(nameof(expected))] string? expectedParamName = null)
    {
        ArgumentNullException.ThrowIfNull(expected, expectedParamName);
        if (actual is not null && expected.Size != actual.Size)
            throw new ArgumentException($"Matrix dimensions {actual.Size} must match expected dimensions {expected.Size}.", paramName);
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="size"/> has a negative width or height.
    /// </summary>
    public static void ThrowIfNegative(
        Size size,
        [CallerArgumentExpression(nameof(size))] string? paramName = null)
    {
        if (size.Width < 0 || size.Height < 0)
            throw new ArgumentOutOfRangeException(paramName, size, $"{paramName} dimensions must be non-negative.");
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="value"/> is non-finite or outside <c>(minExclusive, maxInclusive]</c>.
    /// </summary>
    public static void ThrowIfNotFiniteInHalfOpenRange(
        double value,
        double minExclusive,
        double maxInclusive,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!double.IsFinite(value) || value <= minExclusive || value > maxInclusive)
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be finite and within ({minExclusive}, {maxInclusive}].");
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> if <paramref name="width"/> or <paramref name="height"/> is negative.
    /// </summary>
    public static void ThrowIfNegativeDimensions(
        int width,
        int height,
        [CallerArgumentExpression(nameof(width))] string? widthParamName = null,
        [CallerArgumentExpression(nameof(height))] string? heightParamName = null)
    {
        if (width < 0)
            throw new ArgumentOutOfRangeException(widthParamName, width, $"{widthParamName} must be non-negative.");
        if (height < 0)
            throw new ArgumentOutOfRangeException(heightParamName, height, $"{heightParamName} must be non-negative.");
    }
}
