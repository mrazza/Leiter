namespace Leiter.Algorithms.Segmentation.Internal;

using Leiter.Algorithms.Segmentation;
using Leiter.Pixels;

/// <summary>
/// Full-covariance 3D Gaussian Mixture Model in RGB color space with deterministic
/// Orchard–Bouman clustering initialization, used by <see cref="GrabCut"/>.
/// </summary>
internal sealed class GaussianMixtureModel
{
    private const double CovarianceRegularization = 0.01;
    private const double EmptyModelPenalty = 1e4;

    private readonly record struct GaussianComponent(
        double Weight,
        Rgb64 Mean,
        SymmetricMatrix3x3 InverseCovariance,
        double HalfLogDetMinusLogWeight);

    private record struct ComponentAccumulator(
        int Count,
        Rgb64 SumColors,
        SymmetricMatrix3x3 SumOuterProducts)
    {
        public void AddSample(Rgb8 pixel)
        {
            Count++;
            Rgb64 color = ToUnnormalizedRgb64(pixel);
            SumColors = SumColors.Add(color);
            SumOuterProducts = SumOuterProducts.AddOuterProduct(color);
        }
    }

    private readonly int componentCount;
    private readonly GaussianComponent[] components;
    private readonly ComponentAccumulator[] accumulators;

    public GaussianMixtureModel(int componentCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(componentCount, 1);

        this.componentCount = componentCount;
        components = new GaussianComponent[componentCount];
        accumulators = new ComponentAccumulator[componentCount];
    }

    public void InitializeAndFit(ReadOnlySpan<Rgb8> pixels, ReadOnlySpan<int> samplePixelIndices)
    {
        if (samplePixelIndices.IsEmpty)
        {
            Array.Clear(components);
            return;
        }

        int sampleCount = samplePixelIndices.Length;
        var clusterAssignments = new int[sampleCount];

        int activeClusters = 1;
        while (activeClusters < componentCount)
        {
            AccumulateClusterStatistics(pixels, samplePixelIndices, clusterAssignments, activeClusters);
            if (!TryFindClusterToSplit(activeClusters, out int splitTarget, out Rgb64 splitMean, out Rgb64 splitAxis))
                break;

            SplitClusterAlongAxis(
                pixels,
                samplePixelIndices,
                clusterAssignments,
                splitTarget,
                newClusterId: activeClusters,
                splitMean,
                splitAxis);

            activeClusters++;
        }

        AccumulateClusterStatistics(pixels, samplePixelIndices, clusterAssignments, componentCount);
        FinalizeComponentsFromAccumulators(sampleCount);
    }

    public int AssignBestComponent(Rgb8 pixel)
    {
        int bestComponent = 0;
        double bestEnergy = double.PositiveInfinity;

        for (int k = 0; k < componentCount; k++)
        {
            ref readonly GaussianComponent component = ref components[k];
            if (component.Weight <= 0.0)
                continue;

            double energy = ComputeComponentEnergy(in component, pixel);
            if (energy < bestEnergy)
            {
                bestEnergy = energy;
                bestComponent = k;
            }
        }

        return bestComponent;
    }

    public void FitFromAssignedComponents(
        ReadOnlySpan<Rgb8> pixels,
        ReadOnlySpan<bool> isForeground,
        bool targetForeground,
        ReadOnlySpan<int> assignedComponents,
        int totalMemberCount)
    {
        Array.Clear(accumulators);

        if (totalMemberCount <= 0)
        {
            Array.Clear(components);
            return;
        }

        for (int p = 0; p < isForeground.Length; p++)
        {
            if (isForeground[p] != targetForeground)
                continue;

            accumulators[assignedComponents[p]].AddSample(pixels[p]);
        }

        FinalizeComponentsFromAccumulators(totalMemberCount);
    }

    public double EvaluateNegativeLogLikelihood(Rgb8 pixel)
    {
        double minEnergy = double.PositiveInfinity;

        for (int k = 0; k < componentCount; k++)
        {
            ref readonly GaussianComponent component = ref components[k];
            if (component.Weight <= 0.0)
                continue;

            double energy = ComputeComponentEnergy(in component, pixel);
            if (energy < minEnergy)
                minEnergy = energy;
        }

        if (double.IsPositiveInfinity(minEnergy))
            return EmptyModelPenalty;

        double expSum = 0.0;
        for (int k = 0; k < componentCount; k++)
        {
            ref readonly GaussianComponent component = ref components[k];
            if (component.Weight <= 0.0)
                continue;

            double energy = ComputeComponentEnergy(in component, pixel);
            expSum += Math.Exp(-(energy - minEnergy));
        }

        return minEnergy - Math.Log(expSum);
    }

