namespace Leiter.Tests.Pixels;

using System;
using System.Runtime.Intrinsics;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Rgb8" />.
/// </summary>
public class Rgb8Tests : PixelContractTestsBase<Rgb8, byte>
{
    protected override Rgb8 ArithmeticLeft => new(10, 20, 30);

    protected override Rgb8 ArithmeticRight => new(5, 10, 15);

    protected override (Rgb8 Add, Rgb8 Subtract, Rgb8 Multiply, Rgb8 Divide) ExpectedSelfResults =>
        (new(15, 30, 45), new(5, 10, 15), new(50, 200, 255), new(2, 2, 2));

    protected override (Rgb8 Add, Rgb8 Subtract, Rgb8 Multiply, Rgb8 Divide) ExpectedScaledByTwoResults =>
        (new(12, 22, 32), new(8, 18, 28), new(20, 40, 60), new(5, 10, 15));

    protected override Rgb8 VectorSample => new(255, 255, 255);

    protected override double ExpectedVectorFirstComponent => 1.0;

    protected override Rgb8 EnumeratorSample => new(10, 20, 30);

    protected override byte[] ExpectedComponents => [10, 20, 30];

    protected override Func<byte, byte> ComponentDoubleFunc => static b => (byte)(b * 2);

    protected override Rgb8 ExpectedComponentMapped => new(20, 40, 60);

    protected override Rgb8 ExpectedColorComponentMapped => new(20, 40, 60);

    protected override Rgb8 DistanceFirst => new(10, 20, 30);

    protected override Rgb8 DistanceSecond => new(13, 24, 30);

    protected override double ExpectedDistance => 5.0;

    protected override Rgb8 ExpectedLerpMidpoint => new(8, 15, 23);

    [Fact]
    public void Properties_ShouldWork()
    {
        var p = new Rgb8(10, 20, 30);
        Assert.Equal(10, p.R);
        Assert.Equal(20, p.G);
        Assert.Equal(30, p.B);
        Assert.Equal(0, Rgb8.Zero.R);
    }

    /// <summary>
    /// Verifies that arithmetic and vector conversions clamp out-of-range channel values to the byte range.
    /// </summary>
    [Fact]
    public void Operations_OverflowAndUnderflow_ShouldClampToByteRange()
    {
        var p = new Rgb8(10, 100, 240);

        // Rgb8 pixel operations
        Assert.Equal(new Rgb8(255, 110, 255), p.Add(new Rgb8(250, 10, 50)));
        Assert.Equal(new Rgb8(0, 50, 200), p.Subtract(new Rgb8(50, 50, 40)));
        Assert.Equal(new Rgb8(20, 255, 255), p.Multiply(new Rgb8(2, 3, 2)));
        Assert.Throws<DivideByZeroException>(() => p.Divide(new Rgb8(1, 0, 1)));

        // decimal operations
        Assert.Equal(new Rgb8(255, 255, 255), p.Add(300.0m));
        Assert.Equal(new Rgb8(0, 0, 90), p.Add(-150.0m));
        Assert.Equal(new Rgb8(0, 0, 90), p.Subtract(150.0m));
        Assert.Equal(new Rgb8(255, 255, 255), p.Subtract(-300.0m));
        Assert.Equal(new Rgb8(30, 255, 255), p.Multiply(3.0m));
        Assert.Equal(new Rgb8(0, 0, 0), p.Multiply(-1.0m));
        Assert.Equal(new Rgb8(100, 255, 255), p.Divide(0.1m));
        Assert.Equal(new Rgb8(0, 0, 0), p.Divide(-2.0m));

        // double operations
        Assert.Equal(new Rgb8(255, 255, 255), p.Add(300.0));
        Assert.Equal(new Rgb8(0, 0, 90), p.Add(-150.0));
        Assert.Equal(new Rgb8(0, 0, 90), p.Subtract(150.0));
        Assert.Equal(new Rgb8(255, 255, 255), p.Subtract(-300.0));
        Assert.Equal(new Rgb8(30, 255, 255), p.Multiply(3.0));
        Assert.Equal(new Rgb8(0, 0, 0), p.Multiply(-1.0));
        Assert.Equal(new Rgb8(100, 255, 255), p.Divide(0.1));
        Assert.Equal(new Rgb8(0, 0, 0), p.Divide(-2.0));
        Assert.Equal(new Rgb8(0, 0, 0), p.Add(double.NaN));

        // IScalar operations (negative and > 255)
        var negativeScalar = new DummyScalar(-50.0);
        var largeScalar = new DummyScalar(300.0);
        var smallScalar = new DummyScalar(0.5);

        Assert.Equal(new Rgb8(0, 50, 190), p.Add(negativeScalar));
        Assert.Equal(new Rgb8(60, 150, 255), p.Subtract(negativeScalar));
        Assert.Equal(new Rgb8(0, 0, 0), p.Multiply(negativeScalar));
        Assert.Equal(new Rgb8(0, 0, 0), p.Divide(negativeScalar));

        Assert.Equal(new Rgb8(255, 255, 255), p.Add(largeScalar));
        Assert.Equal(new Rgb8(0, 0, 0), p.Subtract(largeScalar));
        Assert.Equal(new Rgb8(255, 255, 255), p.Multiply(largeScalar));
        Assert.Equal(new Rgb8(0, 0, 1), p.Divide(largeScalar));
        Assert.Equal(new Rgb8(20, 200, 255), p.Divide(smallScalar));

        // Double and IScalar fractional operations round away from zero and clamp
        Assert.Equal(new Rgb8(11, 101, 241), p.Add(0.5));
        Assert.Equal(new Rgb8(10, 100, 240), p.Subtract(0.5));
        Assert.Equal(new Rgb8(9, 99, 239), p.Subtract(0.6));
        Assert.Equal(new Rgb8(1, 2, 3), new Rgb8(1, 3, 5).Multiply(0.5));
        Assert.Equal(new Rgb8(1, 2, 3), new Rgb8(1, 3, 5).Divide(2.0));
        Assert.Equal(new Rgb8(1, 2, 3), new Rgb8(1, 3, 5).Multiply(smallScalar));
        Assert.Equal(new Rgb8(1, 2, 3), new Rgb8(1, 3, 5).Divide(new DummyScalar(2.0)));

        // Out-of-range FromDoubleVector
        Assert.Equal(new Rgb8(0, 255, 0), Rgb8.FromDoubleVector(Vector256.Create(-0.5, 1.5, double.NaN, 0.0)));
    }

