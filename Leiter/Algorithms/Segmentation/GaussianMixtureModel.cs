namespace Leiter.Algorithms.Segmentation;

/// <summary>
/// Full-covariance 3D Gaussian Mixture Model in RGB color space with deterministic
/// Orchard–Bouman clustering initialization, used by <see cref="GrabCut"/>.
/// </summary>
internal sealed class GaussianMixtureModel
{
    private const double CovarianceRegularization = 0.01;
    private const double EmptyModelPenalty = 1e4;

    private readonly int componentCount;
    private readonly double[] weights;
    private readonly double[] means;
    private readonly double[] inverseCovariances;
    private readonly double[] halfLogDetMinusLogWeight;

    private readonly int[] componentCounts;
    private readonly double[] sumColors;
    private readonly double[] sumOuterProducts;

    public GaussianMixtureModel(int componentCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(componentCount, 1);

        this.componentCount = componentCount;
        weights = new double[componentCount];
        means = new double[componentCount * 3];
        inverseCovariances = new double[componentCount * 6];
        halfLogDetMinusLogWeight = new double[componentCount];

        componentCounts = new int[componentCount];
        sumColors = new double[componentCount * 3];
        sumOuterProducts = new double[componentCount * 6];
    }

    public void InitializeAndFit(ReadOnlySpan<float> interleavedRgb, ReadOnlySpan<int> samplePixelIndices)
    {
        if (samplePixelIndices.IsEmpty)
        {
            Array.Clear(weights);
            return;
        }

        int sampleCount = samplePixelIndices.Length;
        var clusterAssignments = new int[sampleCount];

        int activeClusters = 1;
        while (activeClusters < componentCount)
        {
            AccumulateSubset(interleavedRgb, samplePixelIndices, clusterAssignments, activeClusters);

            int splitTarget = -1;
            double bestEigenvalue = 0.0;
            double splitMeanR = 0.0;
            double splitMeanG = 0.0;
            double splitMeanB = 0.0;
            double splitVecR = 0.0;
            double splitVecG = 0.0;
            double splitVecB = 0.0;

            for (int k = 0; k < activeClusters; k++)
            {
                int count = componentCounts[k];
                if (count <= 1)
                    continue;

                double meanR = sumColors[(k * 3) + 0] / count;
                double meanG = sumColors[(k * 3) + 1] / count;
                double meanB = sumColors[(k * 3) + 2] / count;

                int covOffset = k * 6;
                double covRR = Math.Max(0.0, (sumOuterProducts[covOffset + 0] / count) - (meanR * meanR));
                double covRG = (sumOuterProducts[covOffset + 1] / count) - (meanR * meanG);
                double covRB = (sumOuterProducts[covOffset + 2] / count) - (meanR * meanB);
                double covGG = Math.Max(0.0, (sumOuterProducts[covOffset + 3] / count) - (meanG * meanG));
                double covGB = (sumOuterProducts[covOffset + 4] / count) - (meanG * meanB);
                double covBB = Math.Max(0.0, (sumOuterProducts[covOffset + 5] / count) - (meanB * meanB));

                FindPrincipalComponent(
                    covRR,
                    covRG,
                    covRB,
                    covGG,
                    covGB,
                    covBB,
                    out double maxEigenvalue,
                    out double vecR,
                    out double vecG,
                    out double vecB);

                if (maxEigenvalue > bestEigenvalue)
                {
                    bestEigenvalue = maxEigenvalue;
                    splitTarget = k;
                    splitMeanR = meanR;
                    splitMeanG = meanG;
                    splitMeanB = meanB;
                    splitVecR = vecR;
                    splitVecG = vecG;
                    splitVecB = vecB;
                }
            }

            if (splitTarget < 0 || bestEigenvalue <= 1e-6)
                break;

            int newClusterId = activeClusters;
            for (int i = 0; i < sampleCount; i++)
            {
                if (clusterAssignments[i] != splitTarget)
                    continue;

                int rgbOffset = samplePixelIndices[i] * 3;
                double projection =
                    ((interleavedRgb[rgbOffset + 0] - splitMeanR) * splitVecR) +
                    ((interleavedRgb[rgbOffset + 1] - splitMeanG) * splitVecG) +
                    ((interleavedRgb[rgbOffset + 2] - splitMeanB) * splitVecB);

                if (projection > 0.0)
                    clusterAssignments[i] = newClusterId;
            }

            activeClusters++;
        }

        AccumulateSubset(interleavedRgb, samplePixelIndices, clusterAssignments, componentCount);
        FinalizeComponentsFromAccumulators(sampleCount);
    }

