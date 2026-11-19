namespace Leiter.Algorithms.Internal;

using System.Runtime.CompilerServices;

/// <summary>
/// Provides shared deterministic SplitMix64 mixing and coordinate hashing utilities.
/// </summary>
internal static class SplitMix64
{
    /// <summary>
    /// Applies the SplitMix64 finalizer bit-mixer to a 64-bit state value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Mix(ulong value)
    {
        ulong z = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>
    /// Advances a 64-bit SplitMix64 generator state and returns the mixed output.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong Next(ref ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        return Mix(state);
    }

    /// <summary>
    /// Deterministically hashes a seed and 2D integer coordinates to a uniform value in <c>[0.0, 1.0)</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double HashToUnitDouble(int seed, int x, int y)
    {
        ulong z = ((ulong)(uint)seed * 0x9E3779B97F4A7C15UL)
                ^ ((ulong)(uint)x * 0xBF58476D1CE4E5B9UL)
                ^ ((ulong)(uint)y * 0x94D049BB133111EBUL);
        return (Mix(z) >> 11) * (1.0 / 9007199254740992.0);
    }

    /// <summary>
    /// Deterministically hashes two 2D endpoint coordinate pairs to a 64-bit tie-breaker value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong HashCoordinates(int x1, int y1, int x2, int y2)
    {
        ulong z = ((ulong)(uint)x1 * 0x9E3779B97F4A7C15UL)
                ^ ((ulong)(uint)y1 * 0xBF58476D1CE4E5B9UL)
                ^ ((ulong)(uint)x2 * 0x94D049BB133111EBUL)
                ^ ((ulong)(uint)y2 * 0xD6E8FEB86659FD93UL);
        return Mix(z);
    }
}
