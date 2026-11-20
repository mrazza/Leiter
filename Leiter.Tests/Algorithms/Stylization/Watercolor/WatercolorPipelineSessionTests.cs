namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Leiter.Algorithms;
using Leiter.Algorithms.AntiAliasing;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Unit tests for <see cref="WatercolorPipelineSession"/> verifying DAG caching, bit-for-bit parity,
/// bypass toggles, cooperative cancellation, concurrency guards, and cache reset semantics.
/// </summary>
public class WatercolorPipelineSessionTests
{
    private static readonly WatercolorStep[] AllStepsInOrder =
    [
        WatercolorStep.ColorTransfer,
        WatercolorStep.EgbiPreBlur,
        WatercolorStep.EgbiSegmentation,
        WatercolorStep.RegionalContrastSaliency,
        WatercolorStep.SaliencyCut,
        WatercolorStep.SaliencyDistanceField,
        WatercolorStep.MeanShiftFiltering,
        WatercolorStep.MeanShiftClustering,
        WatercolorStep.LodAbstraction,
        WatercolorStep.WetInWet,
        WatercolorStep.HandTremor,
        WatercolorStep.EdgeDarkening,
        WatercolorStep.TurbulenceFlow,
        WatercolorStep.PigmentCombine,
        WatercolorStep.ToneCurve,
        WatercolorStep.PaperHeightMap,
        WatercolorStep.PaperGranulation,
        WatercolorStep.Fxaa
    ];

    private static readonly WatercolorStage[] ExpectedStagesInOrder =
    [
        WatercolorStage.ColorTransfer,
        WatercolorStage.Saliency,
        WatercolorStage.Saliency,
        WatercolorStage.Saliency,
        WatercolorStage.Saliency,
        WatercolorStage.Saliency,
        WatercolorStage.Segmentation,
        WatercolorStage.Segmentation,
        WatercolorStage.LodAbstraction,
        WatercolorStage.WetInWet,
        WatercolorStage.HandTremor,
        WatercolorStage.Finishing,
        WatercolorStage.Finishing,
        WatercolorStage.Finishing,
        WatercolorStage.Finishing,
        WatercolorStage.Finishing,
        WatercolorStage.Finishing,
        WatercolorStage.Finishing
    ];

