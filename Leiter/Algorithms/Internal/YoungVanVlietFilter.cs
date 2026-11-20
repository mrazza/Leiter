namespace Leiter.Algorithms.Internal;

using System.Numerics;
using Leiter.Core;
using Leiter.Pixels;

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
    private readonly IReadOnlyMatrix<DoublePixel> rightBoundaryMatrix;

    private YoungVanVlietFilter(
        double inputGain,
        double feedbackWeight1,
        double feedbackWeight2,
        double feedbackWeight3,
        IReadOnlyMatrix<DoublePixel> rightBoundaryMatrix)
    {
        this.inputGain = inputGain;
        this.feedbackWeight1 = feedbackWeight1;
        this.feedbackWeight2 = feedbackWeight2;
        this.feedbackWeight3 = feedbackWeight3;
        this.rightBoundaryMatrix = rightBoundaryMatrix;
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
        double feedbackWeight1 = (2.44413 * poleScale + 2.85619 * poleScaleSq + 1.26661 * poleScaleCubed) / normalization;
        double feedbackWeight2 = -(1.4281 * poleScaleSq + 1.26661 * poleScaleCubed) / normalization;
        double feedbackWeight3 = (0.422205 * poleScaleCubed) / normalization;

        // Unity DC-gain input weight `B = 1 - (a1 + a2 + a3)`.
        double inputGain = 1.0 - (feedbackWeight1 + feedbackWeight2 + feedbackWeight3);
        IReadOnlyMatrix<DoublePixel> rightBoundaryMatrix = CreateTriggsSdikaBoundaryMatrix(
            feedbackWeight1,
            feedbackWeight2,
            feedbackWeight3);

        return new YoungVanVlietFilter(
            inputGain,
            feedbackWeight1,
            feedbackWeight2,
            feedbackWeight3,
            rightBoundaryMatrix);
    }

    /// <summary>
    /// Applies the bidirectional recursive Gaussian filter to a line of SIMD vector samples in place.
    /// </summary>
    /// <remarks>
    /// Performs a forward causal pass followed by an anti-causal backward pass with Triggs–Sdika boundary condition handling.
    /// </remarks>
    /// <param name="line">The line of SIMD vector samples to filter in place.</param>
    public void FilterLine(Span<Vector<double>> line)
    {
        int length = line.Length;
        Vector<double> leftBoundaryValue = line[0];
        Vector<double> rightBoundaryValue = line[length - 1];

        // Causal left-to-right pass assuming constant left extension equal to line[0].
        Vector<double> prevForward1 = leftBoundaryValue;
        Vector<double> prevForward2 = leftBoundaryValue;
        Vector<double> prevForward3 = leftBoundaryValue;

        for (int i = 0; i < length; i++)
        {
            Vector<double> forwardSample =
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
            Vector<double> backwardSample =
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
    private (Vector<double> AtEdge, Vector<double> BeyondEdge1, Vector<double> BeyondEdge2) ComputeInitialBackwardState(
        Vector<double> rightBoundaryValue,
        Vector<double> prevForward1,
        Vector<double> prevForward2,
        Vector<double> prevForward3)
    {
        Vector<double> deviation1 = prevForward1 - rightBoundaryValue;
        Vector<double> deviation2 = prevForward2 - rightBoundaryValue;
        Vector<double> deviation3 = prevForward3 - rightBoundaryValue;
        IReadOnlyMatrix<DoublePixel> matrix = rightBoundaryMatrix;

        Vector<double> ComputeRow(int row) =>
            rightBoundaryValue
            + matrix[0, row].Value * deviation1
            + matrix[1, row].Value * deviation2
            + matrix[2, row].Value * deviation3;

        return (
            AtEdge: ComputeRow(0),
            BeyondEdge1: ComputeRow(1),
            BeyondEdge2: ComputeRow(2));
    }

    /// <summary>
    /// Constructs the 3x3 Triggs–Sdika boundary matrix used to initialize the anti-causal backward filter pass.
    /// </summary>
    /// <param name="feedback1">The first-order feedback weight.</param>
    /// <param name="feedback2">The second-order feedback weight.</param>
    /// <param name="feedback3">The third-order feedback weight.</param>
    /// <returns>A 3x3 sequential matrix containing the closed-form Triggs–Sdika polynomial boundary coefficients.</returns>
    private static SequentialMatrix<DoublePixel> CreateTriggsSdikaBoundaryMatrix(
        double feedback1,
        double feedback2,
        double feedback3)
    {
        // Triggs & Sdika (2006) Eq. (14), scaled by inputGain = 1 - (a1 + a2 + a3).
        // Local aliases keep the closed-form boundary polynomials concise and directly verifiable.
        double a1 = feedback1;
        double a2 = feedback2;
        double a3 = feedback3;

        double invDenom = 1.0 / ((1.0 + a1 - a2 + a3) * (1.0 + a2 + (a1 - a3) * a3));
        return new SequentialMatrix<DoublePixel>(new DoublePixel[,]
        {
            {
                invDenom * (1.0 - a2 - a3 * (a1 + a3)),
                invDenom * (a1 + a3) * (a2 + a1 * a3),
                invDenom * a3 * (a1 + a2 * a3)
            },
            {
                invDenom * (a1 + a2 * a3),
                invDenom * (1.0 - a2) * (a2 + a1 * a3),
                -invDenom * a3 * (a1 * a3 + a3 * a3 + a2 - 1.0)
            },
            {
                invDenom * (a1 * a3 + a2 + a1 * a1 - a2 * a2),
                invDenom * (a1 * a2 + a3 * a2 * a2 - a1 * a3 * a3 - a3 * a3 * a3 - a2 * a3 + a3),
                invDenom * a3 * (a1 + a2 * a3)
            }
        });
    }
}
