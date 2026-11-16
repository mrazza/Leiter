namespace Leiter.Tests.Algorithms.Segmentation.Internal;

using System;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SymmetricMatrix3x3"/>.
/// </summary>
public class SymmetricMatrix3x3Tests
{
    [Fact]
    public void FromColorSums_Invert_AndQuadraticForm_ShouldComputeExactValues()
    {
        Rgb64[] samples =
        [
            new(10.0, 20.0, 30.0),
            new(14.0, 22.0, 26.0),
            new(12.0, 24.0, 34.0),
        ];

        Rgb64 sumColors = Rgb64.Zero;
        SymmetricMatrix3x3 sumOuterProducts = SymmetricMatrix3x3.Zero;
        foreach (Rgb64 sample in samples)
        {
            sumColors = sumColors.Add(sample);
            sumOuterProducts = sumOuterProducts.AddOuterProduct(sample);
        }

        SymmetricMatrix3x3 covariance = SymmetricMatrix3x3.FromColorSums(
            sumColors,
            sumOuterProducts,
            samples.Length,
            diagonalRegularization: 0.5);

        Assert.True(covariance.RR > 0.5);
        Assert.True(covariance.GG > 0.5);
        Assert.True(covariance.BB > 0.5);

        SymmetricMatrix3x3 inverse = covariance.Invert(fallbackDiagonal: 0.5, out double determinant);
        Assert.True(determinant > 0.0);

        // First row of (covariance * inverse) should equal (1, 0, 0).
        double e00 = (covariance.RR * inverse.RR) + (covariance.RG * inverse.RG) + (covariance.RB * inverse.RB);
        double e01 = (covariance.RR * inverse.RG) + (covariance.RG * inverse.GG) + (covariance.RB * inverse.GB);
        double e02 = (covariance.RR * inverse.RB) + (covariance.RG * inverse.GB) + (covariance.RB * inverse.BB);
        Assert.Equal(1.0, e00, 9);
        Assert.Equal(0.0, e01, 9);
        Assert.Equal(0.0, e02, 9);

        var delta = new Rgb64(2.0, -1.0, 3.0);
        Assert.True(inverse.QuadraticForm(delta) > 0.0);
    }

    [Fact]
    public void Invert_SingularMatrix_ShouldUseFallbackDiagonal()
    {
        var singular = SymmetricMatrix3x3.Zero;
        SymmetricMatrix3x3 inverse = singular.Invert(fallbackDiagonal: 0.1, out double determinant);

        Assert.Equal(0.001, determinant, 12);
        Assert.Equal(10.0, inverse.RR, 9);
        Assert.Equal(10.0, inverse.GG, 9);
        Assert.Equal(10.0, inverse.BB, 9);
        Assert.Equal(0.0, inverse.RG, 12);
        Assert.Equal(0.0, inverse.RB, 12);
        Assert.Equal(0.0, inverse.GB, 12);
    }

    [Fact]
    public void FindPrincipalEigenpair_ShouldRecoverDominantAxisAndHandleDiagonal()
    {
        // Rank-1 outer product along normalized direction (3, 4, 0) / 5 scaled by eigenvalue 25.
        var matrix = new SymmetricMatrix3x3(
            RR: 9.0,
            RG: 12.0,
            RB: 0.0,
            GG: 16.0,
            GB: 0.0,
            BB: 1.0);

        (double eigenvalue, Rgb64 eigenvector) = matrix.FindPrincipalEigenpair();
        Assert.Equal(25.0, eigenvalue, 9);
        Assert.Equal(0.6, Math.Abs(eigenvector.R), 9);
        Assert.Equal(0.8, Math.Abs(eigenvector.G), 9);
        Assert.Equal(0.0, eigenvector.B, 9);

        // Already diagonal matrix exits early and selects largest diagonal entry.
        var diagonal = new SymmetricMatrix3x3(2.0, 0.0, 0.0, 5.0, 0.0, 9.0);
        (double diagVal, Rgb64 diagVec) = diagonal.FindPrincipalEigenpair();
        Assert.Equal(9.0, diagVal, 12);
        Assert.Equal(new Rgb64(0.0, 0.0, 1.0), diagVec);
    }
}
