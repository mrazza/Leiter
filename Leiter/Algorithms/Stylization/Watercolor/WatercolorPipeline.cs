namespace Leiter.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Leiter.Algorithms;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;

/// <summary>
/// Orchestrates the complete Wang et al. (IEEE TVCG 2014) photo watercolorization pipeline across Stages 1–6.
/// </summary>
public static class WatercolorPipeline
{
    private const int TotalStepCount = 18;

    /// <summary>
    /// Executes the full watercolorization pipeline on <paramref name="image"/> and returns the final painting alongside intermediate stage outputs.
    /// </summary>
    /// <param name="image">The input RGB photograph to watercolorize.</param>
    /// <param name="paperHeightMap">Optional custom normalized [0, 1] paper height map used during Stage 6 granulation.</param>
    /// <param name="options">Optional pipeline configuration parameters controlling each stage.</param>
    /// <param name="onStageCompleted">Optional callback invoked as each major pipeline stage completes (not invoked when <paramref name="image"/> is empty).</param>
    /// <returns>A <see cref="WatercolorResult"/> containing the final rendered painting and stage intermediates.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="paperHeightMap"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> contains invalid parameters.</exception>
    public static WatercolorResult RenderDetailed(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel>? paperHeightMap = null,
        WatercolorOptions? options = null,
        Action<WatercolorStageProgress>? onStageCompleted = null)
    {
        ArgumentNullException.ThrowIfNull(image);

        WatercolorOptions opts = OptionsResolver.Resolve(options);
        Guard.ThrowIfSizeMismatchIfNotNull(image, paperHeightMap);

        if (image.Count == 0)
        {
            return CreateEmptyResult(image.Size, opts);
        }

        long initialTimestamp = Stopwatch.GetTimestamp();
        long stageStart = onStageCompleted is not null ? initialTimestamp : 0;
        long stepStart = initialTimestamp;
        var stepTimings = new Dictionary<WatercolorStep, WatercolorStepTiming>(TotalStepCount);

        // Stage 1: Reinhard Color Transfer
        bool colorTransferBypassed = !opts.EnableColorTransfer || opts.ColorTransferStrength == 0.0;
        Matrix<Rgb8> colorTransferred;
        ColorTransferProfile selectedPalette;
        if (!opts.EnableColorTransfer)
        {
            colorTransferred = image.Clone();
            selectedPalette = opts.TargetPalette ?? ColorTransfer.SelectClosestProfile(
                ColorTransfer.ComputeStatistics(image, RgbColorSpace.SRgb, Illuminant.E),
                WatercolorPalettes.Defaults,
                WatercolorPalettes.DefaultFeatureWeights);
        }
        else if (opts.TargetPalette is not null)
        {
            colorTransferred = ColorTransfer.Transfer(image, opts.TargetPalette, strength: opts.ColorTransferStrength);
            selectedPalette = opts.TargetPalette;
        }
        else
        {
            (colorTransferred, selectedPalette) = ColorTransfer.TransferToClosestProfile(
                image,
                WatercolorPalettes.Defaults,
                featureWeights: WatercolorPalettes.DefaultFeatureWeights,
                strength: opts.ColorTransferStrength);
        }

        RecordStep(stepTimings, WatercolorStep.ColorTransfer, ref stepStart, bypassed: colorTransferBypassed);
        CompleteStage(onStageCompleted, WatercolorStage.ColorTransfer, ref stageStart, ref stepStart);

        // Stage 2: Regional Contrast Saliency & Saliency Distance Field
        Matrix<Rgb8> egbiPreBlurred = Blur.GaussianBlur(colorTransferred, opts.EgbiBlurSigma);
        Matrix<Lab32> labBlurred = egbiPreBlurred.ToLab32(RgbColorSpace.SRgb);
        RecordStep(stepTimings, WatercolorStep.EgbiPreBlur, ref stepStart, bypassed: opts.EgbiBlurSigma == 0.0);

        EgbiOptions resolvedEgbi = opts.Egbi ?? WatercolorOptions.DefaultSaliencyEgbi;
        IDisjointSet egbiSeg = EgbiSegmentation.Segment(labBlurred, resolvedEgbi);
        RegionGraph egbiGraph = egbiSeg.ToRegionGraph(resolvedEgbi.Connectivity);
        RecordStep(stepTimings, WatercolorStep.EgbiSegmentation, ref stepStart, bypassed: false);

        Matrix<DoublePixel> saliencyMap = RegionalContrastSaliency.ComputeSaliency(colorTransferred, egbiGraph, opts.RegionalContrastSaliency);
        RecordStep(stepTimings, WatercolorStep.RegionalContrastSaliency, ref stepStart, bypassed: false);

        MaskMatrix salientMask = SaliencyCut.Extract(colorTransferred, saliencyMap, opts.SaliencyCut);
        RecordStep(stepTimings, WatercolorStep.SaliencyCut, ref stepStart, bypassed: false);

        SaliencyDistanceFieldOptions resolvedDistanceOpts = opts.SaliencyDistanceField ?? SaliencyDistanceFieldOptions.Default;
        Matrix<DoublePixel> rawDistanceField = SaliencyDistanceField.ComputeNormalizedField(salientMask);
        double resolvedSigma = resolvedDistanceOpts.SmoothingSigma ?? (resolvedDistanceOpts.DefaultSigmaScale * Math.Min(salientMask.Width, salientMask.Height));
        Matrix<DoublePixel> distanceField = resolvedSigma <= 0.0
            ? rawDistanceField
            : SaliencyDistanceField.Compute(salientMask, resolvedDistanceOpts);
        RecordStep(stepTimings, WatercolorStep.SaliencyDistanceField, ref stepStart, bypassed: false);
        CompleteStage(onStageCompleted, WatercolorStage.Saliency, ref stageStart, ref stepStart);

        // Stage 3a: Mean-Shift Segmentation
        MeanShiftOptions resolvedMeanShift = opts.MeanShift ?? MeanShiftOptions.Default;
        (Matrix<Rgb8> meanShiftFiltered, IDisjointSet meanShiftSeg) = MeanShiftSegmentation.FilterAndSegment(colorTransferred, resolvedMeanShift);
        RecordStep(stepTimings, WatercolorStep.MeanShiftFiltering, ref stepStart, bypassed: false);

        RegionGraph regionGraph = meanShiftSeg.ToRegionGraph(resolvedMeanShift.Connectivity);
        double[] salientFractions = regionGraph.ComputeRegionFractions(salientMask);
        RecordStep(stepTimings, WatercolorStep.MeanShiftClustering, ref stepStart, bypassed: false);
        CompleteStage(onStageCompleted, WatercolorStage.Segmentation, ref stageStart, ref stepStart);

        // Stage 3b: Saliency-Guided LOD Abstraction
        LodAbstractionOptions resolvedLod = opts.LodAbstraction ?? LodAbstractionOptions.Default;
        Matrix<Rgb8> lodAbstracted = opts.EnableLodAbstraction
            ? LodAbstraction.ApplyCore(
                meanShiftFiltered,
                regionGraph,
                distanceField,
                salientMask: null,
                salientFractions,
                resolvedLod)
            : meanShiftFiltered;
        RecordStep(stepTimings, WatercolorStep.LodAbstraction, ref stepStart, bypassed: !opts.EnableLodAbstraction);
        CompleteStage(onStageCompleted, WatercolorStage.LodAbstraction, ref stageStart, ref stepStart);

        // Stage 4: Wet-in-Wet Bleeding
        WetInWetOptions resolvedWetInWet = opts.WetInWet ?? WetInWetOptions.Default;
        bool wetInWetBypassed = !opts.EnableWetInWet || resolvedWetInWet.Mode == WetInWetMode.Disabled;
        WetInWetResult wetInWet = opts.EnableWetInWet
            ? WetInWet.ApplyDetailedCore(
                lodAbstracted,
                regionGraph,
                salientMask: null,
                salientFractions,
                resolvedWetInWet)
            : new WetInWetResult(lodAbstracted, new MaskMatrix(image.Size), new bool[regionGraph.Boundaries.Count]);
        RecordStep(stepTimings, WatercolorStep.WetInWet, ref stepStart, bypassed: wetInWetBypassed);
        CompleteStage(onStageCompleted, WatercolorStage.WetInWet, ref stageStart, ref stepStart);

        // Stage 5: Hand Tremor Distortion
        HandTremorOptions resolvedHandTremor = opts.HandTremor ?? HandTremorOptions.Default;
        bool handTremorBypassed = !opts.EnableHandTremor || resolvedHandTremor.Amplitude == 0.0;
        Matrix<Rgb8> handTremor = opts.EnableHandTremor
            ? HandTremor.ApplyCore(
                wetInWet.Image,
                regionGraph,
                distanceField,
                wetInWet,
                resolvedHandTremor)
            : wetInWet.Image;
        RecordStep(stepTimings, WatercolorStep.HandTremor, ref stepStart, bypassed: handTremorBypassed);
        CompleteStage(onStageCompleted, WatercolorStage.HandTremor, ref stageStart, ref stepStart);

        // Stage 6: Secondary Watercolor Effects & FXAA
        EdgeDarkeningOptions edgeOpts = opts.EdgeDarkening ?? EdgeDarkeningOptions.Default;
        bool edgeBypassed = !opts.EnableEdgeDarkening || edgeOpts.Intensity == 0.0;
        Matrix<DoublePixel>? edgeDensityField = edgeBypassed
            ? null
            : EdgeDarkening.ComputeDensityField(handTremor, edgeOpts);
        RecordStep(stepTimings, WatercolorStep.EdgeDarkening, ref stepStart, bypassed: edgeBypassed);

        TurbulenceFlowOptions turbOpts = opts.TurbulenceFlow ?? TurbulenceFlowOptions.Default;
        bool turbulenceBypassed = !opts.EnableTurbulenceFlow || turbOpts.Intensity == 0.0;
        Matrix<DoublePixel>? turbulenceDensityField = turbulenceBypassed
            ? null
            : TurbulenceFlow.ComputeDensityField(handTremor.Size, turbOpts);
        RecordStep(stepTimings, WatercolorStep.TurbulenceFlow, ref stepStart, bypassed: turbulenceBypassed);

        bool pigmentBypassed = edgeDensityField is null && turbulenceDensityField is null;
        Matrix<Rgb8> densityAdjusted;
        if (pigmentBypassed)
        {
            densityAdjusted = handTremor;
        }
        else
        {
            IReadOnlyMatrix<DoublePixel> combinedDensity;
            if (edgeDensityField is not null && turbulenceDensityField is not null)
            {
                var summed = new SequentialMatrix<DoublePixel>(handTremor.Size);
                for (int i = 0; i < summed.Count; i++)
                {
                    summed[i] = new DoublePixel(edgeDensityField[i].Value + turbulenceDensityField[i].Value);
                }

                combinedDensity = summed;
            }
            else
            {
                combinedDensity = edgeDensityField ?? turbulenceDensityField!;
            }

            densityAdjusted = PigmentBlend.ApplyDensityField(handTremor, combinedDensity);
        }

        RecordStep(stepTimings, WatercolorStep.PigmentCombine, ref stepStart, bypassed: pigmentBypassed);

        Matrix<Rgb8> toneAdjusted = opts.EnableToneCurve
            ? PiecewiseToneCurve.Apply(densityAdjusted, opts.ToneCurve)
            : densityAdjusted;
        RecordStep(stepTimings, WatercolorStep.ToneCurve, ref stepStart, bypassed: !opts.EnableToneCurve);

        PaperGranulationOptions granOpts = opts.PaperGranulation ?? PaperGranulationOptions.Default;
        bool granActive = opts.EnablePaperGranulation && (granOpts.DensityStrength > 0.0 || granOpts.WarpAmplitude > 0.0);
        bool generateProceduralHeightMap = granActive && paperHeightMap is null;
        IReadOnlyMatrix<DoublePixel>? resolvedPaperHeightMap = granActive
            ? paperHeightMap ?? PaperGranulation.GeneratePaperHeightMap(image.Size, granOpts.GrainNoise)
            : null;
        RecordStep(stepTimings, WatercolorStep.PaperHeightMap, ref stepStart, bypassed: !generateProceduralHeightMap);

        Matrix<Rgb8> granulated = granActive
            ? PaperGranulation.Apply(toneAdjusted, paperHeightMap, granOpts)
            : toneAdjusted;
        RecordStep(stepTimings, WatercolorStep.PaperGranulation, ref stepStart, bypassed: !granActive);

        Matrix<Rgb8> output = opts.EnableFxaa
            ? Fxaa.Apply(granulated, opts.Fxaa)
            : granulated;
        RecordStep(stepTimings, WatercolorStep.Fxaa, ref stepStart, bypassed: !opts.EnableFxaa);
        CompleteStage(onStageCompleted, WatercolorStage.Finishing, ref stageStart, ref stepStart);

        return new WatercolorResult(
            output,
            colorTransferred,
            selectedPalette,
            egbiPreBlurred,
            egbiGraph,
            saliencyMap,
            salientMask,
            rawDistanceField,
            distanceField,
            meanShiftFiltered,
            regionGraph,
            lodAbstracted,
            wetInWet,
            handTremor,
            edgeDensityField,
            turbulenceDensityField,
            densityAdjusted,
            toneAdjusted,
            resolvedPaperHeightMap,
            granulated,
            stepTimings);
    }

