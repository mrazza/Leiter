namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using Leiter.Algorithms;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Noise;
using Leiter.Algorithms.Saliency;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Pixels.ColorSpaces;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides end-to-end unit tests for <see cref="WatercolorPipeline"/>.
/// </summary>
public class WatercolorPipelineTests
{
    /// <summary>
    /// Verifies that <see cref="WatercolorPipeline.RenderDetailed"/> executes Stages 1–6 deterministically
    /// on a synthetic scene and matches <see cref="WatercolorPipeline.Render"/>.
    /// </summary>
    [Fact]
    public void RenderDetailed_And_Render_SyntheticScene_ShouldBeDeterministicAndMatch()
    {
        var image = CreateTestScene(24, 24);

        WatercolorResult detailed1 = WatercolorPipeline.RenderDetailed(image);
        WatercolorResult detailed2 = WatercolorPipeline.RenderDetailed(image);
        Matrix<Rgb8> rendered = WatercolorPipeline.Render(image);

        Assert.Equal(image.Size, detailed1.Image.Size);
        Assert.Equal(image.Size, detailed1.ColorTransferred.Size);
        Assert.Equal(image.Size, detailed1.EgbiPreBlurred.Size);
        Assert.Equal(image.Size, detailed1.EgbiGraph.Size);
        Assert.True(detailed1.EgbiGraph.RegionCount >= 1);
        Assert.Equal(image.Size, detailed1.SaliencyMap.Size);
        Assert.Equal(image.Size, detailed1.SalientMask.Size);
        Assert.Equal(image.Size, detailed1.RawSaliencyDistanceField.Size);
        Assert.Equal(image.Size, detailed1.SaliencyDistanceField.Size);
        Assert.Equal(image.Size, detailed1.MeanShiftFiltered.Size);
        Assert.Equal(image.Size, detailed1.SegmentationGraph.Size);
        Assert.Equal(image.Size, detailed1.LodAbstracted.Size);
        Assert.Equal(image.Size, detailed1.WetInWet.Image.Size);
        Assert.Equal(image.Size, detailed1.HandTremor.Size);
        Assert.NotNull(detailed1.EdgeDensityField);
        Assert.Equal(image.Size, detailed1.EdgeDensityField.Size);
        Assert.NotNull(detailed1.TurbulenceDensityField);
        Assert.Equal(image.Size, detailed1.TurbulenceDensityField.Size);
        Assert.Equal(image.Size, detailed1.DensityAdjusted.Size);
        Assert.Equal(image.Size, detailed1.ToneAdjusted.Size);
        Assert.NotNull(detailed1.PaperHeightMap);
        Assert.Equal(image.Size, detailed1.PaperHeightMap.Size);
        Assert.Equal(image.Size, detailed1.Granulated.Size);
        Assert.NotNull(detailed1.SelectedPalette);
        Assert.True(detailed1.SegmentationGraph.RegionCount >= 1);
        Assert.Equal(Enum.GetValues<WatercolorStep>().Length, detailed1.StepTimings.Count);
        foreach (WatercolorStep step in Enum.GetValues<WatercolorStep>())
        {
            Assert.True(detailed1.StepTimings.TryGetValue(step, out WatercolorStepTiming timing));
            Assert.True(timing.Elapsed >= TimeSpan.Zero);
            Assert.False(timing.FromCache);
            Assert.False(timing.Bypassed);
        }

        MatrixAssert.Equal(detailed1.Image, detailed2.Image);
        MatrixAssert.Equal(detailed1.Image, rendered);
    }

    /// <summary>
    /// Verifies that pipeline tone compression prevents pure black and pure white outputs
    /// and honors explicit palettes, custom paper height maps, custom <see cref="IReadOnlyMatrix{Rgb8}"/> inputs, and <c>EnableFxaa = false</c>.
    /// </summary>
    [Fact]
    public void RenderDetailed_CustomOptionsAndHeightMap_ShouldRespectConfiguration()
    {
        Matrix<Rgb8> scene = CreateTestScene(16, 16);
        var readOnlyScene = new MatrixView<Rgb8>(scene, 0, 0, scene.Width, scene.Height, EdgeHandling.Extend);

        SequentialMatrix<DoublePixel> flatHeightMap = TestMatrices.Constant(16, 16, new DoublePixel(0.5));

        ColorTransferProfile customPalette = WatercolorPalettes.Defaults[^1];
        var options = new WatercolorOptions(
            TargetPalette: customPalette,
            ColorTransferStrength: 0.5,
            Egbi: new EgbiOptions(KFactor: 25.0, Epsilon: 1.0, MinRegionSize: 4),
            ToneCurve: new PiecewiseToneCurveOptions(MinTone: 0.1, MaxTone: 0.9),
            PaperGranulation: new PaperGranulationOptions(DensityStrength: 0.0, WarpAmplitude: 0.0),
            EnableFxaa: false);

        WatercolorResult result = WatercolorPipeline.RenderDetailed(readOnlyScene, flatHeightMap, options);

        Assert.Same(customPalette, result.SelectedPalette);
        MatrixAssert.Equal(result.ToneAdjusted, result.Granulated);
        MatrixAssert.Equal(result.Granulated, result.Image);
        for (int i = 0; i < result.Image.Count; i++)
        {
            Rgb8 px = result.Image[i];
            Assert.InRange(px.R, 20, 235);
            Assert.InRange(px.G, 20, 235);
            Assert.InRange(px.B, 20, 235);
        }
    }

