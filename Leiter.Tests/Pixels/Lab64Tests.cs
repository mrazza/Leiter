namespace Leiter.Tests.Pixels;

using System;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="Lab64"/>.
/// </summary>
public class Lab64Tests : PixelContractTestsBase<Lab64, double>
{
    protected override Lab64 ArithmeticLeft => new(10.0, 20.0, 30.0);

    protected override Lab64 ArithmeticRight => new(2.0, 2.0, 2.0);

    protected override (Lab64 Add, Lab64 Subtract, Lab64 Multiply, Lab64 Divide) ExpectedSelfResults =>
        (new(12.0, 22.0, 32.0), new(8.0, 18.0, 28.0), new(20.0, 40.0, 60.0), new(5.0, 10.0, 15.0));

    protected override (Lab64 Add, Lab64 Subtract, Lab64 Multiply, Lab64 Divide) ExpectedScaledByTwoResults =>
        ExpectedSelfResults;

    protected override Lab64 VectorSample => new(1.0, 2.0, 3.0);

    protected override double ExpectedVectorFirstComponent => 1.0;

    protected override Lab64 EnumeratorSample => new(1.0, 2.0, 3.0);

    protected override double[] ExpectedComponents => [1.0, 2.0, 3.0];

    protected override Func<double, double> ComponentDoubleFunc => static b => b * 2.0;

    protected override Lab64 ExpectedComponentMapped => new(20.0, 40.0, 60.0);

    protected override Lab64 ExpectedColorComponentMapped => new(10.0, 40.0, 60.0);

    protected override Lab64 DistanceFirst => new(1.0, 2.0, 3.0);

    protected override Lab64 DistanceSecond => new(4.0, 2.0, 3.0);

    protected override double ExpectedDistance => 1.7550397986383963;

    protected override Lab64 ExpectedLerpMidpoint => new(6.0, 11.0, 16.0);

    [Fact]
    public void Properties_OperatorsAndWideningConversions_ShouldWork()
    {
        var a = new Lab64(12.5, -4.0, 8.25);
        var b = new Lab64(2.5, 1.5, -0.75);

        Assert.Equal(12.5, a.L);
        Assert.Equal(-4.0, a.A);
        Assert.Equal(8.25, a.B);
        Assert.Equal(0.0, Lab64.Zero.L);
        Assert.Equal(new Vector3D(12.5, -4.0, 8.25), a.ToVector3D());

        Assert.Equal(new Lab64(15.0, -2.5, 7.5), a + b);
        Assert.Equal(new Lab64(10.0, -5.5, 9.0), a - b);
        Assert.Equal(new Lab64(-12.5, 4.0, -8.25), -a);
        Assert.Equal(new Lab64(25.0, -8.0, 16.5), a * 2.0);
        Assert.Equal(new Lab64(25.0, -8.0, 16.5), 2.0 * a);
        Assert.Equal(new Lab64(6.25, -2.0, 4.125), a / 2.0);

        Lab32 narrowed = a.ToLab32();
        Assert.Equal(new Lab32(12.5f, -4.0f, 8.25f), narrowed);
        Assert.Equal(a, narrowed.ToLab64());

        // Double precision retains bits that Lab32 cannot represent.
        var highPrecision = new Lab64(50.0, 0.0, 1e-9);
        Assert.NotEqual(0.0, highPrecision.B);
        Assert.Equal(1e-9, highPrecision.B);
    }

    [Fact]
    public void DistancesAndColorMetrics_ShouldComputeCorrectlyAndMatchLab32()
    {
        var lab32A = new Lab32(50f, 30f, 2f);
        var lab32B = new Lab32(50f, 30f, -10f);
        Lab64 lab64A = lab32A.ToLab64();
        Lab64 lab64B = lab32B.ToLab64();

        Assert.Equal(0.0, lab64A.DeltaE2000(lab64A));
        Assert.Equal(lab32A.DeltaE2000(lab32B), lab64A.DeltaE2000(lab64B), 14);
        Assert.Equal(lab64A.DeltaE2000(lab64B), lab64B.DeltaE2000(lab64A), 12);

        var c1 = new Lab64(50.0, 3.0, 4.0);
        var c2 = new Lab64(53.0, 7.0, 16.0);
        Assert.Equal(0.0, c1.EuclideanDistance(c1), 12);
        Assert.Equal(13.0, c1.EuclideanDistance(c2), 12);
        Assert.Equal(169.0, c1.EuclideanDistanceSquared(c2), 12);
        Assert.Equal(169.0, c1.DistanceSquared(c2), 12);

        Assert.Equal(5.0, c1.GetChroma(), 12);
        Assert.Equal(0.5, c1.GetNormalizedLightness(), 12);
        Assert.Equal(Math.Atan2(4.0, 3.0) * (180.0 / Math.PI), c1.GetHueDegrees(), 12);
        Assert.Equal(0.0, new Lab64(50.0, 0.0, 0.0).GetHueDegrees(), 12);
    }

