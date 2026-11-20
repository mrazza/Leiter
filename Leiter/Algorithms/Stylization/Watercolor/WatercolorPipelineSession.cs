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
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Segmentation.Internal;
using Leiter.Algorithms.Stylization;
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
    private int _isBusy;
    private long _nextVersion;

    // Stage 1: ColorTransfer
    private ColorTransferKey _colorTransferKey;
    private Matrix<Rgb8>? _colorTransferred;
    private ColorTransferProfile? _selectedPalette;
    private long _colorTransferVersion;

    // Stage 2a: EgbiPreBlur
    private EgbiPreBlurKey _egbiPreBlurKey;
    private Matrix<Rgb8>? _egbiPreBlurred;
    private Matrix<Lab32>? _egbiLabBlurred;
    private long _egbiPreBlurVersion;

    // Stage 2b: EgbiSegmentation
    private EgbiKey _egbiKey;
    private RegionGraph? _egbiGraph;
    private long _egbiVersion;

    // Stage 2c: RegionalContrastSaliency
    private RegionalContrastSaliencyKey _rcSaliencyKey;
    private Matrix<DoublePixel>? _saliencyMap;
    private long _rcSaliencyVersion;

    // Stage 2d: SaliencyCut
    private SaliencyCutKey _saliencyCutKey;
    private MaskMatrix? _salientMask;
    private long _saliencyCutVersion;

    // Stage 2e-2f: SaliencyDistanceField (raw 1+JFA + smoothed)
    private long _rawDistanceFieldSalientMaskVersion;
    private Matrix<DoublePixel>? _rawDistanceField;
    private long _rawDistanceFieldVersion;

    private SaliencyDistanceFieldKey _distanceFieldKey;
    private Matrix<DoublePixel>? _distanceField;
    private long _distanceFieldVersion;

    // Stage 3a-i: MeanShiftFiltering (modes + source Lab + filtered RGB)
    private MeanShiftFilterKey _meanShiftFilterKey;
    private MeanShiftMode[]? _meanShiftModes;
    private Lab32[]? _meanShiftSourcePixels;
    private Matrix<Rgb8>? _meanShiftFiltered;
    private long _meanShiftFilterVersion;

    // Stage 3a-ii: MeanShiftClustering (RegionGraph)
    private MeanShiftClusterKey _meanShiftClusterKey;
    private RegionGraph? _segmentationGraph;
    private long _meanShiftClusterVersion;

    // Internal join intermediate: SalientFractions (MeanShiftClustering + SaliencyCut)
    private SalientFractionsKey _salientFractionsKey;
    private double[]? _salientFractions;

    // Stage 3b: LodAbstraction
    private LodAbstractionKey _lodKey;
    private Matrix<Rgb8>? _lodAbstracted;
    private long _lodVersion;

    // Stage 4: WetInWet
    private WetInWetKey _wetInWetKey;
    private WetInWetResult? _wetInWet;
    private long _wetInWetVersion;

    // Stage 5: HandTremor
    private HandTremorKey _handTremorKey;
    private Matrix<Rgb8>? _handTremor;
    private long _handTremorVersion;

    // Stage 6a-i: EdgeDarkening (density field)
    private EdgeDarkeningKey _edgeDarkeningKey;
    private bool _hasEdgeDensity;
    private Matrix<DoublePixel>? _edgeDensityField;
    private long _edgeDensityVersion;

    // Stage 6a-ii: TurbulenceFlow (density field)
    private TurbulenceFlowKey _turbulenceFlowKey;
    private bool _hasTurbulenceDensity;
    private Matrix<DoublePixel>? _turbulenceDensityField;
    private long _turbulenceDensityVersion;

    // Stage 6a-iii: PigmentCombine
    private PigmentCombineKey _pigmentCombineKey;
    private Matrix<Rgb8>? _densityAdjusted;
    private long _pigmentCombineVersion;

    // Stage 6b: ToneCurve
    private ToneCurveKey _toneCurveKey;
    private Matrix<Rgb8>? _toneAdjusted;
    private long _toneCurveVersion;

    // Stage 6c-i: PaperHeightMap
    private PaperHeightMapKey _paperHeightMapKey;
    private bool _hasPaperHeightMap;
    private IReadOnlyMatrix<DoublePixel>? _paperHeightMap;
    private long _paperHeightMapVersion;

    // Stage 6c-ii: PaperGranulation
    private PaperGranulationKey _paperGranulationKey;
    private Matrix<Rgb8>? _granulated;
    private long _paperGranulationVersion;

    // Stage 6d: Fxaa
    private FxaaKey _fxaaKey;
    private Matrix<Rgb8>? _output;

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

        // Resolve every nullable sub-option canonically before DAG key diffing so switching
        // between null and an explicit default record never triggers false cache misses.
        EgbiOptions resolvedEgbi = opts.Egbi ?? WatercolorOptions.DefaultSaliencyEgbi;
        RegionalContrastSaliencyOptions resolvedRc = opts.RegionalContrastSaliency ?? RegionalContrastSaliencyOptions.Default;

        SaliencyCutOptions rawCut = opts.SaliencyCut ?? SaliencyCutOptions.Default;
        SaliencyCutOptions resolvedCut = rawCut with
        {
            GrabCut = rawCut.GrabCut ?? SaliencyCutOptions.DefaultGrabCut,
            Mask = rawCut.Mask ?? SaliencyCutOptions.DefaultMask
        };

        SaliencyDistanceFieldOptions resolvedDist = opts.SaliencyDistanceField ?? SaliencyDistanceFieldOptions.Default;
        double effectiveDistanceSigma = resolvedDist.SmoothingSigma
            ?? (resolvedDist.DefaultSigmaScale * Math.Min(ImageSize.Width, ImageSize.Height));

        MeanShiftOptions resolvedMeanShift = opts.MeanShift ?? MeanShiftOptions.Default;
        LodAbstractionOptions resolvedLod = opts.LodAbstraction ?? LodAbstractionOptions.Default;

        WetInWetOptions rawWet = opts.WetInWet ?? WetInWetOptions.Default;
        WetInWetOptions resolvedWet = rawWet with
        {
            EllipticalFilter = rawWet.EllipticalFilter ?? WetInWetOptions.DefaultEllipticalFilter
        };

        HandTremorOptions rawTremor = opts.HandTremor ?? HandTremorOptions.Default;
        HandTremorOptions resolvedTremor = rawTremor with
        {
            PaperColor = rawTremor.PaperColor ?? HandTremorOptions.DefaultPaperColor,
            PrimaryNoise = rawTremor.PrimaryNoise ?? HandTremorOptions.DefaultPrimaryNoise,
            SecondaryNoise = rawTremor.SecondaryNoise ?? HandTremorOptions.DefaultSecondaryNoise
        };

        EdgeDarkeningOptions resolvedEdge = opts.EdgeDarkening ?? EdgeDarkeningOptions.Default;

        TurbulenceFlowOptions rawTurb = opts.TurbulenceFlow ?? TurbulenceFlowOptions.Default;
        TurbulenceFlowOptions resolvedTurb = rawTurb with
        {
            Noise = rawTurb.Noise ?? TurbulenceFlowOptions.DefaultNoise
        };

        PiecewiseToneCurveOptions resolvedTone = opts.ToneCurve ?? PiecewiseToneCurveOptions.Default;

        PaperGranulationOptions rawGran = opts.PaperGranulation ?? PaperGranulationOptions.Default;
        PerlinNoiseOptions resolvedGrainNoise = rawGran.GrainNoise ?? PaperGranulationOptions.DefaultGrainNoise;
        PaperGranulationOptions resolvedGran = rawGran with
        {
            GrainNoise = resolvedGrainNoise
        };

        FxaaOptions resolvedFxaa = opts.Fxaa ?? FxaaOptions.Default;

        var stepTimings = new Dictionary<WatercolorStep, WatercolorStepTiming>(TotalStepCount);
        long stageStart = onStageCompleted is not null ? Stopwatch.GetTimestamp() : 0;

        // =========================================================================
        // Stage 1: Reinhard Color Transfer
        // =========================================================================
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool colorTransferActive = opts.EnableColorTransfer && opts.ColorTransferStrength > 0.0;
            bool colorTransferBypassed = !colorTransferActive;
            var key = new ColorTransferKey(
                colorTransferActive,
                opts.TargetPalette,
                colorTransferActive ? opts.ColorTransferStrength : 0.0);

            bool fromCache = _colorTransferred is not null &&
                             _selectedPalette is not null &&
                             _colorTransferKey == key;

            if (!fromCache)
            {
                InvalidateFromColorTransfer();

                Matrix<Rgb8> colorTransferred;
                ColorTransferProfile selectedPalette;
                if (!opts.EnableColorTransfer)
                {
                    colorTransferred = SourceImage.Clone();
                    selectedPalette = opts.TargetPalette ?? ColorTransfer.SelectClosestProfile(
                        ColorTransfer.ComputeStatistics(SourceImage, RgbColorSpace.SRgb, Illuminant.E),
                        WatercolorPalettes.Defaults,
                        WatercolorPalettes.DefaultFeatureWeights);
                }
                else if (opts.TargetPalette is not null)
                {
                    colorTransferred = ColorTransfer.Transfer(SourceImage, opts.TargetPalette, strength: opts.ColorTransferStrength);
                    selectedPalette = opts.TargetPalette;
                }
                else
                {
                    (colorTransferred, selectedPalette) = ColorTransfer.TransferToClosestProfile(
                        SourceImage,
                        WatercolorPalettes.Defaults,
                        featureWeights: WatercolorPalettes.DefaultFeatureWeights,
                        strength: opts.ColorTransferStrength);
                }

                _colorTransferred = colorTransferred;
                _selectedPalette = selectedPalette;
                _colorTransferKey = key;
                _colorTransferVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.ColorTransfer,
                WatercolorStage.ColorTransfer,
                stepStart,
                fromCache,
                colorTransferBypassed,
                onStepCompleted);
            CompleteStage(onStageCompleted, WatercolorStage.ColorTransfer, ref stageStart);
        }

        // =========================================================================
        // Stage 2: Regional Contrast Saliency & Saliency Distance Field
        // =========================================================================
        // 2a: EgbiPreBlur
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool bypassed = opts.EgbiBlurSigma == 0.0;
            var key = new EgbiPreBlurKey(_colorTransferVersion, opts.EgbiBlurSigma);
            bool fromCache = _egbiPreBlurred is not null &&
                             _egbiLabBlurred is not null &&
                             _egbiPreBlurKey == key;

            if (!fromCache)
            {
                InvalidateFromEgbiPreBlur();

                Matrix<Rgb8> egbiPreBlurred = Blur.GaussianBlur(_colorTransferred!, opts.EgbiBlurSigma);
                cancellationToken.ThrowIfCancellationRequested();
                Matrix<Lab32> labBlurred = egbiPreBlurred.ToLab32(RgbColorSpace.SRgb);

                _egbiPreBlurred = egbiPreBlurred;
                _egbiLabBlurred = labBlurred;
                _egbiPreBlurKey = key;
                _egbiPreBlurVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.EgbiPreBlur,
                WatercolorStage.Saliency,
                stepStart,
                fromCache,
                bypassed,
                onStepCompleted);
        }

        // 2b: EgbiSegmentation
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            var key = new EgbiKey(_egbiPreBlurVersion, resolvedEgbi);
            bool fromCache = _egbiGraph is not null && _egbiKey == key;

            if (!fromCache)
            {
                InvalidateFromEgbiSegmentation();

                IDisjointSet egbiSeg = EgbiSegmentation.Segment(_egbiLabBlurred!, resolvedEgbi, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                RegionGraph egbiGraph = egbiSeg.ToRegionGraph(resolvedEgbi.Connectivity);

                _egbiGraph = egbiGraph;
                _egbiKey = key;
                _egbiVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.EgbiSegmentation,
                WatercolorStage.Saliency,
                stepStart,
                fromCache,
                bypassed: false,
                onStepCompleted);
        }

        // 2c: RegionalContrastSaliency
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            var key = new RegionalContrastSaliencyKey(_colorTransferVersion, _egbiVersion, resolvedRc);
            bool fromCache = _saliencyMap is not null && _rcSaliencyKey == key;

            if (!fromCache)
            {
                InvalidateFromRegionalContrastSaliency();

                Matrix<DoublePixel> saliencyMap = RegionalContrastSaliency.ComputeSaliency(
                    _colorTransferred!,
                    _egbiGraph!,
                    resolvedRc,
                    cancellationToken);

                _saliencyMap = saliencyMap;
                _rcSaliencyKey = key;
                _rcSaliencyVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.RegionalContrastSaliency,
                WatercolorStage.Saliency,
                stepStart,
                fromCache,
                bypassed: false,
                onStepCompleted);
        }

        // 2d: SaliencyCut
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            var key = new SaliencyCutKey(_colorTransferVersion, _rcSaliencyVersion, resolvedCut);
            bool fromCache = _salientMask is not null && _saliencyCutKey == key;

            if (!fromCache)
            {
                InvalidateFromSaliencyCut();

                MaskMatrix salientMask = SaliencyCut.Extract(
                    _colorTransferred!,
                    _saliencyMap!,
                    resolvedCut,
                    cancellationToken);

                _salientMask = salientMask;
                _saliencyCutKey = key;
                _saliencyCutVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.SaliencyCut,
                WatercolorStage.Saliency,
                stepStart,
                fromCache,
                bypassed: false,
                onStepCompleted);
        }

        // 2e-2f: SaliencyDistanceField (Raw 1+JFA cached separately from Gaussian smoothing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool rawFromCache = _rawDistanceField is not null &&
                                _rawDistanceFieldSalientMaskVersion == _saliencyCutVersion;
            if (!rawFromCache)
            {
                InvalidateFromRawDistanceField();

                Matrix<DoublePixel> rawDistanceField = SaliencyDistanceField.ComputeNormalizedField(
                    _salientMask!,
                    cancellationToken);

                _rawDistanceField = rawDistanceField;
                _rawDistanceFieldSalientMaskVersion = _saliencyCutVersion;
                _rawDistanceFieldVersion = NextVersion();
            }

            var smoothKey = new SaliencyDistanceFieldKey(_rawDistanceFieldVersion, effectiveDistanceSigma);
            bool smoothFromCache = _distanceField is not null && _distanceFieldKey == smoothKey;
            if (!smoothFromCache)
            {
                InvalidateFromDistanceField();

                Matrix<DoublePixel> distanceField = SaliencyDistanceField.SmoothNormalizedField(
                    _rawDistanceField!,
                    _salientMask!,
                    resolvedDist,
                    cancellationToken);

                _distanceField = distanceField;
                _distanceFieldKey = smoothKey;
                _distanceFieldVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.SaliencyDistanceField,
                WatercolorStage.Saliency,
                stepStart,
                fromCache: rawFromCache && smoothFromCache,
                bypassed: false,
                onStepCompleted);
            CompleteStage(onStageCompleted, WatercolorStage.Saliency, ref stageStart);
        }

        // =========================================================================
        // Stage 3a: Mean-Shift Segmentation (independent branch off ColorTransfer)
        // =========================================================================
        // 3a-i: MeanShiftFiltering (5D mode seeking + RGB filtering)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            var key = new MeanShiftFilterKey(
                _colorTransferVersion,
                resolvedMeanShift.SpatialRadius,
                resolvedMeanShift.RangeRadius,
                resolvedMeanShift.MaxIterations,
                resolvedMeanShift.ConvergenceEpsilon);
            bool fromCache = _meanShiftModes is not null &&
                             _meanShiftSourcePixels is not null &&
                             _meanShiftFiltered is not null &&
                             _meanShiftFilterKey == key;

            if (!fromCache)
            {
                InvalidateFromMeanShiftFiltering();

                (MeanShiftMode[] modes, Lab32[] sourcePixels, Matrix<Rgb8> filteredImage) =
                    MeanShiftSegmentation.SeekModesAndFilter(_colorTransferred!, resolvedMeanShift, cancellationToken);

                _meanShiftModes = modes;
                _meanShiftSourcePixels = sourcePixels;
                _meanShiftFiltered = filteredImage;
                _meanShiftFilterKey = key;
                _meanShiftFilterVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.MeanShiftFiltering,
                WatercolorStage.Segmentation,
                stepStart,
                fromCache,
                bypassed: false,
                onStepCompleted);
        }

        // 3a-ii: MeanShiftClustering (mode clustering + region graph)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            var key = new MeanShiftClusterKey(
                _meanShiftFilterVersion,
                resolvedMeanShift.MinRegionSize,
                resolvedMeanShift.Connectivity);
            bool fromCache = _segmentationGraph is not null && _meanShiftClusterKey == key;

            if (!fromCache)
            {
                InvalidateFromMeanShiftClustering();

                IDisjointSet meanShiftSeg = MeanShiftSegmentation.ClusterModes(
                    _meanShiftFiltered!,
                    _meanShiftSourcePixels!,
                    _meanShiftModes!,
                    resolvedMeanShift,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                RegionGraph regionGraph = meanShiftSeg.ToRegionGraph(resolvedMeanShift.Connectivity);

                _segmentationGraph = regionGraph;
                _meanShiftClusterKey = key;
                _meanShiftClusterVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.MeanShiftClustering,
                WatercolorStage.Segmentation,
                stepStart,
                fromCache,
                bypassed: false,
                onStepCompleted);
            CompleteStage(onStageCompleted, WatercolorStage.Segmentation, ref stageStart);
        }

        // =========================================================================
        // Stage 3b: Saliency-Guided LOD Abstraction
        // =========================================================================
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool enabled = opts.EnableLodAbstraction;
            var key = enabled
                ? new LodAbstractionKey(
                    Enabled: true,
                    MeanShiftFilterVersion: _meanShiftFilterVersion,
                    MeanShiftClusterVersion: _meanShiftClusterVersion,
                    DistanceFieldVersion: _distanceFieldVersion,
                    SaliencyCutVersion: _saliencyCutVersion,
                    Options: resolvedLod)
                : new LodAbstractionKey(
                    Enabled: false,
                    MeanShiftFilterVersion: _meanShiftFilterVersion,
                    MeanShiftClusterVersion: 0,
                    DistanceFieldVersion: 0,
                    SaliencyCutVersion: 0,
                    Options: null);

            bool fromCache = _lodAbstracted is not null && _lodKey == key;

            if (!fromCache)
            {
                InvalidateFromLodAbstraction();

                Matrix<Rgb8> lodAbstracted;
                if (enabled)
                {
                    double[] salientFractions = EnsureSalientFractions(cancellationToken);
                    lodAbstracted = LodAbstraction.ApplyCore(
                        _meanShiftFiltered!,
                        _segmentationGraph!,
                        _distanceField!,
                        salientMask: null,
                        salientFractions,
                        resolvedLod,
                        cancellationToken);
                }
                else
                {
                    lodAbstracted = _meanShiftFiltered!;
                }

                _lodAbstracted = lodAbstracted;
                _lodKey = key;
                _lodVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.LodAbstraction,
                WatercolorStage.LodAbstraction,
                stepStart,
                fromCache,
                bypassed: !enabled,
                onStepCompleted);
            CompleteStage(onStageCompleted, WatercolorStage.LodAbstraction, ref stageStart);
        }

        // =========================================================================
        // Stage 4: Wet-in-Wet Bleeding
        // =========================================================================
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool wetActive = opts.EnableWetInWet && resolvedWet.Mode != WetInWetMode.Disabled;
            var key = wetActive
                ? new WetInWetKey(
                    Active: true,
                    LodVersion: _lodVersion,
                    MeanShiftClusterVersion: _meanShiftClusterVersion,
                    SaliencyCutVersion: _saliencyCutVersion,
                    Options: resolvedWet)
                : new WetInWetKey(
                    Active: false,
                    LodVersion: _lodVersion,
                    MeanShiftClusterVersion: _meanShiftClusterVersion,
                    SaliencyCutVersion: 0,
                    Options: null);

            bool fromCache = _wetInWet is not null && _wetInWetKey == key;

            if (!fromCache)
            {
                InvalidateFromWetInWet();

                WetInWetResult wetInWet;
                if (wetActive)
                {
                    double[] salientFractions = EnsureSalientFractions(cancellationToken);
                    wetInWet = WetInWet.ApplyDetailedCore(
                        _lodAbstracted!,
                        _segmentationGraph!,
                        salientMask: null,
                        salientFractions,
                        resolvedWet,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    wetInWet = new WetInWetResult(
                        _lodAbstracted!,
                        new MaskMatrix(ImageSize),
                        new bool[_segmentationGraph!.Boundaries.Count]);
                }

                _wetInWet = wetInWet;
                _wetInWetKey = key;
                _wetInWetVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.WetInWet,
                WatercolorStage.WetInWet,
                stepStart,
                fromCache,
                bypassed: !wetActive,
                onStepCompleted);
            CompleteStage(onStageCompleted, WatercolorStage.WetInWet, ref stageStart);
        }

        // =========================================================================
        // Stage 5: Hand Tremor Distortion
        // =========================================================================
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool tremorActive = opts.EnableHandTremor && resolvedTremor.Amplitude > 0.0;
            var key = tremorActive
                ? new HandTremorKey(
                    Active: true,
                    WetInWetVersion: _wetInWetVersion,
                    MeanShiftClusterVersion: _meanShiftClusterVersion,
                    DistanceFieldVersion: _distanceFieldVersion,
                    Options: resolvedTremor)
                : new HandTremorKey(
                    Active: false,
                    WetInWetVersion: _wetInWetVersion,
                    MeanShiftClusterVersion: 0,
                    DistanceFieldVersion: 0,
                    Options: null);

            bool fromCache = _handTremor is not null && _handTremorKey == key;

            if (!fromCache)
            {
                InvalidateFromHandTremor();

                Matrix<Rgb8> handTremor = tremorActive
                    ? HandTremor.ApplyCore(
                        _wetInWet!.Image,
                        _segmentationGraph!,
                        _distanceField!,
                        _wetInWet,
                        resolvedTremor,
                        cancellationToken)
                    : _wetInWet!.Image;

                _handTremor = handTremor;
                _handTremorKey = key;
                _handTremorVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.HandTremor,
                WatercolorStage.HandTremor,
                stepStart,
                fromCache,
                bypassed: !tremorActive,
                onStepCompleted);
            CompleteStage(onStageCompleted, WatercolorStage.HandTremor, ref stageStart);
        }

        // =========================================================================
        // Stage 6: Secondary Watercolor Effects & FXAA
        // =========================================================================
        // 6a-i: EdgeDarkening (density field)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool edgeActive = opts.EnableEdgeDarkening && resolvedEdge.Intensity > 0.0;
            var key = edgeActive
                ? new EdgeDarkeningKey(Active: true, HandTremorVersion: _handTremorVersion, Options: resolvedEdge)
                : new EdgeDarkeningKey(Active: false, HandTremorVersion: 0, Options: null);

            bool fromCache = _hasEdgeDensity && _edgeDarkeningKey == key;

            if (!fromCache)
            {
                InvalidateFromEdgeDarkening();

                Matrix<DoublePixel>? edgeDensityField = edgeActive
                    ? EdgeDarkening.ComputeDensityField(_handTremor!, resolvedEdge, cancellationToken)
                    : null;

                _edgeDensityField = edgeDensityField;
                _hasEdgeDensity = true;
                _edgeDarkeningKey = key;
                _edgeDensityVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.EdgeDarkening,
                WatercolorStage.Finishing,
                stepStart,
                fromCache,
                bypassed: !edgeActive,
                onStepCompleted);
        }

        // 6a-ii: TurbulenceFlow (density field — independent of Stages 1–5)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool turbulenceActive = opts.EnableTurbulenceFlow && resolvedTurb.Intensity > 0.0;
            var key = turbulenceActive
                ? new TurbulenceFlowKey(Active: true, Options: resolvedTurb)
                : new TurbulenceFlowKey(Active: false, Options: null);

            bool fromCache = _hasTurbulenceDensity && _turbulenceFlowKey == key;

            if (!fromCache)
            {
                InvalidateFromTurbulenceFlow();

                Matrix<DoublePixel>? turbulenceDensityField = turbulenceActive
                    ? TurbulenceFlow.ComputeDensityField(ImageSize, resolvedTurb, cancellationToken)
                    : null;

                _turbulenceDensityField = turbulenceDensityField;
                _hasTurbulenceDensity = true;
                _turbulenceFlowKey = key;
                _turbulenceDensityVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.TurbulenceFlow,
                WatercolorStage.Finishing,
                stepStart,
                fromCache,
                bypassed: !turbulenceActive,
                onStepCompleted);
        }

        // 6a-iii: PigmentCombine (never mutates cached EdgeDensityField or TurbulenceDensityField)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool pigmentBypassed = _edgeDensityField is null && _turbulenceDensityField is null;
            var key = new PigmentCombineKey(_handTremorVersion, _edgeDensityVersion, _turbulenceDensityVersion);
            bool fromCache = _densityAdjusted is not null && _pigmentCombineKey == key;

            if (!fromCache)
            {
                InvalidateFromPigmentCombine();

                Matrix<Rgb8> densityAdjusted;
                if (pigmentBypassed)
                {
                    densityAdjusted = _handTremor!;
                }
                else
                {
                    IReadOnlyMatrix<DoublePixel> combinedDensity = _edgeDensityField is not null && _turbulenceDensityField is not null
                        ? SumDensityFields(_edgeDensityField, _turbulenceDensityField, ImageSize, cancellationToken)
                        : (_edgeDensityField ?? _turbulenceDensityField!);

                    densityAdjusted = PigmentBlend.ApplyDensityField(
                        _handTremor!,
                        combinedDensity,
                        cancellationToken: cancellationToken);
                }

                _densityAdjusted = densityAdjusted;
                _pigmentCombineKey = key;
                _pigmentCombineVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.PigmentCombine,
                WatercolorStage.Finishing,
                stepStart,
                fromCache,
                bypassed: pigmentBypassed,
                onStepCompleted);
        }

        // 6b: ToneCurve
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool toneEnabled = opts.EnableToneCurve;
            var key = toneEnabled
                ? new ToneCurveKey(Enabled: true, PigmentCombineVersion: _pigmentCombineVersion, Options: resolvedTone)
                : new ToneCurveKey(Enabled: false, PigmentCombineVersion: _pigmentCombineVersion, Options: null);

            bool fromCache = _toneAdjusted is not null && _toneCurveKey == key;

            if (!fromCache)
            {
                InvalidateFromToneCurve();

                Matrix<Rgb8> toneAdjusted = toneEnabled
                    ? PiecewiseToneCurve.Apply(_densityAdjusted!, resolvedTone, cancellationToken)
                    : _densityAdjusted!;

                _toneAdjusted = toneAdjusted;
                _toneCurveKey = key;
                _toneCurveVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.ToneCurve,
                WatercolorStage.Finishing,
                stepStart,
                fromCache,
                bypassed: !toneEnabled,
                onStepCompleted);
        }

        // 6c-i: PaperHeightMap (independent of Stages 1–5 and DensityStrength/WarpAmplitude)
        bool granActive = opts.EnablePaperGranulation &&
                          (resolvedGran.DensityStrength > 0.0 || resolvedGran.WarpAmplitude > 0.0);
        bool isProceduralPaper = _customPaperHeightMap is null;
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool generateProceduralHeightMap = granActive && isProceduralPaper;
            var key = new PaperHeightMapKey(
                granActive,
                generateProceduralHeightMap ? resolvedGrainNoise : null);

            bool fromCache = _hasPaperHeightMap && _paperHeightMapKey == key;

            if (!fromCache)
            {
                InvalidateFromPaperHeightMap();

                IReadOnlyMatrix<DoublePixel>? paperHeightMap = !granActive
                    ? null
                    : _customPaperHeightMap ?? PaperGranulation.GeneratePaperHeightMap(
                        ImageSize,
                        resolvedGrainNoise,
                        cancellationToken);

                _paperHeightMap = paperHeightMap;
                _hasPaperHeightMap = true;
                _paperHeightMapKey = key;
                _paperHeightMapVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.PaperHeightMap,
                WatercolorStage.Finishing,
                stepStart,
                fromCache,
                bypassed: !generateProceduralHeightMap,
                onStepCompleted);
        }

        // 6c-ii: PaperGranulation (uses ApplyCore with explicit isProcedural to preserve warpScale parity)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            var key = granActive
                ? new PaperGranulationKey(
                    Active: true,
                    ToneCurveVersion: _toneCurveVersion,
                    PaperHeightMapVersion: _paperHeightMapVersion,
                    DensityStrength: resolvedGran.DensityStrength,
                    WarpAmplitude: resolvedGran.WarpAmplitude,
                    ProceduralFrequency: isProceduralPaper ? resolvedGrainNoise.Frequency : 0.0)
                : new PaperGranulationKey(
                    Active: false,
                    ToneCurveVersion: _toneCurveVersion,
                    PaperHeightMapVersion: 0,
                    DensityStrength: 0.0,
                    WarpAmplitude: 0.0,
                    ProceduralFrequency: 0.0);

            bool fromCache = _granulated is not null && _paperGranulationKey == key;

            if (!fromCache)
            {
                InvalidateFromPaperGranulation();

                Matrix<Rgb8> granulated = granActive
                    ? PaperGranulation.ApplyCore(
                        _toneAdjusted!,
                        _paperHeightMap!,
                        isProcedural: isProceduralPaper,
                        resolvedGran,
                        cancellationToken)
                    : _toneAdjusted!;

                _granulated = granulated;
                _paperGranulationKey = key;
                _paperGranulationVersion = NextVersion();
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.PaperGranulation,
                WatercolorStage.Finishing,
                stepStart,
                fromCache,
                bypassed: !granActive,
                onStepCompleted);
        }

        // 6d: Fxaa
        {
            cancellationToken.ThrowIfCancellationRequested();
            long stepStart = Stopwatch.GetTimestamp();

            bool fxaaEnabled = opts.EnableFxaa;
            var key = fxaaEnabled
                ? new FxaaKey(Enabled: true, PaperGranulationVersion: _paperGranulationVersion, Options: resolvedFxaa)
                : new FxaaKey(Enabled: false, PaperGranulationVersion: _paperGranulationVersion, Options: null);

            bool fromCache = _output is not null && _fxaaKey == key;

            if (!fromCache)
            {
                InvalidateFromFxaa();

                Matrix<Rgb8> output = fxaaEnabled
                    ? Fxaa.Apply(_granulated!, resolvedFxaa, cancellationToken)
                    : _granulated!;

                _output = output;
                _fxaaKey = key;
            }

            CompleteStep(
                stepTimings,
                WatercolorStep.Fxaa,
                WatercolorStage.Finishing,
                stepStart,
                fromCache,
                bypassed: !fxaaEnabled,
                onStepCompleted);
            CompleteStage(onStageCompleted, WatercolorStage.Finishing, ref stageStart);
        }

        var result = new WatercolorResult(
            _output!,
            _colorTransferred!,
            _selectedPalette!,
            _egbiPreBlurred!,
            _egbiGraph!,
            _saliencyMap!,
            _salientMask!,
            _rawDistanceField!,
            _distanceField!,
            _meanShiftFiltered!,
            _segmentationGraph!,
            _lodAbstracted!,
            _wetInWet!,
            _handTremor!,
            _edgeDensityField,
            _turbulenceDensityField,
            _densityAdjusted!,
            _toneAdjusted!,
            _paperHeightMap,
            _granulated!,
            stepTimings);

        LastOptions = opts;
        LastResult = result;
        return result;
    }

    private double[] EnsureSalientFractions(CancellationToken cancellationToken)
    {
        var key = new SalientFractionsKey(_meanShiftClusterVersion, _saliencyCutVersion);
        if (_salientFractions is not null && _salientFractionsKey == key)
        {
            return _salientFractions;
        }

        cancellationToken.ThrowIfCancellationRequested();
        _salientFractions = null;

        double[] fractions = _segmentationGraph!.ComputeRegionFractions(_salientMask!);
        _salientFractions = fractions;
        _salientFractionsKey = key;
        return fractions;
    }

    private static SequentialMatrix<DoublePixel> SumDensityFields(
        IReadOnlyMatrix<DoublePixel> edgeDensityField,
        IReadOnlyMatrix<DoublePixel> turbulenceDensityField,
        Size size,
        CancellationToken cancellationToken)
    {
        var summed = new SequentialMatrix<DoublePixel>(size);
        DoublePixel[] summedRaw = summed.RawData;
        bool canCancel = cancellationToken.CanBeCanceled;

        if (edgeDensityField is SequentialMatrix<DoublePixel> seqEdge &&
            turbulenceDensityField is SequentialMatrix<DoublePixel> seqTurb)
        {
            DoublePixel[] edgeRaw = seqEdge.RawData;
            DoublePixel[] turbRaw = seqTurb.RawData;
            for (int i = 0; i < summedRaw.Length; i++)
            {
                if (canCancel && (i & CancellationCheckMask) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                summedRaw[i] = new DoublePixel(edgeRaw[i].Value + turbRaw[i].Value);
            }
        }
        else
        {
            for (int i = 0; i < summedRaw.Length; i++)
            {
                if (canCancel && (i & CancellationCheckMask) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                summedRaw[i] = new DoublePixel(edgeDensityField[i].Value + turbulenceDensityField[i].Value);
            }
        }

        return summed;
    }

    private static void CompleteStep(
        Dictionary<WatercolorStep, WatercolorStepTiming> stepTimings,
        WatercolorStep step,
        WatercolorStage stage,
        long stepStart,
        bool fromCache,
        bool bypassed,
        Action<WatercolorStepProgress>? onStepCompleted)
    {
        TimeSpan elapsed = Stopwatch.GetElapsedTime(stepStart);
        stepTimings[step] = new WatercolorStepTiming(elapsed, FromCache: fromCache, Bypassed: bypassed);
        onStepCompleted?.Invoke(new WatercolorStepProgress(step, stage, elapsed, FromCache: fromCache, Bypassed: bypassed));
    }

    private static void CompleteStage(
        Action<WatercolorStageProgress>? callback,
        WatercolorStage stage,
        ref long stageStart)
    {
        if (callback is null)
        {
            return;
        }

        callback(new WatercolorStageProgress(stage, Stopwatch.GetElapsedTime(stageStart)));
        stageStart = Stopwatch.GetTimestamp();
    }

    private static WatercolorResult CreateEmptyResult(Size size, WatercolorOptions options)
    {
        var emptyRgb = new SequentialMatrix<Rgb8>(size);
        var emptyDouble = new SequentialMatrix<DoublePixel>(size);
        var emptyMask = new MaskMatrix(size);
        GridConnectivity egbiConnectivity = (options.Egbi ?? WatercolorOptions.DefaultSaliencyEgbi).Connectivity;
        var emptyEgbiGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(size), egbiConnectivity);
        GridConnectivity meanShiftConnectivity = (options.MeanShift ?? MeanShiftOptions.Default).Connectivity;
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
        _colorTransferred = null;
        _selectedPalette = null;
        _egbiPreBlurred = null;
        _egbiLabBlurred = null;
        _egbiGraph = null;
        _saliencyMap = null;
        _salientMask = null;
        _rawDistanceField = null;
        _distanceField = null;
        _meanShiftModes = null;
        _meanShiftSourcePixels = null;
        _meanShiftFiltered = null;
        _segmentationGraph = null;
        _salientFractions = null;
        _lodAbstracted = null;
        _wetInWet = null;
        _handTremor = null;
        _hasEdgeDensity = false;
        _edgeDensityField = null;
        _hasTurbulenceDensity = false;
        _turbulenceDensityField = null;
        _densityAdjusted = null;
        _toneAdjusted = null;
        _hasPaperHeightMap = false;
        _paperHeightMap = null;
        _granulated = null;
        _output = null;

        LastOptions = null;
        LastResult = null;
    }

    private void InvalidateFromColorTransfer()
    {
        _colorTransferred = null;
        _selectedPalette = null;
        InvalidateFromEgbiPreBlur();
        InvalidateFromMeanShiftFiltering();
    }

    private void InvalidateFromEgbiPreBlur()
    {
        _egbiPreBlurred = null;
        _egbiLabBlurred = null;
        InvalidateFromEgbiSegmentation();
    }

    private void InvalidateFromEgbiSegmentation()
    {
        _egbiGraph = null;
        InvalidateFromRegionalContrastSaliency();
    }

    private void InvalidateFromRegionalContrastSaliency()
    {
        _saliencyMap = null;
        InvalidateFromSaliencyCut();
    }

    private void InvalidateFromSaliencyCut()
    {
        _salientMask = null;
        _salientFractions = null;
        InvalidateFromRawDistanceField();
        if (_wetInWet is not null && _wetInWetKey.Active)
        {
            InvalidateFromWetInWet();
        }
    }

    private void InvalidateFromRawDistanceField()
    {
        _rawDistanceField = null;
        InvalidateFromDistanceField();
    }

    private void InvalidateFromDistanceField()
    {
        _distanceField = null;
        if (_lodAbstracted is not null && _lodKey.Enabled)
        {
            InvalidateFromLodAbstraction();
        }
        else if (_handTremor is not null && _handTremorKey.Active)
        {
            InvalidateFromHandTremor();
        }
    }

    private void InvalidateFromMeanShiftFiltering()
    {
        _meanShiftModes = null;
        _meanShiftSourcePixels = null;
        _meanShiftFiltered = null;
        InvalidateFromMeanShiftClustering();
        InvalidateFromLodAbstraction();
    }

    private void InvalidateFromMeanShiftClustering()
    {
        _segmentationGraph = null;
        _salientFractions = null;
        if (_lodAbstracted is not null && _lodKey.Enabled)
        {
            InvalidateFromLodAbstraction();
        }
        else
        {
            InvalidateFromWetInWet();
        }
    }

    private void InvalidateFromLodAbstraction()
    {
        _lodAbstracted = null;
        InvalidateFromWetInWet();
    }

    private void InvalidateFromWetInWet()
    {
        _wetInWet = null;
        InvalidateFromHandTremor();
    }

    private void InvalidateFromHandTremor()
    {
        _handTremor = null;
        if (_hasEdgeDensity && _edgeDarkeningKey.Active)
        {
            InvalidateFromEdgeDarkening();
        }

        InvalidateFromPigmentCombine();
    }

    private void InvalidateFromEdgeDarkening()
    {
        _hasEdgeDensity = false;
        _edgeDensityField = null;
        InvalidateFromPigmentCombine();
    }

    private void InvalidateFromTurbulenceFlow()
    {
        _hasTurbulenceDensity = false;
        _turbulenceDensityField = null;
        InvalidateFromPigmentCombine();
    }

    private void InvalidateFromPigmentCombine()
    {
        _densityAdjusted = null;
        InvalidateFromToneCurve();
    }

    private void InvalidateFromToneCurve()
    {
        _toneAdjusted = null;
        InvalidateFromPaperGranulation();
    }

    private void InvalidateFromPaperHeightMap()
    {
        _hasPaperHeightMap = false;
        _paperHeightMap = null;
        if (_granulated is not null && _paperGranulationKey.Active)
        {
            InvalidateFromPaperGranulation();
        }
    }

    private void InvalidateFromPaperGranulation()
    {
        _granulated = null;
        InvalidateFromFxaa();
    }

    private void InvalidateFromFxaa() => _output = null;

    private readonly record struct ColorTransferKey(
        bool Active,
        ColorTransferProfile? TargetPalette,
        double EffectiveStrength);

    private readonly record struct EgbiPreBlurKey(
        long ColorTransferVersion,
        double EgbiBlurSigma);

    private readonly record struct EgbiKey(
        long EgbiPreBlurVersion,
        EgbiOptions Options);

    private readonly record struct RegionalContrastSaliencyKey(
        long ColorTransferVersion,
        long EgbiVersion,
        RegionalContrastSaliencyOptions Options);

    private readonly record struct SaliencyCutKey(
        long ColorTransferVersion,
        long RcSaliencyVersion,
        SaliencyCutOptions Options);

    private readonly record struct SaliencyDistanceFieldKey(
        long RawDistanceFieldVersion,
        double EffectiveSmoothingSigma);

    private readonly record struct MeanShiftFilterKey(
        long ColorTransferVersion,
        int SpatialRadius,
        double RangeRadius,
        int MaxIterations,
        double ConvergenceEpsilon);

    private readonly record struct MeanShiftClusterKey(
        long MeanShiftFilterVersion,
        int MinRegionSize,
        GridConnectivity Connectivity);

    private readonly record struct SalientFractionsKey(
        long MeanShiftClusterVersion,
        long SaliencyCutVersion);

    private readonly record struct LodAbstractionKey(
        bool Enabled,
        long MeanShiftFilterVersion,
        long MeanShiftClusterVersion,
        long DistanceFieldVersion,
        long SaliencyCutVersion,
        LodAbstractionOptions? Options);

    private readonly record struct WetInWetKey(
        bool Active,
        long LodVersion,
        long MeanShiftClusterVersion,
        long SaliencyCutVersion,
        WetInWetOptions? Options);

    private readonly record struct HandTremorKey(
        bool Active,
        long WetInWetVersion,
        long MeanShiftClusterVersion,
        long DistanceFieldVersion,
        HandTremorOptions? Options);

    private readonly record struct EdgeDarkeningKey(
        bool Active,
        long HandTremorVersion,
        EdgeDarkeningOptions? Options);

    private readonly record struct TurbulenceFlowKey(
        bool Active,
        TurbulenceFlowOptions? Options);

    private readonly record struct PigmentCombineKey(
        long HandTremorVersion,
        long EdgeDensityVersion,
        long TurbulenceDensityVersion);

    private readonly record struct ToneCurveKey(
        bool Enabled,
        long PigmentCombineVersion,
        PiecewiseToneCurveOptions? Options);

    private readonly record struct PaperHeightMapKey(
        bool GranActive,
        PerlinNoiseOptions? ProceduralGrainNoise);

    private readonly record struct PaperGranulationKey(
        bool Active,
        long ToneCurveVersion,
        long PaperHeightMapVersion,
        double DensityStrength,
        double WarpAmplitude,
        double ProceduralFrequency);

    private readonly record struct FxaaKey(
        bool Enabled,
        long PaperGranulationVersion,
        FxaaOptions? Options);
}
