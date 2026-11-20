namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;

/// <summary>
/// Configures end-to-end parameters for Wang et al.'s artistic photo watercolorization pipeline (Stages 1–6).
/// </summary>
/// <param name="TargetPalette">Optional explicit watercolor target palette; when <see langword="null"/>, the closest palette is selected automatically.</param>
/// <param name="ColorTransferStrength">The Reinhard color transfer blending strength in [0.0, 1.0] (default 1.0).</param>
/// <param name="Egbi">Optional configuration for EGBI over-segmentation used in Stage 2 regional contrast saliency (when <see langword="null"/>, uses Cheng et al. defaults scaled safely for tiny images).</param>
/// <param name="RegionalContrastSaliency">Optional configuration for Stage 2 regional contrast saliency smoothing and border attenuation.</param>
/// <param name="SaliencyCut">Optional configuration for iterative salient region extraction in Stage 2.</param>
/// <param name="SaliencyDistanceField">Optional smoothing configuration for Stage 2 saliency distance field synthesis.</param>
/// <param name="MeanShift">Optional configuration for CIELAB Mean-Shift segmentation in Stage 3 (when <see langword="null"/>, uses Wang et al. defaults scaled safely for tiny images).</param>
/// <param name="LodAbstraction">Optional configuration for saliency-guided level-of-detail abstraction in Stage 3.</param>
/// <param name="WetInWet">Optional configuration for wet-in-wet edge bleeding in Stage 4.</param>
/// <param name="HandTremor">Optional configuration for hand-tremor boundary perturbation in Stage 5.</param>
/// <param name="EdgeDarkening">Optional configuration for pigment edge darkening in Stage 6.</param>
/// <param name="TurbulenceFlow">Optional configuration for low-frequency turbulence wash variation in Stage 6.</param>
/// <param name="ToneCurve">Optional configuration for the piecewise tone reproduction curve applied prior to paper granulation in Stage 6.</param>
/// <param name="PaperGranulation">Optional configuration for paper fiber granulation and micro-warp in Stage 6.</param>
/// <param name="Fxaa">Optional configuration for final full-screen FXAA anti-aliasing in Stage 6.</param>
/// <param name="EnableFxaa">Whether to apply final FXAA boundary smoothing (default <see langword="true"/>).</param>
/// <param name="EgbiBlurSigma">The standard deviation for the Stage 2 Gaussian pre-blur applied before EGBI segmentation (default 0.8; set to 0.0 to disable pre-blur).</param>
public sealed record WatercolorOptions(
    ColorTransferProfile? TargetPalette = null,
    double ColorTransferStrength = 1.0,
    EgbiOptions? Egbi = null,
    RegionalContrastSaliencyOptions? RegionalContrastSaliency = null,
    SaliencyCutOptions? SaliencyCut = null,
    SaliencyDistanceFieldOptions? SaliencyDistanceField = null,
    MeanShiftOptions? MeanShift = null,
    LodAbstractionOptions? LodAbstraction = null,
    WetInWetOptions? WetInWet = null,
    HandTremorOptions? HandTremor = null,
    EdgeDarkeningOptions? EdgeDarkening = null,
    TurbulenceFlowOptions? TurbulenceFlow = null,
    PiecewiseToneCurveOptions? ToneCurve = null,
    PaperGranulationOptions? PaperGranulation = null,
    FxaaOptions? Fxaa = null,
    bool EnableFxaa = true,
    double EgbiBlurSigma = 0.8) : ISizeTunableOptions<WatercolorOptions>
{
    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which pipeline stage parameters apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// Gets the default <see cref="WatercolorOptions"/> instance.
    /// </summary>
    public static WatercolorOptions Default { get; } = new();

    /// <summary>
    /// Gets the baseline EGBI segmentation options used in Stage 2 regional contrast saliency when <see cref="Egbi"/> is <see langword="null"/>.
    /// </summary>
    public static EgbiOptions DefaultSaliencyEgbi { get; } = new(KFactor: 50.0, Epsilon: 1.0, MinRegionSize: 200);

    internal static EgbiOptions ResolveDefaultEgbi(int pixelCount) =>
        DefaultSaliencyEgbi with { MinRegionSize = OptionTuning.ClampRegionSize(DefaultSaliencyEgbi.MinRegionSize, pixelCount, 16) };

    internal static MeanShiftOptions ResolveDefaultMeanShift(int pixelCount) =>
        new(MinRegionSize: OptionTuning.ClampRegionSize(MeanShiftOptions.Default.MinRegionSize, pixelCount, 32));

    /// <summary>
    /// Creates <see cref="WatercolorOptions"/> with every pipeline stage tuned geometrically for <paramref name="size"/> by delegating to each stage option type's <c>ForSize</c> factory.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="WatercolorOptions"/> instance with all nested stage options populated for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/> has negative dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static WatercolorOptions ForSize(
        Size size,
        double referenceDimension = DefaultReferenceDimension) =>
        ForSize(size, Default, referenceDimension);

    /// <summary>
    /// Creates <see cref="WatercolorOptions"/> by scaling <paramref name="baseline"/> and every nested pipeline stage option geometrically for <paramref name="size"/>.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="baseline">The baseline watercolor pipeline configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>A validated <see cref="WatercolorOptions"/> instance tuned for <paramref name="size"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static WatercolorOptions ForSize(
        Size size,
        WatercolorOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        var (s, _, totalPixels) = OptionTuning.ComputeScales(size, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();
        if (totalPixels == 0)
        {
            return baseline;
        }

        return baseline with
        {
            Egbi = EgbiOptions.ForSize(size, baseline.Egbi ?? DefaultSaliencyEgbi, referenceDimension),
            RegionalContrastSaliency = baseline.RegionalContrastSaliency ?? RegionalContrastSaliencyOptions.Default,
            SaliencyCut = SaliencyCutOptions.ForSize(size, baseline.SaliencyCut ?? SaliencyCutOptions.Default, referenceDimension),
            SaliencyDistanceField = baseline.SaliencyDistanceField ?? SaliencyDistanceFieldOptions.Default,
            MeanShift = MeanShiftOptions.ForSize(size, baseline.MeanShift ?? MeanShiftOptions.Default, referenceDimension),
            LodAbstraction = LodAbstractionOptions.ForSize(size, baseline.LodAbstraction ?? LodAbstractionOptions.Default, referenceDimension),
            WetInWet = WetInWetOptions.ForSize(size, baseline.WetInWet ?? WetInWetOptions.Default, referenceDimension),
            HandTremor = HandTremorOptions.ForSize(size, baseline.HandTremor ?? HandTremorOptions.Default, referenceDimension),
            EdgeDarkening = EdgeDarkeningOptions.ForSize(size, baseline.EdgeDarkening ?? EdgeDarkeningOptions.Default, referenceDimension),
            TurbulenceFlow = TurbulenceFlowOptions.ForSize(size, baseline.TurbulenceFlow ?? TurbulenceFlowOptions.Default, referenceDimension),
            ToneCurve = PiecewiseToneCurveOptions.ForSize(size, baseline.ToneCurve ?? PiecewiseToneCurveOptions.Default, referenceDimension),
            PaperGranulation = PaperGranulationOptions.ForSize(size, baseline.PaperGranulation ?? PaperGranulationOptions.Default, referenceDimension),
            Fxaa = FxaaOptions.ForSize(size, baseline.Fxaa ?? FxaaOptions.Default, referenceDimension),
            EgbiBlurSigma = baseline.EgbiBlurSigma * s
        };
    }

    /// <summary>
    /// Creates <see cref="WatercolorOptions"/> tuned for both the dimensions and visual content of <paramref name="image"/>,
    /// pre-selecting <see cref="TargetPalette"/> and adapting Stage 2–6 parameters from a single-pass scale-invariant image profile.
    /// </summary>
    /// <param name="image">The input RGB photograph to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <param name="candidatePalettes">Optional candidate watercolor palettes for automatic palette selection (defaults to <see cref="WatercolorPalettes.Defaults"/>).</param>
    /// <returns>A validated <see cref="WatercolorOptions"/> instance with <see cref="TargetPalette"/> and all stage options tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or any palette entry in <paramref name="candidatePalettes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="candidatePalettes"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions or <paramref name="referenceDimension"/> is not finite and positive.</exception>
    public static WatercolorOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        double referenceDimension = DefaultReferenceDimension,
        IReadOnlyList<ColorTransferProfile>? candidatePalettes = null) =>
        ForImage(image, Default, referenceDimension, candidatePalettes);

    /// <summary>
    /// Creates <see cref="WatercolorOptions"/> tuned from <paramref name="baseline"/> for both the dimensions and visual content of <paramref name="image"/>,
    /// preserving any explicitly configured <see cref="TargetPalette"/> or selecting the closest candidate palette when <see langword="null"/>.
    /// </summary>
    /// <param name="image">The input RGB photograph to analyze.</param>
    /// <param name="baseline">The baseline watercolor pipeline configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <param name="candidatePalettes">Optional candidate watercolor palettes for automatic palette selection when <paramref name="baseline"/>.<see cref="TargetPalette"/> is <see langword="null"/> (defaults to <see cref="WatercolorPalettes.Defaults"/>).</param>
    /// <returns>A validated <see cref="WatercolorOptions"/> instance tuned for <paramref name="image"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="baseline"/>, or any selected candidate palette entry is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="candidatePalettes"/> is empty (when used) or <paramref name="baseline"/> contains an invalid name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static WatercolorOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        WatercolorOptions baseline,
        double referenceDimension = DefaultReferenceDimension,
        IReadOnlyList<ColorTransferProfile>? candidatePalettes = null)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();

        ImageTuningProfile rawProfile = ImageTuningProfile.Analyze(image);
        ColorTransferProfile selectedPalette = baseline.TargetPalette ?? ColorTransfer.SelectClosestProfile(
            rawProfile.LabStats,
            candidatePalettes ?? WatercolorPalettes.Defaults,
            WatercolorPalettes.DefaultFeatureWeights);

        if (rawProfile.IsEmpty)
        {
            return ForSize(image.Size, baseline, referenceDimension) with
            {
                TargetPalette = selectedPalette
            };
        }

        double srcChromaStd = Math.Sqrt((rawProfile.LabStats.StdDevA * rawProfile.LabStats.StdDevA) + (rawProfile.LabStats.StdDevB * rawProfile.LabStats.StdDevB));
        double palChromaStd = Math.Sqrt((selectedPalette.Statistics.StdDevA * selectedPalette.Statistics.StdDevA) + (selectedPalette.Statistics.StdDevB * selectedPalette.Statistics.StdDevB));
        double chromaGain = palChromaStd / Math.Max(1.0, srcChromaStd);
        double autoStrength = chromaGain > 2.0
            ? Math.Clamp(2.0 / chromaGain, 0.55, 1.0)
            : chromaGain < 0.5
                ? Math.Clamp(chromaGain * 2.0, 0.55, 1.0)
                : 1.0;
        double transferStrength = baseline.ColorTransferStrength * autoStrength;

        ImageTuningProfile postTransferProfile = rawProfile.WithTransferredStatistics(selectedPalette.Statistics, transferStrength);
        var (s, _, _) = OptionTuning.ComputeScales(image.Size, referenceDimension);

        return baseline with
        {
            TargetPalette = selectedPalette,
            ColorTransferStrength = transferStrength,
            Egbi = EgbiOptions.ForProfile(in postTransferProfile, baseline.Egbi ?? DefaultSaliencyEgbi, referenceDimension),
            RegionalContrastSaliency = baseline.RegionalContrastSaliency ?? RegionalContrastSaliencyOptions.Default,
            SaliencyCut = SaliencyCutOptions.ForProfile(in postTransferProfile, baseline.SaliencyCut ?? SaliencyCutOptions.Default, referenceDimension),
            SaliencyDistanceField = baseline.SaliencyDistanceField ?? SaliencyDistanceFieldOptions.Default,
            MeanShift = MeanShiftOptions.ForProfile(in postTransferProfile, baseline.MeanShift ?? MeanShiftOptions.Default, referenceDimension),
            LodAbstraction = LodAbstractionOptions.ForProfile(in postTransferProfile, baseline.LodAbstraction ?? LodAbstractionOptions.Default, referenceDimension),
            WetInWet = WetInWetOptions.ForProfile(in postTransferProfile, baseline.WetInWet ?? WetInWetOptions.Default, referenceDimension),
            HandTremor = HandTremorOptions.ForSize(postTransferProfile.ImageSize, baseline.HandTremor ?? HandTremorOptions.Default, referenceDimension),
            EdgeDarkening = EdgeDarkeningOptions.ForProfile(in postTransferProfile, baseline.EdgeDarkening ?? EdgeDarkeningOptions.Default, referenceDimension),
            TurbulenceFlow = TurbulenceFlowOptions.ForSize(postTransferProfile.ImageSize, baseline.TurbulenceFlow ?? TurbulenceFlowOptions.Default, referenceDimension),
            ToneCurve = PiecewiseToneCurveOptions.ForProfile(in postTransferProfile, baseline.ToneCurve ?? PiecewiseToneCurveOptions.Default, referenceDimension),
            PaperGranulation = PaperGranulationOptions.ForSize(postTransferProfile.ImageSize, baseline.PaperGranulation ?? PaperGranulationOptions.Default, referenceDimension),
            Fxaa = FxaaOptions.ForProfile(in postTransferProfile, baseline.Fxaa ?? FxaaOptions.Default, referenceDimension),
            EgbiBlurSigma = baseline.EgbiBlurSigma * s * Math.Sqrt(postTransferProfile.ClutterFactor)
        };
    }

    /// <summary>
    /// Validates that all configured pipeline options are finite and within their required domains.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <see cref="TargetPalette"/> has an empty or whitespace name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any numeric, enum, or nested option parameter is invalid.</exception>
    public void Validate()
    {
        Guard.ThrowIfNotFiniteInRange(ColorTransferStrength, 0.0, 1.0);
        Guard.ThrowIfNotFiniteNonNegative(EgbiBlurSigma);

        TargetPalette?.Validate(nameof(TargetPalette));

        Egbi?.Validate();
        RegionalContrastSaliency?.Validate();
        SaliencyCut?.Validate();
        SaliencyDistanceField?.Validate();
        MeanShift?.Validate();
        LodAbstraction?.Validate();
        WetInWet?.Validate();
        HandTremor?.Validate();
        EdgeDarkening?.Validate();
        TurbulenceFlow?.Validate();
        ToneCurve?.Validate();
        PaperGranulation?.Validate();
        Fxaa?.Validate();
    }
}
