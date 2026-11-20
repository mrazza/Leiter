namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using Leiter.Algorithms;
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
/// <param name="Egbi">Optional configuration for EGBI over-segmentation used in Stage 2 regional contrast saliency (defaults to <see cref="DefaultSaliencyEgbi"/>).</param>
/// <param name="RegionalContrastSaliency">Optional configuration for Stage 2 regional contrast saliency smoothing and border attenuation.</param>
/// <param name="SaliencyCut">Optional configuration for iterative salient region extraction in Stage 2.</param>
/// <param name="SaliencyDistanceField">Optional smoothing configuration for Stage 2 saliency distance field synthesis.</param>
/// <param name="MeanShift">Optional configuration for CIELAB Mean-Shift segmentation in Stage 3 (defaults to <see cref="MeanShiftOptions.Default"/>).</param>
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
/// <param name="EnableColorTransfer">Whether to apply Stage 1 Reinhard color transfer (default <see langword="true"/>).</param>
/// <param name="EnableLodAbstraction">Whether to apply Stage 3b saliency-guided level-of-detail abstraction (default <see langword="true"/>).</param>
/// <param name="EnableWetInWet">Whether to apply Stage 4 wet-in-wet edge bleeding (default <see langword="true"/>).</param>
/// <param name="EnableHandTremor">Whether to apply Stage 5 hand-tremor boundary perturbation (default <see langword="true"/>).</param>
/// <param name="EnableEdgeDarkening">Whether to apply Stage 6a pigment edge darkening (default <see langword="true"/>).</param>
/// <param name="EnableTurbulenceFlow">Whether to apply Stage 6a low-frequency turbulence flow wash variation (default <see langword="true"/>).</param>
/// <param name="EnableToneCurve">Whether to apply Stage 6b piecewise tone-curve adjustment (default <see langword="true"/>).</param>
/// <param name="EnablePaperGranulation">Whether to apply Stage 6c paper fiber granulation and micro-warp (default <see langword="true"/>).</param>
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
    double EgbiBlurSigma = OptionTuning.DefaultBaseBlurSigma,
    bool EnableColorTransfer = true,
    bool EnableLodAbstraction = true,
    bool EnableWetInWet = true,
    bool EnableHandTremor = true,
    bool EnableEdgeDarkening = true,
    bool EnableTurbulenceFlow = true,
    bool EnableToneCurve = true,
    bool EnablePaperGranulation = true) : ISizeTunableOptions<WatercolorOptions>
{
    private const double MinSrcChromaDenominator = 1.0;
    private const double HighChromaGainThreshold = 2.0;
    private const double LowChromaGainThreshold = 0.5;
    private const double MinAutoTransferStrength = 0.55;
    private const double MaxAutoTransferStrength = 1.0;

    /// <summary>
    /// The default reference linear dimension (<c>sqrt(Width * Height)</c>) at which pipeline stage parameters apply unscaled.
    /// </summary>
    public const double DefaultReferenceDimension = OptionTuning.DefaultReferenceDimension;

    /// <summary>
    /// The default baseline Gaussian pre-blur standard deviation (<c>σ_0 = 0.8</c>) applied before Stage 2 EGBI segmentation.
    /// </summary>
    public const double DefaultEgbiBlurSigma = OptionTuning.DefaultBaseBlurSigma;

    /// <summary>
    /// Gets the baseline EGBI segmentation options used in Stage 2 regional contrast saliency when <c>Egbi</c> is omitted or <see langword="null"/>.
    /// </summary>
    public static EgbiOptions DefaultSaliencyEgbi { get; } = new(KFactor: 50.0, Epsilon: 1.0, MinRegionSize: 200);

    /// <summary>
    /// Gets the default <see cref="WatercolorOptions"/> instance.
    /// </summary>
    public static WatercolorOptions Default { get; } = new();

    /// <summary>
    /// Gets the Stage 2 EGBI over-segmentation options used for regional contrast saliency (defaults to <see cref="DefaultSaliencyEgbi"/>).
    /// </summary>
    public EgbiOptions Egbi
    {
        get;
        init => field = value ?? DefaultSaliencyEgbi;
    } = Egbi ?? DefaultSaliencyEgbi;

    /// <summary>
    /// Gets the Stage 2 regional contrast saliency smoothing and border attenuation options.
    /// </summary>
    public RegionalContrastSaliencyOptions RegionalContrastSaliency
    {
        get;
        init => field = value ?? RegionalContrastSaliencyOptions.Default;
    } = RegionalContrastSaliency ?? RegionalContrastSaliencyOptions.Default;

    /// <summary>
    /// Gets the Stage 2 iterative salient region extraction options.
    /// </summary>
    public SaliencyCutOptions SaliencyCut
    {
        get;
        init => field = value ?? SaliencyCutOptions.Default;
    } = SaliencyCut ?? SaliencyCutOptions.Default;

    /// <summary>
    /// Gets the Stage 2 saliency distance field smoothing options.
    /// </summary>
    public SaliencyDistanceFieldOptions SaliencyDistanceField
    {
        get;
        init => field = value ?? SaliencyDistanceFieldOptions.Default;
    } = SaliencyDistanceField ?? SaliencyDistanceFieldOptions.Default;

    /// <summary>
    /// Gets the Stage 3 CIELAB Mean-Shift segmentation options.
    /// </summary>
    public MeanShiftOptions MeanShift
    {
        get;
        init => field = value ?? MeanShiftOptions.Default;
    } = MeanShift ?? MeanShiftOptions.Default;

    /// <summary>
    /// Gets the Stage 3 saliency-guided level-of-detail abstraction options.
    /// </summary>
    public LodAbstractionOptions LodAbstraction
    {
        get;
        init => field = value ?? LodAbstractionOptions.Default;
    } = LodAbstraction ?? LodAbstractionOptions.Default;

    /// <summary>
    /// Gets the Stage 4 wet-in-wet edge bleeding options.
    /// </summary>
    public WetInWetOptions WetInWet
    {
        get;
        init => field = value ?? WetInWetOptions.Default;
    } = WetInWet ?? WetInWetOptions.Default;

    /// <summary>
    /// Gets the Stage 5 hand-tremor boundary perturbation options.
    /// </summary>
    public HandTremorOptions HandTremor
    {
        get;
        init => field = value ?? HandTremorOptions.Default;
    } = HandTremor ?? HandTremorOptions.Default;

    /// <summary>
    /// Gets the Stage 6 pigment edge darkening options.
    /// </summary>
    public EdgeDarkeningOptions EdgeDarkening
    {
        get;
        init => field = value ?? EdgeDarkeningOptions.Default;
    } = EdgeDarkening ?? EdgeDarkeningOptions.Default;

    /// <summary>
    /// Gets the Stage 6 low-frequency turbulence wash variation options.
    /// </summary>
    public TurbulenceFlowOptions TurbulenceFlow
    {
        get;
        init => field = value ?? TurbulenceFlowOptions.Default;
    } = TurbulenceFlow ?? TurbulenceFlowOptions.Default;

    /// <summary>
    /// Gets the Stage 6 piecewise tone reproduction curve options.
    /// </summary>
    public PiecewiseToneCurveOptions ToneCurve
    {
        get;
        init => field = value ?? PiecewiseToneCurveOptions.Default;
    } = ToneCurve ?? PiecewiseToneCurveOptions.Default;

    /// <summary>
    /// Gets the Stage 6 paper fiber granulation and micro-warp options.
    /// </summary>
    public PaperGranulationOptions PaperGranulation
    {
        get;
        init => field = value ?? PaperGranulationOptions.Default;
    } = PaperGranulation ?? PaperGranulationOptions.Default;

    /// <summary>
    /// Gets the Stage 6 full-screen FXAA anti-aliasing options.
    /// </summary>
    public FxaaOptions Fxaa
    {
        get;
        init => field = value ?? FxaaOptions.Default;
    } = Fxaa ?? FxaaOptions.Default;

    /// <summary>
    /// Creates <see cref="WatercolorOptions"/> with every pipeline stage tuned geometrically for <paramref name="size"/> by delegating to each stage option type's <c>ForSize</c> factory.
    /// </summary>
    /// <param name="size">The target image dimensions.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <returns>
    /// <see cref="Default"/> unchanged when <paramref name="size"/> is empty; otherwise a validated <see cref="WatercolorOptions"/> instance with all nested stage options populated and tuned for <paramref name="size"/>.
    /// </returns>
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
    /// <returns>
    /// <paramref name="baseline"/> unchanged when <paramref name="size"/> is empty; otherwise a validated <see cref="WatercolorOptions"/> instance tuned for <paramref name="size"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="baseline"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="size"/>, <paramref name="baseline"/>, or <paramref name="referenceDimension"/> is invalid.</exception>
    public static WatercolorOptions ForSize(
        Size size,
        WatercolorOptions baseline,
        double referenceDimension = DefaultReferenceDimension)
    {
        OptionTuning.ValidateSizeTuning(size, baseline, referenceDimension);
        var (s, _, totalPixels) = OptionTuning.ComputeScalesUnchecked(size, referenceDimension);
        if (totalPixels == 0)
        {
            return baseline;
        }

        return baseline with
        {
            Egbi = EgbiOptions.ForSize(size, baseline.Egbi, referenceDimension),
            SaliencyCut = SaliencyCutOptions.ForSize(size, baseline.SaliencyCut, referenceDimension),
            MeanShift = MeanShiftOptions.ForSize(size, baseline.MeanShift, referenceDimension),
            LodAbstraction = LodAbstractionOptions.ForSizeUnchecked(size, baseline.LodAbstraction, referenceDimension),
            WetInWet = WetInWetOptions.ForSizeUnchecked(size, baseline.WetInWet, referenceDimension),
            HandTremor = HandTremorOptions.ForSizeUnchecked(size, baseline.HandTremor, referenceDimension),
            EdgeDarkening = EdgeDarkeningOptions.ForSizeUnchecked(size, baseline.EdgeDarkening, referenceDimension),
            TurbulenceFlow = TurbulenceFlowOptions.ForSizeUnchecked(size, baseline.TurbulenceFlow, referenceDimension),
            PaperGranulation = PaperGranulationOptions.ForSizeUnchecked(size, baseline.PaperGranulation, referenceDimension),
            Fxaa = FxaaOptions.ForSize(size, baseline.Fxaa, referenceDimension),
            EgbiBlurSigma = baseline.EgbiBlurSigma == 0.0 ? 0.0 : OptionTuning.ScaleFiniteNonNegative(baseline.EgbiBlurSigma * s)
        };
    }

    /// <summary>
    /// Creates <see cref="WatercolorOptions"/> tuned for both the dimensions and visual content of <paramref name="image"/>,
    /// pre-selecting <see cref="TargetPalette"/> and adapting Stage 2–6 parameters from a single-pass scale-invariant image profile.
    /// </summary>
    /// <param name="image">The input RGB photograph to analyze.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <param name="candidatePalettes">Optional candidate watercolor palettes for automatic palette selection (defaults to <see cref="WatercolorPalettes.Defaults"/>).</param>
    /// <returns>
    /// A validated <see cref="WatercolorOptions"/> instance tuned for <paramref name="image"/> (when <paramref name="image"/> is empty,
    /// preserves <see cref="Default"/> with <see cref="TargetPalette"/> selected from <paramref name="candidatePalettes"/>).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="candidatePalettes"/> entries, or any palette's <see cref="ColorTransferProfile.Name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="candidatePalettes"/> is empty or contains a profile with an empty or whitespace name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/> has invalid dimensions, <paramref name="referenceDimension"/> is not finite and positive, or any candidate palette has invalid illuminant or statistics.</exception>
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
    /// <returns>
    /// A validated <see cref="WatercolorOptions"/> instance tuned for <paramref name="image"/> (when <paramref name="image"/> is empty,
    /// preserves <paramref name="baseline"/> with <see cref="TargetPalette"/> populated).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/>, <paramref name="baseline"/>, any selected candidate palette entry, or its <see cref="ColorTransferProfile.Name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="candidatePalettes"/> is empty (when used), a candidate palette has an empty or whitespace name, or <paramref name="baseline"/> contains an invalid name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="image"/>, <paramref name="baseline"/>, <paramref name="referenceDimension"/>, or any selected candidate palette is invalid.</exception>
    public static WatercolorOptions ForImage(
        IReadOnlyMatrix<Rgb8> image,
        WatercolorOptions baseline,
        double referenceDimension = DefaultReferenceDimension,
        IReadOnlyList<ColorTransferProfile>? candidatePalettes = null)
    {
        OptionTuning.ValidateImageAndReference(image, referenceDimension);
        ArgumentNullException.ThrowIfNull(baseline);
        baseline.Validate();

        if (baseline.TargetPalette is null && candidatePalettes is not null)
        {
            ColorTransfer.SelectClosestProfile(default, candidatePalettes, WatercolorPalettes.DefaultFeatureWeights);
        }

        ImageTuningProfile rawProfile = ImageTuningProfile.AnalyzeUnchecked(image);
        return ForProfile(in rawProfile, baseline, referenceDimension, candidatePalettes);
    }

    /// <summary>
    /// Creates <see cref="WatercolorOptions"/> tuned from a cached <see cref="ImageAnalysis"/> profile,
    /// pre-selecting <see cref="TargetPalette"/> and adapting Stage 2–6 parameters without re-analyzing the source image.
    /// </summary>
    /// <param name="analysis">The precomputed image analysis profile.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <param name="candidatePalettes">Optional candidate watercolor palettes for automatic palette selection (defaults to <see cref="WatercolorPalettes.Defaults"/>).</param>
    /// <returns>
    /// A validated <see cref="WatercolorOptions"/> instance tuned for <paramref name="analysis"/> (when the analyzed image is empty,
    /// preserves <see cref="Default"/> with <see cref="TargetPalette"/> selected from <paramref name="candidatePalettes"/>).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/>, any entry in <paramref name="candidatePalettes"/>, or its <see cref="ColorTransferProfile.Name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="candidatePalettes"/> is empty or contains a profile with an empty or whitespace name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="referenceDimension"/> or any candidate palette is invalid.</exception>
    public static WatercolorOptions ForAnalysis(
        ImageAnalysis analysis,
        double referenceDimension = DefaultReferenceDimension,
        IReadOnlyList<ColorTransferProfile>? candidatePalettes = null) =>
        ForAnalysis(analysis, Default, referenceDimension, candidatePalettes);

    /// <summary>
    /// Creates <see cref="WatercolorOptions"/> tuned from <paramref name="baseline"/> using a cached <see cref="ImageAnalysis"/> profile,
    /// preserving any explicitly configured <see cref="TargetPalette"/> or selecting the closest candidate palette when <see langword="null"/>.
    /// </summary>
    /// <param name="analysis">The precomputed image analysis profile.</param>
    /// <param name="baseline">The baseline watercolor pipeline configuration calibrated at <paramref name="referenceDimension"/>.</param>
    /// <param name="referenceDimension">The reference linear dimension (<c>sqrt(Width * Height)</c>) corresponding to baseline scale <c>1.0</c>.</param>
    /// <param name="candidatePalettes">Optional candidate watercolor palettes for automatic palette selection when <paramref name="baseline"/>.<see cref="TargetPalette"/> is <see langword="null"/> (defaults to <see cref="WatercolorPalettes.Defaults"/>).</param>
    /// <returns>
    /// A validated <see cref="WatercolorOptions"/> instance tuned for <paramref name="analysis"/> (when the analyzed image is empty,
    /// preserves <paramref name="baseline"/> with <see cref="TargetPalette"/> populated).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="analysis"/>, <paramref name="baseline"/>, any selected candidate palette entry, or its <see cref="ColorTransferProfile.Name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="candidatePalettes"/> is empty (when used), a candidate palette has an empty or whitespace name, or <paramref name="baseline"/> contains an invalid name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="baseline"/>, <paramref name="referenceDimension"/>, or any selected candidate palette is invalid.</exception>
    public static WatercolorOptions ForAnalysis(
        ImageAnalysis analysis,
        WatercolorOptions baseline,
        double referenceDimension = DefaultReferenceDimension,
        IReadOnlyList<ColorTransferProfile>? candidatePalettes = null)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        OptionTuning.ValidateBaselineAndReference(baseline, referenceDimension);

        ImageTuningProfile rawProfile = analysis.Profile;
        return ForProfile(in rawProfile, baseline, referenceDimension, candidatePalettes);
    }

    private static WatercolorOptions ForProfile(
        in ImageTuningProfile rawProfile,
        WatercolorOptions baseline,
        double referenceDimension,
        IReadOnlyList<ColorTransferProfile>? candidatePalettes)
    {
        ColorTransferProfile selectedPalette = baseline.TargetPalette ?? ColorTransfer.SelectClosestProfile(
            rawProfile.LabStats,
            candidatePalettes ?? WatercolorPalettes.Defaults,
            WatercolorPalettes.DefaultFeatureWeights);

        if (rawProfile.IsEmpty)
        {
            return baseline with
            {
                TargetPalette = selectedPalette
            };
        }

        double srcChromaStd = double.Hypot(rawProfile.LabStats.StdDevA, rawProfile.LabStats.StdDevB);
        double palChromaStd = double.Hypot(selectedPalette.Statistics.StdDevA, selectedPalette.Statistics.StdDevB);
        double chromaGain = palChromaStd / Math.Max(MinSrcChromaDenominator, srcChromaStd);
        double autoStrength = chromaGain > HighChromaGainThreshold
            ? Math.Clamp(HighChromaGainThreshold / chromaGain, MinAutoTransferStrength, MaxAutoTransferStrength)
            : chromaGain < LowChromaGainThreshold
                ? Math.Clamp(chromaGain / LowChromaGainThreshold, MinAutoTransferStrength, MaxAutoTransferStrength)
                : MaxAutoTransferStrength;
        double transferStrength = baseline.ColorTransferStrength * autoStrength;

        ImageTuningProfile postTransferProfile = rawProfile.WithTransferredStatistics(selectedPalette.Statistics, transferStrength);
        var (s, _, _) = OptionTuning.ComputeScalesUnchecked(rawProfile.ImageSize, referenceDimension);

        return baseline with
        {
            TargetPalette = selectedPalette,
            ColorTransferStrength = transferStrength,
            Egbi = EgbiOptions.ForProfile(in postTransferProfile, baseline.Egbi, referenceDimension),
            SaliencyCut = SaliencyCutOptions.ForProfile(in postTransferProfile, baseline.SaliencyCut, referenceDimension),
            MeanShift = MeanShiftOptions.ForProfile(in postTransferProfile, baseline.MeanShift, referenceDimension),
            LodAbstraction = LodAbstractionOptions.ForProfile(in postTransferProfile, baseline.LodAbstraction, referenceDimension),
            WetInWet = WetInWetOptions.ForProfile(in postTransferProfile, baseline.WetInWet, referenceDimension),
            HandTremor = HandTremorOptions.ForSizeUnchecked(postTransferProfile.ImageSize, baseline.HandTremor, referenceDimension),
            EdgeDarkening = EdgeDarkeningOptions.ForProfile(in postTransferProfile, baseline.EdgeDarkening, referenceDimension),
            TurbulenceFlow = TurbulenceFlowOptions.ForSizeUnchecked(postTransferProfile.ImageSize, baseline.TurbulenceFlow, referenceDimension),
            ToneCurve = PiecewiseToneCurveOptions.ForProfile(in postTransferProfile, baseline.ToneCurve, referenceDimension),
            PaperGranulation = PaperGranulationOptions.ForSizeUnchecked(postTransferProfile.ImageSize, baseline.PaperGranulation, referenceDimension),
            Fxaa = FxaaOptions.ForProfile(in postTransferProfile, baseline.Fxaa, referenceDimension),
            EgbiBlurSigma = baseline.EgbiBlurSigma == 0.0
                ? 0.0
                : OptionTuning.ScaleFiniteNonNegative(baseline.EgbiBlurSigma * s * Math.Sqrt(postTransferProfile.ClutterFactor))
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

        Egbi.Validate();
        RegionalContrastSaliency.Validate();
        SaliencyCut.Validate();
        SaliencyDistanceField.Validate();
        MeanShift.Validate();
        LodAbstraction.Validate();
        WetInWet.Validate();
        HandTremor.Validate();
        EdgeDarkening.Validate();
        TurbulenceFlow.Validate();
        ToneCurve.Validate();
        PaperGranulation.Validate();
        Fxaa.Validate();
    }
}
