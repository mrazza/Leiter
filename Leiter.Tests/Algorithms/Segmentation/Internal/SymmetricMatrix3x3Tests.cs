namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using System;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SymmetricMatrix3x3"/>.
/// </summary>
public class SymmetricMatrix3x3Tests
{
    [Fact]
    public void FromSums_Invert_AndQuadraticForm_ShouldComputeExactValues()
    {
        Vector3D[] samples =
        [
            new(10.0, 20.0, 30.0),
            new(14.0, 22.0, 26.0),
            new(12.0, 24.0, 34.0),
        ];

        Vector3D sumVectors = Vector3D.Zero;
        SymmetricMatrix3x3 sumOuterProducts = SymmetricMatrix3x3.Zero;
        foreach (Vector3D sample in samples)
        {
            sumVectors += sample;
            sumOuterProducts = sumOuterProducts.AddOuterProduct(sample);
        }

        SymmetricMatrix3x3 covariance = SymmetricMatrix3x3.FromSums(
            sumVectors,
            sumOuterProducts,
            samples.Length,
            diagonalRegularization: 0.5);

        Assert.True(covariance.XX > 0.5);
        Assert.True(covariance.YY > 0.5);
        Assert.True(covariance.ZZ > 0.5);

        SymmetricMatrix3x3 inverse = covariance.Invert(fallbackDiagonal: 0.5, out double determinant);
        Assert.True(determinant > 0.0);

        // First row of (covariance * inverse) should equal (1, 0, 0).
        double e00 = (covariance.XX * inverse.XX) + (covariance.XY * inverse.XY) + (covariance.XZ * inverse.XZ);
        double e01 = (covariance.XX * inverse.XY) + (covariance.XY * inverse.YY) + (covariance.XZ * inverse.YZ);
        double e02 = (covariance.XX * inverse.XZ) + (covariance.XY * inverse.YZ) + (covariance.XZ * inverse.ZZ);
        Assert.Equal(1.0, e00, 9);
        Assert.Equal(0.0, e01, 9);
        Assert.Equal(0.0, e02, 9);

        var delta = new Vector3D(2.0, -1.0, 3.0);
        Assert.True(inverse.QuadraticForm(delta) > 0.0);
    }

    [Fact]
    public void Invert_SingularMatrix_ShouldUseFallbackDiagonal()
    {
        var singular = SymmetricMatrix3x3.Zero;
        SymmetricMatrix3x3 inverse = singular.Invert(fallbackDiagonal: 0.1, out double determinant);

        Assert.Equal(0.001, determinant, 12);
        Assert.Equal(10.0, inverse.XX, 9);
        Assert.Equal(10.0, inverse.YY, 9);
        Assert.Equal(10.0, inverse.ZZ, 9);
        Assert.Equal(0.0, inverse.XY, 12);
        Assert.Equal(0.0, inverse.XZ, 12);
        Assert.Equal(0.0, inverse.YZ, 12);
    }

    [Fact]
    public void FindPrincipalEigenpair_ShouldRecoverDominantAxisAndHandleDiagonal()
    {
        // Rank-1 outer product along normalized direction (3, 4, 0) / 5 scaled by eigenvalue 25.
        var matrix = new SymmetricMatrix3x3(
            XX: 9.0,
            XY: 12.0,
            XZ: 0.0,
            YY: 16.0,
            YZ: 0.0,
            ZZ: 1.0);

        (double eigenvalue, Vector3D eigenvector) = matrix.FindPrincipalEigenpair();
        Assert.Equal(25.0, eigenvalue, 9);
        Assert.Equal(0.6, Math.Abs(eigenvector.X), 9);
        Assert.Equal(0.8, Math.Abs(eigenvector.Y), 9);
        Assert.Equal(0.0, eigenvector.Z, 9);

        // Already diagonal matrix exits early and selects largest diagonal entry.
        var diagonal = new SymmetricMatrix3x3(2.0, 0.0, 0.0, 5.0, 0.0, 9.0);
        (double diagVal, Vector3D diagVec) = diagonal.FindPrincipalEigenpair();
        Assert.Equal(9.0, diagVal, 12);
        Assert.Equal(new Vector3D(0.0, 0.0, 1.0), diagVec);
    }
}
