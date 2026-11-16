namespace Leiter.Algorithms.Segmentation.Internal;

using Leiter.Pixels;

/// <summary>
/// Represents a symmetric 3x3 matrix for 3D RGB covariance and precision operations.
/// </summary>
internal readonly record struct SymmetricMatrix3x3(
    double RR,
    double RG,
    double RB,
    double GG,
    double GB,
    double BB)
{
    public static SymmetricMatrix3x3 Zero => default;

    public SymmetricMatrix3x3 AddOuterProduct(Rgb64 color) => new(
        RR + (color.R * color.R),
        RG + (color.R * color.G),
        RB + (color.R * color.B),
        GG + (color.G * color.G),
        GB + (color.G * color.B),
        BB + (color.B * color.B));

    public static SymmetricMatrix3x3 FromColorSums(
        Rgb64 sumColors,
        SymmetricMatrix3x3 sumOuterProducts,
        int sampleCount,
        double diagonalRegularization = 0.0)
    {
        Rgb64 mean = sumColors.Divide((double)sampleCount);
        return new SymmetricMatrix3x3(
            RR: Math.Max(0.0, (sumOuterProducts.RR / sampleCount) - (mean.R * mean.R)) + diagonalRegularization,
            RG: (sumOuterProducts.RG / sampleCount) - (mean.R * mean.G),
            RB: (sumOuterProducts.RB / sampleCount) - (mean.R * mean.B),
            GG: Math.Max(0.0, (sumOuterProducts.GG / sampleCount) - (mean.G * mean.G)) + diagonalRegularization,
            GB: (sumOuterProducts.GB / sampleCount) - (mean.G * mean.B),
            BB: Math.Max(0.0, (sumOuterProducts.BB / sampleCount) - (mean.B * mean.B)) + diagonalRegularization);
    }

    public SymmetricMatrix3x3 Invert(double fallbackDiagonal, out double determinant)
    {
        double cofactorRR = (GG * BB) - (GB * GB);
        double cofactorRG = (RB * GB) - (RG * BB);
        double cofactorRB = (RG * GB) - (RB * GG);
        double cofactorGG = (RR * BB) - (RB * RB);
        double cofactorGB = (RG * RB) - (RR * GB);
        double cofactorBB = (RR * GG) - (RG * RG);

        determinant = (RR * cofactorRR) + (RG * cofactorRG) + (RB * cofactorRB);
        if (determinant <= 1e-12)
        {
            determinant = fallbackDiagonal * fallbackDiagonal * fallbackDiagonal;
            double invDiag = 1.0 / fallbackDiagonal;
            return new SymmetricMatrix3x3(invDiag, 0.0, 0.0, invDiag, 0.0, invDiag);
        }

        double invDet = 1.0 / determinant;
        return new SymmetricMatrix3x3(
            cofactorRR * invDet,
            cofactorRG * invDet,
            cofactorRB * invDet,
            cofactorGG * invDet,
            cofactorGB * invDet,
            cofactorBB * invDet);
    }

    public double QuadraticForm(Rgb64 delta) =>
        (delta.R * ((RR * delta.R) + (RG * delta.G) + (RB * delta.B))) +
        (delta.G * ((RG * delta.R) + (GG * delta.G) + (GB * delta.B))) +
        (delta.B * ((RB * delta.R) + (GB * delta.G) + (BB * delta.B)));

    public (double Eigenvalue, Rgb64 Eigenvector) FindPrincipalEigenpair()
    {
        Span<double> matrix =
        [
            RR, RG, RB,
            RG, GG, GB,
            RB, GB, BB,
        ];

        Span<double> eigenvectors =
        [
            1.0, 0.0, 0.0,
            0.0, 1.0, 0.0,
            0.0, 0.0, 1.0,
        ];

        for (int sweep = 0; sweep < 12; sweep++)
        {
            SelectPivotIndices(matrix, out int p, out int q, out double maxOffDiagonal);
            if (maxOffDiagonal <= 1e-12)
                break;

            ApplyJacobiRotation(matrix, eigenvectors, p, q);
        }

        int bestCol = 0;
        if (matrix[4] > matrix[0])
            bestCol = 1;
        if (matrix[8] > matrix[(bestCol * 3) + bestCol])
            bestCol = 2;

        return (
            matrix[(bestCol * 3) + bestCol],
            new Rgb64(eigenvectors[bestCol], eigenvectors[3 + bestCol], eigenvectors[6 + bestCol]));
    }

    private static void SelectPivotIndices(
        ReadOnlySpan<double> matrix,
        out int p,
        out int q,
        out double maxOffDiagonal)
    {
        double a01 = Math.Abs(matrix[1]);
        double a02 = Math.Abs(matrix[2]);
        double a12 = Math.Abs(matrix[5]);

        if (a01 >= a02 && a01 >= a12)
        {
            p = 0;
            q = 1;
            maxOffDiagonal = a01;
        }
        else if (a02 >= a12)
        {
            p = 0;
            q = 2;
            maxOffDiagonal = a02;
        }
        else
        {
            p = 1;
            q = 2;
            maxOffDiagonal = a12;
        }
    }

    private static void ApplyJacobiRotation(Span<double> matrix, Span<double> eigenvectors, int p, int q)
    {
        int r = 3 - p - q;
        double mpp = matrix[(p * 3) + p];
        double mqq = matrix[(q * 3) + q];
        double mpq = matrix[(p * 3) + q];

        double tau = (mqq - mpp) / (2.0 * mpq);
        double t = tau >= 0.0
            ? 1.0 / (tau + Math.Sqrt(1.0 + (tau * tau)))
            : -1.0 / (-tau + Math.Sqrt(1.0 + (tau * tau)));
        double c = 1.0 / Math.Sqrt(1.0 + (t * t));
        double s = t * c;

        matrix[(p * 3) + p] = (c * c * mpp) - (2.0 * s * c * mpq) + (s * s * mqq);
        matrix[(q * 3) + q] = (s * s * mpp) + (2.0 * s * c * mpq) + (c * c * mqq);
        matrix[(p * 3) + q] = 0.0;
        matrix[(q * 3) + p] = 0.0;

        double mrp = matrix[(r * 3) + p];
        double mrq = matrix[(r * 3) + q];
        double newRp = (c * mrp) - (s * mrq);
        double newRq = (s * mrp) + (c * mrq);
        matrix[(r * 3) + p] = newRp;
        matrix[(p * 3) + r] = newRp;
        matrix[(r * 3) + q] = newRq;
        matrix[(q * 3) + r] = newRq;

        for (int row = 0; row < 3; row++)
        {
            double vrp = eigenvectors[(row * 3) + p];
            double vrq = eigenvectors[(row * 3) + q];
            eigenvectors[(row * 3) + p] = (c * vrp) - (s * vrq);
            eigenvectors[(row * 3) + q] = (s * vrp) + (c * vrq);
        }
    }
}
