namespace Leiter.Core;

using System.Runtime.CompilerServices;

/// <summary>
/// Provides common scalar interpolation, clamping, and smoothstep weighting functions.
/// </summary>
public static class ScalarMath
{
    /// <summary>
    /// Computes linear interpolation between <paramref name="from"/> and <paramref name="to"/> by <paramref name="amount"/>.
    /// </summary>
    /// <remarks>
    /// Does not clamp <paramref name="amount"/> to <c>[0, 1]</c>, allowing extrapolation when <paramref name="amount"/> lies outside the unit interval.
    /// </remarks>
    /// <param name="from">The starting value returned when <paramref name="amount"/> is <c>0.0</c>.</param>
    /// <param name="to">The target value returned when <paramref name="amount"/> is <c>1.0</c>.</param>
    /// <param name="amount">The interpolation factor, typically in <c>[0.0, 1.0]</c>.</param>
    /// <returns>The linearly interpolated value <c>from + (to - from) * amount</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Lerp(double from, double to, double amount) => from + ((to - from) * amount);

    /// <summary>
    /// Evaluates the standard cubic Hermite smoothstep curve <c>3t² - 2t³</c> for <paramref name="t"/> clamped to <c>[0.0, 1.0]</c>.
    /// </summary>
    /// <param name="t">The normalized input parameter (clamped to <c>[0.0, 1.0]</c>).</param>
    /// <returns>A smooth transition value in <c>[0.0, 1.0]</c>, or <see cref="double.NaN"/> if <paramref name="t"/> is <see cref="double.NaN"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Smoothstep(double t)
    {
        double clamped = Math.Clamp(t, 0.0, 1.0);
        return clamped * clamped * (3.0 - (2.0 * clamped));
    }

    /// <summary>
    /// Computes a smoothstep falloff weight in <c>[0.0, 1.0]</c> across a radial transition band from
    /// <paramref name="innerRadius"/> to <paramref name="outerRadius"/>.
    /// </summary>
    /// <param name="distance">The discrete distance from a boundary or seed region.</param>
    /// <param name="innerRadius">The inclusive radius within which weight equals <c>1.0</c>.</param>
    /// <param name="outerRadius">The inclusive outer radius beyond which weight drops to <c>0.0</c>. Must be at least <paramref name="innerRadius"/>.</param>
    /// <returns><c>1.0</c> at or inside <paramref name="innerRadius"/>, <c>0.0</c> beyond <paramref name="outerRadius"/>, and a cubic smoothstep taper in between.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="outerRadius"/> is less than <paramref name="innerRadius"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double OuterBandSmoothstepWeight(int distance, int innerRadius, int outerRadius)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(outerRadius, innerRadius);

        if (distance <= innerRadius)
            return 1.0;

        if (distance > outerRadius)
            return 0.0;

        double t = (double)(distance - innerRadius) / (outerRadius - innerRadius + 1);
        return 1.0 - Smoothstep(t);
    }

    /// <summary>
    /// Clamps finite <paramref name="value"/> to <c>[0.0, 1.0]</c>, or returns <paramref name="fallback"/> if <paramref name="value"/> is non-finite.
    /// </summary>
    /// <param name="value">The scalar value to normalize.</param>
    /// <param name="fallback">The fallback value returned when <paramref name="value"/> is <see cref="double.NaN"/> or infinite (default <c>0.0</c>).</param>
    /// <returns>The clamped unit-interval value, or <paramref name="fallback"/> when <paramref name="value"/> is not finite.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ClampUnitOrDefault(double value, double fallback = 0.0) =>
        double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : fallback;

    /// <summary>
    /// Packs two 32-bit integer identifiers into a canonical order-independent 64-bit pair key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong PackUnorderedPairKey(int first, int second)
    {
        (int low, int high) = first < second ? (first, second) : (second, first);
        return ((ulong)(uint)low << 32) | (uint)high;
    }
}