    /// <summary>
    /// Verifies that empty images return empty result structures cleanly without errors.
    /// </summary>
    [Fact]
    public void RenderDetailed_EmptyImage_ShouldReturnEmptyOutputs()
    {
        var empty = new SequentialMatrix<Rgb8>(0, 0);

        WatercolorResult resultDefault = WatercolorPipeline.RenderDetailed(empty);
        Assert.Empty(resultDefault.Image);
        Assert.Empty(resultDefault.ColorTransferred);
        Assert.Empty(resultDefault.EgbiPreBlurred);
        Assert.Equal(0, resultDefault.EgbiGraph.RegionCount);
        Assert.Empty(resultDefault.SaliencyMap);
        Assert.Empty(resultDefault.SalientMask);
        Assert.Empty(resultDefault.RawSaliencyDistanceField);
        Assert.Empty(resultDefault.SaliencyDistanceField);
        Assert.Empty(resultDefault.MeanShiftFiltered);
        Assert.Equal(0, resultDefault.SegmentationGraph.RegionCount);
        Assert.Empty(resultDefault.LodAbstracted);
        Assert.Empty(resultDefault.WetInWet.Image);
        Assert.Empty(resultDefault.WetInWet.WetAreaMask);
        Assert.Empty(resultDefault.WetInWet.QualifyingBoundaries);
        Assert.Empty(resultDefault.HandTremor);
        Assert.Null(resultDefault.EdgeDensityField);
        Assert.Null(resultDefault.TurbulenceDensityField);
        Assert.Empty(resultDefault.DensityAdjusted);
        Assert.Empty(resultDefault.ToneAdjusted);
        Assert.Null(resultDefault.PaperHeightMap);
        Assert.Empty(resultDefault.Granulated);
        Assert.Empty(resultDefault.StepTimings);
        Assert.Equal(WatercolorPalettes.Defaults[0], resultDefault.SelectedPalette);

        ColorTransferProfile chosenPalette = WatercolorPalettes.Defaults[^1];
        WatercolorResult resultCustom = WatercolorPipeline.RenderDetailed(
            empty,
            options: new WatercolorOptions(
                TargetPalette: chosenPalette,
                Egbi: new EgbiOptions(Connectivity: GridConnectivity.FourConnected),
                EnableColorTransfer: false));
        Assert.Same(chosenPalette, resultCustom.SelectedPalette);
        Assert.Equal(GridConnectivity.FourConnected, resultCustom.EgbiGraph.Connectivity);
        Assert.Equal(GridConnectivity.EightConnected, resultCustom.SegmentationGraph.Connectivity);
        Assert.Empty(resultCustom.StepTimings);
    }

    /// <summary>
    /// Verifies that null inputs, invalid options, and mismatched height map sizes throw appropriate exceptions.
    /// </summary>
    [Fact]
    public void RenderDetailed_InvalidInputs_ShouldThrowAppropriateExceptions()
    {
        var validImage = new SequentialMatrix<Rgb8>(8, 8);
        var mismatchedMap = new SequentialMatrix<DoublePixel>(4, 8);

        Assert.Throws<ArgumentNullException>(() => WatercolorPipeline.RenderDetailed(null!));
        Assert.Throws<ArgumentNullException>(() => WatercolorPipeline.Render(null!));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WatercolorPipeline.RenderDetailed(validImage, options: new WatercolorOptions(ColorTransferStrength: 2.0)));

        Assert.Throws<ArgumentException>(() =>
            WatercolorPipeline.RenderDetailed(validImage, paperHeightMap: mismatchedMap));
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorPipeline.RenderDetailed"/> runs cleanly with <see cref="WatercolorOptions.ForImage"/>
    /// and honors both disabled (<c>0.0</c>) and recursive (<c>&gt; 2.0</c>) <see cref="WatercolorOptions.EgbiBlurSigma"/> settings.
    /// </summary>
    [Fact]
    public void RenderDetailed_WithForImageAndCustomEgbiBlurSigma_ShouldSucceedDeterministically()
    {
        Matrix<Rgb8> scene = CreateTestScene(32, 32);
        WatercolorOptions autoOptions = WatercolorOptions.ForImage(scene);

        WatercolorResult autoResult = WatercolorPipeline.RenderDetailed(scene, options: autoOptions);
        Assert.Equal(scene.Size, autoResult.Image.Size);
        Assert.Equal(autoOptions.TargetPalette, autoResult.SelectedPalette);

        WatercolorResult noBlurResult = WatercolorPipeline.RenderDetailed(scene, options: new WatercolorOptions(EgbiBlurSigma: 0.0));
        WatercolorResult recursiveBlurResult = WatercolorPipeline.RenderDetailed(scene, options: new WatercolorOptions(EgbiBlurSigma: 2.5));
        Assert.Equal(scene.Size, noBlurResult.Image.Size);
        Assert.Equal(scene.Size, recursiveBlurResult.Image.Size);
    }

    /// <summary>
    /// Verifies stage progress reporting order, empty-image non-invocation, and 21-element result deconstruction.
    /// </summary>
    [Fact]
    public void RenderDetailed_WithStageProgress_ShouldReportStagesInOrderAndSupportDeconstruct()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var reported = new System.Collections.Generic.List<WatercolorStageProgress>();

        WatercolorResult result = WatercolorPipeline.RenderDetailed(
            scene,
            options: WatercolorOptions.ForImage(scene),
            onStageCompleted: reported.Add);

        WatercolorStage[] expectedOrder =
        [
            WatercolorStage.ColorTransfer,
            WatercolorStage.Saliency,
            WatercolorStage.Segmentation,
            WatercolorStage.LodAbstraction,
            WatercolorStage.WetInWet,
            WatercolorStage.HandTremor,
            WatercolorStage.Finishing
        ];

        Assert.Equal(expectedOrder, Array.ConvertAll(reported.ToArray(), static p => p.Stage));
        Assert.All(reported, static p => Assert.True(p.Elapsed >= TimeSpan.Zero));

        (
            Matrix<Rgb8> image,
            Matrix<Rgb8> colorTransferred,
            ColorTransferProfile selectedPalette,
            Matrix<Rgb8> egbiPreBlurred,
            RegionGraph egbiGraph,
            Matrix<DoublePixel> saliencyMap,
            MaskMatrix salientMask,
            Matrix<DoublePixel> rawDistanceField,
            Matrix<DoublePixel> distanceField,
            Matrix<Rgb8> meanShiftFiltered,
            RegionGraph graph,
            Matrix<Rgb8> lodAbstracted,
            WetInWetResult wetInWet,
            Matrix<Rgb8> handTremor,
            Matrix<DoublePixel>? edgeDensityField,
            Matrix<DoublePixel>? turbulenceDensityField,
            Matrix<Rgb8> densityAdjusted,
            Matrix<Rgb8> toneAdjusted,
            IReadOnlyMatrix<DoublePixel>? paperHeightMap,
            Matrix<Rgb8> granulated,
            IReadOnlyDictionary<WatercolorStep, WatercolorStepTiming> stepTimings
        ) = result;