    private void AccumulateClusterStatistics(
        ReadOnlySpan<Rgb8> pixels,
        ReadOnlySpan<int> samplePixelIndices,
        ReadOnlySpan<int> clusterAssignments,
        int clearCount)
    {
        Array.Clear(accumulators, 0, clearCount);

        for (int i = 0; i < samplePixelIndices.Length; i++)
        {
            int pixelIndex = samplePixelIndices[i];
            int k = clusterAssignments[i];
            accumulators[k].AddSample(pixels[pixelIndex]);
        }
    }

    private bool TryFindClusterToSplit(
        int activeClusters,
        out int splitTarget,
        out Rgb64 splitMean,
        out Rgb64 splitAxis)
    {
        splitTarget = -1;
        splitMean = Rgb64.Zero;
        splitAxis = Rgb64.Zero;
        double bestEigenvalue = 0.0;

        for (int k = 0; k < activeClusters; k++)
        {
            ComponentAccumulator accumulator = accumulators[k];
            if (accumulator.Count <= 1)
                continue;

            SymmetricMatrix3x3 covariance = SymmetricMatrix3x3.FromColorSums(
                accumulator.SumColors,
                accumulator.SumOuterProducts,
                accumulator.Count);
            (double maxEigenvalue, Rgb64 eigenvector) = covariance.FindPrincipalEigenpair();

            if (maxEigenvalue > bestEigenvalue)
            {
                bestEigenvalue = maxEigenvalue;
                splitTarget = k;
                splitMean = accumulator.SumColors.Divide((double)accumulator.Count);
                splitAxis = eigenvector;
            }
        }

        return splitTarget >= 0 && bestEigenvalue > 1e-6;
    }

    private static void SplitClusterAlongAxis(
        ReadOnlySpan<Rgb8> pixels,
        ReadOnlySpan<int> samplePixelIndices,
        Span<int> clusterAssignments,
        int splitTarget,
        int newClusterId,
        Rgb64 splitMean,
        Rgb64 splitAxis)
    {
        for (int i = 0; i < samplePixelIndices.Length; i++)
        {
            if (clusterAssignments[i] != splitTarget)
                continue;

            Rgb64 delta = ToUnnormalizedRgb64(pixels[samplePixelIndices[i]]).Subtract(splitMean);
            double projection = (delta.R * splitAxis.R) + (delta.G * splitAxis.G) + (delta.B * splitAxis.B);
            if (projection > 0.0)
                clusterAssignments[i] = newClusterId;
        }
    }

    private void FinalizeComponentsFromAccumulators(int totalSampleCount)
    {
        for (int k = 0; k < componentCount; k++)
        {
            ComponentAccumulator accumulator = accumulators[k];
            if (accumulator.Count == 0)
            {
                components[k] = default;
                continue;
            }

            double weight = (double)accumulator.Count / totalSampleCount;
            Rgb64 mean = accumulator.SumColors.Divide((double)accumulator.Count);
            SymmetricMatrix3x3 regularizedCovariance = SymmetricMatrix3x3.FromColorSums(
                accumulator.SumColors,
                accumulator.SumOuterProducts,
                accumulator.Count,
                CovarianceRegularization);
            SymmetricMatrix3x3 inverseCovariance = regularizedCovariance.Invert(
                CovarianceRegularization,
                out double determinant);

            double halfLogDetMinusLogWeight = -Math.Log(weight) + (0.5 * Math.Log(determinant));
            components[k] = new GaussianComponent(weight, mean, inverseCovariance, halfLogDetMinusLogWeight);
        }
    }

    private static double ComputeComponentEnergy(in GaussianComponent component, Rgb8 pixel)
    {
        Rgb64 delta = ToUnnormalizedRgb64(pixel).Subtract(component.Mean);
        double quadForm = component.InverseCovariance.QuadraticForm(delta);
        return component.HalfLogDetMinusLogWeight + (0.5 * Math.Max(0.0, quadForm));
    }

    // GrabCut GMM parameters and regularization are defined in unnormalized [0, 255] RGB units.
    private static Rgb64 ToUnnormalizedRgb64(Rgb8 pixel) => new(pixel.R, pixel.G, pixel.B);
}
