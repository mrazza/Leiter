namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Morphology;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Leiter.Tests.Algorithms;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="WatercolorOptions"/>.
/// </summary>
public class WatercolorOptionsTests : SizeTunableOptionsContractTestsBase<WatercolorOptions>
{
    /// <inheritdoc/>
    protected override WatercolorOptions CreateValidCustomOptions() =>
        new(
            TargetPalette: WatercolorPalettes.CoastalCerulean,
            ColorTransferStrength: 0.75,
            Egbi: new EgbiOptions(KFactor: 75.0, Epsilon: 2.0, MinRegionSize: 150, Connectivity: GridConnectivity.FourConnected),
            RegionalContrastSaliency: new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: false, CenterPriorWeight: 4.5),
            SaliencyCut: new SaliencyCutOptions(
                MaxOuterIterations: 3,
                TrimapBandRadius: 14,
                GrabCut: new GrabCutOptions(Iterations: 3, MixtureComponentCount: 4, SmoothnessWeight: 40.0),
                Mask: new SaliencyMaskOptions(
                    Method: SaliencyThresholdMethod.Otsu,
                    FixedThreshold: 0.3,
                    RetentionPolicy: ComponentRetentionPolicy.LargestComponentOnly,
                    MinAreaFraction: 0.02),
                MaxWorkingDimension: 512),
            SaliencyDistanceField: new SaliencyDistanceFieldOptions(SmoothingSigma: 1.5, DefaultSigmaScale: 0.05),
            MeanShift: new MeanShiftOptions(
                SpatialRadius: 12,
                RangeRadius: 8.0,
                MinRegionSize: 80,
                MaxIterations: 15,
                ConvergenceEpsilon: 0.08,
                Connectivity: GridConnectivity.FourConnected),
            LodAbstraction: new LodAbstractionOptions(
                SalientKernelSize: 7,
                MinNonSalientKernelSize: 5,
                MaxNonSalientKernelSize: 11,
                DistanceKernelScale: 12.0,
                DistanceKernelBias: 0.25,
                CrossSegmentColorScale: 0.35,
                SalientOverlapThreshold: 0.6),
            WetInWet: new WetInWetOptions(
                Mode: WetInWetMode.ExcludeSalientBoundaries,
                ColorGradientThreshold: 0.15,
                SalientHueThresholdDegrees: 25.0,
                NonSalientHueThresholdDegrees: 75.0,
                MinIntensityDifference: 0.04,
                SalientOverlapThreshold: 0.6,
                ScatterRadius: 6,
                SeedProbability: 0.6,
                EllipticalFilter: new OrientedEllipticalFilterOptions(MajorRadius: 8.0, MinorRadius: 3.0, SigmaScale: 0.65),
                Seed: 99),
            HandTremor: new HandTremorOptions(
                Amplitude: 4.5,
                SalientAmplitudeScale: 0.5,
                SimilarHueThresholdDegrees: 25.0,
                OverlapDarkening: 0.6,
                PaperColor: new Rgb8(245, 242, 236),
                PrimaryNoise: new PerlinNoiseOptions(Seed: 55, Frequency: 0.02, Octaves: 2),
                SecondaryNoise: new PerlinNoiseOptions(Seed: 66, Frequency: 0.03, Octaves: 2),
                MinContrastingChroma: 0.04,
                PaperGapBias: 0.2,
                PaperGapOpacity: 0.8),
            EdgeDarkening: new EdgeDarkeningOptions(Intensity: 2.4, BlurSigma: 1.2, GradientOperator: GradientOperator.Scharr),
            TurbulenceFlow: new TurbulenceFlowOptions(
                Intensity: 0.5,
                Noise: new PerlinNoiseOptions(Seed: 909, Frequency: 0.025, Octaves: 3, Lacunarity: 2.2, Persistence: 0.45)),
            ToneCurve: new PiecewiseToneCurveOptions(MinTone: 0.02, MaxTone: 0.98, ShadowKnee: 0.2, HighlightKnee: 0.8),
            PaperGranulation: new PaperGranulationOptions(
                DensityStrength: 0.45,
                WarpAmplitude: 0.9,
                GrainNoise: new PerlinNoiseOptions(Seed: 808, Frequency: 0.12, Octaves: 3, Lacunarity: 2.1, Persistence: 0.5)),
            Fxaa: new FxaaOptions(
                RelativeContrastThreshold: 0.2,
                AbsoluteContrastThreshold: 0.08,
                SubpixelBlendStrength: 0.5,
                MaxSpanSearchSteps: 12),
            EnableFxaa: false,
            EgbiBlurSigma: 1.2,
            EnableColorTransfer: false,
            EnableLodAbstraction: false,
            EnableWetInWet: false,
            EnableHandTremor: false,
            EnableEdgeDarkening: false,
            EnableTurbulenceFlow: false,
            EnableToneCurve: false,
            EnablePaperGranulation: false);

    /// <inheritdoc/>
    protected override WatercolorOptions CreateInvalidOptions() =>
        new(ColorTransferStrength: -0.1);
    /// <summary>
    /// Verifies that default pipeline options match expected defaults and pass validation.
    /// </summary>
    [Fact]
    public void Defaults_ShouldHaveExpectedValuesAndPassValidation()
    {
        var options = new WatercolorOptions();

        Assert.Null(options.TargetPalette);
        Assert.Equal(1.0, options.ColorTransferStrength);
        Assert.Equal(WatercolorOptions.DefaultSaliencyEgbi, options.Egbi);
        Assert.Equal(RegionalContrastSaliencyOptions.Default, options.RegionalContrastSaliency);
        Assert.Equal(SaliencyCutOptions.Default, options.SaliencyCut);
        Assert.Equal(SaliencyDistanceFieldOptions.Default, options.SaliencyDistanceField);
        Assert.Equal(MeanShiftOptions.Default, options.MeanShift);
        Assert.Equal(LodAbstractionOptions.Default, options.LodAbstraction);
        Assert.Equal(WetInWetOptions.Default, options.WetInWet);
        Assert.Equal(HandTremorOptions.Default, options.HandTremor);
        Assert.Equal(EdgeDarkeningOptions.Default, options.EdgeDarkening);
        Assert.Equal(TurbulenceFlowOptions.Default, options.TurbulenceFlow);
        Assert.Equal(PiecewiseToneCurveOptions.Default, options.ToneCurve);
        Assert.Equal(PaperGranulationOptions.Default, options.PaperGranulation);
        Assert.Equal(FxaaOptions.Default, options.Fxaa);
        Assert.True(options.EnableFxaa);
        Assert.Equal(0.8, options.EgbiBlurSigma, 6);
        Assert.True(options.EnableColorTransfer);
        Assert.True(options.EnableLodAbstraction);
        Assert.True(options.EnableWetInWet);
        Assert.True(options.EnableHandTremor);
        Assert.True(options.EnableEdgeDarkening);
        Assert.True(options.EnableTurbulenceFlow);
        Assert.True(options.EnableToneCurve);
        Assert.True(options.EnablePaperGranulation);
        Assert.Equal(WatercolorOptions.Default, options);

        var explicitDefaults = new WatercolorOptions(
            Egbi: WatercolorOptions.DefaultSaliencyEgbi,
            RegionalContrastSaliency: RegionalContrastSaliencyOptions.Default,
            SaliencyCut: SaliencyCutOptions.Default,
            SaliencyDistanceField: SaliencyDistanceFieldOptions.Default,
            MeanShift: MeanShiftOptions.Default,
            LodAbstraction: LodAbstractionOptions.Default,
            WetInWet: WetInWetOptions.Default,
            HandTremor: HandTremorOptions.Default,
            EdgeDarkening: EdgeDarkeningOptions.Default,
            TurbulenceFlow: TurbulenceFlowOptions.Default,
            ToneCurve: PiecewiseToneCurveOptions.Default,
            PaperGranulation: PaperGranulationOptions.Default,
            Fxaa: FxaaOptions.Default);
        Assert.Equal(options, explicitDefaults);

        WatercolorOptions resetViaWith = CreateValidCustomOptions() with
        {
            Egbi = null!,
            RegionalContrastSaliency = null!,
            SaliencyCut = null!,
            SaliencyDistanceField = null!,
            MeanShift = null!,
            LodAbstraction = null!,
            WetInWet = null!,
            HandTremor = null!,
            EdgeDarkening = null!,
            TurbulenceFlow = null!,
            ToneCurve = null!,
            PaperGranulation = null!,
            Fxaa = null!
        };
        Assert.Equal(WatercolorOptions.DefaultSaliencyEgbi, resetViaWith.Egbi);
        Assert.Equal(RegionalContrastSaliencyOptions.Default, resetViaWith.RegionalContrastSaliency);
        Assert.Equal(SaliencyCutOptions.Default, resetViaWith.SaliencyCut);
        Assert.Equal(SaliencyDistanceFieldOptions.Default, resetViaWith.SaliencyDistanceField);
        Assert.Equal(MeanShiftOptions.Default, resetViaWith.MeanShift);
        Assert.Equal(LodAbstractionOptions.Default, resetViaWith.LodAbstraction);
        Assert.Equal(WetInWetOptions.Default, resetViaWith.WetInWet);
        Assert.Equal(HandTremorOptions.Default, resetViaWith.HandTremor);
        Assert.Equal(EdgeDarkeningOptions.Default, resetViaWith.EdgeDarkening);
        Assert.Equal(TurbulenceFlowOptions.Default, resetViaWith.TurbulenceFlow);
        Assert.Equal(PiecewiseToneCurveOptions.Default, resetViaWith.ToneCurve);
        Assert.Equal(PaperGranulationOptions.Default, resetViaWith.PaperGranulation);
        Assert.Equal(FxaaOptions.Default, resetViaWith.Fxaa);

        options.Validate();
    }

    /// <summary>
    /// Verifies that invalid <see cref="WatercolorOptions.EgbiBlurSigma"/> values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="sigma">The invalid blur sigma.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidEgbiBlurSigma_ShouldThrow(double sigma)
    {
        var options = new WatercolorOptions(EgbiBlurSigma: sigma);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that invalid <see cref="WatercolorOptions.ColorTransferStrength"/> values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="strength">The invalid color transfer strength.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidUnitIntervalDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidColorTransferStrength_ShouldThrow(double strength)
    {
        var options = new WatercolorOptions(ColorTransferStrength: strength);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that nested option validation failures propagate cleanly through <see cref="WatercolorOptions.Validate"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidNestedOptions_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(Egbi: new EgbiOptions(KFactor: -1.0)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(RegionalContrastSaliency: new RegionalContrastSaliencyOptions(CenterPriorWeight: -0.1)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(SaliencyCut: new SaliencyCutOptions(Mask: new SaliencyMaskOptions(FixedThreshold: 1.5))).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(MeanShift: new MeanShiftOptions(SpatialRadius: 0)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(LodAbstraction: new LodAbstractionOptions(SalientKernelSize: 0)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(WetInWet: new WetInWetOptions(ScatterRadius: 0)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(HandTremor: new HandTremorOptions(Amplitude: -1.0)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(EdgeDarkening: new EdgeDarkeningOptions(Intensity: -0.1)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(TurbulenceFlow: new TurbulenceFlowOptions(Intensity: -0.1)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(ToneCurve: new PiecewiseToneCurveOptions(MinTone: 0.9, MaxTone: 0.1)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(PaperGranulation: new PaperGranulationOptions(WarpAmplitude: -0.5)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(Fxaa: new FxaaOptions(MaxSpanSearchSteps: 0)).Validate());
    }

    /// <summary>
    /// Verifies that valid customized sub-options pass validation.
    /// </summary>
    [Fact]
    public void Validate_ValidNestedOptions_ShouldSucceed()
    {
        var custom = new WatercolorOptions(
            TargetPalette: WatercolorPalettes.Defaults[0],
            ColorTransferStrength: 0.75,
            Egbi: new EgbiOptions(),
            SaliencyCut: new SaliencyCutOptions(),
            MeanShift: new MeanShiftOptions(),
            LodAbstraction: new LodAbstractionOptions(),
            WetInWet: new WetInWetOptions(),
            HandTremor: new HandTremorOptions(),
            EdgeDarkening: new EdgeDarkeningOptions(),
            TurbulenceFlow: new TurbulenceFlowOptions(),
            ToneCurve: new PiecewiseToneCurveOptions(),
            PaperGranulation: new PaperGranulationOptions(),
            Fxaa: new FxaaOptions(),
            EnableFxaa: false);

        custom.Validate();
        Assert.False(custom.EnableFxaa);
        Assert.Equal(WatercolorPalettes.Defaults[0], custom.TargetPalette);
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorOptions.ForSize"/> populates all sub-options by delegating to each stage option's <c>ForSize</c> factory.
    /// </summary>
    [Theory]
    [InlineData(200, 200)]
    [InlineData(800, 800)]
    [InlineData(3200, 2400)]
    public void ForSize_ShouldDelegateToEveryStageOptionsForSize(int width, int height)
    {
        var size = new Size(width, height);
        WatercolorOptions opts = WatercolorOptions.ForSize(size);
        opts.Validate();

        Assert.Null(opts.TargetPalette);
        Assert.Equal(1.0, opts.ColorTransferStrength, 6);
        Assert.Equal(EgbiOptions.ForSize(size, WatercolorOptions.DefaultSaliencyEgbi), opts.Egbi);
        Assert.Equal(SaliencyCutOptions.ForSize(size), opts.SaliencyCut);
        Assert.Equal(MeanShiftOptions.ForSize(size), opts.MeanShift);
        Assert.Equal(LodAbstractionOptions.ForSize(size), opts.LodAbstraction);
        Assert.Equal(WetInWetOptions.ForSize(size), opts.WetInWet);
        Assert.Equal(HandTremorOptions.ForSize(size), opts.HandTremor);
        Assert.Equal(EdgeDarkeningOptions.ForSize(size), opts.EdgeDarkening);
        Assert.Equal(TurbulenceFlowOptions.ForSize(size), opts.TurbulenceFlow);
        Assert.Equal(PiecewiseToneCurveOptions.Default, opts.ToneCurve);
        Assert.Equal(PaperGranulationOptions.ForSize(size), opts.PaperGranulation);
        Assert.Equal(FxaaOptions.ForSize(size), opts.Fxaa);
        Assert.True(opts.EnableFxaa);
        Assert.True(opts.EnableColorTransfer);
        Assert.True(opts.EnableLodAbstraction);
        Assert.True(opts.EnableWetInWet);
        Assert.True(opts.EnableHandTremor);
        Assert.True(opts.EnableEdgeDarkening);
        Assert.True(opts.EnableTurbulenceFlow);
        Assert.True(opts.EnableToneCurve);
        Assert.True(opts.EnablePaperGranulation);
        Assert.Equal(0.8 * Math.Sqrt((double)(width * height) / (800.0 * 800.0)), opts.EgbiBlurSigma, 6);
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorOptions.ForImage"/> selects palettes, moderates low-chroma transfer strength, preserves content scaling across resamplings, and validates inputs.
    /// </summary>
    [Fact]
    public void ForImage_ShouldSelectPaletteModerateLowChromaAndStayStableAcrossResolutions()
    {
        var empty = new SequentialMatrix<Rgb8>(0, 0);
        WatercolorOptions emptyOpts = WatercolorOptions.ForImage(empty);
        emptyOpts.Validate();
        Assert.NotNull(emptyOpts.TargetPalette);

        SequentialMatrix<Rgb8> neutral = TestMatrices.Create(64, 64, static (x, _) =>
        {
            byte v = (byte)(60 + ((x * 2) % 140));
            return new Rgb8(v, v, v);
        });

        WatercolorOptions neutralOpts = WatercolorOptions.ForImage(neutral);
        neutralOpts.Validate();
        Assert.InRange(neutralOpts.ColorTransferStrength, 0.62, 0.779);

        SequentialMatrix<Rgb8> largeScene = TestMatrices.Create(800, 600, static (x, y) =>
        {
            bool fg = x >= 220 && x < 580 && y >= 150 && y < 450;
            int wave = (x / 16 + y / 16) % 3;
            return fg
                ? new Rgb8((byte)(210 + (wave * 10)), (byte)(60 + (wave * 8)), 35)
                : wave == 0
                    ? new Rgb8(45, 110, 175)
                    : new Rgb8(215, 225, 210);
        });

        Matrix<Rgb8> smallScene = largeScene.ResizeBilinear(new Size(200, 150));

        WatercolorOptions smallOpts = WatercolorOptions.ForImage(smallScene);
        WatercolorOptions largeOpts = WatercolorOptions.ForImage(largeScene);
        smallOpts.Validate();
        largeOpts.Validate();

        WatercolorOptions sizeOpts = WatercolorOptions.ForSize(smallScene.Size);
        Assert.Equal(RegionalContrastSaliencyOptions.Default, sizeOpts.RegionalContrastSaliency);
        Assert.Equal(SaliencyDistanceFieldOptions.Default, sizeOpts.SaliencyDistanceField);
        Assert.Equal(RegionalContrastSaliencyOptions.Default, smallOpts.RegionalContrastSaliency);
        Assert.Equal(SaliencyDistanceFieldOptions.Default, smallOpts.SaliencyDistanceField);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(SaliencyDistanceField: new SaliencyDistanceFieldOptions(SmoothingSigma: -1.0)).Validate());

        Assert.Equal(smallOpts.TargetPalette, largeOpts.TargetPalette);
        Assert.InRange(largeOpts.ColorTransferStrength, 0.62, 0.78);
        Assert.Equal(smallOpts.ColorTransferStrength, largeOpts.ColorTransferStrength, 2);
        Assert.Equal(smallOpts.MeanShift!.RangeRadius, largeOpts.MeanShift!.RangeRadius, 1);
        Assert.True(largeOpts.MeanShift.SpatialRadius >= smallOpts.MeanShift.SpatialRadius * 3);
        Assert.True(largeOpts.HandTremor!.Amplitude >= smallOpts.HandTremor!.Amplitude * 3.5);
        Assert.True(largeOpts.EgbiBlurSigma >= smallOpts.EgbiBlurSigma * 3.5);

        // Parameterless ForImage uses the calibrated painterly baseline while preserving WatercolorOptions.Default.
        double largeLinearScale = Math.Sqrt(800.0 * 600.0) / 800.0;
        Assert.Equal(0.42, largeOpts.SaliencyCut.Mask.FixedThreshold, 6);
        Assert.Equal(3, largeOpts.LodAbstraction.SalientKernelSize);
        Assert.Equal(0.16, largeOpts.HandTremor.MinContrastingChroma, 6);
        Assert.Equal(-0.15, largeOpts.HandTremor.PaperGapBias, 6);
        Assert.Equal(0.65, largeOpts.HandTremor.PaperGapOpacity, 6);
        Assert.Equal(0.03, largeOpts.ToneCurve.MinTone, 6);
        Assert.Equal(0.24, largeOpts.PaperGranulation.DensityStrength, 6);
        Assert.Equal(0.115 / largeLinearScale, largeOpts.PaperGranulation.GrainNoise.Frequency, 6);
        Assert.Equal(2, largeOpts.PaperGranulation.GrainNoise.Octaves);

        // Dark images with large lightness shift toward bright palettes attenuate ColorTransferStrength further than bright images.
        SequentialMatrix<Rgb8> darkScene = TestMatrices.Create(64, 64, static (x, y) =>
            new Rgb8((byte)(12 + (x % 18)), (byte)(18 + (y % 18)), (byte)(24 + ((x + y) % 18))));
        SequentialMatrix<Rgb8> brightScene = TestMatrices.Create(64, 64, static (x, y) =>
            new Rgb8((byte)(150 + (x % 30)), (byte)(160 + (y % 30)), (byte)(170 + ((x + y) % 30))));
        WatercolorOptions darkOpts = WatercolorOptions.ForImage(darkScene, candidatePalettes: [WatercolorPalettes.BalancedWash]);
        WatercolorOptions brightOpts = WatercolorOptions.ForImage(brightScene, candidatePalettes: [WatercolorPalettes.BalancedWash]);
        Assert.True(darkOpts.ColorTransferStrength < brightOpts.ColorTransferStrength);

        IReadOnlyList<ColorTransferProfile> customSubset = [WatercolorPalettes.CoastalCerulean];
        WatercolorOptions subsetOpts = WatercolorOptions.ForImage(largeScene, candidatePalettes: customSubset);
        Assert.Same(WatercolorPalettes.CoastalCerulean, subsetOpts.TargetPalette);

        IReadOnlyList<ColorTransferProfile> lowChromaSubset = [WatercolorPalettes.MistyMonochrome];
        WatercolorOptions mutedOpts = WatercolorOptions.ForImage(largeScene, candidatePalettes: lowChromaSubset);
        Assert.InRange(mutedOpts.ColorTransferStrength, 0.62, 0.779);

        Assert.Throws<ArgumentNullException>(() => WatercolorOptions.ForImage(null!));
        Assert.Throws<ArgumentNullException>(() => WatercolorOptions.ForImage(largeScene, baseline: null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => WatercolorOptions.ForImage(largeScene, 0.0));
        Assert.Throws<ArgumentException>(() => WatercolorOptions.ForImage(largeScene, candidatePalettes: []));
        Assert.Throws<ArgumentNullException>(() => WatercolorOptions.ForImage(largeScene, candidatePalettes: [null!]));

        WatercolorOptions customBaseline = CreateValidCustomOptions();
        WatercolorOptions customHalf = WatercolorOptions.ForSize(new Size(400, 400), customBaseline);
        customHalf.Validate();
        Assert.Same(WatercolorPalettes.CoastalCerulean, customHalf.TargetPalette);
        Assert.Equal(0.75, customHalf.ColorTransferStrength, 6);
        Assert.False(customHalf.EnableFxaa);
        Assert.Equal(0.6, customHalf.EgbiBlurSigma, 6);
        Assert.Equal(customBaseline.RegionalContrastSaliency, customHalf.RegionalContrastSaliency);
        Assert.Equal(customBaseline.SaliencyDistanceField, customHalf.SaliencyDistanceField);
        Assert.Equal(HandTremorOptions.ForSize(new Size(400, 400), customBaseline.HandTremor!), customHalf.HandTremor);

        WatercolorOptions customImageOpts = WatercolorOptions.ForImage(largeScene, customBaseline);
        customImageOpts.Validate();
        Assert.Same(WatercolorPalettes.CoastalCerulean, customImageOpts.TargetPalette);
        Assert.Equal(customBaseline.ColorTransferStrength * subsetOpts.ColorTransferStrength, customImageOpts.ColorTransferStrength, 6);
        Assert.False(customImageOpts.EnableFxaa);
        Assert.Equal(customBaseline.RegionalContrastSaliency, customImageOpts.RegionalContrastSaliency);
        Assert.Equal(customBaseline.SaliencyDistanceField, customImageOpts.SaliencyDistanceField);
        Assert.Equal(HandTremorOptions.ForSize(largeScene.Size, customBaseline.HandTremor!), customImageOpts.HandTremor);
        Assert.Equal(TurbulenceFlowOptions.ForSize(largeScene.Size, customBaseline.TurbulenceFlow!), customImageOpts.TurbulenceFlow);
        Assert.Equal(PaperGranulationOptions.ForSize(largeScene.Size, customBaseline.PaperGranulation!), customImageOpts.PaperGranulation);

        WatercolorOptions customEmptyOpts = WatercolorOptions.ForImage(empty, customBaseline);
        Assert.Equal(customBaseline, customEmptyOpts);

        ImageAnalysis largeAnalysis = ImageAnalysis.Analyze(largeScene);
        Assert.Equal(largeOpts, WatercolorOptions.ForAnalysis(largeAnalysis));
        Assert.Equal(customImageOpts, WatercolorOptions.ForAnalysis(largeAnalysis, customBaseline));
        Assert.Equal(subsetOpts, WatercolorOptions.ForAnalysis(largeAnalysis, candidatePalettes: customSubset));

        var overflowBaseline = new WatercolorOptions(EgbiBlurSigma: 1e300);
        WatercolorOptions overflowScaled = WatercolorOptions.ForSize(new Size(100_000, 100_000), overflowBaseline, referenceDimension: 1.0);
        overflowScaled.Validate();
        Assert.True(double.IsFinite(overflowScaled.EgbiBlurSigma));

        Assert.Throws<ArgumentNullException>(() => WatercolorOptions.ForAnalysis(null!));
        Assert.Throws<ArgumentNullException>(() => WatercolorOptions.ForAnalysis(largeAnalysis, baseline: null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => WatercolorOptions.ForAnalysis(largeAnalysis, 0.0));
        Assert.Throws<ArgumentException>(() => WatercolorOptions.ForAnalysis(largeAnalysis, candidatePalettes: []));
        Assert.Throws<ArgumentNullException>(() => WatercolorOptions.ForAnalysis(largeAnalysis, candidatePalettes: [null!]));

        // Two-hop ForSize scaling (800 -> native -> preview) composes cleanly for EdgeDarkening and Egbi.
        Size nativeSize = new(4000, 3000);
        Size previewSize = new(1024, 768);
        WatercolorOptions nativeScaled = WatercolorOptions.ForSize(nativeSize);
        WatercolorOptions previewTwoHop = WatercolorOptions.ForSize(previewSize, nativeScaled, nativeSize.LinearDimension);
        WatercolorOptions previewDirect = WatercolorOptions.ForSize(previewSize);
        Assert.Equal(previewDirect.EdgeDarkening.Intensity, previewTwoHop.EdgeDarkening.Intensity, 10);
        Assert.Equal(previewDirect.Egbi.KFactor, previewTwoHop.Egbi.KFactor, 10);
        Assert.Equal(previewDirect.Egbi.Epsilon, previewTwoHop.Egbi.Epsilon, 10);

        // When EnableColorTransfer is false, Stages 2-6 must tune against raw source stats rather than TargetPalette.
        WatercolorOptions noTransferVibrant = new WatercolorOptions(
            EnableColorTransfer: false,
            TargetPalette: WatercolorPalettes.VibrantCarnival);
        WatercolorOptions noTransferMuted = new WatercolorOptions(
            EnableColorTransfer: false,
            TargetPalette: WatercolorPalettes.MistyMonochrome);
        WatercolorOptions tunedVibrant = WatercolorOptions.ForAnalysis(largeAnalysis, noTransferVibrant);
        WatercolorOptions tunedMuted = WatercolorOptions.ForAnalysis(largeAnalysis, noTransferMuted);
        Assert.Equal(tunedVibrant.MeanShift, tunedMuted.MeanShift);
        Assert.Equal(tunedVibrant.EdgeDarkening, tunedMuted.EdgeDarkening);
        Assert.Equal(tunedVibrant.ToneCurve, tunedMuted.ToneCurve);
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorOptions.ForImage"/> validates candidate palettes before reading image pixels.
    /// </summary>
    [Fact]
    public void ForImage_InvalidCandidatePalettes_ShouldThrowBeforeReadingPixels()
    {
        var image = new ThrowingReadOnlyMatrix(8, 8);

        Assert.Throws<ArgumentException>(() => WatercolorOptions.ForImage(image, candidatePalettes: []));
        Assert.Throws<ArgumentNullException>(() => WatercolorOptions.ForImage(image, candidatePalettes: [null!]));
        Assert.Throws<ArgumentException>(() => WatercolorOptions.ForImage(image, candidatePalettes: [new("   ", WatercolorPalettes.BalancedWash.Statistics)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => WatercolorOptions.ForImage(image, candidatePalettes: [new("BadIlluminant", WatercolorPalettes.BalancedWash.Statistics, (Illuminant)999)]));
        Assert.Throws<ArgumentOutOfRangeException>(() => WatercolorOptions.ForImage(image, candidatePalettes: [new("NaNMeanL", new LabColorStatistics(double.NaN, 0.0, 0.0, 1.0, 1.0, 1.0))]));
        Assert.Throws<ArgumentOutOfRangeException>(() => WatercolorOptions.ForImage(image, candidatePalettes: [new("NegStdDevA", new LabColorStatistics(50.0, 0.0, 0.0, 1.0, -0.1, 1.0))]));
    }

    /// <summary>
    /// Verifies that invalid <see cref="WatercolorOptions.TargetPalette"/> names, illuminants, or CIELAB statistics fail validation.
    /// </summary>
    [Fact]
    public void Validate_InvalidTargetPalette_ShouldThrowArgumentOutOfRangeException()
    {
        ColorTransferProfile[] invalidNamePalettes =
        [
            new(null!, WatercolorPalettes.BalancedWash.Statistics),
            new(string.Empty, WatercolorPalettes.BalancedWash.Statistics),
            new("   ", WatercolorPalettes.BalancedWash.Statistics)
        ];

        foreach (ColorTransferProfile invalidNamePalette in invalidNamePalettes)
        {
            var ex = Assert.ThrowsAny<ArgumentException>(() =>
                new WatercolorOptions(TargetPalette: invalidNamePalette).Validate());
            Assert.Equal(nameof(WatercolorOptions.TargetPalette), ex.ParamName);
        }

        ColorTransferProfile[] invalidPalettes =
        [
            new("BadIlluminant", WatercolorPalettes.BalancedWash.Statistics, (Illuminant)999),
            new("NaNMeanL", new LabColorStatistics(double.NaN, 0.0, 0.0, 1.0, 1.0, 1.0)),
            new("NaNMeanA", new LabColorStatistics(50.0, double.NaN, 0.0, 1.0, 1.0, 1.0)),
            new("InfMeanB", new LabColorStatistics(50.0, 0.0, double.PositiveInfinity, 1.0, 1.0, 1.0)),
            new("NaNStdDevL", new LabColorStatistics(50.0, 0.0, 0.0, double.NaN, 1.0, 1.0)),
            new("NegStdDevA", new LabColorStatistics(50.0, 0.0, 0.0, 1.0, -0.1, 1.0)),
            new("InfStdDevB", new LabColorStatistics(50.0, 0.0, 0.0, 1.0, 1.0, double.PositiveInfinity))
        ];

        foreach (ColorTransferProfile invalidPalette in invalidPalettes)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WatercolorOptions(TargetPalette: invalidPalette).Validate());
            Assert.Equal(nameof(WatercolorOptions.TargetPalette), ex.ParamName);
        }
    }

    /// <summary>
    /// Verifies that all public default static option properties across watercolor stages are valid.
    /// </summary>
    [Fact]
    public void PublicDefaultStaticProperties_ShouldAllBeValid()
    {
        Assert.Equal(0.8, WatercolorOptions.DefaultEgbiBlurSigma);
        WatercolorOptions.DefaultSaliencyEgbi.Validate();
        Assert.Equal(new Rgb8(250, 248, 242), HandTremorOptions.DefaultPaperColor);
        HandTremorOptions.DefaultPrimaryNoise.Validate();
        HandTremorOptions.DefaultSecondaryNoise.Validate();
        WetInWetOptions.DefaultEllipticalFilter.Validate();
        PaperGranulationOptions.DefaultGrainNoise.Validate();
        TurbulenceFlowOptions.DefaultNoise.Validate();
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorOptions.ForSize(Size, WatercolorOptions, double)"/>,
    /// <see cref="WatercolorOptions.ForImage(IReadOnlyMatrix{Rgb8}, WatercolorOptions, double, IReadOnlyList{ColorTransferProfile}?)"/>,
    /// and <see cref="WatercolorOptions.ForAnalysis(ImageAnalysis, WatercolorOptions, double, IReadOnlyList{ColorTransferProfile}?)"/>
    /// preserve all nine <c>Enable*</c> stage toggles from the baseline across both non-empty and empty inputs.
    /// </summary>
    [Fact]
    public void TuningFactories_ShouldPreserveAllEnableFlagsFromBaseline()
    {
        var allDisabled = new WatercolorOptions(
            EnableFxaa: false,
            EnableColorTransfer: false,
            EnableLodAbstraction: false,
            EnableWetInWet: false,
            EnableHandTremor: false,
            EnableEdgeDarkening: false,
            EnableTurbulenceFlow: false,
            EnableToneCurve: false,
            EnablePaperGranulation: false);

        var mixed = new WatercolorOptions(
            EnableFxaa: false,
            EnableColorTransfer: true,
            EnableLodAbstraction: false,
            EnableWetInWet: true,
            EnableHandTremor: false,
            EnableEdgeDarkening: true,
            EnableTurbulenceFlow: false,
            EnableToneCurve: true,
            EnablePaperGranulation: false);

        SequentialMatrix<Rgb8> image = TestMatrices.Create(64, 48, static (x, y) =>
            new Rgb8((byte)(40 + (x * 3)), (byte)(80 + (y * 2)), 160));
        var emptyImage = new SequentialMatrix<Rgb8>(0, 0);
        ImageAnalysis analysis = ImageAnalysis.Analyze(image);
        ImageAnalysis emptyAnalysis = ImageAnalysis.Analyze(emptyImage);

        foreach (WatercolorOptions baseline in new[] { allDisabled, mixed })
        {
            WatercolorOptions[] tunedVariants =
            [
                WatercolorOptions.ForSize(image.Size, baseline),
                WatercolorOptions.ForSize(emptyImage.Size, baseline),
                WatercolorOptions.ForImage(image, baseline),
                WatercolorOptions.ForImage(emptyImage, baseline),
                WatercolorOptions.ForAnalysis(analysis, baseline),
                WatercolorOptions.ForAnalysis(emptyAnalysis, baseline)
            ];

            foreach (WatercolorOptions tuned in tunedVariants)
            {
                tuned.Validate();
                Assert.Equal(baseline.EnableFxaa, tuned.EnableFxaa);
                Assert.Equal(baseline.EnableColorTransfer, tuned.EnableColorTransfer);
                Assert.Equal(baseline.EnableLodAbstraction, tuned.EnableLodAbstraction);
                Assert.Equal(baseline.EnableWetInWet, tuned.EnableWetInWet);
                Assert.Equal(baseline.EnableHandTremor, tuned.EnableHandTremor);
                Assert.Equal(baseline.EnableEdgeDarkening, tuned.EnableEdgeDarkening);
                Assert.Equal(baseline.EnableTurbulenceFlow, tuned.EnableTurbulenceFlow);
                Assert.Equal(baseline.EnableToneCurve, tuned.EnableToneCurve);
                Assert.Equal(baseline.EnablePaperGranulation, tuned.EnablePaperGranulation);
            }
        }
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorOptions.Validate"/> still validates nested stage options even when their corresponding <c>Enable*</c> flag is <see langword="false"/>.
    /// </summary>
    [Fact]
    public void Validate_DisabledStagesWithInvalidNestedOptions_ShouldStillThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(EnableColorTransfer: false, ColorTransferStrength: 1.5).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(EnableLodAbstraction: false, LodAbstraction: new LodAbstractionOptions(SalientKernelSize: 0)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(EnableWetInWet: false, WetInWet: new WetInWetOptions(ScatterRadius: 0)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(EnableHandTremor: false, HandTremor: new HandTremorOptions(Amplitude: -1.0)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(EnableEdgeDarkening: false, EdgeDarkening: new EdgeDarkeningOptions(Intensity: -1.0)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(EnableTurbulenceFlow: false, TurbulenceFlow: new TurbulenceFlowOptions(Intensity: -1.0)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(EnableToneCurve: false, ToneCurve: new PiecewiseToneCurveOptions(MinTone: 0.9, MaxTone: 0.1)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(EnablePaperGranulation: false, PaperGranulation: new PaperGranulationOptions(WarpAmplitude: -1.0)).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WatercolorOptions(EnableFxaa: false, Fxaa: new FxaaOptions(MaxSpanSearchSteps: 0)).Validate());
    }
}