        Assert.Same(result.Image, image);
        Assert.Same(result.ColorTransferred, colorTransferred);
        Assert.Same(result.SelectedPalette, selectedPalette);
        Assert.Same(result.EgbiPreBlurred, egbiPreBlurred);
        Assert.Same(result.EgbiGraph, egbiGraph);
        Assert.Same(result.SaliencyMap, saliencyMap);
        Assert.Same(result.SalientMask, salientMask);
        Assert.Same(result.RawSaliencyDistanceField, rawDistanceField);
        Assert.Same(result.SaliencyDistanceField, distanceField);
        Assert.Same(result.MeanShiftFiltered, meanShiftFiltered);
        Assert.Same(result.SegmentationGraph, graph);
        Assert.Same(result.LodAbstracted, lodAbstracted);
        Assert.Same(result.WetInWet, wetInWet);
        Assert.Same(result.HandTremor, handTremor);
        Assert.Same(result.EdgeDensityField, edgeDensityField);
        Assert.Same(result.TurbulenceDensityField, turbulenceDensityField);
        Assert.Same(result.DensityAdjusted, densityAdjusted);
        Assert.Same(result.ToneAdjusted, toneAdjusted);
        Assert.Same(result.PaperHeightMap, paperHeightMap);
        Assert.Same(result.Granulated, granulated);
        Assert.Same(result.StepTimings, stepTimings);

