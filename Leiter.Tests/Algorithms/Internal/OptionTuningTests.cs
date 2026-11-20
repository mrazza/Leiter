namespace Leiter.Tests.Algorithms.Internal;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Leiter.Algorithms;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Saliency;
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
    public void PaletteRecompute_ShouldMatchReferenceLabDispersionAndStdDevLWithinTolerance()
    {
        Assert.NotEmpty(WatercolorPalettes.Defaults);
        Assert.All(WatercolorPalettes.Defaults, static palette => Assert.Equal(Illuminant.E, palette.Illuminant));

        double meanDispersion = WatercolorPalettes.Defaults.Average(
            static palette => ImageTuningProfile.ComputeDispersion(palette.Statistics));
        double meanStdDevL = WatercolorPalettes.Defaults.Average(
            static palette => palette.Statistics.StdDevL);

        Assert.Equal(25.14, OptionTuning.ReferenceLabDispersion, 12);
        Assert.Equal(17.696, OptionTuning.ReferenceStdDevL, 12);
        Assert.InRange(Math.Abs(meanDispersion - OptionTuning.ReferenceLabDispersion), 0.0, 0.01);
        Assert.InRange(Math.Abs(meanStdDevL - OptionTuning.ReferenceStdDevL), 0.0, 0.01);
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

    [Fact]
    public void ForProfile_AllProfileTunableOptions_ShouldFollowAdaptationCurves()
    {
        const double refDim = OptionTuning.DefaultReferenceDimension;
        var refSize = new Size(800, 800);

        ImageTuningProfile zeroSizeProfile = ImageTuningProfile.CreateEmpty(new Size(0, 0));
        ImageTuningProfile emptyContentProfile = ImageTuningProfile.CreateEmpty(refSize);
        ImageTuningProfile neutralProfile = CreateSyntheticProfile(refSize);

        // Zero-area empty profiles preserve baselines verbatim across all 9 IProfileTunableOptions.
        Assert.Equal(FxaaOptions.Default, InvokeInterfaceForProfile(in zeroSizeProfile, FxaaOptions.Default, refDim));
        Assert.Equal(EgbiOptions.Default, InvokeInterfaceForProfile(in zeroSizeProfile, EgbiOptions.Default, refDim));
        Assert.Equal(GrabCutOptions.Default, InvokeInterfaceForProfile(in zeroSizeProfile, GrabCutOptions.Default, refDim));
        Assert.Equal(MeanShiftOptions.Default, InvokeInterfaceForProfile(in zeroSizeProfile, MeanShiftOptions.Default, refDim));
        Assert.Equal(SaliencyCutOptions.Default, InvokeInterfaceForProfile(in zeroSizeProfile, SaliencyCutOptions.Default, refDim));
        Assert.Equal(LodAbstractionOptions.Default, InvokeInterfaceForProfile(in zeroSizeProfile, LodAbstractionOptions.Default, refDim));
        Assert.Equal(PiecewiseToneCurveOptions.Default, InvokeInterfaceForProfile(in zeroSizeProfile, PiecewiseToneCurveOptions.Default, refDim));
        Assert.Equal(EdgeDarkeningOptions.Default, InvokeInterfaceForProfile(in zeroSizeProfile, EdgeDarkeningOptions.Default, refDim));
        Assert.Equal(WetInWetOptions.Default, InvokeInterfaceForProfile(in zeroSizeProfile, WetInWetOptions.Default, refDim));

        // Non-zero-sized IsEmpty profiles (and neutral reference profiles) apply size scaling without content adaptation.
        WetInWetOptions expectedWetNeutral = WetInWetOptions.Default with { EllipticalFilter = WetInWetOptions.DefaultEllipticalFilter };
        SaliencyCutOptions expectedSaliencyCutNeutral = SaliencyCutOptions.Default with { GrabCut = SaliencyCutOptions.DefaultGrabCut, Mask = SaliencyCutOptions.DefaultMask };
        Assert.Equal(FxaaOptions.Default, InvokeInterfaceForProfile(in emptyContentProfile, FxaaOptions.Default, refDim));
        Assert.Equal(EgbiOptions.Default, InvokeInterfaceForProfile(in emptyContentProfile, EgbiOptions.Default, refDim));
        Assert.Equal(GrabCutOptions.Default, InvokeInterfaceForProfile(in emptyContentProfile, GrabCutOptions.Default, refDim));
        Assert.Equal(MeanShiftOptions.Default, InvokeInterfaceForProfile(in emptyContentProfile, MeanShiftOptions.Default, refDim));
        Assert.Equal(SaliencyCutOptions.Default, InvokeInterfaceForProfile(in emptyContentProfile, SaliencyCutOptions.Default, refDim));
        Assert.Equal(LodAbstractionOptions.Default, InvokeInterfaceForProfile(in emptyContentProfile, LodAbstractionOptions.Default, refDim));
        Assert.Equal(PiecewiseToneCurveOptions.Default, InvokeInterfaceForProfile(in emptyContentProfile, PiecewiseToneCurveOptions.Default, refDim));
        Assert.Equal(EdgeDarkeningOptions.Default, InvokeInterfaceForProfile(in emptyContentProfile, EdgeDarkeningOptions.Default, refDim));
        Assert.Equal(expectedWetNeutral, InvokeInterfaceForProfile(in emptyContentProfile, WetInWetOptions.Default, refDim));

        Assert.Equal(FxaaOptions.Default, InvokeInterfaceForProfile(in neutralProfile, FxaaOptions.Default, refDim));
        Assert.Equal(EgbiOptions.Default, InvokeInterfaceForProfile(in neutralProfile, EgbiOptions.Default, refDim));
        Assert.Equal(GrabCutOptions.Default, InvokeInterfaceForProfile(in neutralProfile, GrabCutOptions.Default, refDim));
        Assert.Equal(MeanShiftOptions.Default, InvokeInterfaceForProfile(in neutralProfile, MeanShiftOptions.Default, refDim));
        Assert.Equal(expectedSaliencyCutNeutral, InvokeInterfaceForProfile(in neutralProfile, SaliencyCutOptions.Default, refDim));
        Assert.Equal(LodAbstractionOptions.Default, InvokeInterfaceForProfile(in neutralProfile, LodAbstractionOptions.Default, refDim));
        Assert.Equal(PiecewiseToneCurveOptions.Default, InvokeInterfaceForProfile(in neutralProfile, PiecewiseToneCurveOptions.Default, refDim));
        Assert.Equal(EdgeDarkeningOptions.Default, InvokeInterfaceForProfile(in neutralProfile, EdgeDarkeningOptions.Default, refDim));
        Assert.Equal(expectedWetNeutral, InvokeInterfaceForProfile(in neutralProfile, WetInWetOptions.Default, refDim));

        // 1. FxaaOptions: low-key luminance lowers AbsoluteContrastThreshold linearly toward 0.0312; >= 35 is unchanged.
        Assert.Equal(0.0625, FxaaOptions.ForProfile(CreateSyntheticProfile(meanL: 35.0), FxaaOptions.Default, refDim).AbsoluteContrastThreshold, 12);
        Assert.Equal(0.04685, FxaaOptions.ForProfile(CreateSyntheticProfile(meanL: 17.5), FxaaOptions.Default, refDim).AbsoluteContrastThreshold, 12);
        Assert.Equal(0.0312, FxaaOptions.ForProfile(CreateSyntheticProfile(meanL: 0.0), FxaaOptions.Default, refDim).AbsoluteContrastThreshold, 12);
        Assert.Equal(0.01, FxaaOptions.ForProfile(CreateSyntheticProfile(meanL: 0.0), new FxaaOptions(AbsoluteContrastThreshold: 0.01), refDim).AbsoluteContrastThreshold, 12);

        // 2. EgbiOptions: KFactor scales with ColorSpreadFactor; MinRegionSize scales with ClutterFactor (0 stays 0).
        EgbiOptions egbiLow = EgbiOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 0.75, clutterFactor: 0.75), EgbiOptions.Default, refDim);
        EgbiOptions egbiHigh = EgbiOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 1.35, clutterFactor: 1.50), EgbiOptions.Default, refDim);
        Assert.Equal(75.0, egbiLow.KFactor, 12);
        Assert.Equal(135.0, egbiHigh.KFactor, 12);
        Assert.Equal(1.0, egbiLow.Epsilon, 12);
        Assert.Equal(1.0, egbiHigh.Epsilon, 12);
        Assert.Equal(75, egbiLow.MinRegionSize);
        Assert.Equal(150, egbiHigh.MinRegionSize);
        Assert.Equal(0, EgbiOptions.ForProfile(CreateSyntheticProfile(clutterFactor: 1.50), new EgbiOptions(MinRegionSize: 0), refDim).MinRegionSize);

        // 3. GrabCutOptions: LabDispersion adjusts MixtureComponentCount (-1 / 0 / +1, >= 1); clamped ColorSpreadFactor scales SmoothnessWeight inversely by sqrt.
        GrabCutOptions gcLow = GrabCutOptions.ForProfile(CreateSyntheticProfile(labDispersion: 10.0, colorSpreadFactor: 0.50), GrabCutOptions.Default, refDim);
        GrabCutOptions gcLowAtClamp = GrabCutOptions.ForProfile(CreateSyntheticProfile(labDispersion: 10.0, colorSpreadFactor: 0.80), GrabCutOptions.Default, refDim);
        GrabCutOptions gcHigh = GrabCutOptions.ForProfile(CreateSyntheticProfile(labDispersion: 35.0, colorSpreadFactor: 2.00), GrabCutOptions.Default, refDim);
        GrabCutOptions gcHighAtClamp = GrabCutOptions.ForProfile(CreateSyntheticProfile(labDispersion: 35.0, colorSpreadFactor: 1.25), GrabCutOptions.Default, refDim);
        Assert.Equal(4, gcLow.MixtureComponentCount);
        Assert.Equal(6, gcHigh.MixtureComponentCount);
        Assert.Equal(gcLowAtClamp, gcLow);
        Assert.Equal(gcHighAtClamp, gcHigh);
        Assert.Equal(50.0 / Math.Sqrt(0.80), gcLow.SmoothnessWeight, 12);
        Assert.Equal(50.0 / Math.Sqrt(1.25), gcHigh.SmoothnessWeight, 12);
        Assert.Equal(1, GrabCutOptions.ForProfile(CreateSyntheticProfile(labDispersion: 5.0), new GrabCutOptions(MixtureComponentCount: 1), refDim).MixtureComponentCount);

        // 4. MeanShiftOptions: RangeRadius scales with ColorSpreadFactor; MinRegionSize scales with ClutterFactor (0 stays 0).
        MeanShiftOptions msLow = MeanShiftOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 0.75, clutterFactor: 0.75), MeanShiftOptions.Default, refDim);
        MeanShiftOptions msHigh = MeanShiftOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 1.35, clutterFactor: 1.50), MeanShiftOptions.Default, refDim);
        Assert.Equal(5.5 * 0.75, msLow.RangeRadius, 12);
        Assert.Equal(5.5 * 1.35, msHigh.RangeRadius, 12);
        Assert.Equal(38, msLow.MinRegionSize);
        Assert.Equal(75, msHigh.MinRegionSize);
        Assert.Equal(0, MeanShiftOptions.ForProfile(CreateSyntheticProfile(clutterFactor: 1.50), new MeanShiftOptions(MinRegionSize: 0), refDim).MinRegionSize);

        // 5. SaliencyCutOptions: delegates nested GrabCut adaptation via GrabCutOptions.ForProfile and scales TrimapBandRadius.
        ImageTuningProfile largeHighSpread = CreateSyntheticProfile(size: new Size(1600, 1600), labDispersion: 35.0, colorSpreadFactor: 1.25);
        SaliencyCutOptions scTuned = SaliencyCutOptions.ForProfile(in largeHighSpread, SaliencyCutOptions.Default, refDim);
        Assert.Equal(20, scTuned.TrimapBandRadius);
        Assert.Equal(SaliencyCutOptions.DefaultMask, scTuned.Mask);
        Assert.Equal(GrabCutOptions.ForProfile(in largeHighSpread, SaliencyCutOptions.DefaultGrabCut, refDim), scTuned.GrabCut);

        // 6. LodAbstractionOptions: CrossSegmentColorScale scales with ColorSpreadFactor and clamps within [0.15, 0.50] (respecting custom baselines).
        Assert.Equal(0.225, LodAbstractionOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 0.75), LodAbstractionOptions.Default, refDim).CrossSegmentColorScale, 12);
        Assert.Equal(0.405, LodAbstractionOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 1.35), LodAbstractionOptions.Default, refDim).CrossSegmentColorScale, 12);
        Assert.Equal(0.15, LodAbstractionOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 0.20), LodAbstractionOptions.Default, refDim).CrossSegmentColorScale, 12);
        Assert.Equal(0.50, LodAbstractionOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 3.00), LodAbstractionOptions.Default, refDim).CrossSegmentColorScale, 12);
        Assert.Equal(0.05, LodAbstractionOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 0.20), new LodAbstractionOptions(CrossSegmentColorScale: 0.05), refDim).CrossSegmentColorScale, 12);

        // 7. PiecewiseToneCurveOptions: StdDevL widens knees for flat contrast and narrows for high contrast; resolution-invariant.
        PiecewiseToneCurveOptions toneLow = PiecewiseToneCurveOptions.ForProfile(CreateSyntheticProfile(stdDevL: 0.0), PiecewiseToneCurveOptions.Default, refDim);
        PiecewiseToneCurveOptions toneHigh = PiecewiseToneCurveOptions.ForProfile(CreateSyntheticProfile(stdDevL: OptionTuning.ReferenceStdDevL * 2.5), PiecewiseToneCurveOptions.Default, refDim);
        Assert.Equal(0.12, toneLow.ShadowKnee, 12);
        Assert.Equal(0.88, toneLow.HighlightKnee, 12);
        Assert.Equal(0.21, toneHigh.ShadowKnee, 12);
        Assert.Equal(0.79, toneHigh.HighlightKnee, 12);
        Assert.Equal(toneHigh, PiecewiseToneCurveOptions.ForProfile(CreateSyntheticProfile(size: new Size(200, 200), stdDevL: OptionTuning.ReferenceStdDevL * 2.5), PiecewiseToneCurveOptions.Default, refDim));

        // 8. EdgeDarkeningOptions: Intensity divides by clamped ColorSpreadFactor in [0.85, 1.20].
        EdgeDarkeningOptions edgeLow = EdgeDarkeningOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 0.50), EdgeDarkeningOptions.Default, refDim);
        EdgeDarkeningOptions edgeHigh = EdgeDarkeningOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 2.00), EdgeDarkeningOptions.Default, refDim);
        Assert.Equal(1.8 / 0.85, edgeLow.Intensity, 12);
        Assert.Equal(1.8 / 1.20, edgeHigh.Intensity, 12);
        Assert.Equal(edgeLow, EdgeDarkeningOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 0.85), EdgeDarkeningOptions.Default, refDim));
        Assert.Equal(edgeHigh, EdgeDarkeningOptions.ForProfile(CreateSyntheticProfile(colorSpreadFactor: 1.20), EdgeDarkeningOptions.Default, refDim));

        // 9. WetInWetOptions: ColorGradientThreshold scales with ClutterFactor and clamps within [0.005, 0.5].
        Assert.Equal(0.075, WetInWetOptions.ForProfile(CreateSyntheticProfile(clutterFactor: 0.75), WetInWetOptions.Default, refDim).ColorGradientThreshold, 12);
        Assert.Equal(0.15, WetInWetOptions.ForProfile(CreateSyntheticProfile(clutterFactor: 1.50), WetInWetOptions.Default, refDim).ColorGradientThreshold, 12);
        Assert.Equal(0.005, WetInWetOptions.ForProfile(CreateSyntheticProfile(clutterFactor: 0.01), WetInWetOptions.Default, refDim).ColorGradientThreshold, 12);
        Assert.Equal(0.5, WetInWetOptions.ForProfile(CreateSyntheticProfile(clutterFactor: 10.0), WetInWetOptions.Default, refDim).ColorGradientThreshold, 12);
    }

    [Fact]
    public void ImageAnalysis_RgbAndLabE_And_ForAnalysis_ShouldMatchAcrossAllOptions()
    {
        const double refDim = OptionTuning.DefaultReferenceDimension;
        var emptyRgb = new SequentialMatrix<Rgb8>(0, 12);
        Matrix<Lab32> emptyLabE = emptyRgb.ToLab32(RgbColorSpace.SRgb, Illuminant.E);
        ImageAnalysis emptyRgbAnalysis = ImageAnalysis.Analyze(emptyRgb);
        ImageAnalysis emptyLabAnalysis = ImageAnalysis.Analyze(emptyLabE, Illuminant.E);
        Assert.Equal(emptyRgbAnalysis.Profile, emptyLabAnalysis.Profile);

        var medium = CreatePatternRgbImage(128, 96);
        Matrix<Lab32> mediumLabE = medium.ToLab32(RgbColorSpace.SRgb, Illuminant.E);
        ImageAnalysis rgbAnalysis = ImageAnalysis.Analyze(medium);
        ImageAnalysis labAnalysisE = ImageAnalysis.Analyze(mediumLabE, Illuminant.E);

        Assert.Equal(rgbAnalysis.Size, labAnalysisE.Size);
        Assert.Equal(rgbAnalysis.Profile.LabStats.MeanL, labAnalysisE.Profile.LabStats.MeanL, 3);
        Assert.Equal(rgbAnalysis.Profile.LabStats.StdDevL, labAnalysisE.Profile.LabStats.StdDevL, 3);
        Assert.Equal(rgbAnalysis.Profile.LabDispersion, labAnalysisE.Profile.LabDispersion, 3);
        Assert.Equal(rgbAnalysis.Profile.ColorSpreadFactor, labAnalysisE.Profile.ColorSpreadFactor, 4);
        Assert.Equal(rgbAnalysis.Profile.ClutterFactor, labAnalysisE.Profile.ClutterFactor, 6);

        // Every concrete ForAnalysis(ImageAnalysis) overload must match ForImage(Rgb8) for both empty and populated images.
        AssertAllProfileAnalysisParity(emptyRgb, emptyRgbAnalysis, refDim);
        AssertAllProfileAnalysisParity(medium, rgbAnalysis, refDim);

        // Larger-than-canonical (> 256) thumbnail path also matches ForImage <-> ForAnalysis exactly.
        var large = CreatePatternRgbImage(320, 260);
        ImageAnalysis largeAnalysis = ImageAnalysis.Analyze(large);
        AssertAllProfileAnalysisParity(large, largeAnalysis, refDim);

        // TuneForLabImage(Lab32, ..., Illuminant.E) must match TuneForAnalysis(ImageAnalysis.Analyze(labE, Illuminant.E)).
        AssertTuneForLabMatchesAnalysis<FxaaOptions>(mediumLabE, labAnalysisE, refDim);
        AssertTuneForLabMatchesAnalysis<EgbiOptions>(mediumLabE, labAnalysisE, refDim);
        AssertTuneForLabMatchesAnalysis<GrabCutOptions>(mediumLabE, labAnalysisE, refDim);
        AssertTuneForLabMatchesAnalysis<MeanShiftOptions>(mediumLabE, labAnalysisE, refDim);
        AssertTuneForLabMatchesAnalysis<SaliencyCutOptions>(mediumLabE, labAnalysisE, refDim);
        AssertTuneForLabMatchesAnalysis<LodAbstractionOptions>(mediumLabE, labAnalysisE, refDim);
        AssertTuneForLabMatchesAnalysis<PiecewiseToneCurveOptions>(mediumLabE, labAnalysisE, refDim);
        AssertTuneForLabMatchesAnalysis<EdgeDarkeningOptions>(mediumLabE, labAnalysisE, refDim);
        AssertTuneForLabMatchesAnalysis<WetInWetOptions>(mediumLabE, labAnalysisE, refDim);

        // Size-only and pipeline options also match ForSize/ForImage <-> ForAnalysis.
        Assert.Equal(OrientedEllipticalFilterOptions.ForSize(medium.Size), OptionTuning.TuneForSizeAnalysis<OrientedEllipticalFilterOptions>(rgbAnalysis, refDim));
        Assert.Equal(PerlinNoiseOptions.ForSize(medium.Size), OptionTuning.TuneForSizeAnalysis<PerlinNoiseOptions>(rgbAnalysis, refDim));
        Assert.Equal(HandTremorOptions.ForImage(medium), HandTremorOptions.ForAnalysis(rgbAnalysis));
        Assert.Equal(TurbulenceFlowOptions.ForImage(medium), TurbulenceFlowOptions.ForAnalysis(rgbAnalysis));
        Assert.Equal(PaperGranulationOptions.ForImage(medium), PaperGranulationOptions.ForAnalysis(rgbAnalysis));
        Assert.Equal(WatercolorOptions.ForImage(medium), WatercolorOptions.ForAnalysis(rgbAnalysis));
        Assert.Equal(WatercolorOptions.ForImage(large), WatercolorOptions.ForAnalysis(largeAnalysis));
    }

    private static ImageTuningProfile CreateSyntheticProfile(
        Size? size = null,
        double meanL = 50.0,
        double stdDevL = OptionTuning.ReferenceStdDevL,
        double labDispersion = OptionTuning.ReferenceLabDispersion,
        double colorSpreadFactor = 1.0,
        double clutterFactor = 1.0) =>
        new(
            size ?? new Size(800, 800),
            IsEmpty: false,
            new LabColorStatistics(meanL, 0.0, 0.0, stdDevL, 5.0, 5.0),
            labDispersion,
            colorSpreadFactor,
            clutterFactor);

    private static T InvokeInterfaceForProfile<T>(in ImageTuningProfile profile, T baseline, double referenceDimension)
        where T : class, IProfileTunableOptions<T> =>
        T.ForProfile(in profile, baseline, referenceDimension);

    private static void AssertAllProfileAnalysisParity(IReadOnlyMatrix<Rgb8> image, ImageAnalysis analysis, double refDim)
    {
        Assert.Equal(FxaaOptions.ForImage(image, refDim), FxaaOptions.ForAnalysis(analysis, refDim));
        Assert.Equal(EgbiOptions.ForImage(image, refDim), EgbiOptions.ForAnalysis(analysis, refDim));
        Assert.Equal(GrabCutOptions.ForImage(image, refDim), GrabCutOptions.ForAnalysis(analysis, refDim));
        Assert.Equal(MeanShiftOptions.ForImage(image, refDim), MeanShiftOptions.ForAnalysis(analysis, refDim));
        Assert.Equal(SaliencyCutOptions.ForImage(image, refDim), SaliencyCutOptions.ForAnalysis(analysis, refDim));
        Assert.Equal(LodAbstractionOptions.ForImage(image, refDim), LodAbstractionOptions.ForAnalysis(analysis, refDim));
        Assert.Equal(PiecewiseToneCurveOptions.ForImage(image, refDim), PiecewiseToneCurveOptions.ForAnalysis(analysis, refDim));
        Assert.Equal(EdgeDarkeningOptions.ForImage(image, refDim), EdgeDarkeningOptions.ForAnalysis(analysis, refDim));
        Assert.Equal(WetInWetOptions.ForImage(image, refDim), WetInWetOptions.ForAnalysis(analysis, refDim));

        var customFxaa = new FxaaOptions(RelativeContrastThreshold: 0.2, AbsoluteContrastThreshold: 0.08, SubpixelBlendStrength: 0.5, MaxSpanSearchSteps: 12);
        var customEgbi = new EgbiOptions(KFactor: 120.0, Epsilon: 2.0, MinRegionSize: 80);
        var customGrabCut = new GrabCutOptions(Iterations: 3, MixtureComponentCount: 4, SmoothnessWeight: 35.0);
        var customMeanShift = new MeanShiftOptions(SpatialRadius: 10, RangeRadius: 7.0, MinRegionSize: 60);
        var customSaliencyCut = new SaliencyCutOptions(MaxOuterIterations: 3, TrimapBandRadius: 8, GrabCut: customGrabCut);
        var customLod = new LodAbstractionOptions(SalientKernelSize: 3, MinNonSalientKernelSize: 3, MaxNonSalientKernelSize: 7, CrossSegmentColorScale: 0.25);
        var customTone = new PiecewiseToneCurveOptions(MinTone: 0.02, MaxTone: 0.98, ShadowKnee: 0.2, HighlightKnee: 0.8);
        var customEdge = new EdgeDarkeningOptions(Intensity: 2.2, BlurSigma: 1.1);
        var customWet = new WetInWetOptions(ColorGradientThreshold: 0.12, ScatterRadius: 6);

        Assert.Equal(FxaaOptions.ForImage(image, customFxaa, refDim), FxaaOptions.ForAnalysis(analysis, customFxaa, refDim));
        Assert.Equal(EgbiOptions.ForImage(image, customEgbi, refDim), EgbiOptions.ForAnalysis(analysis, customEgbi, refDim));
        Assert.Equal(GrabCutOptions.ForImage(image, customGrabCut, refDim), GrabCutOptions.ForAnalysis(analysis, customGrabCut, refDim));
        Assert.Equal(MeanShiftOptions.ForImage(image, customMeanShift, refDim), MeanShiftOptions.ForAnalysis(analysis, customMeanShift, refDim));
        Assert.Equal(SaliencyCutOptions.ForImage(image, customSaliencyCut, refDim), SaliencyCutOptions.ForAnalysis(analysis, customSaliencyCut, refDim));
        Assert.Equal(LodAbstractionOptions.ForImage(image, customLod, refDim), LodAbstractionOptions.ForAnalysis(analysis, customLod, refDim));
        Assert.Equal(PiecewiseToneCurveOptions.ForImage(image, customTone, refDim), PiecewiseToneCurveOptions.ForAnalysis(analysis, customTone, refDim));
        Assert.Equal(EdgeDarkeningOptions.ForImage(image, customEdge, refDim), EdgeDarkeningOptions.ForAnalysis(analysis, customEdge, refDim));
        Assert.Equal(WetInWetOptions.ForImage(image, customWet, refDim), WetInWetOptions.ForAnalysis(analysis, customWet, refDim));
    }

    private static void AssertTuneForLabMatchesAnalysis<TOptions>(IReadOnlyMatrix<Lab32> labImage, ImageAnalysis labAnalysis, double refDim)
        where TOptions : class, IProfileTunableOptions<TOptions>
    {
        Assert.Equal(
            OptionTuning.TuneForLabImage<TOptions>(labImage, refDim, Illuminant.E),
            OptionTuning.TuneForAnalysis<TOptions>(labAnalysis, refDim));
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
