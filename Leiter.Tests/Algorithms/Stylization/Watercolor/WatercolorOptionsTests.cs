namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
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
        Assert.Null(options.SaliencyCut);
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

        options.Validate();
    }

    /// <summary>
    /// Verifies that invalid <see cref="WatercolorOptions.ColorTransferStrength"/> values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="strength">The invalid color transfer strength.</param>
    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
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
            new WatercolorOptions(SaliencyCut: new SaliencyCutOptions(InitialThreshold: 1.5)).Validate());
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
}
