namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using Leiter.Algorithms;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Internal;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor.Internal;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Maintains fine-grained incremental DAG caches across repeated <see cref="WatercolorPipeline"/> renders
/// for a fixed source image and optional custom paper height map.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="WatercolorPipelineSession"/> caches intermediate artifacts across all 18 <see cref="WatercolorStep"/> nodes
/// as well as internal sub-step products (CIELAB pre-blur, raw Jump-Flooding saliency distance field,
/// 5D Mean-Shift converged modes, and per-region salient overlap fractions). Independent DAG branches
/// (Stage 2 Saliency vs. Stage 3a Mean-Shift, and Stage 6 procedural Turbulence Flow and Paper Height Map vs. Stages 1–5)
/// remain cached when unrelated parameters are edited.
/// </para>
/// <para>
/// This class is designed for sequential single-writer execution; concurrent or re-entrant calls to
/// <see cref="Render"/> or <see cref="ResetCache"/> throw <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
public sealed class WatercolorPipelineSession
{
    private const int TotalStepCount = 18;
    private const int CancellationCheckMask = 4095;
    private const string BusyErrorMessage =
        $"Concurrent or re-entrant operations on {nameof(WatercolorPipelineSession)} are not supported.";

    private readonly IReadOnlyMatrix<DoublePixel>? _customPaperHeightMap;
    private readonly CacheSlot<(bool Active, ColorTransferProfile? Palette, double Strength), (Matrix<Rgb8> Image, ColorTransferProfile Palette)> _colorTransfer = new();
    private readonly SaliencyCache _saliency = new();
    private readonly SegmentationCache _segmentation = new();
    private readonly StylizationCache _stylization = new();
    private readonly FinishingCache _finishing = new();
    private readonly ICacheSlot[] _allSlots;

    private int _isBusy;
    private long _nextVersion;

    /// <summary>
    /// Initializes a new <see cref="WatercolorPipelineSession"/> bound to <paramref name="sourceImage"/>
    /// and an optional custom <paramref name="paperHeightMap"/>.
    /// </summary>
    /// <param name="sourceImage">The input RGB photograph to watercolorize.</param>
    /// <param name="paperHeightMap">Optional custom normalized [0, 1] paper height map used during Stage 6 granulation.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sourceImage"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="paperHeightMap"/> dimensions do not match <paramref name="sourceImage"/>.</exception>
    public WatercolorPipelineSession(
        IReadOnlyMatrix<Rgb8> sourceImage,
        IReadOnlyMatrix<DoublePixel>? paperHeightMap = null)
    {
        ArgumentNullException.ThrowIfNull(sourceImage);
        Guard.ThrowIfSizeMismatchIfNotNull(sourceImage, paperHeightMap);

        SourceImage = sourceImage;
        _customPaperHeightMap = paperHeightMap;
        ImageSize = sourceImage.Size;

        _allSlots =
        [
            _colorTransfer,
            _saliency.PreBlur,
            _saliency.EgbiGraph,
            _saliency.SaliencyMap,
            _saliency.SalientMask,
            _saliency.RawDistanceField,
            _saliency.DistanceField,
            _segmentation.Filtering,
            _segmentation.Clustering,
            _segmentation.SalientFractions,
            _stylization.Lod,
            _stylization.WetInWet,
            _stylization.HandTremor,
            _finishing.EdgeDensity,
            _finishing.TurbulenceDensity,
            _finishing.PigmentCombine,
            _finishing.ToneCurve,
            _finishing.PaperHeightMap,
            _finishing.PaperGranulation,
            _finishing.Fxaa
        ];
    }

    /// <summary>
    /// Gets the source RGB image bound to this session.
    /// </summary>
    public IReadOnlyMatrix<Rgb8> SourceImage { get; }

    /// <summary>
    /// Gets the dimensions of <see cref="SourceImage"/>.
    /// </summary>
    public Size ImageSize { get; }

    /// <summary>
    /// Gets the validated <see cref="WatercolorOptions"/> from the most recent completed <see cref="Render"/> call,
    /// or <see langword="null"/> if no render has completed since construction or <see cref="ResetCache"/>.
    /// </summary>
    public WatercolorOptions? LastOptions { get; private set; }