    /// <summary>
    /// Verifies constructor argument validation and initial session property state.
    /// </summary>
    [Fact]
    public void Constructor_ValidAndInvalidInputs_ShouldInitializeAndValidate()
    {
        var image = CreateTestScene(16, 16);
        var mismatchedMap = new SequentialMatrix<DoublePixel>(8, 16);

        Assert.Throws<ArgumentNullException>(() => new WatercolorPipelineSession(null!));
        Assert.Throws<ArgumentException>(() => new WatercolorPipelineSession(image, mismatchedMap));

        var session = new WatercolorPipelineSession(image);
        Assert.Same(image, session.SourceImage);
        Assert.Equal(image.Size, session.ImageSize);
        Assert.Null(session.LastOptions);
        Assert.Null(session.LastResult);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            session.Render(new WatercolorOptions(ColorTransferStrength: 2.0)));
        Assert.Null(session.LastOptions);
        Assert.Null(session.LastResult);
    }

    /// <summary>
    /// Verifies bit-for-bit parity between <see cref="WatercolorPipelineSession.Render"/> and
    /// <see cref="WatercolorPipeline.RenderDetailed"/> across default, <c>ForSize</c>, <c>ForImage</c>, and custom paper height map inputs.
    /// </summary>
    [Fact]
    public void Render_ParityWithRenderDetailed_DefaultForSizeForImageAndCustomPaperMap_ShouldMatchBitForBit()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var session = new WatercolorPipelineSession(scene);

        WatercolorOptions[] optionSets =
        [
            WatercolorOptions.Default,
            WatercolorOptions.ForSize(scene.Size),
            WatercolorOptions.ForImage(scene)
        ];

        foreach (WatercolorOptions options in optionSets)
        {
            WatercolorResult sessionResult = session.Render(options);
            WatercolorResult freshResult = WatercolorPipeline.RenderDetailed(scene, options: options);
            AssertResultsBitIdentical(freshResult, sessionResult);
            Assert.Same(options, session.LastOptions);
            Assert.Same(sessionResult, session.LastResult);
        }

        SequentialMatrix<DoublePixel> customPaper = TestMatrices.Create(
            24,
            24,
            static (x, y) => new DoublePixel((x + y) / 48.0));
        var customSession = new WatercolorPipelineSession(scene, customPaper);
        WatercolorOptions customOptions = WatercolorOptions.ForImage(scene);

        WatercolorResult customSessionResult = customSession.Render(customOptions);
        WatercolorResult customFreshResult = WatercolorPipeline.RenderDetailed(scene, customPaper, customOptions);
        AssertResultsBitIdentical(customFreshResult, customSessionResult);
        Assert.Same(customPaper, customSessionResult.PaperHeightMap);
        Assert.True(customSessionResult.StepTimings[WatercolorStep.PaperHeightMap].Bypassed);
    }

    /// <summary>
    /// Verifies that reusing a cached procedural <see cref="WatercolorResult.PaperHeightMap"/> with a non-default
    /// <see cref="PaperGranulationOptions.GrainNoise"/> frequency preserves procedural <c>warpScale</c> parity
    /// when mutating <see cref="PaperGranulationOptions.DensityStrength"/> or <see cref="WatercolorOptions.ToneCurve"/>.
    /// </summary>
    [Fact]
    public void Render_CachedProceduralPaperGranulation_NonDefaultFrequency_ShouldPreserveWarpScaleParity()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var nonDefaultGrain = new PerlinNoiseOptions(Seed: 707, Frequency: 0.045, Octaves: 3, Lacunarity: 2.0, Persistence: 0.55);
        var initialGran = new PaperGranulationOptions(
            DensityStrength: 0.25,
            WarpAmplitude: 1.8,
            GrainNoise: nonDefaultGrain);
        var opts1 = new WatercolorOptions(PaperGranulation: initialGran, EnableFxaa: false);

        var session = new WatercolorPipelineSession(scene);
        WatercolorResult r1 = session.Render(opts1);
        WatercolorResult fresh1 = WatercolorPipeline.RenderDetailed(scene, options: opts1);
        AssertResultsBitIdentical(fresh1, r1);
        Assert.NotNull(r1.PaperHeightMap);

        // Confirm the test scene actually differentiates procedural vs custom warpScale.
        Matrix<Rgb8> unscaledGran = PaperGranulation.Apply(r1.ToneAdjusted, r1.PaperHeightMap, initialGran);
        Assert.False(r1.Granulated.ContentEquals(unscaledGran));

        // Mutate DensityStrength only: PaperHeightMap must stay cached and PaperGranulation must preserve procedural warpScale.
        var opts2 = opts1 with { PaperGranulation = initialGran with { DensityStrength = 0.55 } };
        WatercolorResult r2 = session.Render(opts2);
        WatercolorResult fresh2 = WatercolorPipeline.RenderDetailed(scene, options: opts2);

        Assert.Same(r1.PaperHeightMap, r2.PaperHeightMap);
        AssertCacheStatus(r2, expectedMisses: [WatercolorStep.PaperGranulation, WatercolorStep.Fxaa]);
        AssertResultsBitIdentical(fresh2, r2);

        // Mutate ToneCurve only: PaperHeightMap must still stay cached and PaperGranulation must preserve procedural warpScale.
        var opts3 = opts2 with { ToneCurve = new PiecewiseToneCurveOptions(MinTone: 0.1, MaxTone: 0.9) };
        WatercolorResult r3 = session.Render(opts3);
        WatercolorResult fresh3 = WatercolorPipeline.RenderDetailed(scene, options: opts3);

        Assert.Same(r1.PaperHeightMap, r3.PaperHeightMap);
        AssertCacheStatus(r3, expectedMisses: [WatercolorStep.ToneCurve, WatercolorStep.PaperGranulation, WatercolorStep.Fxaa]);
        AssertResultsBitIdentical(fresh3, r3);
    }

    /// <summary>
    /// Verifies identical re-runs and switching between <see langword="null"/> and explicit default sub-options
    /// hit cache across all 18 DAG steps without false invalidations.
    /// </summary>
    [Fact]
    public void Render_IdenticalAndExplicitDefaultOptions_ShouldHitCacheForAll18Steps()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);
        var session = new WatercolorPipelineSession(scene);

        var progress1 = new List<WatercolorStepProgress>();
        WatercolorResult r1 = session.Render(options: null, onStepCompleted: progress1.Add);

        Assert.Equal(AllStepsInOrder, Array.ConvertAll(progress1.ToArray(), static p => p.Step));
        Assert.Equal(ExpectedStagesInOrder, Array.ConvertAll(progress1.ToArray(), static p => p.Stage));
        Assert.All(progress1, static p =>
        {
            Assert.False(p.FromCache);
            Assert.False(p.Bypassed);
            Assert.True(p.Elapsed >= TimeSpan.Zero);
        });
        AssertProgressMatchesTimings(progress1, r1);
        Assert.Same(WatercolorOptions.Default, session.LastOptions);

        // Identical re-run with null
        var progress2 = new List<WatercolorStepProgress>();
        WatercolorResult r2 = session.Render(options: null, onStepCompleted: progress2.Add);
        Assert.All(progress2, static p => Assert.True(p.FromCache));
        AssertProgressMatchesTimings(progress2, r2);
        Assert.Same(r1.Image, r2.Image);

        // Explicit defaults for every single top-level and nested nullable sub-option
        var explicitDefaults = new WatercolorOptions(
            TargetPalette: null,
            ColorTransferStrength: 1.0,
            Egbi: WatercolorOptions.DefaultSaliencyEgbi,
            RegionalContrastSaliency: RegionalContrastSaliencyOptions.Default,
            SaliencyCut: new SaliencyCutOptions(
                GrabCut: SaliencyCutOptions.DefaultGrabCut,
                Mask: SaliencyCutOptions.DefaultMask),
            SaliencyDistanceField: SaliencyDistanceFieldOptions.Default,
            MeanShift: MeanShiftOptions.Default,
            LodAbstraction: LodAbstractionOptions.Default,
            WetInWet: new WetInWetOptions(EllipticalFilter: WetInWetOptions.DefaultEllipticalFilter),
            HandTremor: new HandTremorOptions(
                PaperColor: HandTremorOptions.DefaultPaperColor,
                PrimaryNoise: HandTremorOptions.DefaultPrimaryNoise,
                SecondaryNoise: HandTremorOptions.DefaultSecondaryNoise),
            EdgeDarkening: EdgeDarkeningOptions.Default,
            TurbulenceFlow: new TurbulenceFlowOptions(Noise: TurbulenceFlowOptions.DefaultNoise),
            ToneCurve: PiecewiseToneCurveOptions.Default,
            PaperGranulation: new PaperGranulationOptions(GrainNoise: PaperGranulationOptions.DefaultGrainNoise),
            Fxaa: FxaaOptions.Default,
            EnableFxaa: true,
            EgbiBlurSigma: WatercolorOptions.DefaultEgbiBlurSigma);

        var progress3 = new List<WatercolorStepProgress>();
        WatercolorResult r3 = session.Render(explicitDefaults, onStepCompleted: progress3.Add);
        AssertCacheStatus(r3, expectedMisses: []);
        AssertProgressMatchesTimings(progress3, r3);
        Assert.Same(r1.Image, r3.Image);
        Assert.Same(explicitDefaults, session.LastOptions);

        // Explicit SaliencyDistanceField SmoothingSigma equal to DefaultSigmaScale * minDim must also hit cache
        double equivalentSigma = SaliencyDistanceFieldOptions.Default.DefaultSigmaScale * Math.Min(scene.Width, scene.Height);
        var equivalentSigmaOpts = explicitDefaults with
        {
            SaliencyDistanceField = new SaliencyDistanceFieldOptions(SmoothingSigma: equivalentSigma, DefaultSigmaScale: 0.25)
        };
        WatercolorResult r4 = session.Render(equivalentSigmaOpts);
        AssertCacheStatus(r4, expectedMisses: []);
        Assert.Same(r1.RawSaliencyDistanceField, r4.RawSaliencyDistanceField);
        Assert.Same(r1.SaliencyDistanceField, r4.SaliencyDistanceField);
    }

    /// <summary>
    /// Verifies that with a custom <c>paperHeightMap</c>, mutating procedural <see cref="PaperGranulationOptions.GrainNoise"/>
    /// does not cause cache misses, while mutating <see cref="PaperGranulationOptions.DensityStrength"/> keeps
    /// <see cref="WatercolorStep.PaperHeightMap"/> cached (bypassed) and recomputes <see cref="WatercolorStep.PaperGranulation"/>.
    /// </summary>
    [Fact]
    public void Render_CustomPaperHeightMap_ShouldKeepPaperHeightMapBypassedAndCachedAcrossEdits()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);
        SequentialMatrix<DoublePixel> customPaper = TestMatrices.Create(
            20,
            20,
            static (x, y) => new DoublePixel((x + y) / 40.0));
        var session = new WatercolorPipelineSession(scene, customPaper);

        WatercolorOptions opts1 = WatercolorOptions.Default;
        WatercolorResult r1 = session.Render(opts1);
        Assert.Same(customPaper, r1.PaperHeightMap);
        Assert.True(r1.StepTimings[WatercolorStep.PaperHeightMap].Bypassed);
        Assert.False(r1.StepTimings[WatercolorStep.PaperHeightMap].FromCache);

        // Mutating GrainNoise has no effect when a custom paper height map is bound
        WatercolorOptions opts2 = opts1 with
        {
            PaperGranulation = new PaperGranulationOptions(
                GrainNoise: new PerlinNoiseOptions(Seed: 999, Frequency: 0.05))
        };
        WatercolorResult r2 = session.Render(opts2);
        AssertCacheStatus(r2, expectedMisses: []);
        Assert.Same(customPaper, r2.PaperHeightMap);
        Assert.True(r2.StepTimings[WatercolorStep.PaperHeightMap].Bypassed);

        // Mutating DensityStrength keeps PaperHeightMap cached & bypassed, recomputes PaperGranulation + Fxaa
        WatercolorOptions opts3 = opts2 with
        {
            PaperGranulation = opts2.PaperGranulation! with { DensityStrength = 0.6 }
        };
        WatercolorResult r3 = session.Render(opts3);
        AssertCacheStatus(r3, expectedMisses: [WatercolorStep.PaperGranulation, WatercolorStep.Fxaa]);
        Assert.Same(customPaper, r3.PaperHeightMap);
        Assert.True(r3.StepTimings[WatercolorStep.PaperHeightMap].Bypassed);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, customPaper, opts3), r3);
    }

    /// <summary>
    /// Verifies fine-grained Stage 6 cache boundaries for FXAA, PaperGranulation, PaperHeightMap,
    /// ToneCurve, TurbulenceFlow (independent of Stages 1–5 and EdgeDarkening), and EdgeDarkening
    /// (independent of TurbulenceFlow and PaperHeightMap).
    /// </summary>
    [Fact]
    public void Render_Stage6Mutations_ShouldInvalidateOnlyAffectedSubgraph()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var session = new WatercolorPipelineSession(scene);
        WatercolorOptions baseOpts = WatercolorOptions.Default;

        WatercolorResult r0 = session.Render(baseOpts);
        AssertCacheStatus(r0, expectedMisses: AllStepsInOrder);

        // 1. FXAA only
        WatercolorOptions fxaaOpts = baseOpts with { Fxaa = new FxaaOptions(SubpixelBlendStrength: 0.4) };
        WatercolorResult rFxaa = session.Render(fxaaOpts);
        AssertCacheStatus(rFxaa, expectedMisses: [WatercolorStep.Fxaa]);
        Assert.Same(r0.Granulated, rFxaa.Granulated);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: fxaaOpts), rFxaa);

        // 2. PaperGranulation DensityStrength only -> PaperHeightMap stays cached
        WatercolorOptions granStrengthOpts = fxaaOpts with
        {
            PaperGranulation = new PaperGranulationOptions(DensityStrength: 0.5, WarpAmplitude: 0.6)
        };
        WatercolorResult rGranStrength = session.Render(granStrengthOpts);
        AssertCacheStatus(rGranStrength, expectedMisses: [WatercolorStep.PaperGranulation, WatercolorStep.Fxaa]);
        Assert.Same(r0.PaperHeightMap, rGranStrength.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: granStrengthOpts), rGranStrength);

        // 3. PaperGranulation GrainNoise -> PaperHeightMap + PaperGranulation + Fxaa miss
        WatercolorOptions granNoiseOpts = granStrengthOpts with
        {
            PaperGranulation = granStrengthOpts.PaperGranulation! with
            {
                GrainNoise = PaperGranulationOptions.DefaultGrainNoise with { Seed = 909 }
            }
        };
        WatercolorResult rGranNoise = session.Render(granNoiseOpts);
        AssertCacheStatus(
            rGranNoise,
            expectedMisses: [WatercolorStep.PaperHeightMap, WatercolorStep.PaperGranulation, WatercolorStep.Fxaa]);
        Assert.NotSame(r0.PaperHeightMap, rGranNoise.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: granNoiseOpts), rGranNoise);

        // 4. ToneCurve only -> PaperHeightMap stays cached
        WatercolorOptions toneOpts = granNoiseOpts with
        {
            ToneCurve = new PiecewiseToneCurveOptions(MinTone: 0.08, MaxTone: 0.92)
        };
        WatercolorResult rTone = session.Render(toneOpts);
        AssertCacheStatus(
            rTone,
            expectedMisses: [WatercolorStep.ToneCurve, WatercolorStep.PaperGranulation, WatercolorStep.Fxaa]);
        Assert.Same(rGranNoise.PaperHeightMap, rTone.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: toneOpts), rTone);

        // 5. TurbulenceFlow only -> EdgeDarkening, PaperHeightMap, and Stages 1-5 stay cached; EdgeDensityField is untouched
        Matrix<DoublePixel> edgeSnapshot = rTone.EdgeDensityField!.Clone();
        WatercolorOptions turbOpts = toneOpts with
        {
            TurbulenceFlow = new TurbulenceFlowOptions(Intensity: 0.65)
        };
        WatercolorResult rTurb = session.Render(turbOpts);
        AssertCacheStatus(
            rTurb,
            expectedMisses:
            [
                WatercolorStep.TurbulenceFlow,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(rTone.EdgeDensityField, rTurb.EdgeDensityField);
        MatrixAssert.Equal(edgeSnapshot, rTurb.EdgeDensityField!);
        Assert.Same(rGranNoise.PaperHeightMap, rTurb.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: turbOpts), rTurb);

        // 6. EdgeDarkening only -> TurbulenceFlow, PaperHeightMap, and Stages 1-5 stay cached; TurbulenceDensityField is untouched
        Matrix<DoublePixel> turbSnapshot = rTurb.TurbulenceDensityField!.Clone();
        WatercolorOptions edgeOpts = turbOpts with
        {
            EdgeDarkening = new EdgeDarkeningOptions(Intensity: 2.5, BlurSigma: 1.1)
        };
        WatercolorResult rEdge = session.Render(edgeOpts);
        AssertCacheStatus(
            rEdge,
            expectedMisses:
            [
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(rTurb.TurbulenceDensityField, rEdge.TurbulenceDensityField);
        MatrixAssert.Equal(turbSnapshot, rEdge.TurbulenceDensityField!);
        Assert.Same(rGranNoise.PaperHeightMap, rEdge.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: edgeOpts), rEdge);
    }

    /// <summary>
    /// Verifies that mutating Stage 2 Saliency steps keeps Stage 3a Mean-Shift (<see cref="WatercolorStep.MeanShiftFiltering"/>
    /// and <see cref="WatercolorStep.MeanShiftClustering"/>), <see cref="WatercolorStep.TurbulenceFlow"/>, and
    /// <see cref="WatercolorStep.PaperHeightMap"/> cached, and that mutating only <see cref="WatercolorOptions.SaliencyDistanceField"/>
    /// reuses the exact cached <see cref="WatercolorResult.RawSaliencyDistanceField"/> instance without recomputing JFA.
    /// </summary>
    [Fact]
    public void Render_Stage2SaliencyMutations_ShouldKeepMeanShiftAndIndependentStage6Cached()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var session = new WatercolorPipelineSession(scene);
        WatercolorOptions baseOpts = WatercolorOptions.Default;

        WatercolorResult r0 = session.Render(baseOpts);

        // 1. Mutate SaliencyDistanceField only -> RawSaliencyDistanceField reused by reference, SaliencyCut & MeanShift* cached
        WatercolorOptions distOpts = baseOpts with
        {
            SaliencyDistanceField = new SaliencyDistanceFieldOptions(SmoothingSigma: 1.75)
        };
        WatercolorResult rDist = session.Render(distOpts);
        AssertCacheStatus(
            rDist,
            expectedMisses:
            [
                WatercolorStep.SaliencyDistanceField,
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(r0.RawSaliencyDistanceField, rDist.RawSaliencyDistanceField);
        Assert.NotSame(r0.SaliencyDistanceField, rDist.SaliencyDistanceField);
        Assert.Same(r0.SalientMask, rDist.SalientMask);
        Assert.Same(r0.MeanShiftFiltered, rDist.MeanShiftFiltered);
        Assert.Same(r0.SegmentationGraph, rDist.SegmentationGraph);
        Assert.Same(r0.TurbulenceDensityField, rDist.TurbulenceDensityField);
        Assert.Same(r0.PaperHeightMap, rDist.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: distOpts), rDist);

        // 2. Mutate SaliencyCut only -> MeanShiftFiltering and MeanShiftClustering remain cached
        WatercolorOptions cutOpts = distOpts with
        {
            SaliencyCut = new SaliencyCutOptions(MaxOuterIterations: 2, TrimapBandRadius: 5)
        };
        WatercolorResult rCut = session.Render(cutOpts);
        AssertCacheStatus(
            rCut,
            expectedMisses:
            [
                WatercolorStep.SaliencyCut,
                WatercolorStep.SaliencyDistanceField,
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(r0.SaliencyMap, rCut.SaliencyMap);
        Assert.Same(r0.MeanShiftFiltered, rCut.MeanShiftFiltered);
        Assert.Same(r0.SegmentationGraph, rCut.SegmentationGraph);
        Assert.Same(r0.TurbulenceDensityField, rCut.TurbulenceDensityField);
        Assert.Same(r0.PaperHeightMap, rCut.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: cutOpts), rCut);

        // 3. Mutate RegionalContrastSaliency only -> EGBI and MeanShift* remain cached
        WatercolorOptions rcOpts = cutOpts with
        {
            RegionalContrastSaliency = new RegionalContrastSaliencyOptions(CenterPriorWeight: 2.0)
        };
        WatercolorResult rRc = session.Render(rcOpts);
        AssertCacheStatus(
            rRc,
            expectedMisses:
            [
                WatercolorStep.RegionalContrastSaliency,
                WatercolorStep.SaliencyCut,
                WatercolorStep.SaliencyDistanceField,
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(r0.EgbiGraph, rRc.EgbiGraph);
        Assert.Same(r0.MeanShiftFiltered, rRc.MeanShiftFiltered);
        Assert.Same(r0.SegmentationGraph, rRc.SegmentationGraph);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: rcOpts), rRc);

        // 4. Mutate Egbi options only -> EgbiPreBlur and MeanShift* remain cached
        WatercolorOptions egbiOpts = rcOpts with
        {
            Egbi = WatercolorOptions.DefaultSaliencyEgbi with { KFactor = 35.0 }
        };
        WatercolorResult rEgbi = session.Render(egbiOpts);
        AssertCacheStatus(
            rEgbi,
            expectedMisses:
            [
                WatercolorStep.EgbiSegmentation,
                WatercolorStep.RegionalContrastSaliency,
                WatercolorStep.SaliencyCut,
                WatercolorStep.SaliencyDistanceField,
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(r0.EgbiPreBlurred, rEgbi.EgbiPreBlurred);
        Assert.Same(r0.MeanShiftFiltered, rEgbi.MeanShiftFiltered);
        Assert.Same(r0.SegmentationGraph, rEgbi.SegmentationGraph);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: egbiOpts), rEgbi);

        // 5. Mutate EgbiBlurSigma -> ColorTransfer, MeanShift*, TurbulenceFlow, PaperHeightMap remain cached
        WatercolorOptions blurOpts = egbiOpts with { EgbiBlurSigma = 0.0 };
        WatercolorResult rBlur = session.Render(blurOpts);
        AssertCacheStatus(
            rBlur,
            expectedMisses:
            [
                WatercolorStep.EgbiPreBlur,
                WatercolorStep.EgbiSegmentation,
                WatercolorStep.RegionalContrastSaliency,
                WatercolorStep.SaliencyCut,
                WatercolorStep.SaliencyDistanceField,
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.True(rBlur.StepTimings[WatercolorStep.EgbiPreBlur].Bypassed);
        Assert.Same(r0.ColorTransferred, rBlur.ColorTransferred);
        Assert.Same(r0.MeanShiftFiltered, rBlur.MeanShiftFiltered);
        Assert.Same(r0.SegmentationGraph, rBlur.SegmentationGraph);
        Assert.Same(r0.TurbulenceDensityField, rBlur.TurbulenceDensityField);
        Assert.Same(r0.PaperHeightMap, rBlur.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: blurOpts), rBlur);
    }

    /// <summary>
    /// Verifies fine-grained cache invalidation for Stage 3b (<see cref="WatercolorStep.LodAbstraction"/>),
    /// Stage 4 (<see cref="WatercolorStep.WetInWet"/>), and Stage 5 (<see cref="WatercolorStep.HandTremor"/>)
    /// while preserving all upstream steps, <see cref="WatercolorStep.TurbulenceFlow"/>, and <see cref="WatercolorStep.PaperHeightMap"/>.
    /// </summary>
    [Fact]
    public void Render_Stages3bTo5Mutations_ShouldPreserveUpstreamAndIndependentStage6()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var session = new WatercolorPipelineSession(scene);
        WatercolorOptions baseOpts = WatercolorOptions.Default;

        WatercolorResult r0 = session.Render(baseOpts);

        // 1. HandTremor only
        WatercolorOptions tremorOpts = baseOpts with { HandTremor = new HandTremorOptions(Amplitude: 4.5) };
        WatercolorResult rTremor = session.Render(tremorOpts);
        AssertCacheStatus(
            rTremor,
            expectedMisses:
            [
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(r0.WetInWet, rTremor.WetInWet);
        Assert.Same(r0.TurbulenceDensityField, rTremor.TurbulenceDensityField);
        Assert.Same(r0.PaperHeightMap, rTremor.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: tremorOpts), rTremor);

        // 2. WetInWet only
        WatercolorOptions wetOpts = tremorOpts with { WetInWet = new WetInWetOptions(SeedProbability: 0.7) };
        WatercolorResult rWet = session.Render(wetOpts);
        AssertCacheStatus(
            rWet,
            expectedMisses:
            [
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(r0.LodAbstracted, rWet.LodAbstracted);
        Assert.Same(r0.TurbulenceDensityField, rWet.TurbulenceDensityField);
        Assert.Same(r0.PaperHeightMap, rWet.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: wetOpts), rWet);

        // 3. LodAbstraction only
        WatercolorOptions lodOpts = wetOpts with { LodAbstraction = new LodAbstractionOptions(SalientKernelSize: 3) };
        WatercolorResult rLod = session.Render(lodOpts);
        AssertCacheStatus(
            rLod,
            expectedMisses:
            [
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(r0.SegmentationGraph, rLod.SegmentationGraph);
        Assert.Same(r0.SaliencyDistanceField, rLod.SaliencyDistanceField);
        Assert.Same(r0.TurbulenceDensityField, rLod.TurbulenceDensityField);
        Assert.Same(r0.PaperHeightMap, rLod.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: lodOpts), rLod);
    }

    /// <summary>
    /// Verifies Stage 3a Mean-Shift independence from Stage 2 and the internal mode-seeking vs clustering cache split:
    /// mutating only <see cref="MeanShiftOptions.MinRegionSize"/> or <see cref="MeanShiftOptions.Connectivity"/> keeps
    /// <see cref="WatercolorStep.MeanShiftFiltering"/> and all Stage 2 steps cached; mutating
    /// <see cref="MeanShiftOptions.SpatialRadius"/> recomputes filtering &amp; clustering while keeping Stage 2,
    /// <see cref="WatercolorStep.TurbulenceFlow"/>, and <see cref="WatercolorStep.PaperHeightMap"/> cached.
    /// </summary>
    [Fact]
    public void Render_Stage3aMeanShiftMutations_ShouldHonorFilteringClusteringSplitAndStage2Independence()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var session = new WatercolorPipelineSession(scene);
        var baseMeanShift = new MeanShiftOptions(SpatialRadius: 6, RangeRadius: 12.0, MinRegionSize: 16);
        var baseOpts = new WatercolorOptions(MeanShift: baseMeanShift);

        WatercolorResult r0 = session.Render(baseOpts);

        // 1. Mutate MinRegionSize only -> MeanShiftFiltering + all Stage 2 + TurbulenceFlow + PaperHeightMap cached
        WatercolorOptions minSizeOpts = baseOpts with
        {
            MeanShift = baseMeanShift with { MinRegionSize = 4 }
        };
        WatercolorResult rMinSize = session.Render(minSizeOpts);
        AssertCacheStatus(
            rMinSize,
            expectedMisses:
            [
                WatercolorStep.MeanShiftClustering,
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(r0.MeanShiftFiltered, rMinSize.MeanShiftFiltered);
        Assert.Same(r0.SaliencyDistanceField, rMinSize.SaliencyDistanceField);
        Assert.Same(r0.TurbulenceDensityField, rMinSize.TurbulenceDensityField);
        Assert.Same(r0.PaperHeightMap, rMinSize.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: minSizeOpts), rMinSize);

        // 2. Mutate Connectivity only -> MeanShiftFiltering + all Stage 2 + TurbulenceFlow + PaperHeightMap cached
        WatercolorOptions connOpts = minSizeOpts with
        {
            MeanShift = minSizeOpts.MeanShift! with { Connectivity = GridConnectivity.FourConnected }
        };
        WatercolorResult rConn = session.Render(connOpts);
        AssertCacheStatus(
            rConn,
            expectedMisses:
            [
                WatercolorStep.MeanShiftClustering,
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Equal(GridConnectivity.FourConnected, rConn.SegmentationGraph.Connectivity);
        Assert.Same(r0.MeanShiftFiltered, rConn.MeanShiftFiltered);
        Assert.Same(r0.SaliencyDistanceField, rConn.SaliencyDistanceField);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: connOpts), rConn);

        // 3. Mutate SpatialRadius -> MeanShiftFiltering + MeanShiftClustering miss, all Stage 2 + TurbulenceFlow + PaperHeightMap stay cached
        WatercolorOptions spatialOpts = connOpts with
        {
            MeanShift = connOpts.MeanShift! with { SpatialRadius = 4 }
        };
        WatercolorResult rSpatial = session.Render(spatialOpts);
        AssertCacheStatus(
            rSpatial,
            expectedMisses:
            [
                WatercolorStep.MeanShiftFiltering,
                WatercolorStep.MeanShiftClustering,
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.NotSame(r0.MeanShiftFiltered, rSpatial.MeanShiftFiltered);
        Assert.Same(r0.EgbiGraph, rSpatial.EgbiGraph);
        Assert.Same(r0.SalientMask, rSpatial.SalientMask);
        Assert.Same(r0.SaliencyDistanceField, rSpatial.SaliencyDistanceField);
        Assert.Same(r0.TurbulenceDensityField, rSpatial.TurbulenceDensityField);
        Assert.Same(r0.PaperHeightMap, rSpatial.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: spatialOpts), rSpatial);
    }

    /// <summary>
    /// Verifies that mutating Stage 1 <see cref="WatercolorOptions.ColorTransferStrength"/> invalidates Stages 1–5
    /// and dependent Stage 6 steps while keeping <see cref="WatercolorStep.TurbulenceFlow"/> and
    /// <see cref="WatercolorStep.PaperHeightMap"/> cached.
    /// </summary>
    [Fact]
    public void Render_Stage1ColorTransferMutation_ShouldKeepTurbulenceAndPaperHeightMapCached()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);
        var session = new WatercolorPipelineSession(scene);
        WatercolorResult r0 = session.Render(WatercolorOptions.Default);

        var updated = new WatercolorOptions(ColorTransferStrength: 0.4);
        WatercolorResult r1 = session.Render(updated);

        AssertCacheStatus(
            r1,
            expectedMisses:
            [
                WatercolorStep.ColorTransfer,
                WatercolorStep.EgbiPreBlur,
                WatercolorStep.EgbiSegmentation,
                WatercolorStep.RegionalContrastSaliency,
                WatercolorStep.SaliencyCut,
                WatercolorStep.SaliencyDistanceField,
                WatercolorStep.MeanShiftFiltering,
                WatercolorStep.MeanShiftClustering,
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(r0.TurbulenceDensityField, r1.TurbulenceDensityField);
        Assert.Same(r0.PaperHeightMap, r1.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: updated), r1);
    }

    /// <summary>
    /// Verifies toggling each <c>Enable*</c> flag off, repeating the disabled render (cached bypass),
    /// and toggling back on produces exact parity with fresh <see cref="WatercolorPipeline.RenderDetailed"/> runs.
    /// </summary>
    [Fact]
    public void Render_ToggleEnableFlagsOffAndBackOn_ShouldBypassCacheAndRestoreBitIdentically()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);
        var session = new WatercolorPipelineSession(scene);
        WatercolorResult baseline = session.Render(WatercolorOptions.Default);

        (WatercolorOptions DisabledOptions, WatercolorStep ToggledStep)[] cases =
        [
            (new WatercolorOptions(EnableFxaa: false), WatercolorStep.Fxaa),
            (new WatercolorOptions(EnablePaperGranulation: false), WatercolorStep.PaperGranulation),
            (new WatercolorOptions(EnableToneCurve: false), WatercolorStep.ToneCurve),
            (new WatercolorOptions(EnableTurbulenceFlow: false), WatercolorStep.TurbulenceFlow),
            (new WatercolorOptions(EnableEdgeDarkening: false), WatercolorStep.EdgeDarkening),
            (new WatercolorOptions(EnableHandTremor: false), WatercolorStep.HandTremor),
            (new WatercolorOptions(EnableWetInWet: false), WatercolorStep.WetInWet),
            (new WatercolorOptions(EnableLodAbstraction: false), WatercolorStep.LodAbstraction),
            (new WatercolorOptions(EnableColorTransfer: false), WatercolorStep.ColorTransfer)
        ];

        foreach ((WatercolorOptions disabledOpts, WatercolorStep step) in cases)
        {
            WatercolorResult disabled = session.Render(disabledOpts);
            Assert.False(disabled.StepTimings[step].FromCache);
            Assert.True(disabled.StepTimings[step].Bypassed);
            AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: disabledOpts), disabled);

            // Second render with identical disabled options must hit cache for all 18 steps while preserving Bypassed
            WatercolorResult disabledCached = session.Render(disabledOpts);
            AssertCacheStatus(disabledCached, expectedMisses: []);
            Assert.True(disabledCached.StepTimings[step].Bypassed);

            // Re-enabling restores exact baseline output
            WatercolorResult restored = session.Render(WatercolorOptions.Default);
            Assert.False(restored.StepTimings[step].FromCache);
            Assert.False(restored.StepTimings[step].Bypassed);
            AssertResultsBitIdentical(baseline, restored);
        }
    }

    /// <summary>
    /// Verifies that when <see cref="WatercolorOptions.EnableLodAbstraction"/> is disabled, mutating
    /// <see cref="WatercolorOptions.SaliencyDistanceField"/> leaves <see cref="WatercolorStep.LodAbstraction"/>
    /// and <see cref="WatercolorStep.WetInWet"/> cached while recomputing <see cref="WatercolorStep.HandTremor"/>,
    /// and that when <see cref="WatercolorOptions.EnableWetInWet"/> is disabled, mutating
    /// <see cref="MeanShiftOptions.MinRegionSize"/> updates <see cref="WetInWetResult.QualifyingBoundaries"/> length.
    /// </summary>
    [Fact]
    public void Render_DisabledUpstreamStages_ShouldMaintainDownstreamInvariantsAndCachePrecision()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var session = new WatercolorPipelineSession(scene);

        var opts1 = new WatercolorOptions(
            MeanShift: new MeanShiftOptions(SpatialRadius: 6, RangeRadius: 12.0, MinRegionSize: 16),
            EnableLodAbstraction: false,
            EnableWetInWet: false,
            EnableHandTremor: true);
        WatercolorResult r1 = session.Render(opts1);

        // Mutating SaliencyDistanceField when LOD & WetInWet are disabled should keep both cached and recompute HandTremor
        WatercolorOptions opts2 = opts1 with
        {
            SaliencyDistanceField = new SaliencyDistanceFieldOptions(SmoothingSigma: 1.5)
        };
        WatercolorResult r2 = session.Render(opts2);
        AssertCacheStatus(
            r2,
            expectedMisses:
            [
                WatercolorStep.SaliencyDistanceField,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.True(r2.StepTimings[WatercolorStep.LodAbstraction].FromCache);
        Assert.True(r2.StepTimings[WatercolorStep.LodAbstraction].Bypassed);
        Assert.True(r2.StepTimings[WatercolorStep.WetInWet].FromCache);
        Assert.True(r2.StepTimings[WatercolorStep.WetInWet].Bypassed);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: opts2), r2);

        // Mutating MinRegionSize when WetInWet is disabled must still refresh WetInWet.QualifyingBoundaries count
        WatercolorOptions opts3 = opts2 with
        {
            MeanShift = opts2.MeanShift! with { MinRegionSize = 4 }
        };
        WatercolorResult r3 = session.Render(opts3);
        Assert.Equal(r3.SegmentationGraph.Boundaries.Count, r3.WetInWet.QualifyingBoundaries.Count);
        Assert.All(r3.WetInWet.QualifyingBoundaries, static q => Assert.False(q));
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: opts3), r3);
    }

    /// <summary>
    /// Verifies that a pre-canceled token throws <see cref="OperationCanceledException"/> immediately
    /// without invoking callbacks or mutating session state.
    /// </summary>
    [Fact]
    public void Render_PreCanceledToken_ShouldThrowWithoutInvokingCallbackOrMutatingState()
    {
        Matrix<Rgb8> scene = CreateTestScene(16, 16);
        var session = new WatercolorPipelineSession(scene);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        int callbackCount = 0;
        Assert.Throws<OperationCanceledException>(() =>
            session.Render(onStepCompleted: _ => callbackCount++, cancellationToken: cts.Token));

        Assert.Equal(0, callbackCount);
        Assert.Null(session.LastOptions);
        Assert.Null(session.LastResult);

        // Pre-canceled token also throws on empty images
        var emptySession = new WatercolorPipelineSession(new SequentialMatrix<Rgb8>(0, 0));
        Assert.Throws<OperationCanceledException>(() =>
            emptySession.Render(cancellationToken: cts.Token));
    }

    /// <summary>
    /// Verifies that mid-run cancellation during a cold render preserves already-completed upstream steps in cache
    /// for a subsequent uncancelled render and yields bit-for-bit identical output to a fresh run.
    /// </summary>
    [Fact]
    public void Render_MidRunCancellation_ColdSession_ShouldPreserveCompletedUpstreamCache()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var session = new WatercolorPipelineSession(scene);
        using var cts = new CancellationTokenSource();
        var canceledSteps = new List<WatercolorStep>();

        Assert.Throws<OperationCanceledException>(() =>
            session.Render(
                WatercolorOptions.Default,
                onStepCompleted: p =>
                {
                    canceledSteps.Add(p.Step);
                    if (p.Step == WatercolorStep.SaliencyCut)
                    {
                        cts.Cancel();
                    }
                },
                cancellationToken: cts.Token));

        Assert.Equal(
            [
                WatercolorStep.ColorTransfer,
                WatercolorStep.EgbiPreBlur,
                WatercolorStep.EgbiSegmentation,
                WatercolorStep.RegionalContrastSaliency,
                WatercolorStep.SaliencyCut
            ],
            canceledSteps);
        Assert.Null(session.LastOptions);
        Assert.Null(session.LastResult);

        // Subsequent uncancelled render reuses Steps 1..5 from cache and computes Steps 6..18 cleanly
        WatercolorResult resumed = session.Render(WatercolorOptions.Default);
        AssertCacheStatus(
            resumed,
            expectedMisses:
            [
                WatercolorStep.SaliencyDistanceField,
                WatercolorStep.MeanShiftFiltering,
                WatercolorStep.MeanShiftClustering,
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.TurbulenceFlow,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperHeightMap,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);

        WatercolorResult fresh = WatercolorPipeline.RenderDetailed(scene, options: WatercolorOptions.Default);
        AssertResultsBitIdentical(fresh, resumed);
    }

    /// <summary>
    /// Verifies atomic / cancellation-safe cache invalidation on a warm session: mutating an upstream stage
    /// and canceling mid-run invalidates stale downstream entries before execution so the next render never
    /// pairs stale downstream artifacts with newer upstream artifacts, while keeping already-completed upstream
    /// and independent Stage 6 branches cached.
    /// </summary>
    [Fact]
    public void Render_MidRunCancellation_WarmSession_ShouldNotLeakStaleDownstreamArtifacts()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var session = new WatercolorPipelineSession(scene);
        var optsA = new WatercolorOptions(ColorTransferStrength: 1.0);
        var optsB = new WatercolorOptions(ColorTransferStrength: 0.35);

        WatercolorResult runA = session.Render(optsA);
        Assert.Same(optsA, session.LastOptions);
        Assert.Same(runA, session.LastResult);

        // Mutate ColorTransfer (optsB) and cancel immediately after ColorTransfer completes
        using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() =>
            session.Render(
                optsB,
                onStepCompleted: p =>
                {
                    if (p.Step == WatercolorStep.ColorTransfer)
                    {
                        cts.Cancel();
                    }
                },
                cancellationToken: cts.Token));

        // Failed render must not overwrite LastOptions / LastResult
        Assert.Same(optsA, session.LastOptions);
        Assert.Same(runA, session.LastResult);

        // Complete optsB: ColorTransfer, TurbulenceFlow, and PaperHeightMap stay cached;
        // all downstream steps of ColorTransfer were invalidated and must recompute.
        WatercolorResult runB = session.Render(optsB);
        AssertCacheStatus(
            runB,
            expectedMisses:
            [
                WatercolorStep.EgbiPreBlur,
                WatercolorStep.EgbiSegmentation,
                WatercolorStep.RegionalContrastSaliency,
                WatercolorStep.SaliencyCut,
                WatercolorStep.SaliencyDistanceField,
                WatercolorStep.MeanShiftFiltering,
                WatercolorStep.MeanShiftClustering,
                WatercolorStep.LodAbstraction,
                WatercolorStep.WetInWet,
                WatercolorStep.HandTremor,
                WatercolorStep.EdgeDarkening,
                WatercolorStep.PigmentCombine,
                WatercolorStep.ToneCurve,
                WatercolorStep.PaperGranulation,
                WatercolorStep.Fxaa
            ]);
        Assert.Same(runA.TurbulenceDensityField, runB.TurbulenceDensityField);
        Assert.Same(runA.PaperHeightMap, runB.PaperHeightMap);
        AssertResultsBitIdentical(WatercolorPipeline.RenderDetailed(scene, options: optsB), runB);

        // Also test canceling after SaliencyCut mutation and then reverting back to optsB
        var optsC = optsB with { SaliencyCut = new SaliencyCutOptions(MaxOuterIterations: 2, TrimapBandRadius: 5) };
        using var cts2 = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() =>
            session.Render(
                optsC,
                onStepCompleted: p =>
                {
                    if (p.Step == WatercolorStep.SaliencyCut)
                    {
                        cts2.Cancel();
                    }
                },
                cancellationToken: cts2.Token));

        WatercolorResult revertedB = session.Render(optsB);
        Assert.Same(runB.MeanShiftFiltered, revertedB.MeanShiftFiltered);
        Assert.Same(runB.SegmentationGraph, revertedB.SegmentationGraph);
        Assert.Same(runA.TurbulenceDensityField, revertedB.TurbulenceDensityField);
        Assert.Same(runA.PaperHeightMap, revertedB.PaperHeightMap);
        AssertResultsBitIdentical(runB, revertedB);
    }

    /// <summary>
    /// Verifies that re-entrant and concurrent calls to <see cref="WatercolorPipelineSession.Render"/>
    /// or <see cref="WatercolorPipelineSession.ResetCache"/> throw <see cref="InvalidOperationException"/>
    /// and leave the session usable afterward.
    /// </summary>
    [Fact]
    public async Task Render_And_ResetCache_ReentrantOrConcurrentCalls_ShouldThrowInvalidOperationException()
    {
        Matrix<Rgb8> scene = CreateTestScene(16, 16);
        var session = new WatercolorPipelineSession(scene);

        // 1. Re-entrant Render inside onStepCompleted
        Assert.Throws<InvalidOperationException>(() =>
            session.Render(onStepCompleted: p =>
            {
                if (p.Step == WatercolorStep.ColorTransfer)
                {
                    session.Render();
                }
            }));

        // Session must not be stuck busy after the exception
        WatercolorResult afterReentrantRender = session.Render();
        Assert.NotNull(afterReentrantRender);

        // 2. Re-entrant ResetCache inside onStepCompleted
        Assert.Throws<InvalidOperationException>(() =>
            session.Render(
                new WatercolorOptions(EnableFxaa: false),
                onStepCompleted: p =>
                {
                    if (p.Step == WatercolorStep.Fxaa)
                    {
                        session.ResetCache();
                    }
                }));

        // 3. Concurrent Render and ResetCache from another thread
        var enteredCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCallback = new ManualResetEventSlim(false);
        Task<WatercolorResult> backgroundRender = Task.Run(() =>
            session.Render(
                new WatercolorOptions(EnableFxaa: true),
                onStepCompleted: p =>
                {
                    if (p.Step == WatercolorStep.ColorTransfer)
                    {
                        enteredCallback.TrySetResult();
                        releaseCallback.Wait();
                    }
                }));

        await enteredCallback.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.Throws<InvalidOperationException>(() => session.Render());
            Assert.Throws<InvalidOperationException>(() => session.ResetCache());
        }
        finally
        {
            releaseCallback.Set();
        }

        WatercolorResult completed = await backgroundRender;
        Assert.Same(completed, session.LastResult);
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorPipelineSession.ResetCache"/> clears all cached artifacts,
    /// resets <see cref="WatercolorPipelineSession.LastOptions"/> and <see cref="WatercolorPipelineSession.LastResult"/>,
    /// and forces all 18 steps to recompute on the next <see cref="WatercolorPipelineSession.Render"/> call.
    /// </summary>
    [Fact]
    public void ResetCache_ShouldInvalidateAllStepsAndClearLastState()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);
        var session = new WatercolorPipelineSession(scene);

        WatercolorResult r1 = session.Render(WatercolorOptions.Default);
        Assert.NotNull(session.LastOptions);
        Assert.NotNull(session.LastResult);

        session.ResetCache();
        Assert.Null(session.LastOptions);
        Assert.Null(session.LastResult);

        WatercolorResult r2 = session.Render(WatercolorOptions.Default);
        AssertCacheStatus(r2, expectedMisses: AllStepsInOrder);
        Assert.NotSame(r1.RawSaliencyDistanceField, r2.RawSaliencyDistanceField);
        Assert.NotSame(r1.PaperHeightMap, r2.PaperHeightMap);
        AssertResultsBitIdentical(r1, r2);
    }

    /// <summary>
    /// Verifies that empty images (0x0, 0x5, 5x0) return empty results without invoking callbacks
    /// and with empty <see cref="WatercolorResult.StepTimings"/>.
    /// </summary>
    /// <param name="width">The empty image width.</param>
    /// <param name="height">The empty image height.</param>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void Render_EmptyImage_ShouldReturnEmptyResultWithoutCallbacks(int width, int height)
    {
        var empty = new SequentialMatrix<Rgb8>(width, height);
        var session = new WatercolorPipelineSession(empty);
        var reported = new List<WatercolorStepProgress>();

        WatercolorResult result = session.Render(onStepCompleted: reported.Add);
        WatercolorResult fresh = WatercolorPipeline.RenderDetailed(empty);

        Assert.Empty(reported);
        Assert.Empty(result.StepTimings);
        Assert.Equal(empty.Size, result.Image.Size);
        Assert.Same(WatercolorOptions.Default, session.LastOptions);
        Assert.Same(result, session.LastResult);
        AssertResultsBitIdentical(fresh, result);
    }

    private static void AssertProgressMatchesTimings(
        IReadOnlyList<WatercolorStepProgress> progress,
        WatercolorResult result)
    {
        Assert.Equal(AllStepsInOrder.Length, progress.Count);
        Assert.Equal(AllStepsInOrder.Length, result.StepTimings.Count);
        for (int i = 0; i < progress.Count; i++)
        {
            WatercolorStepProgress p = progress[i];
            WatercolorStepTiming t = result.StepTimings[p.Step];
            Assert.Equal(p.Elapsed, t.Elapsed);
            Assert.Equal(p.FromCache, t.FromCache);
            Assert.Equal(p.Bypassed, t.Bypassed);
        }
    }

    private static void AssertCacheStatus(WatercolorResult result, IReadOnlyCollection<WatercolorStep> expectedMisses)
    {
        var missSet = new HashSet<WatercolorStep>(expectedMisses);
        Assert.Equal(AllStepsInOrder.Length, result.StepTimings.Count);
        foreach (WatercolorStep step in AllStepsInOrder)
        {
            bool expectedFromCache = !missSet.Contains(step);
            WatercolorStepTiming timing = result.StepTimings[step];
            Assert.True(
                timing.FromCache == expectedFromCache,
                $"Step {step}: expected FromCache={expectedFromCache}, actual FromCache={timing.FromCache}.");
        }
    }

    private static void AssertResultsBitIdentical(WatercolorResult expected, WatercolorResult actual)
    {
        Assert.Equal(expected.SelectedPalette, actual.SelectedPalette);
        MatrixAssert.Equal(expected.ColorTransferred, actual.ColorTransferred);
        MatrixAssert.Equal(expected.EgbiPreBlurred, actual.EgbiPreBlurred);
        Assert.Equal(expected.EgbiGraph.Connectivity, actual.EgbiGraph.Connectivity);
        Assert.Equal(expected.EgbiGraph.RegionCount, actual.EgbiGraph.RegionCount);
        Assert.Equal(expected.EgbiGraph.Boundaries.Count, actual.EgbiGraph.Boundaries.Count);
        MatrixAssert.Equal(expected.EgbiGraph.Labels, actual.EgbiGraph.Labels);
        MatrixAssert.Equal(expected.SaliencyMap, actual.SaliencyMap);
        MatrixAssert.Equal(expected.SalientMask, actual.SalientMask);
        MatrixAssert.Equal(expected.RawSaliencyDistanceField, actual.RawSaliencyDistanceField);
        MatrixAssert.Equal(expected.SaliencyDistanceField, actual.SaliencyDistanceField);
        MatrixAssert.Equal(expected.MeanShiftFiltered, actual.MeanShiftFiltered);
        Assert.Equal(expected.SegmentationGraph.Connectivity, actual.SegmentationGraph.Connectivity);
        Assert.Equal(expected.SegmentationGraph.RegionCount, actual.SegmentationGraph.RegionCount);
        Assert.Equal(expected.SegmentationGraph.Boundaries.Count, actual.SegmentationGraph.Boundaries.Count);
        MatrixAssert.Equal(expected.SegmentationGraph.Labels, actual.SegmentationGraph.Labels);
        MatrixAssert.Equal(expected.LodAbstracted, actual.LodAbstracted);
        MatrixAssert.Equal(expected.WetInWet.Image, actual.WetInWet.Image);
        MatrixAssert.Equal(expected.WetInWet.WetAreaMask, actual.WetInWet.WetAreaMask);
        Assert.Equal(expected.WetInWet.QualifyingBoundaries, actual.WetInWet.QualifyingBoundaries);
        MatrixAssert.Equal(expected.HandTremor, actual.HandTremor);

        if (expected.EdgeDensityField is null)
        {
            Assert.Null(actual.EdgeDensityField);
        }
        else
        {
            Assert.NotNull(actual.EdgeDensityField);
            MatrixAssert.Equal(expected.EdgeDensityField, actual.EdgeDensityField);
        }

        if (expected.TurbulenceDensityField is null)
        {
            Assert.Null(actual.TurbulenceDensityField);
        }
        else
        {
            Assert.NotNull(actual.TurbulenceDensityField);
            MatrixAssert.Equal(expected.TurbulenceDensityField, actual.TurbulenceDensityField);
        }

        MatrixAssert.Equal(expected.DensityAdjusted, actual.DensityAdjusted);
        MatrixAssert.Equal(expected.ToneAdjusted, actual.ToneAdjusted);

        if (expected.PaperHeightMap is null)
        {
            Assert.Null(actual.PaperHeightMap);
        }
        else
        {
            Assert.NotNull(actual.PaperHeightMap);
            MatrixAssert.Equal(expected.PaperHeightMap, actual.PaperHeightMap);
        }

        MatrixAssert.Equal(expected.Granulated, actual.Granulated);
        MatrixAssert.Equal(expected.Image, actual.Image);
    }

    private static Matrix<Rgb8> CreateTestScene(int width, int height) =>
        TestMatrices.Create(width, height, (x, y) =>
        {
            bool insideSubject = x >= width / 4 && x < (3 * width) / 4 && y >= height / 4 && y < (3 * height) / 4;
            if (insideSubject)
            {
                return new Rgb8((byte)(210 + (x % 15)), (byte)(40 + (y % 15)), 35);
            }

            return x < width / 2 ? new Rgb8(30, 80, 190) : new Rgb8(230, 235, 240);
        });
}