    private static void RecordStep(
        Dictionary<WatercolorStep, WatercolorStepTiming> stepTimings,
        WatercolorStep step,
        ref long stepStart,
        bool bypassed)
    {
        stepTimings[step] = new WatercolorStepTiming(
            Stopwatch.GetElapsedTime(stepStart),
            FromCache: false,
            Bypassed: bypassed);
        stepStart = Stopwatch.GetTimestamp();
    }

    private static void CompleteStage(
        Action<WatercolorStageProgress>? callback,
        WatercolorStage stage,
        ref long stageStart,
        ref long stepStart)
    {
        if (callback is null)
            return;

        callback(new WatercolorStageProgress(stage, Stopwatch.GetElapsedTime(stageStart)));
        long now = Stopwatch.GetTimestamp();
        stageStart = now;
        stepStart = now;
    }

    /// <summary>
    /// Executes the full watercolorization pipeline on <paramref name="image"/> and returns the final rendered watercolor painting.
    /// </summary>
    /// <param name="image">The input RGB photograph to watercolorize.</param>
    /// <param name="paperHeightMap">Optional custom normalized [0, 1] paper height map used during Stage 6 granulation.</param>
    /// <param name="options">Optional pipeline configuration parameters controlling each stage.</param>
    /// <param name="onStageCompleted">Optional callback invoked as each major pipeline stage completes (not invoked when <paramref name="image"/> is empty).</param>
    /// <returns>A new <see cref="Matrix{Rgb8}"/> containing the final rendered watercolor painting.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="paperHeightMap"/> dimensions do not match <paramref name="image"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="options"/> contains invalid parameters.</exception>
    public static Matrix<Rgb8> Render(
        IReadOnlyMatrix<Rgb8> image,
        IReadOnlyMatrix<DoublePixel>? paperHeightMap = null,
        WatercolorOptions? options = null,
        Action<WatercolorStageProgress>? onStageCompleted = null) =>
        RenderDetailed(image, paperHeightMap, options, onStageCompleted).Image;

    private static WatercolorResult CreateEmptyResult(Size size, WatercolorOptions options)
    {
        var emptyRgb = new SequentialMatrix<Rgb8>(size);
        var emptyDouble = new SequentialMatrix<DoublePixel>(size);
        var emptyMask = new MaskMatrix(size);
        GridConnectivity egbiConnectivity = (options.Egbi ?? WatercolorOptions.DefaultSaliencyEgbi).Connectivity;
        var emptyEgbiGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(size), egbiConnectivity);
        GridConnectivity meanShiftConnectivity = options.MeanShift?.Connectivity ?? GridConnectivity.EightConnected;
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
}
