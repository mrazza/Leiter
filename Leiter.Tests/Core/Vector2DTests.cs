namespace Leiter.Tests.Core;

using System;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Vector2D"/>.
/// </summary>
public class Vector2DTests
{
    /// <summary>
    /// Verifies that <see cref="Vector2D.Zero"/> returns a vector with both components equal to zero.
    /// </summary>
    [Fact]
    public void Zero_ShouldReturnZeroComponents()
    {
        Vector2D zero = Vector2D.Zero;

        Assert.Equal(0.0, zero.X);
        Assert.Equal(0.0, zero.Y);
    }

    /// <summary>
    /// Verifies that <see cref="Vector2D.LengthSquared"/> and <see cref="Vector2D.Length"/> compute correct Euclidean values.
    /// </summary>
    [Fact]
    public void LengthAndLengthSquared_ShouldComputeCorrectly()
    {
        var vector = new Vector2D(3.0, 4.0);

        Assert.Equal(25.0, vector.LengthSquared);
        Assert.Equal(5.0, vector.Length);
    }

    /// <summary>
    /// Verifies that <see cref="Vector2D.Perpendicular"/> produces a counter-clockwise orthogonal vector (-Y, X).
    /// </summary>
    [Fact]
    public void Perpendicular_ShouldRotateCounterClockwise()
    {
        var vector = new Vector2D(3.0, 4.0);
        Vector2D perp = vector.Perpendicular;

        Assert.Equal(-4.0, perp.X);
        Assert.Equal(3.0, perp.Y);
        Assert.Equal(0.0, vector.Dot(perp));
    }

    /// <summary>
    /// Verifies that <see cref="Vector2D.Dot"/> computes the correct scalar product.
    /// </summary>
    [Fact]
    public void Dot_ShouldComputeScalarProduct()
    {
        var a = new Vector2D(2.0, 3.0);
        var b = new Vector2D(4.0, -5.0);

        Assert.Equal(-7.0, a.Dot(b));
    }

    /// <summary>
    /// Verifies that <see cref="Vector2D.Normalize"/> normalizes normal vectors to unit length.
    /// </summary>
    [Fact]
    public void Normalize_NormalVector_ShouldReturnUnitLength()
    {
        var vector = new Vector2D(3.0, 4.0);
        Vector2D normalized = vector.Normalize();

        Assert.Equal(0.6, normalized.X, 9);
        Assert.Equal(0.8, normalized.Y, 9);
        Assert.Equal(1.0, normalized.Length, 9);
    }

    /// <summary>
    /// Verifies that <see cref="Vector2D.Normalize"/> returns <see cref="Vector2D.Zero"/> when called on a zero vector.
    /// </summary>
    [Fact]
    public void Normalize_ZeroVector_ShouldReturnZero()
    {
        Vector2D normalized = Vector2D.Zero.Normalize();

        Assert.Equal(Vector2D.Zero, normalized);
    }

    /// <summary>
    /// Verifies that <see cref="Vector2D.Normalize"/> returns <see cref="Vector2D.Zero"/> on sub-epsilon vectors.
    /// </summary>
    [Fact]
    public void Normalize_SubEpsilonVector_ShouldReturnZero()
    {
        var tiny = new Vector2D(1e-13, 0.0);
        Vector2D normalized = tiny.Normalize();

        Assert.Equal(Vector2D.Zero, normalized);
    }

    /// <summary>
    /// Verifies that <see cref="Vector2D.Normalize"/> returns <see cref="Vector2D.Zero"/> on non-finite components.
    /// </summary>
    [Theory]
    [InlineData(double.NaN, 1.0)]
    [InlineData(1.0, double.NaN)]
    [InlineData(double.PositiveInfinity, 0.0)]
    [InlineData(0.0, double.NegativeInfinity)]
    public void Normalize_NonFiniteComponents_ShouldReturnZero(double x, double y)
    {
        var nonFinite = new Vector2D(x, y);
        Vector2D normalized = nonFinite.Normalize();

        Assert.Equal(Vector2D.Zero, normalized);
    }

    /// <summary>
    /// Verifies that the addition operator sums vector components.
    /// </summary>
    [Fact]
    public void AdditionOperator_ShouldAddComponents()
    {
        var a = new Vector2D(1.5, 2.5);
        var b = new Vector2D(3.0, 4.0);
        Vector2D sum = a + b;

        Assert.Equal(4.5, sum.X);
        Assert.Equal(6.5, sum.Y);
    }

    /// <summary>
    /// Verifies that the subtraction operator subtracts vector components.
    /// </summary>
    [Fact]
    public void SubtractionOperator_ShouldSubtractComponents()
    {
        var a = new Vector2D(5.0, 7.0);
        var b = new Vector2D(2.0, 3.0);
        Vector2D diff = a - b;

        Assert.Equal(3.0, diff.X);
        Assert.Equal(4.0, diff.Y);
    }

    /// <summary>
    /// Verifies that the unary negation operator negates both components.
    /// </summary>
    [Fact]
    public void UnaryNegationOperator_ShouldNegateComponents()
    {
        var vector = new Vector2D(2.5, -3.5);
        Vector2D negated = -vector;

        Assert.Equal(-2.5, negated.X);
        Assert.Equal(3.5, negated.Y);
    }

    /// <summary>
    /// Verifies that the multiplication operator scales vector components in both operand orders.
    /// </summary>
    [Fact]
    public void MultiplicationOperators_ShouldScaleComponents()
    {
        var vector = new Vector2D(2.0, -3.0);
        Vector2D scaled1 = vector * 2.5;
        Vector2D scaled2 = 2.5 * vector;

        Assert.Equal(5.0, scaled1.X);
        Assert.Equal(-7.5, scaled1.Y);
        Assert.Equal(scaled1, scaled2);
    }

    /// <summary>
    /// Verifies that the division operator divides vector components by a scalar divisor.
    /// </summary>
    [Fact]
    public void DivisionOperator_ShouldDivideComponents()
    {
        var vector = new Vector2D(6.0, -8.0);
        Vector2D divided = vector / 2.0;

        Assert.Equal(3.0, divided.X);
        Assert.Equal(-4.0, divided.Y);
    }

    /// <summary>
    /// Verifies record struct equality, inequality, and string representation.
    /// </summary>
    [Fact]
    public void RecordProperties_EqualityAndFormatting_ShouldBehaveCorrectly()
    {
        var a = new Vector2D(1.0, 2.0);
        var b = new Vector2D(1.0, 2.0);
        var c = new Vector2D(2.0, 1.0);

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.NotEqual(a, c);
        Assert.True(a != c);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Contains("1", a.ToString());
    }

    /// <summary>
    /// Verifies that <see cref="Vector2D.DistanceSquared"/> and <see cref="Vector2D.Distance"/> compute Euclidean distances.
    /// </summary>
    [Fact]
    public void DistanceAndDistanceSquared_ShouldComputeEuclideanDistance()
    {
        var a = new Vector2D(1.0, 2.0);
        var b = new Vector2D(4.0, 6.0);

        Assert.Equal(25.0, a.DistanceSquared(b), 12);
        Assert.Equal(5.0, a.Distance(b), 12);
        Assert.Equal(a.Distance(b), b.Distance(a), 12);
        Assert.Equal(0.0, a.DistanceSquared(a));
        Assert.Equal(0.0, a.Distance(a));

        var neg = new Vector2D(-2.0, -2.0);
        Assert.Equal(25.0, a.DistanceSquared(neg), 12);
        Assert.Equal(5.0, a.Distance(neg), 12);
    }
}