    [Fact]
    public void PixelConversion_Lab64_PixelAndMatrixOverloads_ShouldWork()
    {
        var neutralGray = new Rgb64(0.5, 0.5, 0.5);
        Lab64 grayD65 = neutralGray.ToLab64(RgbColorSpace.SRgb);
        Lab64 grayE = neutralGray.ToLab64(RgbColorSpace.SRgb, Illuminant.E);
        Assert.True(Math.Abs(grayD65.A) < 1e-4);
        Assert.True(Math.Abs(grayD65.B) < 1e-4);
        Assert.True(Math.Abs(grayE.A) < 1e-4);
        Assert.True(Math.Abs(grayE.B) < 1e-4);

        Rgb64 roundTripRgb64 = grayE.ToRgb64(RgbColorSpace.SRgb, Illuminant.E);
        Assert.Equal(neutralGray.R, roundTripRgb64.R, 6);
        Assert.Equal(neutralGray.G, roundTripRgb64.G, 6);
        Assert.Equal(neutralGray.B, roundTripRgb64.B, 6);

        var srgbPixel = new Rgb8(64, 140, 210);
        Lab64 fromRgb8 = srgbPixel.ToLab64(RgbColorSpace.SRgb);
        Assert.Equal(srgbPixel, fromRgb8.ToRgb8(RgbColorSpace.SRgb));

        Xyz32 xyz = srgbPixel.ToXyz32(RgbColorSpace.SRgb);
        Assert.Equal(srgbPixel, xyz.ToLab64().ToRgb8(RgbColorSpace.SRgb));
        Assert.Equal(xyz.X, xyz.ToLab64(Illuminant.E).ToXyz32(Illuminant.E).X, 4);

        var rgbMatrix = new SequentialMatrix<Rgb8>(2, 2, [new Rgb8(10, 20, 30), new Rgb8(200, 150, 80), new Rgb8(0, 0, 0), new Rgb8(255, 255, 255)]);
        Matrix<Lab64> labMatrix = rgbMatrix.ToLab64(RgbColorSpace.SRgb, Illuminant.E);
        Matrix<Rgb8> rgbBack = labMatrix.ToRgb8(RgbColorSpace.SRgb, Illuminant.E);
        Matrix<Lab32> narrowedMatrix = labMatrix.ToLab32();
        Matrix<Lab64> widenedMatrix = narrowedMatrix.ToLab64();

        for (int i = 0; i < rgbMatrix.Count; i++)
        {
            Assert.Equal(rgbMatrix[i], rgbBack[i]);
            Assert.Equal(labMatrix[i].ToLab32(), narrowedMatrix[i]);
            Assert.Equal(narrowedMatrix[i].ToLab64(), widenedMatrix[i]);
        }

        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Lab32>)null!).ToLab64());
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Lab64>)null!).ToLab32());
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Lab64>)null!).ToRgb8(RgbColorSpace.SRgb));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Lab64>)null!).ToRgb64(RgbColorSpace.SRgb));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Lab64>)null!).ToXyz32());
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Rgb8>)null!).ToLab64(RgbColorSpace.SRgb));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Rgb64>)null!).ToLab64(RgbColorSpace.SRgb));
        Assert.Throws<ArgumentNullException>(() => ((IReadOnlyMatrix<Xyz32>)null!).ToLab64());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Lab64>(0, 0).ToRgb8((RgbColorSpace)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequentialMatrix<Lab64>(0, 0).ToXyz32((Illuminant)999));
    }
}
