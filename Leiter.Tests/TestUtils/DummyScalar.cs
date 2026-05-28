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
    public static DummyScalar Zero => new DummyScalar(0.0);
    public double Val { get; }
    public DummyScalar(double val) => Val = val;
    public double AsDouble() => Val;
    public DummyScalar Add(DummyScalar right) => new(Val + right.Val);
    public DummyScalar Subtract(DummyScalar right) => new(Val - right.Val);
    public DummyScalar Multiply(DummyScalar right) => new(Val * right.Val);
    public DummyScalar Divide(DummyScalar right) => new(Val / right.Val);
    public DummyScalar Add(decimal right) => new(Val + (double)right);
    public DummyScalar Subtract(decimal right) => new(Val - (double)right);
    public DummyScalar Multiply(decimal right) => new(Val * (double)right);
    public DummyScalar Divide(decimal right) => new(Val / (double)right);
    public DummyScalar Add(double right) => new(Val + right);
    public DummyScalar Subtract(double right) => new(Val - right);
    public DummyScalar Multiply(double right) => new(Val * right);
    public DummyScalar Divide(double right) => new(Val / right);
    public Vector<double> ToDoubleVector() => new(Val);
    public static DummyScalar FromDoubleVector(Vector<double> vector) => new(vector[0]);
}
