namespace Leiter.Tests.TestUtils;

using System;
using System.Runtime.Intrinsics;
using Leiter.Core;

/// <summary>
/// A reusable, concrete scalar test implementation of <see cref="IScalar{S}"/>
/// for use across different pixel type test suites.
/// </summary>
public class DummyScalar : IScalar<DummyScalar>
{
    /// <inheritdoc/>
    public static DummyScalar Zero => new DummyScalar(0.0);

    /// <summary>
    /// Gets the underlying double scalar value.
    /// </summary>
    public double Val { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DummyScalar"/> class.
    /// </summary>
    /// <param name="val">The underlying double scalar value.</param>
    public DummyScalar(double val) => Val = val;

    /// <inheritdoc/>
    public double AsDouble() => Val;

    /// <inheritdoc/>
    public DummyScalar Add(DummyScalar right) => new(Val + right.Val);

    /// <inheritdoc/>
    public DummyScalar Subtract(DummyScalar right) => new(Val - right.Val);

    /// <inheritdoc/>
    public DummyScalar Multiply(DummyScalar right) => new(Val * right.Val);

    /// <inheritdoc/>
    public DummyScalar Divide(DummyScalar right) => new(Val / right.Val);

    /// <inheritdoc/>
    public DummyScalar Add(decimal right) => new(Val + (double)right);

    /// <inheritdoc/>
    public DummyScalar Subtract(decimal right) => new(Val - (double)right);

    /// <inheritdoc/>
    public DummyScalar Multiply(decimal right) => new(Val * (double)right);

    /// <inheritdoc/>
    public DummyScalar Divide(decimal right) => new(Val / (double)right);

    /// <inheritdoc/>
    public DummyScalar Add(double right) => new(Val + right);

    /// <inheritdoc/>
    public DummyScalar Subtract(double right) => new(Val - right);

    /// <inheritdoc/>
    public DummyScalar Multiply(double right) => new(Val * right);

    /// <inheritdoc/>
    public DummyScalar Divide(double right) => new(Val / right);

    /// <inheritdoc/>
    public Vector256<double> ToDoubleVector() => Vector256.CreateScalar(Val);

    /// <inheritdoc/>
    public static DummyScalar FromDoubleVector(Vector256<double> vector) => new(vector[0]);
}
