namespace Leiter.Algorithms.Color;

/// <summary>
/// Represents the 6-dimensional CIELAB color distribution statistics <c>(L*, a*, b*)</c>
/// mean and standard deviation for an image or palette.
/// </summary>
/// <param name="MeanL">Mean lightness (<c>L*</c>).</param>
/// <param name="MeanA">Mean green-red chromaticity (<c>a*</c>).</param>
/// <param name="MeanB">Mean blue-yellow chromaticity (<c>b*</c>).</param>
/// <param name="StdDevL">Population standard deviation of lightness (<c>L*</c>).</param>
/// <param name="StdDevA">Population standard deviation of <c>a*</c>.</param>
/// <param name="StdDevB">Population standard deviation of <c>b*</c>.</param>
public readonly record struct LabColorStatistics(
    double MeanL,
    double MeanA,
    double MeanB,
    double StdDevL,
    double StdDevA,
    double StdDevB)
{
    /// <summary>
    /// Gets equal unit weights across all six CIELAB feature dimensions.
    /// </summary>
    public static LabColorStatistics UnitWeights => new(1.0, 1.0, 1.0, 1.0, 1.0, 1.0);

    /// <summary>
    /// Computes the unweighted Euclidean distance between this 6D feature vector and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The target statistics to compare against.</param>
    /// <returns>The non-negative Euclidean distance in 6D CIELAB feature space.</returns>
    public double FeatureDistance(LabColorStatistics other) => FeatureDistance(other, UnitWeights);

    /// <summary>
    /// Computes the weighted Euclidean distance between this 6D feature vector and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The target statistics to compare against.</param>
    /// <param name="weights">Per-component non-negative squared weights applied to each dimension difference.</param>
    /// <returns>The non-negative weighted Euclidean distance in 6D CIELAB feature space.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any weight is negative or <see cref="double.NaN"/>.</exception>
    public double FeatureDistance(LabColorStatistics other, LabColorStatistics weights)
    {
        ValidateWeight(weights.MeanL, nameof(weights));
        ValidateWeight(weights.MeanA, nameof(weights));
        ValidateWeight(weights.MeanB, nameof(weights));
        ValidateWeight(weights.StdDevL, nameof(weights));
        ValidateWeight(weights.StdDevA, nameof(weights));
        ValidateWeight(weights.StdDevB, nameof(weights));

        double dMeanL = MeanL - other.MeanL;
        double dMeanA = MeanA - other.MeanA;
        double dMeanB = MeanB - other.MeanB;
        double dStdL = StdDevL - other.StdDevL;
        double dStdA = StdDevA - other.StdDevA;
        double dStdB = StdDevB - other.StdDevB;

        return Math.Sqrt(
            (weights.MeanL * dMeanL * dMeanL)
            + (weights.MeanA * dMeanA * dMeanA)
            + (weights.MeanB * dMeanB * dMeanB)
            + (weights.StdDevL * dStdL * dStdL)
            + (weights.StdDevA * dStdA * dStdA)
            + (weights.StdDevB * dStdB * dStdB));
    }

    private static void ValidateWeight(double weight, string paramName)
    {
        if (double.IsNaN(weight) || weight < 0.0)
            throw new ArgumentOutOfRangeException(paramName, "Feature weights must be non-negative.");
    }
}
