namespace Leiter.Core;

/// <summary>
/// Defines an algorithm options type whose parameters can be scaled cleanly to target image or matrix dimensions.
/// </summary>
/// <typeparam name="TSelf">The concrete options type implementing this interface.</typeparam>
public interface ISizeTunableOptions<TSelf> : IAlgorithmOptions<TSelf>
    where TSelf : ISizeTunableOptions<TSelf>
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which baseline option values apply with scale <c>1.0</c>.
    /// </summary>
    const double DefaultReferenceDimension = 800.0;

    /// <summary>
    /// Returns a validated options preset adapted for matrices of <paramref name="size"/> using <typeparamref name="TSelf"/> defaults.
    /// </summary>
    /// <param name="size">The target matrix or image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension at which baseline defaults apply with scale <c>1.0</c>.</param>
    /// <returns>A validated <typeparamref name="TSelf"/> preset scaled for <paramref name="size"/>.</returns>
    static virtual TSelf ForSize(Size size, double referenceDimension = DefaultReferenceDimension) =>
        TSelf.ForSize(size, TSelf.Default, referenceDimension);

    /// <summary>
    /// Returns a validated options preset by scaling <paramref name="baseline"/> for matrices of <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target matrix or image dimensions.</param>
    /// <param name="baseline">The baseline configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension at which <paramref name="baseline"/> applies with scale <c>1.0</c>.</param>
    /// <returns>A validated <typeparamref name="TSelf"/> preset scaled from <paramref name="baseline"/>.</returns>
    static abstract TSelf ForSize(Size size, TSelf baseline, double referenceDimension = DefaultReferenceDimension);
}