    public int AssignBestComponent(float r, float g, float b)
    {
        int bestComponent = 0;
        double bestEnergy = double.PositiveInfinity;

        for (int k = 0; k < componentCount; k++)
        {
            if (weights[k] <= 0.0)
                continue;

            double energy = ComputeComponentEnergy(k, r, g, b);
            if (energy < bestEnergy)
            {
                bestEnergy = energy;
                bestComponent = k;
            }
        }

        return bestComponent;
    }

    public void FitFromAssignedComponents(
        ReadOnlySpan<float> interleavedRgb,
        ReadOnlySpan<byte> labels,
        byte expectedLabel,
        ReadOnlySpan<int> assignedComponents,
        int totalMemberCount)
    {
        Array.Clear(componentCounts);
        Array.Clear(sumColors);
        Array.Clear(sumOuterProducts);

        if (totalMemberCount <= 0)
        {
            Array.Clear(weights);
            return;
        }

        for (int p = 0; p < labels.Length; p++)
        {
            if (labels[p] != expectedLabel)
                continue;

            int k = assignedComponents[p];
            int rgbOffset = p * 3;
            AddSampleToAccumulators(k, interleavedRgb[rgbOffset + 0], interleavedRgb[rgbOffset + 1], interleavedRgb[rgbOffset + 2]);
        }

        FinalizeComponentsFromAccumulators(totalMemberCount);
    }

    public double EvaluateNegativeLogLikelihood(float r, float g, float b)
    {
        double minEnergy = double.PositiveInfinity;

        for (int k = 0; k < componentCount; k++)
        {
            if (weights[k] <= 0.0)
                continue;

            double energy = ComputeComponentEnergy(k, r, g, b);
            if (energy < minEnergy)
                minEnergy = energy;
        }

        if (double.IsPositiveInfinity(minEnergy))
            return EmptyModelPenalty;

        double expSum = 0.0;
        for (int k = 0; k < componentCount; k++)
        {
            if (weights[k] <= 0.0)
                continue;

            double energy = ComputeComponentEnergy(k, r, g, b);
            expSum += Math.Exp(-(energy - minEnergy));
        }

        return minEnergy - Math.Log(expSum);
    }

    private void AccumulateSubset(
        ReadOnlySpan<float> interleavedRgb,
        ReadOnlySpan<int> samplePixelIndices,
        ReadOnlySpan<int> clusterAssignments,
        int clearCount)
    {
        Array.Clear(componentCounts, 0, clearCount);
        Array.Clear(sumColors, 0, clearCount * 3);
        Array.Clear(sumOuterProducts, 0, clearCount * 6);

        for (int i = 0; i < samplePixelIndices.Length; i++)
        {
            int pixelIndex = samplePixelIndices[i];
            int k = clusterAssignments[i];
            int rgbOffset = pixelIndex * 3;
            AddSampleToAccumulators(k, interleavedRgb[rgbOffset + 0], interleavedRgb[rgbOffset + 1], interleavedRgb[rgbOffset + 2]);
        }
    }