    /// <summary>
    /// Gets the <see cref="WatercolorResult"/> from the most recent completed <see cref="Render"/> call,
    /// or <see langword="null"/> if no render has completed since construction or <see cref="ResetCache"/>.
    /// </summary>
    public WatercolorResult? LastResult { get; private set; }

    /// <summary>
    /// Renders the watercolor pipeline using <paramref name="options"/>, reusing cached intermediate outputs
    /// for all DAG steps whose upstream inputs and resolved parameters have not changed.
    /// </summary>
    /// <param name="options">Optional pipeline configuration parameters controlling each stage. If <see langword="null"/>, <see cref="WatercolorOptions.Default"/> is used.</param>
    /// <param name="onStepCompleted">Optional callback invoked as each fine-grained <see cref="WatercolorStep"/> completes (not invoked when <see cref="SourceImage"/> is empty).</param>
    /// <param name="cancellationToken">Optional token used to cancel execution cooperatively between and within pipeline steps.</param>
    /// <returns>A <see cref="WatercolorResult"/> containing the final rendered painting, intermediate outputs, and per-step timings.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="options"/> contains an invalid palette name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> contains invalid parameter values.</exception>
    /// <exception cref="InvalidOperationException">Thrown when called concurrently or re-entrantly while another <see cref="Render"/> or <see cref="ResetCache"/> call is in progress.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    public WatercolorResult Render(
        WatercolorOptions? options = null,
        Action<WatercolorStepProgress>? onStepCompleted = null,
        CancellationToken cancellationToken = default)
    {
        EnterExclusive();
        try
        {
            WatercolorOptions resolvedOptions = OptionsResolver.Resolve(options);
            return RenderValidatedCore(resolvedOptions, onStepCompleted, onStageCompleted: null, cancellationToken);
        }
        finally
        {
            ExitExclusive();
        }
    }

    /// <summary>
    /// Clears all cached intermediate artifacts and resets <see cref="LastOptions"/> and <see cref="LastResult"/> to <see langword="null"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when called concurrently or re-entrantly while a <see cref="Render"/> or <see cref="ResetCache"/> call is in progress.</exception>
    public void ResetCache()
    {
        EnterExclusive();
        try
        {
            ClearAllCacheState();
        }
        finally
        {
            ExitExclusive();
        }
    }

    internal WatercolorResult RenderCore(
        WatercolorOptions resolvedOptions,
        Action<WatercolorStepProgress>? onStepCompleted,
        Action<WatercolorStageProgress>? onStageCompleted,
        CancellationToken cancellationToken)
    {
        EnterExclusive();
        try
        {
            return RenderValidatedCore(resolvedOptions, onStepCompleted, onStageCompleted, cancellationToken);
        }
        finally
        {
            ExitExclusive();
        }
    }

    private WatercolorResult RenderValidatedCore(
        WatercolorOptions opts,
        Action<WatercolorStepProgress>? onStepCompleted,
        Action<WatercolorStageProgress>? onStageCompleted,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (SourceImage.Count == 0)
        {
            WatercolorResult emptyResult = CreateEmptyResult(ImageSize, opts);
            LastOptions = opts;
            LastResult = emptyResult;
            return emptyResult;
        }

        var ctx = new RenderContext(onStepCompleted, onStageCompleted, cancellationToken);

        RunColorTransfer(opts, ctx);
        RunSaliencyStage(opts, ctx);
        RunSegmentationStage(opts, ctx);
        RunLodAbstraction(opts, ctx);
        RunWetInWet(opts, ctx);
        RunHandTremor(opts, ctx);
        RunFinishingStage(opts, ctx);

        return BuildResult(opts, ctx.StepTimings);
    }

    private void RunColorTransfer(WatercolorOptions opts, RenderContext ctx)
    {
        bool enableColorTransfer = opts.EnableColorTransfer;
        bool active = enableColorTransfer && opts.ColorTransferStrength > 0.0;
        ColorTransferProfile? targetPalette = opts.TargetPalette;
        double transferStrength = opts.ColorTransferStrength;

        RunStep(
            _colorTransfer,
            default,
            (active, targetPalette, active ? transferStrength : 0.0),
            WatercolorStep.ColorTransfer,
            WatercolorStage.ColorTransfer,
            bypassed: !active,
            ctx,
            () =>
            {
                if (!enableColorTransfer)
                {
                    ColorTransferProfile palette = targetPalette ?? ColorTransfer.SelectClosestProfile(
                        ColorTransfer.ComputeStatistics(SourceImage, RgbColorSpace.SRgb, Illuminant.E),
                        WatercolorPalettes.Defaults,
                        WatercolorPalettes.DefaultFeatureWeights);
                    return (SourceImage.Clone(), palette);
                }

                if (targetPalette is not null)
                {
                    return (ColorTransfer.Transfer(SourceImage, targetPalette, strength: transferStrength), targetPalette);
                }

                return ColorTransfer.TransferToClosestProfile(
                    SourceImage,
                    WatercolorPalettes.Defaults,
                    featureWeights: WatercolorPalettes.DefaultFeatureWeights,
                    strength: transferStrength);
            });

        ctx.CompleteStage(WatercolorStage.ColorTransfer);
    }

    private void RunSaliencyStage(WatercolorOptions opts, RenderContext ctx)
    {
        double egbiBlurSigma = opts.EgbiBlurSigma;
        EgbiOptions egbiOptions = opts.Egbi;
        RegionalContrastSaliencyOptions rcOptions = opts.RegionalContrastSaliency;
        SaliencyCutOptions cutOptions = opts.SaliencyCut;
        SaliencyDistanceFieldOptions distOptions = opts.SaliencyDistanceField;
        double effectiveDistanceSigma = distOptions.SmoothingSigma
            ?? (distOptions.DefaultSigmaScale * Math.Min(ImageSize.Width, ImageSize.Height));

        RunStep(
            _saliency.PreBlur,
            new UpstreamVersions(_colorTransfer.Version),
            egbiBlurSigma,
            WatercolorStep.EgbiPreBlur,
            WatercolorStage.Saliency,
            bypassed: egbiBlurSigma == 0.0,
            ctx,
            () =>
            {
                Matrix<Rgb8> preBlurred = Blur.GaussianBlur(_colorTransfer.Value.Image, egbiBlurSigma);
                ctx.CancellationToken.ThrowIfCancellationRequested();
                Matrix<Lab32> labBlurred = preBlurred.ToLab32(RgbColorSpace.SRgb);
                return (preBlurred, labBlurred);
            });

        RunStep(
            _saliency.EgbiGraph,
            new UpstreamVersions(_saliency.PreBlur.Version),
            egbiOptions,
            WatercolorStep.EgbiSegmentation,
            WatercolorStage.Saliency,
            bypassed: false,
            ctx,
            () =>
            {
                IDisjointSet egbiSeg = EgbiSegmentation.Segment(_saliency.PreBlur.Value.Lab, egbiOptions, ctx.CancellationToken);
                ctx.CancellationToken.ThrowIfCancellationRequested();
                return egbiSeg.ToRegionGraph(egbiOptions.Connectivity);
            });

        RunStep(
            _saliency.SaliencyMap,
            new UpstreamVersions(_colorTransfer.Version, _saliency.EgbiGraph.Version),
            rcOptions,
            WatercolorStep.RegionalContrastSaliency,
            WatercolorStage.Saliency,
            bypassed: false,
            ctx,
            () => RegionalContrastSaliency.ComputeSaliency(
                _colorTransfer.Value.Image,
                _saliency.EgbiGraph.Value,
                rcOptions,
                ctx.CancellationToken));

        RunStep(
            _saliency.SalientMask,
            new UpstreamVersions(_colorTransfer.Version, _saliency.SaliencyMap.Version),
            cutOptions,
            WatercolorStep.SaliencyCut,
            WatercolorStage.Saliency,
            bypassed: false,
            ctx,
            () => SaliencyCut.Extract(
                _colorTransfer.Value.Image,
                _saliency.SaliencyMap.Value,
                cutOptions,
                ctx.CancellationToken));

        ctx.CancellationToken.ThrowIfCancellationRequested();
        long stepStart = Stopwatch.GetTimestamp();

        bool rawFromCache = EnsureSlot(
            _saliency.RawDistanceField,
            new UpstreamVersions(_saliency.SalientMask.Version),
            options: false,
            ctx.CancellationToken,
            () => SaliencyDistanceField.ComputeNormalizedField(_saliency.SalientMask.Value, ctx.CancellationToken));

        bool smoothFromCache = EnsureSlot(
            _saliency.DistanceField,
            new UpstreamVersions(_saliency.RawDistanceField.Version),
            effectiveDistanceSigma,
            ctx.CancellationToken,
            () => SaliencyDistanceField.SmoothNormalizedField(
                _saliency.RawDistanceField.Value,
                _saliency.SalientMask.Value,
                distOptions,
                ctx.CancellationToken));

        ctx.CompleteStep(
            WatercolorStep.SaliencyDistanceField,
            WatercolorStage.Saliency,
            stepStart,
            fromCache: rawFromCache && smoothFromCache,
            bypassed: false);
        ctx.CompleteStage(WatercolorStage.Saliency);
    }

    private void RunSegmentationStage(WatercolorOptions opts, RenderContext ctx)
    {
        MeanShiftOptions meanShiftOptions = opts.MeanShift;

        RunStep(
            _segmentation.Filtering,
            new UpstreamVersions(_colorTransfer.Version),
            (meanShiftOptions.SpatialRadius, meanShiftOptions.RangeRadius, meanShiftOptions.MaxIterations, meanShiftOptions.ConvergenceEpsilon),
            WatercolorStep.MeanShiftFiltering,
            WatercolorStage.Segmentation,
            bypassed: false,
            ctx,
            () => MeanShiftSegmentation.SeekModesAndFilter(_colorTransfer.Value.Image, meanShiftOptions, ctx.CancellationToken));

        RunStep(
            _segmentation.Clustering,
            new UpstreamVersions(_segmentation.Filtering.Version),
            (meanShiftOptions.MinRegionSize, meanShiftOptions.Connectivity),
            WatercolorStep.MeanShiftClustering,
            WatercolorStage.Segmentation,
            bypassed: false,
            ctx,
            () =>
            {
                var (modes, sourcePixels, filtered) = _segmentation.Filtering.Value;
                IDisjointSet meanShiftSeg = MeanShiftSegmentation.ClusterModes(
                    filtered,
                    sourcePixels,
                    modes,
                    meanShiftOptions,
                    ctx.CancellationToken);
                ctx.CancellationToken.ThrowIfCancellationRequested();
                return meanShiftSeg.ToRegionGraph(meanShiftOptions.Connectivity);
            });

        ctx.CompleteStage(WatercolorStage.Segmentation);
    }

    private void RunLodAbstraction(WatercolorOptions opts, RenderContext ctx)
    {
        bool enabled = opts.EnableLodAbstraction;
        LodAbstractionOptions lodOptions = opts.LodAbstraction;

        RunStep(
            _stylization.Lod,
            enabled
                ? new UpstreamVersions(_segmentation.Filtering.Version, _segmentation.Clustering.Version, _saliency.DistanceField.Version, _saliency.SalientMask.Version)
                : new UpstreamVersions(_segmentation.Filtering.Version),
            enabled ? lodOptions : null,
            WatercolorStep.LodAbstraction,
            WatercolorStage.LodAbstraction,
            bypassed: !enabled,
            ctx,
            () => enabled
                ? LodAbstraction.ApplyCore(
                    _segmentation.Filtering.Value.Filtered,
                    _segmentation.Clustering.Value,
                    _saliency.DistanceField.Value,
                    salientMask: null,
                    EnsureSalientFractions(ctx.CancellationToken),
                    lodOptions,
                    ctx.CancellationToken)
                : _segmentation.Filtering.Value.Filtered);

        ctx.CompleteStage(WatercolorStage.LodAbstraction);
    }

    private void RunWetInWet(WatercolorOptions opts, RenderContext ctx)
    {
        WetInWetOptions wetOptions = opts.WetInWet;
        bool wetActive = opts.EnableWetInWet && wetOptions.Mode != WetInWetMode.Disabled;

        RunStep(
            _stylization.WetInWet,
            wetActive
                ? new UpstreamVersions(_stylization.Lod.Version, _segmentation.Clustering.Version, _saliency.SalientMask.Version)
                : new UpstreamVersions(_stylization.Lod.Version, _segmentation.Clustering.Version),
            wetActive ? wetOptions : null,
            WatercolorStep.WetInWet,
            WatercolorStage.WetInWet,
            bypassed: !wetActive,
            ctx,
            () => wetActive
                ? WetInWet.ApplyDetailedCore(
                    _stylization.Lod.Value,
                    _segmentation.Clustering.Value,
                    salientMask: null,
                    EnsureSalientFractions(ctx.CancellationToken),
                    wetOptions,
                    cancellationToken: ctx.CancellationToken)
                : new WetInWetResult(
                    _stylization.Lod.Value,
                    new MaskMatrix(ImageSize),
                    new bool[_segmentation.Clustering.Value.Boundaries.Count]));

        ctx.CompleteStage(WatercolorStage.WetInWet);
    }

    private void RunHandTremor(WatercolorOptions opts, RenderContext ctx)
    {
        HandTremorOptions tremorOptions = opts.HandTremor;
        bool tremorActive = opts.EnableHandTremor && tremorOptions.Amplitude > 0.0;

        RunStep(
            _stylization.HandTremor,
            tremorActive
                ? new UpstreamVersions(_stylization.WetInWet.Version, _segmentation.Clustering.Version, _saliency.DistanceField.Version)
                : new UpstreamVersions(_stylization.WetInWet.Version),
            tremorActive ? tremorOptions : null,
            WatercolorStep.HandTremor,
            WatercolorStage.HandTremor,
            bypassed: !tremorActive,
            ctx,
            () => tremorActive
                ? HandTremor.ApplyCore(
                    _stylization.WetInWet.Value.Image,
                    _segmentation.Clustering.Value,
                    _saliency.DistanceField.Value,
                    _stylization.WetInWet.Value,
                    tremorOptions,
                    ctx.CancellationToken)
                : _stylization.WetInWet.Value.Image);

        ctx.CompleteStage(WatercolorStage.HandTremor);
    }

    private void RunFinishingStage(WatercolorOptions opts, RenderContext ctx)
    {
        EdgeDarkeningOptions edgeOptions = opts.EdgeDarkening;
        bool edgeActive = opts.EnableEdgeDarkening && edgeOptions.Intensity > 0.0;
        RunStep(
            _finishing.EdgeDensity,
            edgeActive ? new UpstreamVersions(_stylization.HandTremor.Version) : default,
            edgeActive ? edgeOptions : null,
            WatercolorStep.EdgeDarkening,
            WatercolorStage.Finishing,
            bypassed: !edgeActive,
            ctx,
            () => edgeActive
                ? EdgeDarkening.ComputeDensityField(_stylization.HandTremor.Value, edgeOptions, ctx.CancellationToken)
                : null);

        TurbulenceFlowOptions turbOptions = opts.TurbulenceFlow;
        bool turbulenceActive = opts.EnableTurbulenceFlow && turbOptions.Intensity > 0.0;
        RunStep(
            _finishing.TurbulenceDensity,
            default,
            turbulenceActive ? turbOptions : null,
            WatercolorStep.TurbulenceFlow,
            WatercolorStage.Finishing,
            bypassed: !turbulenceActive,
            ctx,
            () => turbulenceActive
                ? TurbulenceFlow.ComputeDensityField(ImageSize, turbOptions, ctx.CancellationToken)
                : null);

        Matrix<DoublePixel>? edgeDensity = _finishing.EdgeDensity.Value;
        Matrix<DoublePixel>? turbulenceDensity = _finishing.TurbulenceDensity.Value;
        bool pigmentBypassed = edgeDensity is null && turbulenceDensity is null;
        RunStep(
            _finishing.PigmentCombine,
            new UpstreamVersions(_stylization.HandTremor.Version, _finishing.EdgeDensity.Version, _finishing.TurbulenceDensity.Version),
            options: false,
            WatercolorStep.PigmentCombine,
            WatercolorStage.Finishing,
            bypassed: pigmentBypassed,
            ctx,
            () =>
            {
                if (pigmentBypassed)
                {
                    return _stylization.HandTremor.Value;
                }

                // Never mutate cached EdgeDensityField or TurbulenceDensityField when combining them.
                IReadOnlyMatrix<DoublePixel> combinedDensity = edgeDensity is not null && turbulenceDensity is not null
                    ? SumDensityFields(edgeDensity, turbulenceDensity, ImageSize, ctx.CancellationToken)
                    : (edgeDensity ?? turbulenceDensity!);

                return PigmentBlend.ApplyDensityField(
                    _stylization.HandTremor.Value,
                    combinedDensity,
                    cancellationToken: ctx.CancellationToken);
            });

        bool toneEnabled = opts.EnableToneCurve;
        PiecewiseToneCurveOptions toneOptions = opts.ToneCurve;
        RunStep(
            _finishing.ToneCurve,
            new UpstreamVersions(_finishing.PigmentCombine.Version),
            toneEnabled ? toneOptions : null,
            WatercolorStep.ToneCurve,
            WatercolorStage.Finishing,
            bypassed: !toneEnabled,
            ctx,
            () => toneEnabled
                ? PiecewiseToneCurve.Apply(_finishing.PigmentCombine.Value, toneOptions, ctx.CancellationToken)
                : _finishing.PigmentCombine.Value);

        PaperGranulationOptions granOptions = opts.PaperGranulation;
        bool granActive = opts.EnablePaperGranulation && (granOptions.DensityStrength > 0.0 || granOptions.WarpAmplitude > 0.0);
        bool isProceduralPaper = _customPaperHeightMap is null;
        bool generateProcedural = granActive && isProceduralPaper;
        PerlinNoiseOptions grainNoise = granOptions.GrainNoise;
        RunStep(
            _finishing.PaperHeightMap,
            default,
            (granActive, generateProcedural ? grainNoise : null),
            WatercolorStep.PaperHeightMap,
            WatercolorStage.Finishing,
            bypassed: !generateProcedural,
            ctx,
            () => !granActive
                ? null
                : _customPaperHeightMap ?? PaperGranulation.GeneratePaperHeightMap(
                    ImageSize,
                    grainNoise,
                    ctx.CancellationToken));

        RunStep(
            _finishing.PaperGranulation,
            granActive
                ? new UpstreamVersions(_finishing.ToneCurve.Version, _finishing.PaperHeightMap.Version)
                : new UpstreamVersions(_finishing.ToneCurve.Version),
            granActive
                ? (true, granOptions.DensityStrength, granOptions.WarpAmplitude, isProceduralPaper ? grainNoise.Frequency : 0.0)
                : (false, 0.0, 0.0, 0.0),
            WatercolorStep.PaperGranulation,
            WatercolorStage.Finishing,
            bypassed: !granActive,
            ctx,
            () => granActive
                ? PaperGranulation.ApplyCore(
                    _finishing.ToneCurve.Value,
                    _finishing.PaperHeightMap.Value!,
                    isProcedural: isProceduralPaper,
                    granOptions,
                    ctx.CancellationToken)
                : _finishing.ToneCurve.Value);

        bool fxaaEnabled = opts.EnableFxaa;
        FxaaOptions fxaaOptions = opts.Fxaa;
        RunStep(
            _finishing.Fxaa,
            new UpstreamVersions(_finishing.PaperGranulation.Version),
            fxaaEnabled ? fxaaOptions : null,
            WatercolorStep.Fxaa,
            WatercolorStage.Finishing,
            bypassed: !fxaaEnabled,
            ctx,
            () => fxaaEnabled
                ? Fxaa.Apply(_finishing.PaperGranulation.Value, fxaaOptions, ctx.CancellationToken)
                : _finishing.PaperGranulation.Value);

        ctx.CompleteStage(WatercolorStage.Finishing);
    }

    private WatercolorResult BuildResult(
        WatercolorOptions opts,
        Dictionary<WatercolorStep, WatercolorStepTiming> stepTimings)
    {
        var result = new WatercolorResult(
            _finishing.Fxaa.Value,
            _colorTransfer.Value.Image,
            _colorTransfer.Value.Palette,
            _saliency.PreBlur.Value.Rgb,
            _saliency.EgbiGraph.Value,
            _saliency.SaliencyMap.Value,
            _saliency.SalientMask.Value,
            _saliency.RawDistanceField.Value,
            _saliency.DistanceField.Value,
            _segmentation.Filtering.Value.Filtered,
            _segmentation.Clustering.Value,
            _stylization.Lod.Value,
            _stylization.WetInWet.Value,
            _stylization.HandTremor.Value,
            _finishing.EdgeDensity.Value,
            _finishing.TurbulenceDensity.Value,
            _finishing.PigmentCombine.Value,
            _finishing.ToneCurve.Value,
            _finishing.PaperHeightMap.Value,
            _finishing.PaperGranulation.Value,
            stepTimings);

        LastOptions = opts;
        LastResult = result;
        return result;
    }

    private double[] EnsureSalientFractions(CancellationToken cancellationToken)
    {
        EnsureSlot(
            _segmentation.SalientFractions,
            new UpstreamVersions(_segmentation.Clustering.Version, _saliency.SalientMask.Version),
            options: false,
            cancellationToken,
            () => _segmentation.Clustering.Value.ComputeRegionFractions(_saliency.SalientMask.Value));

        return _segmentation.SalientFractions.Value;
    }

    private TValue RunStep<TOptions, TValue>(
        CacheSlot<TOptions, TValue> slot,
        in UpstreamVersions upstream,
        TOptions? options,
        WatercolorStep step,
        WatercolorStage stage,
        bool bypassed,
        RenderContext ctx,
        Func<TValue> compute)
    {
        ctx.CancellationToken.ThrowIfCancellationRequested();
        long stepStart = Stopwatch.GetTimestamp();
        bool fromCache = EnsureSlot(slot, in upstream, options, ctx.CancellationToken, compute);
        ctx.CompleteStep(step, stage, stepStart, fromCache, bypassed);
        return slot.Value;
    }

    private bool EnsureSlot<TOptions, TValue>(
        CacheSlot<TOptions, TValue> slot,
        in UpstreamVersions upstream,
        TOptions? options,
        CancellationToken cancellationToken,
        Func<TValue> compute)
    {
        if (slot.Matches(in upstream, options))
        {
            return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        slot.Clear();
        TValue computed = compute();
        slot.Set(in upstream, options, computed, NextVersion());
        return false;
    }

    private static SequentialMatrix<DoublePixel> SumDensityFields(
        IReadOnlyMatrix<DoublePixel> edgeDensityField,
        IReadOnlyMatrix<DoublePixel> turbulenceDensityField,
        Size size,
        CancellationToken cancellationToken)
    {
        var summed = new SequentialMatrix<DoublePixel>(size);
        DoublePixel[] summedRaw = summed.RawData;

        if (edgeDensityField is SequentialMatrix<DoublePixel> seqEdge &&
            turbulenceDensityField is SequentialMatrix<DoublePixel> seqTurb)
        {
            DoublePixel[] edgeRaw = seqEdge.RawData;
            DoublePixel[] turbRaw = seqTurb.RawData;
            ParallelRunner.ForChunks(
                summedRaw.Length,
                cancellationToken,
                (start, end) =>
                {
                    for (int i = start; i < end; i++)
                        summedRaw[i] = new DoublePixel(edgeRaw[i].Value + turbRaw[i].Value);
                });
        }
        else
        {
            ParallelRunner.ForChunks(
                summedRaw.Length,
                cancellationToken,
                (start, end) =>
                {
                    for (int i = start; i < end; i++)
                        summedRaw[i] = new DoublePixel(edgeDensityField[i].Value + turbulenceDensityField[i].Value);
                });
        }

        return summed;
    }

    private static WatercolorResult CreateEmptyResult(Size size, WatercolorOptions options)
    {
        var emptyRgb = new SequentialMatrix<Rgb8>(size);
        var emptyDouble = new SequentialMatrix<DoublePixel>(size);
        var emptyMask = new MaskMatrix(size);
        GridConnectivity egbiConnectivity = options.Egbi.Connectivity;
        var emptyEgbiGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(size), egbiConnectivity);
        GridConnectivity meanShiftConnectivity = options.MeanShift.Connectivity;
        RegionGraph emptyGraph = meanShiftConnectivity == egbiConnectivity
            ? emptyEgbiGraph
            : RegionGraph.FromLabels(new SequentialMatrix<int>(size), meanShiftConnectivity);
        var emptyWetInWet = new WetInWetResult(emptyRgb, emptyMask, []);

        return new WatercolorResult(
            emptyRgb,
            emptyRgb,
            options.TargetPalette ?? WatercolorPalettes.Defaults[0],
            emptyRgb,
            emptyEgbiGraph,
            emptyDouble,
            emptyMask,
            emptyDouble,
            emptyDouble,
            emptyRgb,
            emptyGraph,
            emptyRgb,
            emptyWetInWet,
            emptyRgb,
            edgeDensityField: null,
            turbulenceDensityField: null,
            emptyRgb,
            emptyRgb,
            paperHeightMap: null,
            emptyRgb,
            ReadOnlyDictionary<WatercolorStep, WatercolorStepTiming>.Empty);
    }

    private void EnterExclusive()
    {
        if (Interlocked.CompareExchange(ref _isBusy, 1, 0) != 0)
        {
            throw new InvalidOperationException(BusyErrorMessage);
        }
    }

    private void ExitExclusive() => Interlocked.Exchange(ref _isBusy, 0);

    private long NextVersion() => ++_nextVersion;

    private void ClearAllCacheState()
    {
        foreach (ICacheSlot slot in _allSlots)
        {
            slot.Clear();
        }

        LastOptions = null;
        LastResult = null;
    }

    private sealed class SaliencyCache
    {
        public CacheSlot<double, (Matrix<Rgb8> Rgb, Matrix<Lab32> Lab)> PreBlur { get; } = new();
        public CacheSlot<EgbiOptions, RegionGraph> EgbiGraph { get; } = new();
        public CacheSlot<RegionalContrastSaliencyOptions, Matrix<DoublePixel>> SaliencyMap { get; } = new();
        public CacheSlot<SaliencyCutOptions, MaskMatrix> SalientMask { get; } = new();
        public CacheSlot<bool, Matrix<DoublePixel>> RawDistanceField { get; } = new();
        public CacheSlot<double, Matrix<DoublePixel>> DistanceField { get; } = new();
    }

    private sealed class SegmentationCache
    {
        public CacheSlot<(int SpatialRadius, double RangeRadius, int MaxIterations, double ConvergenceEpsilon), (MeanShiftMode[] Modes, Lab32[] SourcePixels, Matrix<Rgb8> Filtered)> Filtering { get; } = new();
        public CacheSlot<(int MinRegionSize, GridConnectivity Connectivity), RegionGraph> Clustering { get; } = new();
        public CacheSlot<bool, double[]> SalientFractions { get; } = new();
    }

    private sealed class StylizationCache
    {
        public CacheSlot<LodAbstractionOptions?, Matrix<Rgb8>> Lod { get; } = new();
        public CacheSlot<WetInWetOptions?, WetInWetResult> WetInWet { get; } = new();
        public CacheSlot<HandTremorOptions?, Matrix<Rgb8>> HandTremor { get; } = new();
    }

    private sealed class FinishingCache
    {
        public CacheSlot<EdgeDarkeningOptions?, Matrix<DoublePixel>?> EdgeDensity { get; } = new();
        public CacheSlot<TurbulenceFlowOptions?, Matrix<DoublePixel>?> TurbulenceDensity { get; } = new();
        public CacheSlot<bool, Matrix<Rgb8>> PigmentCombine { get; } = new();
        public CacheSlot<PiecewiseToneCurveOptions?, Matrix<Rgb8>> ToneCurve { get; } = new();
        public CacheSlot<(bool GranActive, PerlinNoiseOptions? ProceduralGrainNoise), IReadOnlyMatrix<DoublePixel>?> PaperHeightMap { get; } = new();
        public CacheSlot<(bool Active, double DensityStrength, double WarpAmplitude, double ProceduralFrequency), Matrix<Rgb8>> PaperGranulation { get; } = new();
        public CacheSlot<FxaaOptions?, Matrix<Rgb8>> Fxaa { get; } = new();
    }

    private sealed class RenderContext(
        Action<WatercolorStepProgress>? onStepCompleted,
        Action<WatercolorStageProgress>? onStageCompleted,
        CancellationToken cancellationToken)
    {
        private long _stageStart = onStageCompleted is not null ? Stopwatch.GetTimestamp() : 0;

        public Dictionary<WatercolorStep, WatercolorStepTiming> StepTimings { get; } = new(TotalStepCount);

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public void CompleteStep(
            WatercolorStep step,
            WatercolorStage stage,
            long stepStart,
            bool fromCache,
            bool bypassed)
        {
            TimeSpan elapsed = Stopwatch.GetElapsedTime(stepStart);
            StepTimings[step] = new WatercolorStepTiming(elapsed, FromCache: fromCache, Bypassed: bypassed);
            onStepCompleted?.Invoke(new WatercolorStepProgress(step, stage, elapsed, FromCache: fromCache, Bypassed: bypassed));
        }

        public void CompleteStage(WatercolorStage stage)
        {
            if (onStageCompleted is null)
            {
                return;
            }

            onStageCompleted(new WatercolorStageProgress(stage, Stopwatch.GetElapsedTime(_stageStart)));
            _stageStart = Stopwatch.GetTimestamp();
        }
    }
}
