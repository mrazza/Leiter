namespace Leiter.Tests.Algorithms.Internal;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Leiter.Algorithms;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="OptionTuning"/>, <see cref="ImageTuningProfile"/>, and <see cref="ImageAnalysis"/>.
/// </summary>
public class OptionTuningTests
{
    private sealed class InvalidSizeMatrix<T>(int width, int height) : IReadOnlyMatrix<T>
        where T : struct
    {
        public int Width => width;
        public int Height => height;
        public Size Size => new(width, height);
        public int Count => width * height;
        public T this[int index] => throw new NotSupportedException();
        public T this[int x, int y] => throw new NotSupportedException();
        public T this[Coord coord] => throw new NotSupportedException();
        public T GetElement(int index) => throw new NotSupportedException();
        public T GetElement(int x, int y) => throw new NotSupportedException();
        public IEnumerator<T> GetEnumerator() { yield break; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void Constants_ShouldMatchExactPaletteReferenceStatistics()
    {
        Assert.Equal(0.8, OptionTuning.DefaultBaseBlurSigma, 12);
        Assert.Equal(800.0, OptionTuning.DefaultReferenceDimension, 12);
        Assert.All(WatercolorPalettes.Defaults, p => Assert.Equal(Illuminant.E, p.Illuminant));

        double exactMeanStdDevL = WatercolorPalettes.Defaults.Average(p => p.Statistics.StdDevL);
        Assert.Equal(OptionTuning.ReferenceStdDevL, exactMeanStdDevL, 12);
        Assert.Equal(17.696, OptionTuning.ReferenceStdDevL, 12);

        double exactMeanDispersion = WatercolorPalettes.Defaults.Average(p => ImageTuningProfile.ComputeDispersion(p.Statistics));
        Assert.Equal(OptionTuning.ReferenceLabDispersion, exactMeanDispersion, 2);
    }

    [Fact]
    public void GeometricMeanDimension_ShouldComputeSqrtWidthTimesHeight()
    {
        Assert.Equal(0.0, OptionTuning.GeometricMeanDimension(new Size(0, 100)), 12);
        Assert.Equal(256.0, OptionTuning.GeometricMeanDimension(new Size(256, 256)), 12);
        Assert.Equal(600.0, OptionTuning.GeometricMeanDimension(new Size(400, 900)), 12);
        Assert.Equal(50_000.0, OptionTuning.GeometricMeanDimension(new Size(50_000, 50_000)), 6);
    }

    [Fact]
    public void ComputeScales_And_ComputeScalesUnchecked_ShouldScaleCorrectlyAndStayFinite()
    {
        Assert.Equal((1.0, 1.0, 640_000L), OptionTuning.ComputeScales(new Size(800, 800), 800.0));
        Assert.Equal((1.0, 1.0, 640_000L), OptionTuning.ComputeScales(new Size(1000, 640), 800.0));
        Assert.Equal((2.0, 4.0, 2_560_000L), OptionTuning.ComputeScales(new Size(1600, 1600), 800.0));
        Assert.Equal((0.5, 0.25, 160_000L), OptionTuning.ComputeScales(new Size(400, 400), 800.0));

        foreach (Size empty in new[] { new Size(0, 0), new Size(0, 120), new Size(120, 0) })
        {
            Assert.Equal((1.0, 1.0, 0L), OptionTuning.ComputeScales(empty, 800.0));
            Assert.Equal((1.0, 1.0, 0L), OptionTuning.ComputeScalesUnchecked(empty, 800.0));
        }

        // Extreme reference dimensions must remain finite and non-negative.
        foreach (double extremeRef in new[] { 1e-308, 1e-200, 1e200, 1e308 })
        {
            var (lin, area, pixels) = OptionTuning.ComputeScales(new Size(800, 800), extremeRef);
            Assert.Equal(640_000L, pixels);
            Assert.True(double.IsFinite(lin) && lin >= 0.0);
            Assert.True(double.IsFinite(area) && area >= 0.0);
            Assert.Equal((lin, area, pixels), OptionTuning.ComputeScalesUnchecked(new Size(800, 800), extremeRef));
        }

        Assert.Throws<ArgumentOutOfRangeException>("size", () => OptionTuning.ComputeScales(new Size(-1, 100), 800.0));
        Assert.Throws<ArgumentOutOfRangeException>("size", () => OptionTuning.ComputeScales(new Size(100, -1), 800.0));
        Assert.Throws<ArgumentOutOfRangeException>("referenceDimension", () => OptionTuning.ComputeScales(new Size(100, 100), 0.0));
        Assert.Throws<ArgumentOutOfRangeException>("referenceDimension", () => OptionTuning.ComputeScales(new Size(100, 100), -10.0));
        Assert.Throws<ArgumentOutOfRangeException>("referenceDimension", () => OptionTuning.ComputeScales(new Size(100, 100), double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>("referenceDimension", () => OptionTuning.ComputeScales(new Size(100, 100), double.PositiveInfinity));
    }

    [Fact]
    public void ComputeBlurredEdgeAttenuation_ShouldScaleMonotonicallyAndHandleExtremes()
    {
        Assert.Equal(1.0, OptionTuning.ComputeBlurredEdgeAttenuation(1.0), 12);
        Assert.Equal(1.0, OptionTuning.ComputeBlurredEdgeAttenuation(1.0, OptionTuning.DefaultBaseBlurSigma), 12);

        double smallScale = OptionTuning.ComputeBlurredEdgeAttenuation(0.25);
        double largeScale = OptionTuning.ComputeBlurredEdgeAttenuation(4.0);
        Assert.True(smallScale > 1.0);
        Assert.True(largeScale is > 0.0 and < 1.0);

        Assert.Equal(1.0, OptionTuning.ComputeBlurredEdgeAttenuation(double.PositiveInfinity, baseSigma: 0.0), 12);

        double hugeFinite = OptionTuning.ComputeBlurredEdgeAttenuation(1e200);
        Assert.True(double.IsFinite(hugeFinite) && hugeFinite > 0.0);

        double infScale = OptionTuning.ComputeBlurredEdgeAttenuation(double.PositiveInfinity);
        Assert.Equal(0.0, infScale, 12);
    }

    [Fact]
    public void RoundPositiveInt_ShouldRoundAwayFromZeroAndClampOverflowWithoutWrapping()
    {
        Assert.Equal(1, OptionTuning.RoundPositiveInt(0.0));
        Assert.Equal(1, OptionTuning.RoundPositiveInt(0.4));
        Assert.Equal(1, OptionTuning.RoundPositiveInt(-100.0));
        Assert.Equal(2, OptionTuning.RoundPositiveInt(1.5));
        Assert.Equal(3, OptionTuning.RoundPositiveInt(2.5));
        Assert.Equal(5, OptionTuning.RoundPositiveInt(3.2, minValue: 5));

        // Values >= int.MaxValue must clamp to int.MaxValue rather than wrapping negative to minValue.
        Assert.Equal(int.MaxValue, OptionTuning.RoundPositiveInt(int.MaxValue));
        Assert.Equal(int.MaxValue, OptionTuning.RoundPositiveInt((double)int.MaxValue + 1.0));
        Assert.Equal(int.MaxValue, OptionTuning.RoundPositiveInt((double)int.MaxValue + 10_000.0));
        Assert.Equal(int.MaxValue, OptionTuning.RoundPositiveInt(1e15));
        Assert.Equal(int.MaxValue, OptionTuning.RoundPositiveInt(double.PositiveInfinity));

        Assert.Equal(4, OptionTuning.RoundPositiveInt(double.NegativeInfinity, minValue: 4));
        Assert.Equal(4, OptionTuning.RoundPositiveInt(double.NaN, minValue: 4));

        Assert.Throws<ArgumentOutOfRangeException>("minValue", () => OptionTuning.RoundPositiveInt(1.0, minValue: 0));
        Assert.Throws<ArgumentOutOfRangeException>("minValue", () => OptionTuning.RoundPositiveInt(1.0, minValue: -3));
    }

    [Fact]
    public void ClampRegionSize_ShouldClampToTotalPixelsFractionAndHandleOverflow()
    {
        Assert.Equal(100, OptionTuning.ClampRegionSize(100.0, totalPixels: 0L, maxFractionDivisor: 16));
        Assert.Equal(25, OptionTuning.ClampRegionSize(100.0, totalPixels: 400L, maxFractionDivisor: 16));
        Assert.Equal(1, OptionTuning.ClampRegionSize(100.0, totalPixels: 16L, maxFractionDivisor: 32));

        // Large scaled values > int.MaxValue must clamp to maxAllowed (or int.MaxValue), never wrap to 1.
        Assert.Equal(1_000, OptionTuning.ClampRegionSize(1e15, totalPixels: 16_000L, maxFractionDivisor: 16));
        Assert.Equal(int.MaxValue, OptionTuning.ClampRegionSize(1e15, totalPixels: 100_000_000_000L, maxFractionDivisor: 16));
    }

    [Fact]
    public void ClampFrequency_ShouldClampToNyquistBandAndHandleNonFinite()
    {
        Assert.Equal(0.05, OptionTuning.ClampFrequency(0.05), 12);
        Assert.Equal(OptionTuning.MinFrequency, OptionTuning.ClampFrequency(0.0), 12);
        Assert.Equal(OptionTuning.MinFrequency, OptionTuning.ClampFrequency(-1.0), 12);
        Assert.Equal(OptionTuning.MinFrequency, OptionTuning.ClampFrequency(double.NegativeInfinity), 12);
        Assert.Equal(OptionTuning.MinFrequency, OptionTuning.ClampFrequency(double.NaN), 12);
        Assert.Equal(OptionTuning.MaxNyquistFrequency, OptionTuning.ClampFrequency(1.0), 12);
        Assert.Equal(OptionTuning.MaxNyquistFrequency, OptionTuning.ClampFrequency(double.PositiveInfinity), 12);
    }

    [Fact]
    public void ScaleFiniteHelpers_And_ClampFinite_ShouldPreventOverflowAndHandleNaN()
    {
        Assert.Equal(5.0, OptionTuning.ScaleFinitePositive(5.0, 0.25), 12);
        Assert.Equal(0.25, OptionTuning.ScaleFinitePositive(0.01, 0.25), 12);
        Assert.Equal(0.25, OptionTuning.ScaleFinitePositive(-5.0, 0.25), 12);
        Assert.Equal(0.25, OptionTuning.ScaleFinitePositive(double.NegativeInfinity, 0.25), 12);
        Assert.Equal(0.25, OptionTuning.ScaleFinitePositive(double.NaN, 0.25), 12);
        Assert.Equal(double.MaxValue, OptionTuning.ScaleFinitePositive(double.PositiveInfinity, 0.25));
        Assert.Equal(10.0, OptionTuning.ScaleFinitePositive(25.0, 0.25, 10.0), 12);

        Assert.Throws<ArgumentOutOfRangeException>("minValue", () => OptionTuning.ScaleFinitePositive(1.0, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>("minValue", () => OptionTuning.ScaleFinitePositive(1.0, -1.0));
        Assert.Throws<ArgumentOutOfRangeException>("minValue", () => OptionTuning.ScaleFinitePositive(1.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>("maxValue", () => OptionTuning.ScaleFinitePositive(1.0, 5.0, 2.0));
        Assert.Throws<ArgumentOutOfRangeException>("maxValue", () => OptionTuning.ScaleFinitePositive(1.0, 1.0, double.PositiveInfinity));

        Assert.Equal(0.0, OptionTuning.ScaleFiniteNonNegative(0.0), 12);
        Assert.Equal(0.0, OptionTuning.ScaleFiniteNonNegative(-3.0), 12);
        Assert.Equal(0.0, OptionTuning.ScaleFiniteNonNegative(double.NegativeInfinity), 12);
        Assert.Equal(0.0, OptionTuning.ScaleFiniteNonNegative(double.NaN), 12);
        Assert.Equal(42.5, OptionTuning.ScaleFiniteNonNegative(42.5), 12);
        Assert.Equal(double.MaxValue, OptionTuning.ScaleFiniteNonNegative(double.PositiveInfinity));
        Assert.Equal(10.0, OptionTuning.ScaleFiniteNonNegative(double.PositiveInfinity, 10.0), 12);

        Assert.Throws<ArgumentOutOfRangeException>("maxValue", () => OptionTuning.ScaleFiniteNonNegative(1.0, -0.1));
        Assert.Throws<ArgumentOutOfRangeException>("maxValue", () => OptionTuning.ScaleFiniteNonNegative(1.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>("maxValue", () => OptionTuning.ScaleFiniteNonNegative(1.0, double.PositiveInfinity));

        Assert.Equal(-2.0, OptionTuning.ClampFinite(-2.0, -5.0, 5.0), 12);
        Assert.Equal(-5.0, OptionTuning.ClampFinite(-10.0, -5.0, 5.0), 12);
        Assert.Equal(-5.0, OptionTuning.ClampFinite(double.NaN, -5.0, 5.0), 12);
        Assert.Equal(5.0, OptionTuning.ClampFinite(double.PositiveInfinity, -5.0, 5.0), 12);
        Assert.Throws<ArgumentOutOfRangeException>("maxValue", () => OptionTuning.ClampFinite(0.0, 5.0, 4.0));
    }

    [Fact]
    public void ImageTuningProfile_Analyze_ShouldHandleEmptyLargeTinyAndIlluminants()
    {
        var emptyRgb = new SequentialMatrix<Rgb8>(0, 12);
        ImageTuningProfile emptyProfile = ImageTuningProfile.Analyze(emptyRgb);
        Assert.True(emptyProfile.IsEmpty);
        Assert.Equal(emptyRgb.Size, emptyProfile.ImageSize);
        Assert.Equal(1.0, emptyProfile.ColorSpreadFactor, 12);
        Assert.Equal(1.0, emptyProfile.ClutterFactor, 12);
        Assert.Equal(OptionTuning.ReferenceLabDispersion, emptyProfile.LabDispersion, 12);

        var emptyLab = new SequentialMatrix<Lab32>(12, 0);
        ImageTuningProfile emptyLabProfile = ImageTuningProfile.Analyze(emptyLab, Illuminant.E);
        Assert.True(emptyLabProfile.IsEmpty);
        Assert.Equal(emptyLab.Size, emptyLabProfile.ImageSize);

        // <= 16px geometric mean holds ClutterFactor neutrally at 1.0.
        var tiny = CreatePatternRgbImage(16, 16);
        ImageTuningProfile tinyProfile = ImageTuningProfile.Analyze(tiny);
        Assert.False(tinyProfile.IsEmpty);
        Assert.Equal(1.0, tinyProfile.ClutterFactor, 12);
        Assert.InRange(tinyProfile.ColorSpreadFactor, ImageTuningProfile.MinColorSpreadFactor, ImageTuningProfile.MaxColorSpreadFactor);

        // > 256px exercises canonical thumbnail downsampling.
        var large = CreatePatternRgbImage(320, 320);
        ImageTuningProfile largeProfile = ImageTuningProfile.Analyze(large);
        Assert.False(largeProfile.IsEmpty);
        Assert.Equal(large.Size, largeProfile.ImageSize);
        Assert.InRange(largeProfile.ColorSpreadFactor, ImageTuningProfile.MinColorSpreadFactor, ImageTuningProfile.MaxColorSpreadFactor);
        Assert.InRange(largeProfile.ClutterFactor, ImageTuningProfile.MinClutterFactor, ImageTuningProfile.MaxClutterFactor);

        // Lab32 Analyze with explicit illuminant must match RGB clutter/stats on direct conversion without white-point mismatch.
        var medium = CreatePatternRgbImage(64, 64);
        ImageTuningProfile rgbMediumProfile = ImageTuningProfile.Analyze(medium);
        Matrix<Lab32> labMediumE = medium.ToLab32(RgbColorSpace.SRgb, Illuminant.E);
        ImageTuningProfile labMediumProfileE = ImageTuningProfile.Analyze(labMediumE, Illuminant.E);
        Assert.Equal(rgbMediumProfile.LabStats.MeanL, labMediumProfileE.LabStats.MeanL, 6);
        Assert.Equal(rgbMediumProfile.ColorSpreadFactor, labMediumProfileE.ColorSpreadFactor, 6);
        Assert.Equal(rgbMediumProfile.ClutterFactor, labMediumProfileE.ClutterFactor, 6);

        Matrix<Lab32> labMediumD65 = medium.ToLab32(RgbColorSpace.SRgb, Illuminant.D65);
        ImageTuningProfile labMediumProfileD65 = ImageTuningProfile.Analyze(labMediumD65);
        Assert.Equal(rgbMediumProfile.ClutterFactor, labMediumProfileD65.ClutterFactor, 3);

        // ComputeDispersion uses Vector3D norm.
        Assert.Equal(13.0, ImageTuningProfile.ComputeDispersion(new LabColorStatistics(0, 0, 0, 3.0, 4.0, 12.0)), 12);

        // WithTransferredStatistics interpolates LabStats while preserving ClutterFactor.
        var targetStats = new LabColorStatistics(80.0, 10.0, -5.0, 20.0, 12.0, 15.0);
        ImageTuningProfile unchanged = rgbMediumProfile.WithTransferredStatistics(targetStats, 0.0);
        Assert.Equal(rgbMediumProfile, unchanged);
        ImageTuningProfile transferred = rgbMediumProfile.WithTransferredStatistics(targetStats, 1.0);
        Assert.Equal(targetStats, transferred.LabStats);
        Assert.Equal(rgbMediumProfile.ClutterFactor, transferred.ClutterFactor, 12);
        Assert.Equal(emptyProfile, emptyProfile.WithTransferredStatistics(targetStats, 1.0));

        // Validation guards.
        Assert.Throws<ArgumentNullException>(() => ImageTuningProfile.Analyze((IReadOnlyMatrix<Rgb8>)null!));
        Assert.Throws<ArgumentNullException>(() => ImageTuningProfile.Analyze((IReadOnlyMatrix<Lab32>)null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageTuningProfile.Analyze(new InvalidSizeMatrix<Rgb8>(-1, 8)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageTuningProfile.Analyze(new InvalidSizeMatrix<Lab32>(8, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ImageTuningProfile.Analyze(emptyLab, (Illuminant)999));
    }

    [Fact]
    public void ImageAnalysis_And_TuneForAnalysis_ShouldMatchDirectImageTuningAndValidateInputs()
    {
        var rgb = CreatePatternRgbImage(128, 96);
        ImageAnalysis rgbAnalysis = ImageAnalysis.Analyze(rgb);
        Assert.Equal(rgb.Size, rgbAnalysis.Size);
        Assert.Equal(ImageTuningProfile.Analyze(rgb), rgbAnalysis.Profile);

        Assert.Equal(
            OptionTuning.TuneForImage<EgbiOptions>(rgb, OptionTuning.DefaultReferenceDimension),
            OptionTuning.TuneForAnalysis<EgbiOptions>(rgbAnalysis, OptionTuning.DefaultReferenceDimension));
        Assert.Equal(
            OptionTuning.TuneForImage<PiecewiseToneCurveOptions>(rgb, OptionTuning.DefaultReferenceDimension),
            OptionTuning.TuneForAnalysis<PiecewiseToneCurveOptions>(rgbAnalysis, OptionTuning.DefaultReferenceDimension));

        var customNoise = new PerlinNoiseOptions(Seed: 42, Frequency: 0.1, Octaves: 3);
        Assert.Equal(
            PerlinNoiseOptions.ForSize(rgb.Size, customNoise, OptionTuning.DefaultReferenceDimension),
            OptionTuning.TuneForSizeAnalysis(rgbAnalysis, customNoise, OptionTuning.DefaultReferenceDimension));
        Assert.Equal(
            PerlinNoiseOptions.ForSize(rgb.Size, OptionTuning.DefaultReferenceDimension),
            OptionTuning.TuneForSizeAnalysis<PerlinNoiseOptions>(rgbAnalysis, OptionTuning.DefaultReferenceDimension));

        Matrix<Lab32> labE = rgb.ToLab32(RgbColorSpace.SRgb, Illuminant.E);
        ImageAnalysis labAnalysis = ImageAnalysis.Analyze(labE, Illuminant.E);
        Assert.Equal(labE.Size, labAnalysis.Size);
        Assert.Equal(ImageTuningProfile.Analyze(labE, Illuminant.E), labAnalysis.Profile);
        Assert.Equal(
            OptionTuning.TuneForLabImage<EgbiOptions>(labE, OptionTuning.DefaultReferenceDimension, Illuminant.E),
            OptionTuning.TuneForAnalysis<EgbiOptions>(labAnalysis, OptionTuning.DefaultReferenceDimension));

        Assert.Throws<ArgumentNullException>("image", () => ImageAnalysis.Analyze((IReadOnlyMatrix<Rgb8>)null!));
        Assert.Throws<ArgumentNullException>("labImage", () => ImageAnalysis.Analyze((IReadOnlyMatrix<Lab32>)null!));
        Assert.Throws<ArgumentOutOfRangeException>("image", () => ImageAnalysis.Analyze(new InvalidSizeMatrix<Rgb8>(-1, 8)));
        Assert.Throws<ArgumentOutOfRangeException>("labImage", () => ImageAnalysis.Analyze(new InvalidSizeMatrix<Lab32>(8, -1)));
        Assert.Throws<ArgumentOutOfRangeException>("illuminant", () => ImageAnalysis.Analyze(labE, (Illuminant)999));

        Assert.Throws<ArgumentNullException>("analysis", () => OptionTuning.TuneForAnalysis<EgbiOptions>(null!, 800.0));
        Assert.Throws<ArgumentNullException>("baseline", () => OptionTuning.TuneForAnalysis<EgbiOptions>(rgbAnalysis, null!, 800.0));
        Assert.Throws<ArgumentOutOfRangeException>("referenceDimension", () => OptionTuning.TuneForAnalysis<EgbiOptions>(rgbAnalysis, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => OptionTuning.TuneForAnalysis(rgbAnalysis, new EgbiOptions(KFactor: -1.0), 800.0));

        Assert.Throws<ArgumentNullException>("analysis", () => OptionTuning.TuneForSizeAnalysis<PerlinNoiseOptions>(null!, 800.0));
        Assert.Throws<ArgumentNullException>("baseline", () => OptionTuning.TuneForSizeAnalysis<PerlinNoiseOptions>(rgbAnalysis, null!, 800.0));
        Assert.Throws<ArgumentOutOfRangeException>("referenceDimension", () => OptionTuning.TuneForSizeAnalysis<PerlinNoiseOptions>(rgbAnalysis, -5.0));
    }

    private static SequentialMatrix<Rgb8> CreatePatternRgbImage(int width, int height)
    {
        var img = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte r = (byte)(((x + y) & 1) == 0 ? 30 : 225);
                byte g = (byte)((x * 255) / Math.Max(1, width - 1));
                byte b = (byte)((y * 255) / Math.Max(1, height - 1));
                img[x, y] = new Rgb8(r, g, b);
            }
        }

        return img;
    }
}
