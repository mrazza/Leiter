namespace Leiter.Tests.TestUtils;

using System;
using System.Numerics;
using Leiter.Core;

/// <summary>
/// A reusable, concrete scalar test implementation of <see cref="IScalar{S}"/>
/// for use across different pixel type test suites.
/// </summary>
public class DummyScalar : IScalar<DummyScalar>
{
    /// <summary>
    /// Gets the zero property.
    /// </summary>
    public static DummyScalar Zero => new DummyScalar(0.0);
    /// <summary>
    /// Gets the val property.
    /// </summary>
    public double Val { get; }
    /// <summary>
    /// Initializes a new instance of the <see cref="DummyScalar" /> class.
    /// </summary>
    public DummyScalar(double val) => Val = val;
    /// <summary>
    /// Executes the as double operation.
    /// </summary>
    public double AsDouble() => Val;
    /// <summary>
    /// Executes the add operation.
    /// </summary>
    public DummyScalar Add(DummyScalar right) => new(Val + right.Val);
    /// <summary>
    /// Executes the subtract operation.
    /// </summary>
    public DummyScalar Subtract(DummyScalar right) => new(Val - right.Val);
    /// <summary>
    /// Executes the multiply operation.
    /// </summary>
    public DummyScalar Multiply(DummyScalar right) => new(Val * right.Val);
    /// <summary>
    /// Executes the divide operation.
    /// </summary>
    public DummyScalar Divide(DummyScalar right) => new(Val / right.Val);
    /// <summary>
    /// Executes the add operation.
    /// </summary>
    public DummyScalar Add(decimal right) => new(Val + (double)right);
    /// <summary>
    /// Executes the subtract operation.
    /// </summary>
    public DummyScalar Subtract(decimal right) => new(Val - (double)right);
    /// <summary>
    /// Executes the multiply operation.
    /// </summary>
    public DummyScalar Multiply(decimal right) => new(Val * (double)right);
    /// <summary>
    /// Executes the divide operation.
    /// </summary>
    public DummyScalar Divide(decimal right) => new(Val / (double)right);
    /// <summary>
    /// Executes the add operation.
    /// </summary>
    public DummyScalar Add(double right) => new(Val + right);
    /// <summary>
    /// Executes the subtract operation.
    /// </summary>
    public DummyScalar Subtract(double right) => new(Val - right);
    /// <summary>
    /// Executes the multiply operation.
    /// </summary>
    public DummyScalar Multiply(double right) => new(Val * right);
    /// <summary>
    /// Executes the divide operation.
    /// </summary>
    public DummyScalar Divide(double right) => new(Val / right);
    /// <summary>
    /// Executes the to double vector operation.
    /// </summary>
    public Vector<double> ToDoubleVector() => new(Val);
    /// <summary>
    /// Executes the from double vector operation.
    /// </summary>
    public static DummyScalar FromDoubleVector(Vector<double> vector) => new(vector[0]);
}
