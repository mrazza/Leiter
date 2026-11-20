namespace Leiter.Algorithms.Internal;

using Leiter.Core;

/// <summary>
/// Defines an internal contract for algorithm options that adapt to canonical image statistics and dimensions.
/// </summary>
/// <typeparam name="TSelf">The concrete options type implementing this interface.</typeparam>
internal interface IProfileTunableOptions<TSelf> : IAlgorithmOptions<TSelf>
    where TSelf : class, IProfileTunableOptions<TSelf>
{
    /// <summary>
    /// Returns a validated options preset tuned for <paramref name="profile"/> by adapting <paramref name="baseline"/>.
    /// </summary>
    /// <param name="profile">The canonical image statistics and dimensions to tune for.</param>
    /// <param name="baseline">The baseline configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <typeparamref name="TSelf"/> preset tuned for <paramref name="profile"/>.</returns>
    static abstract TSelf ForProfile(in ImageTuningProfile profile, TSelf baseline, double referenceDimension);
}
