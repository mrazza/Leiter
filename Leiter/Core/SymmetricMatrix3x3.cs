namespace Leiter.Core;

using System.Text;

/// <summary>
/// Represents a symmetric 3×3 matrix for 3D covariance, quadratic form, and eigenanalysis operations.
/// </summary>
/// <param name="XX">The (0, 0) diagonal component.</param>
/// <param name="XY">The off-diagonal (0, 1) and (1, 0) component.</param>
/// <param name="XZ">The off-diagonal (0, 2) and (2, 0) component.</param>
/// <param name="YY">The (1, 1) diagonal component.</param>
/// <param name="YZ">The off-diagonal (1, 2) and (2, 1) component.</param>
/// <param name="ZZ">The (2, 2) diagonal component.</param>
public readonly record struct SymmetricMatrix3x3(
    double XX,
    double XY,
    double XZ,
    double YY,
    double YZ,
    double ZZ)
{
    /// <summary>
    /// Gets the 3×3 zero matrix.
    /// </summary>
    public static SymmetricMatrix3x3 Zero => default;

    /// <summary>
    /// Gets the 3×3 identity matrix.
    /// </summary>
    public static SymmetricMatrix3x3 Identity => new(1.0, 0.0, 0.0, 1.0, 0.0, 1.0);

    /// <summary>
    /// Gets the determinant of this symmetric 3×3 matrix.
    /// </summary>
    public double Determinant =>
        (XX * ((YY * ZZ) - (YZ * YZ))) +
        (XY * ((XZ * YZ) - (XY * ZZ))) +
        (XZ * ((XY * YZ) - (XZ * YY)));

    /// <summary>
    /// Returns a new matrix equal to <c>this + vector ⊗ vector</c>.
    /// </summary>
    /// <param name="vector">The vector whose outer product is added.</param>
    /// <returns>The updated symmetric matrix.</returns>
    public SymmetricMatrix3x3 AddOuterProduct(Vector3D vector) => new(
        XX + (vector.X * vector.X),
        XY + (vector.X * vector.Y),
        XZ + (vector.X * vector.Z),
        YY + (vector.Y * vector.Y),
        YZ + (vector.Y * vector.Z),
        ZZ + (vector.Z * vector.Z));

    /// <summary>
    /// Returns the component-wise sum of this matrix and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The matrix to add.</param>
    /// <returns>The component-wise matrix sum.</returns>
    public SymmetricMatrix3x3 Add(SymmetricMatrix3x3 other) => new(
        XX + other.XX,
        XY + other.XY,
        XZ + other.XZ,
        YY + other.YY,
        YZ + other.YZ,
        ZZ + other.ZZ);

    /// <summary>
    /// Adds two symmetric 3×3 matrices component-wise.
    /// </summary>
    /// <param name="left">The left matrix operand.</param>
    /// <param name="right">The right matrix operand.</param>
    /// <returns>The component-wise sum of the two matrices.</returns>
    public static SymmetricMatrix3x3 operator +(SymmetricMatrix3x3 left, SymmetricMatrix3x3 right) =>
        left.Add(right);

    /// <summary>
    /// Computes a sample covariance matrix (with optional diagonal regularization) from first- and second-order sums.
    /// </summary>
    /// <param name="sumVectors">The sum of all sample vectors.</param>
    /// <param name="sumOuterProducts">The sum of sample outer products <c>Σ v_i ⊗ v_i</c>.</param>
    /// <param name="sampleCount">The positive number of accumulated samples.</param>
    /// <param name="diagonalRegularization">Optional non-negative finite regularization constant added to each diagonal entry.</param>
    /// <returns>The computed covariance matrix.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="sampleCount"/> is less than or equal to zero, or <paramref name="diagonalRegularization"/> is negative or non-finite.
    /// </exception>
    public static SymmetricMatrix3x3 FromSums(
        Vector3D sumVectors,
        SymmetricMatrix3x3 sumOuterProducts,
        int sampleCount,
        double diagonalRegularization = 0.0)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleCount, 0);
        Guard.ThrowIfNotFiniteNonNegative(diagonalRegularization);

        Vector3D mean = sumVectors / sampleCount;
        return new SymmetricMatrix3x3(
            XX: Math.Max(0.0, (sumOuterProducts.XX / sampleCount) - (mean.X * mean.X)) + diagonalRegularization,
            XY: (sumOuterProducts.XY / sampleCount) - (mean.X * mean.Y),
            XZ: (sumOuterProducts.XZ / sampleCount) - (mean.X * mean.Z),
            YY: Math.Max(0.0, (sumOuterProducts.YY / sampleCount) - (mean.Y * mean.Y)) + diagonalRegularization,
            YZ: (sumOuterProducts.YZ / sampleCount) - (mean.Y * mean.Z),
            ZZ: Math.Max(0.0, (sumOuterProducts.ZZ / sampleCount) - (mean.Z * mean.Z)) + diagonalRegularization);
    }

    /// <summary>
    /// Attempts to invert this symmetric matrix analytically when the magnitude of its determinant exceeds <paramref name="determinantEpsilon"/>.
    /// </summary>
    /// <param name="inverse">When this method returns <see langword="true"/>, contains the matrix inverse; otherwise <see cref="Zero"/>.</param>
    /// <param name="determinant">When this method returns, contains the computed determinant of this matrix.</param>
    /// <param name="determinantEpsilon">The non-negative finite determinant magnitude threshold at or below which the matrix is treated as singular.</param>
    /// <returns><see langword="true"/> if the matrix was inverted; <see langword="false"/> if its determinant is non-finite or within <paramref name="determinantEpsilon"/> of zero.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="determinantEpsilon"/> is negative or non-finite.</exception>
    public bool TryInvert(
        out SymmetricMatrix3x3 inverse,
        out double determinant,
        double determinantEpsilon = 1e-12)
    {
        Guard.ThrowIfNotFiniteNonNegative(determinantEpsilon);

        SymmetricMatrix3x3 cofactors = ComputeCofactors(out determinant);
        if (!double.IsFinite(determinant) || Math.Abs(determinant) <= determinantEpsilon)
        {
            inverse = default;
            return false;
        }

        inverse = Scale(cofactors, 1.0 / determinant);
        return true;
    }

    /// <summary>
    /// Computes the matrix inverse analytically, substituting a regularized isotropic diagonal matrix
    /// <c>(1 / fallbackDiagonal) · I</c> when <paramref name="determinant"/> is at or below <c>1e-12</c> or non-finite.
    /// </summary>
    /// <param name="fallbackDiagonal">The positive finite diagonal variance used to synthesize a positive-definite fallback when near-singular.</param>
    /// <param name="determinant">When this method returns, contains the determinant (or <c>fallbackDiagonal³</c> when the fallback was used).</param>
    /// <returns>The exact or regularized inverse matrix.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="fallbackDiagonal"/> is less than or equal to zero or non-finite.</exception>
    public SymmetricMatrix3x3 Invert(double fallbackDiagonal, out double determinant)
    {
        Guard.ThrowIfNotFinitePositive(fallbackDiagonal);

        SymmetricMatrix3x3 cofactors = ComputeCofactors(out determinant);
        if (!double.IsFinite(determinant) || determinant <= 1e-12)
        {
            determinant = fallbackDiagonal * fallbackDiagonal * fallbackDiagonal;
            double invDiag = 1.0 / fallbackDiagonal;
            return new SymmetricMatrix3x3(invDiag, 0.0, 0.0, invDiag, 0.0, invDiag);
        }

        return Scale(cofactors, 1.0 / determinant);
    }

    /// <summary>
    /// Evaluates the quadratic form <c>deltaᵀ · M · delta</c>.
    /// </summary>
    /// <param name="delta">The 3D vector operand.</param>
    /// <returns>The scalar quadratic form value.</returns>
    public double QuadraticForm(Vector3D delta) =>
        (delta.X * ((XX * delta.X) + (XY * delta.Y) + (XZ * delta.Z))) +
        (delta.Y * ((XY * delta.X) + (YY * delta.Y) + (YZ * delta.Z))) +
        (delta.Z * ((XZ * delta.X) + (YZ * delta.Y) + (ZZ * delta.Z)));

    /// <summary>
    /// Computes the algebraically largest eigenvalue and its corresponding unit-length eigenvector using Jacobi eigenvalue iteration.
    /// </summary>
    /// <returns>A tuple containing the largest eigenvalue and an associated unit eigenvector.</returns>
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

    private SymmetricMatrix3x3 ComputeCofactors(out double determinant)
    {
        double cofactorXX = (YY * ZZ) - (YZ * YZ);
        double cofactorXY = (XZ * YZ) - (XY * ZZ);
        double cofactorXZ = (XY * YZ) - (XZ * YY);
        double cofactorYY = (XX * ZZ) - (XZ * XZ);
        double cofactorYZ = (XY * XZ) - (XX * YZ);
        double cofactorZZ = (XX * YY) - (XY * XY);

        determinant = (XX * cofactorXX) + (XY * cofactorXY) + (XZ * cofactorXZ);
        return new SymmetricMatrix3x3(cofactorXX, cofactorXY, cofactorXZ, cofactorYY, cofactorYZ, cofactorZZ);
    }

    private static SymmetricMatrix3x3 Scale(SymmetricMatrix3x3 matrix, double scalar) => new(
        matrix.XX * scalar,
        matrix.XY * scalar,
        matrix.XZ * scalar,
        matrix.YY * scalar,
        matrix.YZ * scalar,
        matrix.ZZ * scalar);

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

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append($"XX = {XX}, XY = {XY}, XZ = {XZ}, YY = {YY}, YZ = {YZ}, ZZ = {ZZ}");
        return true;
    }
}