        reported.Clear();
        WatercolorPipeline.RenderDetailed(
            new SequentialMatrix<Rgb8>(0, 0),
            onStageCompleted: reported.Add);
        Assert.Empty(reported);
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorPipeline.RenderDetailed"/> still reports all stages in order when optional stages are disabled.
    /// </summary>
    [Fact]
    public void RenderDetailed_DisabledOptionalStages_ShouldReportAllStagesInOrder()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);
        var reported = new List<WatercolorStage>();
        var options = new WatercolorOptions(
            WetInWet: new WetInWetOptions(Mode: WetInWetMode.Disabled),
            HandTremor: new HandTremorOptions(Amplitude: 0.0),
            EdgeDarkening: new EdgeDarkeningOptions(Intensity: 0.0),
            TurbulenceFlow: new TurbulenceFlowOptions(Intensity: 0.0),
            PaperGranulation: new PaperGranulationOptions(DensityStrength: 0.0, WarpAmplitude: 0.0),
            EnableFxaa: false);

        WatercolorResult result = WatercolorPipeline.RenderDetailed(
            scene,
            options: options,
            onStageCompleted: p => reported.Add(p.Stage));

        WatercolorStage[] expectedOrder =
        [
            WatercolorStage.ColorTransfer,
            WatercolorStage.Saliency,
            WatercolorStage.Segmentation,
            WatercolorStage.LodAbstraction,
            WatercolorStage.WetInWet,
            WatercolorStage.HandTremor,
            WatercolorStage.Finishing
        ];

        Assert.Equal(expectedOrder, reported);
        MatrixAssert.Equal(result.LodAbstracted, result.WetInWet.Image);
        Assert.All(result.WetInWet.QualifyingBoundaries, static q => Assert.False(q));
        MatrixAssert.Equal(result.WetInWet.Image, result.HandTremor);
        Assert.Null(result.EdgeDensityField);
        Assert.Null(result.TurbulenceDensityField);
        MatrixAssert.Equal(result.HandTremor, result.DensityAdjusted);
        Assert.Null(result.PaperHeightMap);
        MatrixAssert.Equal(result.ToneAdjusted, result.Granulated);
        MatrixAssert.Equal(result.Granulated, result.Image);
        Assert.True(result.StepTimings[WatercolorStep.WetInWet].Bypassed);
        Assert.True(result.StepTimings[WatercolorStep.HandTremor].Bypassed);
        Assert.True(result.StepTimings[WatercolorStep.EdgeDarkening].Bypassed);
        Assert.True(result.StepTimings[WatercolorStep.TurbulenceFlow].Bypassed);
        Assert.True(result.StepTimings[WatercolorStep.PigmentCombine].Bypassed);
        Assert.True(result.StepTimings[WatercolorStep.PaperHeightMap].Bypassed);
        Assert.True(result.StepTimings[WatercolorStep.PaperGranulation].Bypassed);
        Assert.True(result.StepTimings[WatercolorStep.Fxaa].Bypassed);
    }

    /// <summary>
    /// Verifies that throwing from <c>onStageCompleted</c> propagates immediately and short-circuits remaining pipeline stages.
    /// </summary>
    [Fact]
    public void RenderDetailed_OnStageCompletedThrows_ShouldShortCircuitRemainingStages()
    {
        Matrix<Rgb8> scene = CreateTestScene(16, 16);
        var reported = new List<WatercolorStage>();

        Assert.Throws<InvalidOperationException>(() =>
            WatercolorPipeline.RenderDetailed(
                scene,
                onStageCompleted: progress =>
                {
                    reported.Add(progress.Stage);
                    if (progress.Stage == WatercolorStage.Segmentation)
                    {
                        throw new InvalidOperationException("Abort pipeline.");
                    }
                }));

        Assert.Equal(
            [WatercolorStage.ColorTransfer, WatercolorStage.Saliency, WatercolorStage.Segmentation],
            reported);
    }

    /// <summary>
    /// Verifies that 0x5 and 5x0 empty images produce empty results of matching dimensions across all artifacts.
    /// </summary>
    /// <param name="width">The empty image width.</param>
    /// <param name="height">The empty image height.</param>
    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    public void RenderDetailed_And_Render_ZeroAreaImages_ShouldPreserveDimensions(int width, int height)
    {
        var empty = new SequentialMatrix<Rgb8>(width, height);
        var reported = new List<WatercolorStageProgress>();

        WatercolorResult detailed = WatercolorPipeline.RenderDetailed(empty, onStageCompleted: reported.Add);
        Matrix<Rgb8> rendered = WatercolorPipeline.Render(empty, onStageCompleted: reported.Add);

        Assert.Empty(reported);
        Assert.Equal(empty.Size, detailed.Image.Size);
        Assert.Equal(empty.Size, detailed.ColorTransferred.Size);
        Assert.Equal(empty.Size, detailed.EgbiPreBlurred.Size);
        Assert.Equal(empty.Size, detailed.EgbiGraph.Size);
        Assert.Equal(0, detailed.EgbiGraph.RegionCount);
        Assert.Equal(empty.Size, detailed.SaliencyMap.Size);
        Assert.Equal(empty.Size, detailed.SalientMask.Size);
        Assert.Equal(empty.Size, detailed.RawSaliencyDistanceField.Size);
        Assert.Equal(empty.Size, detailed.SaliencyDistanceField.Size);
        Assert.Equal(empty.Size, detailed.MeanShiftFiltered.Size);
        Assert.Equal(empty.Size, detailed.SegmentationGraph.Size);
        Assert.Equal(empty.Size, detailed.LodAbstracted.Size);
        Assert.Equal(empty.Size, detailed.WetInWet.Image.Size);
        Assert.Equal(empty.Size, detailed.WetInWet.WetAreaMask.Size);
        Assert.Equal(empty.Size, detailed.HandTremor.Size);
        Assert.Null(detailed.EdgeDensityField);
        Assert.Null(detailed.TurbulenceDensityField);
        Assert.Equal(empty.Size, detailed.DensityAdjusted.Size);
        Assert.Equal(empty.Size, detailed.ToneAdjusted.Size);
        Assert.Null(detailed.PaperHeightMap);
        Assert.Equal(empty.Size, detailed.Granulated.Size);
        Assert.Empty(detailed.StepTimings);
        Assert.Equal(0, detailed.SegmentationGraph.RegionCount);
        Assert.Empty(detailed.WetInWet.QualifyingBoundaries);
        Assert.Equal(empty.Size, rendered.Size);
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorPipeline.Render"/> forwards <c>onStageCompleted</c> and matches <see cref="WatercolorPipeline.RenderDetailed"/>.
    /// </summary>
    [Fact]
    public void Render_WithStageProgress_ShouldReportStagesInOrderAndMatchDetailedImage()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);
        var reported = new List<WatercolorStage>();

        Matrix<Rgb8> rendered = WatercolorPipeline.Render(
            scene,
            onStageCompleted: progress => reported.Add(progress.Stage));
        WatercolorResult detailed = WatercolorPipeline.RenderDetailed(scene);

        Assert.Equal(
            [
                WatercolorStage.ColorTransfer,
                WatercolorStage.Saliency,
                WatercolorStage.Segmentation,
                WatercolorStage.LodAbstraction,
                WatercolorStage.WetInWet,
                WatercolorStage.HandTremor,
                WatercolorStage.Finishing
            ],
            reported);
        MatrixAssert.Equal(detailed.Image, rendered);
    }

    /// <summary>
    /// Verifies that <see cref="MeanShiftOptions.Connectivity"/> is preserved on the resulting <see cref="WatercolorResult.SegmentationGraph"/>.
    /// </summary>
    [Fact]
    public void RenderDetailed_FourConnectedMeanShift_ShouldPropagateConnectivityToSegmentationGraph()
    {
        Matrix<Rgb8> scene = CreateTestScene(16, 16);
        var options = new WatercolorOptions(
            MeanShift: new MeanShiftOptions(MinRegionSize: 4, Connectivity: GridConnectivity.FourConnected));

        WatercolorResult result = WatercolorPipeline.RenderDetailed(scene, options: options);

        Assert.Equal(GridConnectivity.FourConnected, result.SegmentationGraph.Connectivity);
    }

    /// <summary>
    /// Verifies bit-identical parity between <see cref="WatercolorPipeline"/>'s shared <c>salientFractions</c> optimization
    /// and direct stage calls when <see cref="LodAbstractionOptions.SalientOverlapThreshold"/> differs from <see cref="WetInWetOptions.SalientOverlapThreshold"/>.
    /// </summary>
    [Fact]
    public void RenderDetailed_DifferingSalientOverlapThresholds_ShouldMatchDirectStageResultsBitIdentically()
    {
        SequentialMatrix<Rgb8> scene = TestMatrices.Create(24, 24, static (x, y) =>
        {
            bool centerBox = x >= 6 && x < 18 && y >= 6 && y < 18;
            return centerBox
                ? new Rgb8((byte)(210 + (x % 12)), (byte)(40 + (y % 12)), 35)
                : new Rgb8(35, 85, 190);
        });

        var meanShiftOpts = new MeanShiftOptions(SpatialRadius: 8, RangeRadius: 45.0, MinRegionSize: 4);
        var lodOpts = new LodAbstractionOptions(SalientOverlapThreshold: 0.2);
        var wetOpts = new WetInWetOptions(
            SalientOverlapThreshold: 0.85,
            Mode: WetInWetMode.ExcludeSalientBoundaries);
        var options = new WatercolorOptions(
            MeanShift: meanShiftOpts,
            LodAbstraction: lodOpts,
            WetInWet: wetOpts);

        WatercolorResult result = WatercolorPipeline.RenderDetailed(scene, options: options);

        (Matrix<Rgb8> meanShiftFiltered, IDisjointSet meanShiftSeg) =
            MeanShiftSegmentation.FilterAndSegment(result.ColorTransferred, meanShiftOpts);
        var graph = RegionGraph.FromDisjointSet(meanShiftSeg, meanShiftOpts.Connectivity);
        Matrix<Rgb8> expectedLod = LodAbstraction.Apply(
            meanShiftFiltered,
            graph,
            result.SaliencyDistanceField,
            result.SalientMask,
            lodOpts);
        WetInWetResult expectedWet = WetInWet.ApplyDetailed(
            expectedLod,
            graph,
            result.SalientMask,
            wetOpts);

        MatrixAssert.Equal(meanShiftFiltered, result.MeanShiftFiltered);
        MatrixAssert.Equal(expectedLod, result.LodAbstracted);
        MatrixAssert.Equal(expectedWet.Image, result.WetInWet.Image);
        MatrixAssert.Equal(expectedWet.WetAreaMask, result.WetInWet.WetAreaMask);
        Assert.Equal(expectedWet.QualifyingBoundaries, result.WetInWet.QualifyingBoundaries);
    }

    /// <summary>
    /// Verifies that omitted (<see langword="null"/>) stage options resolve consistently to each stage's default options.
    /// </summary>
    [Fact]
    public void RenderDetailed_DefaultOptions_ShouldMatchExplicitDefaultSubOptions()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);
        var explicitDefaults = new WatercolorOptions(
            Egbi: WatercolorOptions.DefaultSaliencyEgbi,
            MeanShift: MeanShiftOptions.Default);

        WatercolorResult fromDefault = WatercolorPipeline.RenderDetailed(scene, options: new WatercolorOptions());
        WatercolorResult fromExplicit = WatercolorPipeline.RenderDetailed(scene, options: explicitDefaults);

        Assert.Equal(fromExplicit.SelectedPalette, fromDefault.SelectedPalette);
        Assert.Equal(fromExplicit.EgbiGraph.RegionCount, fromDefault.EgbiGraph.RegionCount);
        Assert.Equal(fromExplicit.SegmentationGraph.RegionCount, fromDefault.SegmentationGraph.RegionCount);
        MatrixAssert.Equal(fromExplicit.ColorTransferred, fromDefault.ColorTransferred);
        MatrixAssert.Equal(fromExplicit.EgbiPreBlurred, fromDefault.EgbiPreBlurred);
        MatrixAssert.Equal(fromExplicit.SalientMask, fromDefault.SalientMask);
        MatrixAssert.Equal(fromExplicit.RawSaliencyDistanceField, fromDefault.RawSaliencyDistanceField);
        MatrixAssert.Equal(fromExplicit.SaliencyDistanceField, fromDefault.SaliencyDistanceField);
        MatrixAssert.Equal(fromExplicit.MeanShiftFiltered, fromDefault.MeanShiftFiltered);
        MatrixAssert.Equal(fromExplicit.LodAbstracted, fromDefault.LodAbstracted);
        MatrixAssert.Equal(fromExplicit.WetInWet.Image, fromDefault.WetInWet.Image);
        MatrixAssert.Equal(fromExplicit.HandTremor, fromDefault.HandTremor);
        MatrixAssert.Equal(fromExplicit.EdgeDensityField!, fromDefault.EdgeDensityField!);
        MatrixAssert.Equal(fromExplicit.TurbulenceDensityField!, fromDefault.TurbulenceDensityField!);
        MatrixAssert.Equal(fromExplicit.DensityAdjusted, fromDefault.DensityAdjusted);
        MatrixAssert.Equal(fromExplicit.ToneAdjusted, fromDefault.ToneAdjusted);
        MatrixAssert.Equal(fromExplicit.PaperHeightMap!, fromDefault.PaperHeightMap!);
        MatrixAssert.Equal(fromExplicit.Granulated, fromDefault.Granulated);
        MatrixAssert.Equal(fromExplicit.Image, fromDefault.Image);
    }

    /// <summary>
    /// Verifies that Stage 6 intermediates match standalone tone-curve and paper-granulation stages.
    /// </summary>
    [Fact]
    public void RenderDetailed_Stage6Intermediates_ShouldMatchStandaloneFinishingStages()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);
        SequentialMatrix<DoublePixel> heightMap = TestMatrices.Create(20, 20, static (x, _) => new DoublePixel(x < 10 ? 0.2 : 0.8));
        var toneOpts = new PiecewiseToneCurveOptions(MinTone: 0.08, MaxTone: 0.92);
        var granOpts = new PaperGranulationOptions(DensityStrength: 0.4, WarpAmplitude: 0.5);
        var options = new WatercolorOptions(
            ToneCurve: toneOpts,
            PaperGranulation: granOpts,
            EnableFxaa: false);

        WatercolorResult result = WatercolorPipeline.RenderDetailed(scene, heightMap, options);

        Matrix<Rgb8> expectedTone = PiecewiseToneCurve.Apply(result.DensityAdjusted, toneOpts);
        Matrix<Rgb8> expectedGranulated = PaperGranulation.Apply(expectedTone, heightMap, granOpts);

        Assert.Same(heightMap, result.PaperHeightMap);
        MatrixAssert.Equal(expectedTone, result.ToneAdjusted);
        MatrixAssert.Equal(expectedGranulated, result.Granulated);
        MatrixAssert.Equal(expectedGranulated, result.Image);
    }

    /// <summary>
    /// Verifies that Stage 2 intermediates (<see cref="WatercolorResult.EgbiPreBlurred"/>, <see cref="WatercolorResult.EgbiGraph"/>,
    /// <see cref="WatercolorResult.RawSaliencyDistanceField"/>, and <see cref="WatercolorResult.SaliencyDistanceField"/>) match standalone Stage 2 calls bit-for-bit.
    /// </summary>
    [Fact]
    public void RenderDetailed_Stage2Intermediates_ShouldMatchStandaloneStage2Algorithms()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var egbiOpts = new EgbiOptions(KFactor: 40.0, Epsilon: 1.0, MinRegionSize: 4, Connectivity: GridConnectivity.FourConnected);
        var rcOpts = new RegionalContrastSaliencyOptions(EnableSmoothing: true, ComputeBorderRegions: true, CenterPriorWeight: 3.0);
        var distOpts = new SaliencyDistanceFieldOptions(SmoothingSigma: 1.2);
        var options = new WatercolorOptions(
            EgbiBlurSigma: 1.1,
            Egbi: egbiOpts,
            RegionalContrastSaliency: rcOpts,
            SaliencyDistanceField: distOpts);

        WatercolorResult result = WatercolorPipeline.RenderDetailed(scene, options: options);

        Matrix<Rgb8> expectedBlurred = Blur.GaussianBlur(result.ColorTransferred, options.EgbiBlurSigma);
        Matrix<Lab32> expectedLab = expectedBlurred.ToLab32(RgbColorSpace.SRgb);
        RegionGraph expectedEgbiGraph = EgbiSegmentation.Segment(expectedLab, egbiOpts).ToRegionGraph(egbiOpts.Connectivity);
        Matrix<DoublePixel> expectedSaliency = RegionalContrastSaliency.ComputeSaliency(result.ColorTransferred, expectedEgbiGraph, rcOpts);
        Matrix<DoublePixel> expectedRawDist = SaliencyDistanceField.ComputeNormalizedField(result.SalientMask);
        Matrix<DoublePixel> expectedSmoothedDist = SaliencyDistanceField.Compute(result.SalientMask, distOpts);

        MatrixAssert.Equal(expectedBlurred, result.EgbiPreBlurred);
        Assert.Equal(GridConnectivity.FourConnected, result.EgbiGraph.Connectivity);
        Assert.Equal(expectedEgbiGraph.RegionCount, result.EgbiGraph.RegionCount);
        Assert.Equal(expectedEgbiGraph.Boundaries.Count, result.EgbiGraph.Boundaries.Count);
        MatrixAssert.Equal(expectedEgbiGraph.Labels, result.EgbiGraph.Labels);
        MatrixAssert.Equal(expectedSaliency, result.SaliencyMap);
        MatrixAssert.Equal(expectedRawDist, result.RawSaliencyDistanceField);
        MatrixAssert.Equal(expectedSmoothedDist, result.SaliencyDistanceField);
        Assert.False(result.StepTimings[WatercolorStep.EgbiPreBlur].Bypassed);

        WatercolorResult unsmoothedResult = WatercolorPipeline.RenderDetailed(
            scene,
            options: new WatercolorOptions(
                EgbiBlurSigma: 0.0,
                SaliencyDistanceField: new SaliencyDistanceFieldOptions(SmoothingSigma: 0.0)));
        MatrixAssert.Equal(unsmoothedResult.ColorTransferred, unsmoothedResult.EgbiPreBlurred);
        MatrixAssert.Equal(unsmoothedResult.RawSaliencyDistanceField, unsmoothedResult.SaliencyDistanceField);
        Assert.True(unsmoothedResult.StepTimings[WatercolorStep.EgbiPreBlur].Bypassed);
    }

    /// <summary>
    /// Verifies that setting <see cref="WatercolorOptions.EnableColorTransfer"/> to <see langword="false"/>
    /// passes through a clone of the source image while still resolving <see cref="WatercolorResult.SelectedPalette"/>.
    /// </summary>
    [Fact]
    public void RenderDetailed_EnableColorTransferFalse_ShouldPassThroughSourceAndResolvePalette()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);
        ColorTransferProfile expectedAutoPalette = ColorTransfer.SelectClosestProfile(
            ColorTransfer.ComputeStatistics(scene, RgbColorSpace.SRgb, Illuminant.E),
            WatercolorPalettes.Defaults,
            WatercolorPalettes.DefaultFeatureWeights);

        WatercolorResult autoResult = WatercolorPipeline.RenderDetailed(
            scene,
            options: new WatercolorOptions(EnableColorTransfer: false));

        Assert.NotSame(scene, autoResult.ColorTransferred);
        MatrixAssert.Equal(scene, autoResult.ColorTransferred);
        Assert.Equal(expectedAutoPalette, autoResult.SelectedPalette);
        Assert.True(autoResult.StepTimings[WatercolorStep.ColorTransfer].Bypassed);
        Assert.False(autoResult.StepTimings[WatercolorStep.ColorTransfer].FromCache);

        ColorTransferProfile explicitPalette = WatercolorPalettes.CoastalCerulean;
        WatercolorResult explicitResult = WatercolorPipeline.RenderDetailed(
            scene,
            options: new WatercolorOptions(TargetPalette: explicitPalette, EnableColorTransfer: false));

        Assert.NotSame(scene, explicitResult.ColorTransferred);
        MatrixAssert.Equal(scene, explicitResult.ColorTransferred);
        Assert.Same(explicitPalette, explicitResult.SelectedPalette);
        Assert.True(explicitResult.StepTimings[WatercolorStep.ColorTransfer].Bypassed);
    }

    /// <summary>
    /// Verifies that setting <see cref="WatercolorOptions.EnableLodAbstraction"/> to <see langword="false"/>
    /// bypasses adaptive LOD smoothing while preserving Mean-Shift segmentation outputs.
    /// </summary>
    [Fact]
    public void RenderDetailed_EnableLodAbstractionFalse_ShouldPassThroughMeanShiftFiltered()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);

        WatercolorResult result = WatercolorPipeline.RenderDetailed(
            scene,
            options: new WatercolorOptions(EnableLodAbstraction: false));

        Assert.True(result.SegmentationGraph.RegionCount >= 1);
        MatrixAssert.Equal(result.MeanShiftFiltered, result.LodAbstracted);
        Assert.True(result.StepTimings[WatercolorStep.LodAbstraction].Bypassed);
        Assert.False(result.StepTimings[WatercolorStep.MeanShiftFiltering].Bypassed);
        Assert.False(result.StepTimings[WatercolorStep.MeanShiftClustering].Bypassed);
    }

    /// <summary>
    /// Verifies that setting <see cref="WatercolorOptions.EnableWetInWet"/> to <see langword="false"/>
    /// passes through LOD output with an empty wet mask and all-false boundary flags matching the segmentation graph,
    /// while allowing downstream Stage 5 <see cref="HandTremor"/> to execute normally.
    /// </summary>
    [Fact]
    public void RenderDetailed_EnableWetInWetFalse_ShouldPassThroughLodAndMaintainBoundaryInvariant()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var options = new WatercolorOptions(
            MeanShift: new MeanShiftOptions(SpatialRadius: 8, RangeRadius: 30.0, MinRegionSize: 4),
            EnableWetInWet: false,
            EnableHandTremor: true);

        WatercolorResult result = WatercolorPipeline.RenderDetailed(scene, options: options);

        Assert.True(result.SegmentationGraph.Boundaries.Count >= 1);
        MatrixAssert.Equal(result.LodAbstracted, result.WetInWet.Image);
        Assert.Equal(0, result.WetInWet.WetAreaMask.CountTrue());
        Assert.Equal(result.SegmentationGraph.Boundaries.Count, result.WetInWet.QualifyingBoundaries.Count);
        Assert.All(result.WetInWet.QualifyingBoundaries, static qualified => Assert.False(qualified));
        Assert.True(result.StepTimings[WatercolorStep.WetInWet].Bypassed);
        Assert.False(result.StepTimings[WatercolorStep.HandTremor].Bypassed);
        Assert.Equal(scene.Size, result.HandTremor.Size);
    }

    /// <summary>
    /// Verifies that setting <see cref="WatercolorOptions.EnableHandTremor"/> to <see langword="false"/>
    /// passes through <see cref="WetInWetResult.Image"/> unchanged.
    /// </summary>
    [Fact]
    public void RenderDetailed_EnableHandTremorFalse_ShouldPassThroughWetInWetImage()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);

        WatercolorResult result = WatercolorPipeline.RenderDetailed(
            scene,
            options: new WatercolorOptions(EnableHandTremor: false));

        MatrixAssert.Equal(result.WetInWet.Image, result.HandTremor);
        Assert.True(result.StepTimings[WatercolorStep.HandTremor].Bypassed);
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorOptions.EnableEdgeDarkening"/> and <see cref="WatercolorOptions.EnableTurbulenceFlow"/>
    /// expose pure non-mutated density fields when combined and honor independent and joint bypass toggles.
    /// </summary>
    [Fact]
    public void RenderDetailed_EdgeDarkeningAndTurbulenceToggles_ShouldPreservePureFieldsAndCombineAccurately()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var edgeOpts = new EdgeDarkeningOptions(Intensity: 2.0, BlurSigma: 0.8);
        var turbOpts = new TurbulenceFlowOptions(Intensity: 0.6);

        WatercolorResult bothEnabled = WatercolorPipeline.RenderDetailed(
            scene,
            options: new WatercolorOptions(
                EdgeDarkening: edgeOpts,
                TurbulenceFlow: turbOpts,
                EnableEdgeDarkening: true,
                EnableTurbulenceFlow: true));

        Assert.NotNull(bothEnabled.EdgeDensityField);
        Assert.NotNull(bothEnabled.TurbulenceDensityField);
        Matrix<DoublePixel> expectedEdgeDensity = EdgeDarkening.ComputeDensityField(bothEnabled.HandTremor, edgeOpts);
        Matrix<DoublePixel> expectedTurbDensity = TurbulenceFlow.ComputeDensityField(scene.Size, turbOpts);
        MatrixAssert.Equal(expectedEdgeDensity, bothEnabled.EdgeDensityField);
        MatrixAssert.Equal(expectedTurbDensity, bothEnabled.TurbulenceDensityField);

        var expectedCombined = new SequentialMatrix<DoublePixel>(scene.Size);
        bool anyDifference = false;
        for (int i = 0; i < expectedCombined.Count; i++)
        {
            double summed = expectedEdgeDensity[i].Value + expectedTurbDensity[i].Value;
            expectedCombined[i] = new DoublePixel(summed);
            if (Math.Abs(summed - bothEnabled.EdgeDensityField[i].Value) > 1e-9)
            {
                anyDifference = true;
            }
        }

        Assert.True(anyDifference);
        MatrixAssert.Equal(
            PigmentBlend.ApplyDensityField(bothEnabled.HandTremor, expectedCombined),
            bothEnabled.DensityAdjusted);
        Assert.False(bothEnabled.StepTimings[WatercolorStep.EdgeDarkening].Bypassed);
        Assert.False(bothEnabled.StepTimings[WatercolorStep.TurbulenceFlow].Bypassed);
        Assert.False(bothEnabled.StepTimings[WatercolorStep.PigmentCombine].Bypassed);

        WatercolorResult edgeDisabled = WatercolorPipeline.RenderDetailed(
            scene,
            options: new WatercolorOptions(
                EdgeDarkening: edgeOpts,
                TurbulenceFlow: turbOpts,
                EnableEdgeDarkening: false,
                EnableTurbulenceFlow: true));

        Assert.Null(edgeDisabled.EdgeDensityField);
        Assert.NotNull(edgeDisabled.TurbulenceDensityField);
        MatrixAssert.Equal(
            TurbulenceFlow.Apply(edgeDisabled.HandTremor, turbOpts),
            edgeDisabled.DensityAdjusted);
        Assert.True(edgeDisabled.StepTimings[WatercolorStep.EdgeDarkening].Bypassed);
        Assert.False(edgeDisabled.StepTimings[WatercolorStep.TurbulenceFlow].Bypassed);
        Assert.False(edgeDisabled.StepTimings[WatercolorStep.PigmentCombine].Bypassed);

        WatercolorResult turbDisabled = WatercolorPipeline.RenderDetailed(
            scene,
            options: new WatercolorOptions(
                EdgeDarkening: edgeOpts,
                TurbulenceFlow: turbOpts,
                EnableEdgeDarkening: true,
                EnableTurbulenceFlow: false));

        Assert.NotNull(turbDisabled.EdgeDensityField);
        Assert.Null(turbDisabled.TurbulenceDensityField);
        MatrixAssert.Equal(
            EdgeDarkening.Apply(turbDisabled.HandTremor, edgeOpts),
            turbDisabled.DensityAdjusted);
        Assert.False(turbDisabled.StepTimings[WatercolorStep.EdgeDarkening].Bypassed);
        Assert.True(turbDisabled.StepTimings[WatercolorStep.TurbulenceFlow].Bypassed);
        Assert.False(turbDisabled.StepTimings[WatercolorStep.PigmentCombine].Bypassed);

        WatercolorResult bothDisabled = WatercolorPipeline.RenderDetailed(
            scene,
            options: new WatercolorOptions(
                EdgeDarkening: edgeOpts,
                TurbulenceFlow: turbOpts,
                EnableEdgeDarkening: false,
                EnableTurbulenceFlow: false));

        Assert.Null(bothDisabled.EdgeDensityField);
        Assert.Null(bothDisabled.TurbulenceDensityField);
        MatrixAssert.Equal(bothDisabled.HandTremor, bothDisabled.DensityAdjusted);
        Assert.True(bothDisabled.StepTimings[WatercolorStep.EdgeDarkening].Bypassed);
        Assert.True(bothDisabled.StepTimings[WatercolorStep.TurbulenceFlow].Bypassed);
        Assert.True(bothDisabled.StepTimings[WatercolorStep.PigmentCombine].Bypassed);
    }

    /// <summary>
    /// Verifies that setting <see cref="WatercolorOptions.EnableToneCurve"/> to <see langword="false"/>
    /// passes through <see cref="WatercolorResult.DensityAdjusted"/> unchanged.
    /// </summary>
    [Fact]
    public void RenderDetailed_EnableToneCurveFalse_ShouldPassThroughDensityAdjusted()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);

        WatercolorResult result = WatercolorPipeline.RenderDetailed(
            scene,
            options: new WatercolorOptions(
                ToneCurve: new PiecewiseToneCurveOptions(MinTone: 0.2, MaxTone: 0.8),
                EnableToneCurve: false));

        MatrixAssert.Equal(result.DensityAdjusted, result.ToneAdjusted);
        Assert.True(result.StepTimings[WatercolorStep.ToneCurve].Bypassed);
    }

    /// <summary>
    /// Verifies procedural paper granulation <c>warpScale</c> parity when frequency differs from default,
    /// custom height-map forwarding, and <see cref="WatercolorOptions.EnablePaperGranulation"/> bypass behavior.
    /// </summary>
    [Fact]
    public void RenderDetailed_PaperGranulation_ProceduralWarpScaleAndToggle_ShouldBehaveCorrectly()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var nonDefaultGrain = new PerlinNoiseOptions(Seed: 707, Frequency: 0.045, Octaves: 3, Lacunarity: 2.0, Persistence: 0.55);
        var granOpts = new PaperGranulationOptions(
            DensityStrength: 0.25,
            WarpAmplitude: 1.8,
            GrainNoise: nonDefaultGrain);

        WatercolorResult proceduralResult = WatercolorPipeline.RenderDetailed(
            scene,
            paperHeightMap: null,
            options: new WatercolorOptions(PaperGranulation: granOpts, EnableFxaa: false));

        Assert.NotNull(proceduralResult.PaperHeightMap);
        Matrix<DoublePixel> expectedHeightMap = PaperGranulation.GeneratePaperHeightMap(scene.Size, nonDefaultGrain);
        MatrixAssert.Equal(expectedHeightMap, proceduralResult.PaperHeightMap);

        Matrix<Rgb8> expectedProceduralGranulated = PaperGranulation.Apply(proceduralResult.ToneAdjusted, paperHeightMap: null, granOpts);
        MatrixAssert.Equal(expectedProceduralGranulated, proceduralResult.Granulated);
        Assert.False(proceduralResult.StepTimings[WatercolorStep.PaperHeightMap].Bypassed);
        Assert.False(proceduralResult.StepTimings[WatercolorStep.PaperGranulation].Bypassed);

        // Passing the generated height map directly to public PaperGranulation.Apply omits procedural warpScale;
        // verify the test scene actually differentiates procedural vs custom warp scaling.
        Matrix<Rgb8> unscaledGranulated = PaperGranulation.Apply(proceduralResult.ToneAdjusted, proceduralResult.PaperHeightMap, granOpts);
        Assert.False(proceduralResult.Granulated.ContentEquals(unscaledGranulated));

        SequentialMatrix<DoublePixel> customMap = TestMatrices.Create(24, 24, static (x, y) => new DoublePixel((x + y) / 48.0));
        WatercolorResult customMapResult = WatercolorPipeline.RenderDetailed(
            scene,
            paperHeightMap: customMap,
            options: new WatercolorOptions(PaperGranulation: granOpts, EnableFxaa: false));

        Assert.Same(customMap, customMapResult.PaperHeightMap);
        Assert.True(customMapResult.StepTimings[WatercolorStep.PaperHeightMap].Bypassed);
        Assert.False(customMapResult.StepTimings[WatercolorStep.PaperGranulation].Bypassed);

        WatercolorResult bypassedProcedural = WatercolorPipeline.RenderDetailed(
            scene,
            paperHeightMap: null,
            options: new WatercolorOptions(PaperGranulation: granOpts, EnablePaperGranulation: false, EnableFxaa: false));

        Assert.Null(bypassedProcedural.PaperHeightMap);
        MatrixAssert.Equal(bypassedProcedural.ToneAdjusted, bypassedProcedural.Granulated);
        Assert.True(bypassedProcedural.StepTimings[WatercolorStep.PaperHeightMap].Bypassed);
        Assert.True(bypassedProcedural.StepTimings[WatercolorStep.PaperGranulation].Bypassed);

        WatercolorResult bypassedCustom = WatercolorPipeline.RenderDetailed(
            scene,
            paperHeightMap: customMap,
            options: new WatercolorOptions(PaperGranulation: granOpts, EnablePaperGranulation: false, EnableFxaa: false));

        Assert.Null(bypassedCustom.PaperHeightMap);
        MatrixAssert.Equal(bypassedCustom.ToneAdjusted, bypassedCustom.Granulated);
        Assert.True(bypassedCustom.StepTimings[WatercolorStep.PaperHeightMap].Bypassed);
        Assert.True(bypassedCustom.StepTimings[WatercolorStep.PaperGranulation].Bypassed);
    }

    /// <summary>
    /// Verifies that disabling all nine <c>Enable*</c> toggles bypasses every optional effect cleanly while populating all 18 step timings.
    /// </summary>
    [Fact]
    public void RenderDetailed_AllEnableFlagsFalse_ShouldBypassAllOptionalEffectsAndRecordAllStepTimings()
    {
        Matrix<Rgb8> scene = CreateTestScene(20, 20);
        var options = new WatercolorOptions(
            EnableColorTransfer: false,
            EnableLodAbstraction: false,
            EnableWetInWet: false,
            EnableHandTremor: false,
            EnableEdgeDarkening: false,
            EnableTurbulenceFlow: false,
            EnableToneCurve: false,
            EnablePaperGranulation: false,
            EnableFxaa: false);

        WatercolorResult result = WatercolorPipeline.RenderDetailed(scene, options: options);

        MatrixAssert.Equal(scene, result.ColorTransferred);
        MatrixAssert.Equal(result.MeanShiftFiltered, result.LodAbstracted);
        MatrixAssert.Equal(result.LodAbstracted, result.WetInWet.Image);
        Assert.Equal(0, result.WetInWet.WetAreaMask.CountTrue());
        Assert.Equal(result.SegmentationGraph.Boundaries.Count, result.WetInWet.QualifyingBoundaries.Count);
        Assert.All(result.WetInWet.QualifyingBoundaries, static qualified => Assert.False(qualified));
        MatrixAssert.Equal(result.WetInWet.Image, result.HandTremor);
        Assert.Null(result.EdgeDensityField);
        Assert.Null(result.TurbulenceDensityField);
        MatrixAssert.Equal(result.HandTremor, result.DensityAdjusted);
        MatrixAssert.Equal(result.DensityAdjusted, result.ToneAdjusted);
        Assert.Null(result.PaperHeightMap);
        MatrixAssert.Equal(result.ToneAdjusted, result.Granulated);
        MatrixAssert.Equal(result.Granulated, result.Image);
        MatrixAssert.Equal(result.MeanShiftFiltered, result.Image);

        Assert.Equal(18, result.StepTimings.Count);
        WatercolorStep[] expectedBypassed =
        [
            WatercolorStep.ColorTransfer,
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
        var bypassedSet = new HashSet<WatercolorStep>(expectedBypassed);

        foreach (WatercolorStep step in Enum.GetValues<WatercolorStep>())
        {
            WatercolorStepTiming timing = result.StepTimings[step];
            Assert.True(timing.Elapsed >= TimeSpan.Zero);
            Assert.False(timing.FromCache);
            Assert.Equal(bypassedSet.Contains(step), timing.Bypassed);
        }
    }

    private static Matrix<Rgb8> CreateTestScene(int width, int height) =>
        TestMatrices.Create(width, height, (x, y) =>
        {
            bool insideSubject = x >= width / 4 && x < (3 * width) / 4 && y >= height / 4 && y < (3 * height) / 4;
            if (insideSubject)
            {
                return new Rgb8(220, 45, 35);
            }

            return x < width / 2 ? new Rgb8(30, 80, 190) : new Rgb8(230, 235, 240);
        });
}
