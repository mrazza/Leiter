namespace Leiter.Algorithms.DistanceTransform;

using Leiter.Core;

/// <summary>
/// Represents the nearest seed coordinate discovered by <see cref="JumpFlooding"/>, or <see cref="None"/> when no seeds exist.
/// </summary>
/// <param name="Seed">The 2D integer coordinate of the nearest seed.</param>
/// <param name="HasSeed">Whether a valid seed was assigned (<see langword="false"/> for <see cref="None"/>).</param>
public readonly record struct NearestSeed(Coord Seed, bool HasSeed)
{
    /// <summary>
    /// Gets the sentinel value indicating no seed is present.
    /// </summary>
    public static NearestSeed None => default;

    /// <summary>
    /// Creates a <see cref="NearestSeed"/> referencing <paramref name="seed"/>.
    /// </summary>
    /// <param name="seed">The coordinate of the seed.</param>
    /// <returns>A populated <see cref="NearestSeed"/> with <see cref="HasSeed"/> set to <see langword="true"/>.</returns>
    public static NearestSeed FromCoord(Coord seed) => new(seed, true);
}
