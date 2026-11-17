namespace Leiter.Tests.Core;

using System;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Vector3D"/>.
/// </summary>
public class Vector3DTests
{
    /// <summary>
    /// Verifies that <see cref="Vector3D.Zero"/> returns a vector with all components equal to zero.
    /// </summary>
    [Fact]
    public void Zero_ShouldReturnZeroComponents()
    {
        Vector3D zero = Vector3D.Zero;

        Assert.Equal(0.0, zero.X);
        Assert.Equal(0.0, zero.Y);
        Assert.Equal(0.0, zero.Z);
    }

    /// <summary>
    /// Verifies that <see cref="Vector3D.LengthSquared"/> and <see cref="Vector3D.Length"/> compute correct Euclidean values.
    /// </summary>
    [Fact]
    public void LengthAndLengthSquared_ShouldComputeCorrectly()
    {
        var vector = new Vector3D(2.0, 3.0, 6.0);

        Assert.Equal(49.0, vector.LengthSquared);
        Assert.Equal(7.0, vector.Length);
    }

    /// <summary>
    /// Verifies that <see cref="Vector3D.Dot"/> and <see cref="Vector3D.Cross"/> follow right-handed 3D vector identities.
    /// </summary>
    [Fact]
    public void DotAndCross_ShouldComputeCorrectly()
    {
        var a = new Vector3D(1.0, 2.0, 3.0);
        var b = new Vector3D(4.0, -5.0, 6.0);

        Assert.Equal(12.0, a.Dot(b));

        Vector3D cross = a.Cross(b);
        Assert.Equal(new Vector3D(27.0, 6.0, -13.0), cross);
        Assert.Equal(0.0, cross.Dot(a), 12);
        Assert.Equal(0.0, cross.Dot(b), 12);

        var unitX = new Vector3D(1.0, 0.0, 0.0);
        var unitY = new Vector3D(0.0, 1.0, 0.0);
        Assert.Equal(new Vector3D(0.0, 0.0, 1.0), unitX.Cross(unitY));
    }

    /// <summary>
    /// Verifies that <see cref="Vector3D.Normalize"/> normalizes normal vectors to unit length.
    /// </summary>
    [Fact]
    public void Normalize_NormalVector_ShouldReturnUnitLength()
    {
        var vector = new Vector3D(2.0, 3.0, 6.0);
        Vector3D normalized = vector.Normalize();

        Assert.Equal(2.0 / 7.0, normalized.X, 9);
        Assert.Equal(3.0 / 7.0, normalized.Y, 9);
        Assert.Equal(6.0 / 7.0, normalized.Z, 9);
        Assert.Equal(1.0, normalized.Length, 9);
    }

    /// <summary>
    /// Verifies that <see cref="Vector3D.Normalize"/> returns <see cref="Vector3D.Zero"/> on zero and sub-epsilon vectors.
    /// </summary>
    [Fact]
    public void Normalize_ZeroAndSubEpsilon_ShouldReturnZero()
    {
        Assert.Equal(Vector3D.Zero, Vector3D.Zero.Normalize());
        Assert.Equal(Vector3D.Zero, new Vector3D(1e-13, 0.0, 0.0).Normalize());
    }

    /// <summary>
    /// Verifies that <see cref="Vector3D.Normalize"/> returns <see cref="Vector3D.Zero"/> on non-finite components.
    /// </summary>
    [Theory]
    [InlineData(double.NaN, 1.0, 0.0)]
    [InlineData(1.0, double.NaN, 0.0)]
    [InlineData(1.0, 0.0, double.NaN)]
    [InlineData(double.PositiveInfinity, 0.0, 0.0)]
    [InlineData(0.0, double.NegativeInfinity, 0.0)]
    public void Normalize_NonFiniteComponents_ShouldReturnZero(double x, double y, double z)
    {
        var nonFinite = new Vector3D(x, y, z);
        Assert.Equal(Vector3D.Zero, nonFinite.Normalize());
    }

    /// <summary>
    /// Verifies component-wise arithmetic operators and record semantics.
    /// </summary>
    [Fact]
    public void Operators_EqualityAndFormatting_ShouldBehaveCorrectly()
    {
        var a = new Vector3D(1.5, -2.0, 4.0);
        var b = new Vector3D(2.5, 5.0, -1.0);

        Assert.Equal(new Vector3D(4.0, 3.0, 3.0), a + b);
        Assert.Equal(new Vector3D(-1.0, -7.0, 5.0), a - b);
        Assert.Equal(new Vector3D(-1.5, 2.0, -4.0), -a);
        Assert.Equal(new Vector3D(3.0, -4.0, 8.0), a * 2.0);
        Assert.Equal(a * 2.0, 2.0 * a);
        Assert.Equal(new Vector3D(0.75, -1.0, 2.0), a / 2.0);

        var aCopy = new Vector3D(1.5, -2.0, 4.0);
        Assert.Equal(a, aCopy);
        Assert.NotEqual(a, b);
        Assert.Equal(a.GetHashCode(), aCopy.GetHashCode());
        Assert.Contains("1.5", a.ToString());
    }
}