    private void AddSampleToAccumulators(int componentIndex, double r, double g, double b)
    {
        componentCounts[componentIndex]++;

        int meanOffset = componentIndex * 3;
        sumColors[meanOffset + 0] += r;
        sumColors[meanOffset + 1] += g;
        sumColors[meanOffset + 2] += b;

        int covOffset = componentIndex * 6;
        sumOuterProducts[covOffset + 0] += r * r;
        sumOuterProducts[covOffset + 1] += r * g;
        sumOuterProducts[covOffset + 2] += r * b;
        sumOuterProducts[covOffset + 3] += g * g;
        sumOuterProducts[covOffset + 4] += g * b;
        sumOuterProducts[covOffset + 5] += b * b;
    }

    private void FinalizeComponentsFromAccumulators(int totalSampleCount)
    {
        for (int k = 0; k < componentCount; k++)
        {
            int count = componentCounts[k];
            if (count == 0)
            {
                weights[k] = 0.0;
                continue;
            }

            double weight = (double)count / totalSampleCount;
            weights[k] = weight;

            int meanOffset = k * 3;
            double meanR = sumColors[meanOffset + 0] / count;
            double meanG = sumColors[meanOffset + 1] / count;
            double meanB = sumColors[meanOffset + 2] / count;
            means[meanOffset + 0] = meanR;
            means[meanOffset + 1] = meanG;
            means[meanOffset + 2] = meanB;

            int covOffset = k * 6;
            double covRR = Math.Max(0.0, (sumOuterProducts[covOffset + 0] / count) - (meanR * meanR)) + CovarianceRegularization;
            double covRG = (sumOuterProducts[covOffset + 1] / count) - (meanR * meanG);
            double covRB = (sumOuterProducts[covOffset + 2] / count) - (meanR * meanB);
            double covGG = Math.Max(0.0, (sumOuterProducts[covOffset + 3] / count) - (meanG * meanG)) + CovarianceRegularization;
            double covGB = (sumOuterProducts[covOffset + 4] / count) - (meanG * meanB);
            double covBB = Math.Max(0.0, (sumOuterProducts[covOffset + 5] / count) - (meanB * meanB)) + CovarianceRegularization;

            double cofactor00 = (covGG * covBB) - (covGB * covGB);
            double cofactor01 = (covRB * covGB) - (covRG * covBB);
            double cofactor02 = (covRG * covGB) - (covRB * covGG);
            double cofactor11 = (covRR * covBB) - (covRB * covRB);
            double cofactor12 = (covRG * covRB) - (covRR * covGB);
            double cofactor22 = (covRR * covGG) - (covRG * covRG);

            double determinant = (covRR * cofactor00) + (covRG * cofactor01) + (covRB * cofactor02);
            if (determinant <= 1e-12)
            {
                double fallback = CovarianceRegularization;
                determinant = fallback * fallback * fallback;
                double invDiag = 1.0 / fallback;
                inverseCovariances[covOffset + 0] = invDiag;
                inverseCovariances[covOffset + 1] = 0.0;
                inverseCovariances[covOffset + 2] = 0.0;
                inverseCovariances[covOffset + 3] = invDiag;
                inverseCovariances[covOffset + 4] = 0.0;
                inverseCovariances[covOffset + 5] = invDiag;
            }
            else
            {
                double invDet = 1.0 / determinant;
                inverseCovariances[covOffset + 0] = cofactor00 * invDet;
                inverseCovariances[covOffset + 1] = cofactor01 * invDet;
                inverseCovariances[covOffset + 2] = cofactor02 * invDet;
                inverseCovariances[covOffset + 3] = cofactor11 * invDet;
                inverseCovariances[covOffset + 4] = cofactor12 * invDet;
                inverseCovariances[covOffset + 5] = cofactor22 * invDet;
            }

            halfLogDetMinusLogWeight[k] = -Math.Log(weight) + (0.5 * Math.Log(determinant));
        }
    }

