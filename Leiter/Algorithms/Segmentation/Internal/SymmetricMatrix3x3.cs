namespace Leiter.Algorithms.Segmentation.Internal;

using Leiter.Core;

/// <summary>
/// Represents a symmetric 3x3 matrix for 3D covariance and precision operations.
/// </summary>
internal readonly record struct SymmetricMatrix3x3(
    double XX,
    double XY,
    double XZ,
    double YY,
    double YZ,
    double ZZ)
{
    public static SymmetricMatrix3x3 Zero => default;

    public SymmetricMatrix3x3 AddOuterProduct(Vector3D vector) => new(
        XX + (vector.X * vector.X),
        XY + (vector.X * vector.Y),
        XZ + (vector.X * vector.Z),
        YY + (vector.Y * vector.Y),
        YZ + (vector.Y * vector.Z),
        ZZ + (vector.Z * vector.Z));

    public SymmetricMatrix3x3 Add(SymmetricMatrix3x3 other) => new(
        XX + other.XX,
        XY + other.XY,
        XZ + other.XZ,
        YY + other.YY,
        YZ + other.YZ,
        ZZ + other.ZZ);

    public static SymmetricMatrix3x3 FromSums(
        Vector3D sumVectors,
        SymmetricMatrix3x3 sumOuterProducts,
        int sampleCount,
        double diagonalRegularization = 0.0)
    {
        Vector3D mean = sumVectors / sampleCount;
        return new SymmetricMatrix3x3(
            XX: Math.Max(0.0, (sumOuterProducts.XX / sampleCount) - (mean.X * mean.X)) + diagonalRegularization,
            XY: (sumOuterProducts.XY / sampleCount) - (mean.X * mean.Y),
            XZ: (sumOuterProducts.XZ / sampleCount) - (mean.X * mean.Z),
            YY: Math.Max(0.0, (sumOuterProducts.YY / sampleCount) - (mean.Y * mean.Y)) + diagonalRegularization,
            YZ: (sumOuterProducts.YZ / sampleCount) - (mean.Y * mean.Z),
            ZZ: Math.Max(0.0, (sumOuterProducts.ZZ / sampleCount) - (mean.Z * mean.Z)) + diagonalRegularization);
    }

    public SymmetricMatrix3x3 Invert(double fallbackDiagonal, out double determinant)
    {
        double cofactorXX = (YY * ZZ) - (YZ * YZ);
        double cofactorXY = (XZ * YZ) - (XY * ZZ);
        double cofactorXZ = (XY * YZ) - (XZ * YY);
        double cofactorYY = (XX * ZZ) - (XZ * XZ);
        double cofactorYZ = (XY * XZ) - (XX * YZ);
        double cofactorZZ = (XX * YY) - (XY * XY);

        determinant = (XX * cofactorXX) + (XY * cofactorXY) + (XZ * cofactorXZ);
        if (determinant <= 1e-12)
        {
            determinant = fallbackDiagonal * fallbackDiagonal * fallbackDiagonal;
            double invDiag = 1.0 / fallbackDiagonal;
            return new SymmetricMatrix3x3(invDiag, 0.0, 0.0, invDiag, 0.0, invDiag);
        }

        double invDet = 1.0 / determinant;
        return new SymmetricMatrix3x3(
            cofactorXX * invDet,
            cofactorXY * invDet,
            cofactorXZ * invDet,
            cofactorYY * invDet,
            cofactorYZ * invDet,
            cofactorZZ * invDet);
    }

    public double QuadraticForm(Vector3D delta) =>
        (delta.X * ((XX * delta.X) + (XY * delta.Y) + (XZ * delta.Z))) +
        (delta.Y * ((XY * delta.X) + (YY * delta.Y) + (YZ * delta.Z))) +
        (delta.Z * ((XZ * delta.X) + (YZ * delta.Y) + (ZZ * delta.Z)));

    public (double Eigenvalue, Vector3D Eigenvector) FindPrincipalEigenpair()
    {
        Span<double> matrix =
        [
            XX, XY, XZ,
            XY, YY, YZ,
            XZ, YZ, ZZ,
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
            new Vector3D(eigenvectors[bestCol], eigenvectors[3 + bestCol], eigenvectors[6 + bestCol]));
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
