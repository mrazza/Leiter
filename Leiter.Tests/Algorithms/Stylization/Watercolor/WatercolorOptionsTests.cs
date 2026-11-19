namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="WatercolorOptions"/>.
/// </summary>
public class WatercolorOptionsTests
{
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
        Assert.Equal(EgbiOptions.ForSize(size, WatercolorOptions.SaliencyEgbiBaseline), opts.Egbi);
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
        Assert.Throws<ArgumentOutOfRangeException>(() => WatercolorOptions.ForImage(largeScene, 0.0));
        Assert.Throws<ArgumentException>(() => WatercolorOptions.ForImage(largeScene, candidatePalettes: []));
        Assert.Throws<ArgumentNullException>(() => WatercolorOptions.ForImage(largeScene, candidatePalettes: [null!]));
    }
}
