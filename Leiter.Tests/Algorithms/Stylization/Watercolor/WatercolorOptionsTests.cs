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
                NoiseOptions: new PerlinNoiseOptions(Seed: 909, Frequency: 0.025, Octaves: 3, Lacunarity: 2.2, Persistence: 0.45)),
            ToneCurve: new PiecewiseToneCurveOptions(MinTone: 0.02, MaxTone: 0.98, ShadowKnee: 0.2, HighlightKnee: 0.8),
            PaperGranulation: new PaperGranulationOptions(
                DensityStrength: 0.45,
                WarpAmplitude: 0.9,
                GrainNoiseOptions: new PerlinNoiseOptions(Seed: 808, Frequency: 0.12, Octaves: 3, Lacunarity: 2.1, Persistence: 0.5)),
            Fxaa: new FxaaOptions(
                RelativeContrastThreshold: 0.2,
                AbsoluteContrastThreshold: 0.08,
                SubpixelBlendStrength: 0.5,
                MaxSpanSearchSteps: 12),
            EnableFxaa: false,
            EgbiBlurSigma: 1.2);

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
        Assert.Null(options.Egbi);
        Assert.Null(options.RegionalContrastSaliency);
        Assert.Null(options.SaliencyCut);
        Assert.Null(options.SaliencyDistanceField);
        Assert.Null(options.MeanShift);
        Assert.Null(options.LodAbstraction);
        Assert.Null(options.WetInWet);
        Assert.Null(options.HandTremor);
        Assert.Null(options.EdgeDarkening);
        Assert.Null(options.TurbulenceFlow);
        Assert.Null(options.ToneCurve);
        Assert.Null(options.PaperGranulation);
        Assert.Null(options.Fxaa);
        Assert.True(options.EnableFxaa);
        Assert.Equal(0.8, options.EgbiBlurSigma, 6);
        Assert.Equal(WatercolorOptions.Default, options);

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
        Assert.Equal(PiecewiseToneCurveOptions.ForSize(size), opts.ToneCurve);
        Assert.Equal(PaperGranulationOptions.ForSize(size), opts.PaperGranulation);
        Assert.Equal(FxaaOptions.ForSize(size), opts.Fxaa);
        Assert.True(opts.EnableFxaa);
        Assert.Equal(0.8 * Math.Sqrt((double)(width * height) / (800.0 * 800.0)), opts.EgbiBlurSigma, 6);
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorOptions.ForSize"/> validates extreme and degenerate dimensions cleanly and rejects invalid arguments.
    /// </summary>
    [Fact]
    public void ForSize_ExtremeSizesAndInvalidArguments_ShouldBehaveAsExpected()
    {
        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(3, 3), new Size(4000, 80), new Size(12000, 9000) })
        {
            WatercolorOptions.ForSize(size).Validate();
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => WatercolorOptions.ForSize(new Size(-1, 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => WatercolorOptions.ForSize(new Size(100, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => WatercolorOptions.ForSize(new Size(100, 100), 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => WatercolorOptions.ForSize(new Size(100, 100), double.NaN));
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

        var neutral = new SequentialMatrix<Rgb8>(64, 64);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                byte v = (byte)(60 + ((x * 2) % 140));
                neutral[x, y] = new Rgb8(v, v, v);
            }
        }

        WatercolorOptions neutralOpts = WatercolorOptions.ForImage(neutral);
        neutralOpts.Validate();
        Assert.InRange(neutralOpts.ColorTransferStrength, 0.55, 0.999);

        var largeScene = new SequentialMatrix<Rgb8>(800, 600);
        for (int y = 0; y < 600; y++)
        {
            for (int x = 0; x < 800; x++)
            {
                bool fg = x >= 220 && x < 580 && y >= 150 && y < 450;
                int wave = (x / 16 + y / 16) % 3;
                largeScene[x, y] = fg
                    ? new Rgb8((byte)(210 + (wave * 10)), (byte)(60 + (wave * 8)), 35)
                    : wave == 0
                        ? new Rgb8(45, 110, 175)
                        : new Rgb8(215, 225, 210);
            }
        }

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
        Assert.Equal(1.0, largeOpts.ColorTransferStrength, 6);
        Assert.Equal(smallOpts.ColorTransferStrength, largeOpts.ColorTransferStrength, 2);
        Assert.Equal(smallOpts.MeanShift!.RangeRadius, largeOpts.MeanShift!.RangeRadius, 1);
        Assert.True(largeOpts.MeanShift.SpatialRadius >= smallOpts.MeanShift.SpatialRadius * 3);
        Assert.True(largeOpts.HandTremor!.Amplitude >= smallOpts.HandTremor!.Amplitude * 3.5);
        Assert.True(largeOpts.EgbiBlurSigma >= smallOpts.EgbiBlurSigma * 3.5);

        IReadOnlyList<ColorTransferProfile> customSubset = [WatercolorPalettes.CoastalCerulean];
        WatercolorOptions subsetOpts = WatercolorOptions.ForImage(largeScene, candidatePalettes: customSubset);
        Assert.Same(WatercolorPalettes.CoastalCerulean, subsetOpts.TargetPalette);

        IReadOnlyList<ColorTransferProfile> lowChromaSubset = [WatercolorPalettes.MistyMonochrome];
        WatercolorOptions mutedOpts = WatercolorOptions.ForImage(largeScene, candidatePalettes: lowChromaSubset);
        Assert.InRange(mutedOpts.ColorTransferStrength, 0.55, 0.999);

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
        WatercolorOptions.DefaultSaliencyEgbi.Validate();
        Assert.Equal(new Rgb8(250, 248, 242), HandTremorOptions.DefaultPaperColor);
        HandTremorOptions.DefaultPrimaryNoise.Validate();
        HandTremorOptions.DefaultSecondaryNoise.Validate();
        WetInWetOptions.DefaultEllipticalFilter.Validate();
        PaperGranulationOptions.DefaultGrainNoise.Validate();
        TurbulenceFlowOptions.DefaultNoise.Validate();
    }
}