    [Fact]
    public void GetHashCode_AndDistanceSquared_ShouldWork()
    {
        var p1 = new Rgb8(10, 20, 30);
        var p2 = new Rgb8(13, 24, 30);

        Assert.Equal(10 << 16 | 20 << 8 | 30, p1.GetHashCode());
        Assert.Equal(25.0, p1.DistanceSquared(p2));
    }

    [Fact]
    public void ClampRoundedByte_FromNormalized_AndFromDoubleVector_ShouldRoundAwayFromZeroAndClamp()
    {
        Assert.Equal((byte)0, Rgb8.ClampRoundedByte(double.NaN));
        Assert.Equal((byte)0, Rgb8.ClampRoundedByte(double.NegativeInfinity));
        Assert.Equal((byte)0, Rgb8.ClampRoundedByte(-5.0));
        Assert.Equal((byte)128, Rgb8.ClampRoundedByte(127.5));
        Assert.Equal((byte)255, Rgb8.ClampRoundedByte(255.0));
        Assert.Equal((byte)255, Rgb8.ClampRoundedByte(double.PositiveInfinity));

        Assert.Equal(new Rgb8(0, 128, 255), Rgb8.FromNormalized(-0.1, 0.5, 1.2));

        Assert.Equal(new Rgb8(128, 255, 0), Rgb8.FromDoubleVector(Vector256.Create(0.5, double.PositiveInfinity, double.NegativeInfinity, 0.0)));
    }

    [Fact]
    public void ScalarOperableDefaultMethods_ShouldDelegateToDoubleOperations()
    {
        IScalarOperable<MinimalScalarOperable> value = new MinimalScalarOperable(10.0);
        var scalar = new DummyScalar(2.0);

        Assert.Equal(12.0, value.Add(scalar).Value);
        Assert.Equal(8.0, value.Subtract(scalar).Value);
        Assert.Equal(20.0, value.Multiply(scalar).Value);
        Assert.Equal(5.0, value.Divide(scalar).Value);
    }

    [Fact]
    public void Lerp_ShouldInterpolateRoundAwayFromZeroAndClamp()
    {
        var from = new Rgb8(10, 200, 50);
        var to = new Rgb8(11, 100, 150);

        Assert.Equal(from, Rgb8.Lerp(from, to, 0.0));
        Assert.Equal(to, Rgb8.Lerp(from, to, 1.0));
        Assert.Equal(new Rgb8(11, 150, 100), Rgb8.Lerp(from, to, 0.5));
        Assert.Equal(new Rgb8(8, 255, 0), Rgb8.Lerp(from, to, -2.0));
        Assert.Equal(new Rgb8(13, 0, 255), Rgb8.Lerp(from, to, 2.5));
    }

    [Fact]
    public void ToRawVector3D_ShouldReturnUnnormalizedByteChannels()
    {
        var pixel = new Rgb8(12, 128, 255);
        Assert.Equal(new Vector3D(12.0, 128.0, 255.0), pixel.ToRawVector3D());
    }

    private readonly record struct MinimalScalarOperable(double Value) : IScalarOperable<MinimalScalarOperable>
    {
        public MinimalScalarOperable Add(decimal right) => new(Value + (double)right);
        public MinimalScalarOperable Subtract(decimal right) => new(Value - (double)right);
        public MinimalScalarOperable Multiply(decimal right) => new(Value * (double)right);
        public MinimalScalarOperable Divide(decimal right) => new(Value / (double)right);
        public MinimalScalarOperable Add(double right) => new(Value + right);
        public MinimalScalarOperable Subtract(double right) => new(Value - right);
        public MinimalScalarOperable Multiply(double right) => new(Value * right);
        public MinimalScalarOperable Divide(double right) => new(Value / right);
        public Vector256<double> ToDoubleVector() => Vector256.CreateScalar(Value);
        public static MinimalScalarOperable FromDoubleVector(Vector256<double> vector) => new(vector[0]);
    }
}
