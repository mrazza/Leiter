namespace Leiter.Algorithms.Internal;

using System.Runtime.Intrinsics;

/// <summary>
/// Implements the 3rd-order Young–van Vliet recursive IIR Gaussian filter with
/// exact Triggs–Sdika constant-extension boundary conditions.
/// </summary>
internal readonly struct YoungVanVlietFilter
{
    private readonly double inputGain;
    private readonly double feedbackWeight1;
    private readonly double feedbackWeight2;
    private readonly double feedbackWeight3;
    private readonly double m00, m01, m02;
    private readonly double m10, m11, m12;
    private readonly double m20, m21, m22;

    private YoungVanVlietFilter(
        double inputGain,
        double feedbackWeight1,
        double feedbackWeight2,
        double feedbackWeight3,
        double m00, double m01, double m02,
        double m10, double m11, double m12,
        double m20, double m21, double m22)
    {
        this.inputGain = inputGain;
        this.feedbackWeight1 = feedbackWeight1;
        this.feedbackWeight2 = feedbackWeight2;
        this.feedbackWeight3 = feedbackWeight3;
        this.m00 = m00;
        this.m01 = m01;
        this.m02 = m02;
        this.m10 = m10;
        this.m11 = m11;
        this.m12 = m12;
        this.m20 = m20;
        this.m21 = m21;
        this.m22 = m22;
    }

    /// <summary>
    /// Creates a Young–van Vliet recursive Gaussian filter configured for the specified standard deviation.
    /// </summary>
    /// <param name="sigma">The standard deviation of the Gaussian filter.</param>
    /// <returns>A new <see cref="YoungVanVlietFilter"/> instance configured for <paramref name="sigma"/>.</returns>
    public static YoungVanVlietFilter Create(double sigma)
    {
        // Dimensionless pole-scaling parameter `q` from Young & van Vliet (1995) Eq. (11b).
        double poleScale = sigma >= 2.5
            ? 0.98711 * sigma - 0.96330
            : 3.97156 - 4.14554 * Math.Sqrt(1.0 - 0.26891 * sigma);

        double poleScaleSq = poleScale * poleScale;
        double poleScaleCubed = poleScaleSq * poleScale;

        // Normalized feedback weights `b1/b0`, `b2/b0`, `b3/b0` from Young & van Vliet (1995) Eq. (11a).
        double normalization = 1.57825 + 2.44413 * poleScale + 1.4281 * poleScaleSq + 0.422205 * poleScaleCubed;
        double a1 = (2.44413 * poleScale + 2.85619 * poleScaleSq + 1.26661 * poleScaleCubed) / normalization;
        double a2 = -(1.4281 * poleScaleSq + 1.26661 * poleScaleCubed) / normalization;
        double a3 = (0.422205 * poleScaleCubed) / normalization;

        // Unity DC-gain input weight `B = 1 - (a1 + a2 + a3)`.
        double inputGain = 1.0 - (a1 + a2 + a3);

        // Triggs & Sdika (2006) Eq. (14), scaled by inputGain = 1 - (a1 + a2 + a3).
        double invDenom = 1.0 / ((1.0 + a1 - a2 + a3) * (1.0 + a2 + (a1 - a3) * a3));
        double m00 = invDenom * (1.0 - a2 - a3 * (a1 + a3));
        double m01 = invDenom * (a1 + a3) * (a2 + a1 * a3);
        double m02 = invDenom * a3 * (a1 + a2 * a3);
        double m10 = invDenom * (a1 + a2 * a3);
        double m11 = invDenom * (1.0 - a2) * (a2 + a1 * a3);
        double m12 = -invDenom * a3 * (a1 * a3 + a3 * a3 + a2 - 1.0);
        double m20 = invDenom * (a1 * a3 + a2 + a1 * a1 - a2 * a2);
        double m21 = invDenom * (a1 * a2 + a3 * a2 * a2 - a1 * a3 * a3 - a3 * a3 * a3 - a2 * a3 + a3);
        double m22 = m02;

        return new YoungVanVlietFilter(
            inputGain,
            a1,
            a2,
            a3,
            m00, m01, m02,
            m10, m11, m12,
            m20, m21, m22);
    }

    /// <summary>
    /// Applies the bidirectional recursive Gaussian filter to a line of SIMD vector samples in place.
    /// </summary>
    /// <remarks>
    /// Performs a forward causal pass followed by an anti-causal backward pass with Triggs–Sdika boundary condition handling.
    /// </remarks>
    /// <param name="line">The line of SIMD vector samples to filter in place.</param>
    public void FilterLine(Span<Vector256<double>> line)
    {
        int length = line.Length;
        Vector256<double> leftBoundaryValue = line[0];
        Vector256<double> rightBoundaryValue = line[length - 1];

        // Causal left-to-right pass assuming constant left extension equal to line[0].
        Vector256<double> prevForward1 = leftBoundaryValue;
        Vector256<double> prevForward2 = leftBoundaryValue;
        Vector256<double> prevForward3 = leftBoundaryValue;

        for (int i = 0; i < length; i++)
        {
            Vector256<double> forwardSample =
                inputGain * line[i]
                + feedbackWeight1 * prevForward1
                + feedbackWeight2 * prevForward2
                + feedbackWeight3 * prevForward3;

            line[i] = forwardSample;
            prevForward3 = prevForward2;
            prevForward2 = prevForward1;
            prevForward1 = forwardSample;
        }

        // Exact right-boundary initialization assuming constant right extension equal to line[length - 1].
        var (prevBackward1, prevBackward2, prevBackward3) = ComputeInitialBackwardState(
            rightBoundaryValue,
            prevForward1,
            prevForward2,
            prevForward3);

        // Anti-causal right-to-left pass restoring zero-phase symmetry.
        line[length - 1] = prevBackward1;
        for (int i = length - 2; i >= 0; i--)
        {
            Vector256<double> backwardSample =
                inputGain * line[i]
                + feedbackWeight1 * prevBackward1
                + feedbackWeight2 * prevBackward2
                + feedbackWeight3 * prevBackward3;

            line[i] = backwardSample;
            prevBackward3 = prevBackward2;
            prevBackward2 = prevBackward1;
            prevBackward1 = backwardSample;
        }
    }

    /// <summary>
    /// Computes the initial backward filter state at the right boundary using the Triggs–Sdika boundary matrix.
    /// </summary>
    /// <param name="rightBoundaryValue">The constant boundary value at the right edge.</param>
    /// <param name="prevForward1">The most recent forward pass output sample.</param>
    /// <param name="prevForward2">The second most recent forward pass output sample.</param>
    /// <param name="prevForward3">The third most recent forward pass output sample.</param>
    /// <returns>A tuple containing the backward initial state at the edge and two virtual points beyond the edge.</returns>
    private (Vector256<double> AtEdge, Vector256<double> BeyondEdge1, Vector256<double> BeyondEdge2) ComputeInitialBackwardState(
        Vector256<double> rightBoundaryValue,
        Vector256<double> prevForward1,
        Vector256<double> prevForward2,
        Vector256<double> prevForward3)
    {
        Vector256<double> deviation1 = prevForward1 - rightBoundaryValue;
        Vector256<double> deviation2 = prevForward2 - rightBoundaryValue;
        Vector256<double> deviation3 = prevForward3 - rightBoundaryValue;

        return (
            AtEdge: rightBoundaryValue + (m00 * deviation1) + (m01 * deviation2) + (m02 * deviation3),
            BeyondEdge1: rightBoundaryValue + (m10 * deviation1) + (m11 * deviation2) + (m12 * deviation3),
            BeyondEdge2: rightBoundaryValue + (m20 * deviation1) + (m21 * deviation2) + (m22 * deviation3));
    }
}