    private double ComputeComponentEnergy(int k, double r, double g, double b)
    {
        int meanOffset = k * 3;
        double dr = r - means[meanOffset + 0];
        double dg = g - means[meanOffset + 1];
        double db = b - means[meanOffset + 2];

        int covOffset = k * 6;
        double quadForm =
            (dr * ((inverseCovariances[covOffset + 0] * dr) + (inverseCovariances[covOffset + 1] * dg) + (inverseCovariances[covOffset + 2] * db))) +
            (dg * ((inverseCovariances[covOffset + 1] * dr) + (inverseCovariances[covOffset + 3] * dg) + (inverseCovariances[covOffset + 4] * db))) +
            (db * ((inverseCovariances[covOffset + 2] * dr) + (inverseCovariances[covOffset + 4] * dg) + (inverseCovariances[covOffset + 5] * db)));

        return halfLogDetMinusLogWeight[k] + (0.5 * Math.Max(0.0, quadForm));
    }

    private static void FindPrincipalComponent(
        double covRR,
        double covRG,
        double covRB,
        double covGG,
        double covGB,
        double covBB,
        out double maxEigenvalue,
        out double vecR,
        out double vecG,
        out double vecB)
    {
        Span<double> m =
        [
            covRR, covRG, covRB,
            covRG, covGG, covGB,
            covRB, covGB, covBB,
        ];

        Span<double> v =
        [
            1.0, 0.0, 0.0,
            0.0, 1.0, 0.0,
            0.0, 0.0, 1.0,
        ];

        for (int sweep = 0; sweep < 12; sweep++)
        {
            double a01 = Math.Abs(m[1]);
            double a02 = Math.Abs(m[2]);
            double a12 = Math.Abs(m[5]);

            int p;
            int q;
            double maxOff;
            if (a01 >= a02 && a01 >= a12)
            {
                p = 0;
                q = 1;
                maxOff = a01;
            }
            else if (a02 >= a12)
            {
                p = 0;
                q = 2;
                maxOff = a02;
            }
            else
            {
                p = 1;
                q = 2;
                maxOff = a12;
            }

            if (maxOff <= 1e-12)
                break;

            int r = 3 - p - q;
            double mpp = m[(p * 3) + p];
            double mqq = m[(q * 3) + q];
            double mpq = m[(p * 3) + q];
            double tau = (mqq - mpp) / (2.0 * mpq);
            double t = tau >= 0.0
                ? 1.0 / (tau + Math.Sqrt(1.0 + (tau * tau)))
                : -1.0 / (-tau + Math.Sqrt(1.0 + (tau * tau)));
            double c = 1.0 / Math.Sqrt(1.0 + (t * t));
            double s = t * c;

            m[(p * 3) + p] = (c * c * mpp) - (2.0 * s * c * mpq) + (s * s * mqq);
            m[(q * 3) + q] = (s * s * mpp) + (2.0 * s * c * mpq) + (c * c * mqq);
            m[(p * 3) + q] = 0.0;
            m[(q * 3) + p] = 0.0;

            double mrp = m[(r * 3) + p];
            double mrq = m[(r * 3) + q];
            double newRp = (c * mrp) - (s * mrq);
            double newRq = (s * mrp) + (c * mrq);
            m[(r * 3) + p] = newRp;
            m[(p * 3) + r] = newRp;
            m[(r * 3) + q] = newRq;
            m[(q * 3) + r] = newRq;

            for (int row = 0; row < 3; row++)
            {
                double vrp = v[(row * 3) + p];
                double vrq = v[(row * 3) + q];
                v[(row * 3) + p] = (c * vrp) - (s * vrq);
                v[(row * 3) + q] = (s * vrp) + (c * vrq);
            }
        }

        int bestCol = 0;
        if (m[4] > m[0])
            bestCol = 1;
        if (m[8] > m[(bestCol * 3) + bestCol])
            bestCol = 2;

        maxEigenvalue = m[(bestCol * 3) + bestCol];
        vecR = v[bestCol];
        vecG = v[3 + bestCol];
        vecB = v[6 + bestCol];
    }
}
