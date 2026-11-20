namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
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
        Assert.Equal(image.Size, detailed1.SaliencyMap.Size);
        Assert.Equal(image.Size, detailed1.SalientMask.Size);
        Assert.Equal(image.Size, detailed1.SaliencyDistanceField.Size);
        Assert.Equal(image.Size, detailed1.MeanShiftFiltered.Size);
        Assert.Equal(image.Size, detailed1.SegmentationGraph.Size);
        Assert.Equal(image.Size, detailed1.LodAbstracted.Size);
        Assert.Equal(image.Size, detailed1.WetInWet.Image.Size);
        Assert.Equal(image.Size, detailed1.HandTremor.Size);
        Assert.Equal(image.Size, detailed1.DensityAdjusted.Size);
        Assert.Equal(image.Size, detailed1.ToneAdjusted.Size);
        Assert.Equal(image.Size, detailed1.Granulated.Size);
        Assert.NotNull(detailed1.SelectedPalette);
        Assert.True(detailed1.SegmentationGraph.RegionCount >= 1);

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
        Assert.Empty(resultDefault.SaliencyMap);
        Assert.Empty(resultDefault.SalientMask);
        Assert.Empty(resultDefault.SaliencyDistanceField);
        Assert.Empty(resultDefault.MeanShiftFiltered);
        Assert.Equal(0, resultDefault.SegmentationGraph.RegionCount);
        Assert.Empty(resultDefault.LodAbstracted);
        Assert.Empty(resultDefault.WetInWet.Image);
        Assert.Empty(resultDefault.WetInWet.WetAreaMask);
        Assert.Empty(resultDefault.WetInWet.QualifyingBoundaries);
        Assert.Empty(resultDefault.HandTremor);
        Assert.Empty(resultDefault.DensityAdjusted);
        Assert.Empty(resultDefault.ToneAdjusted);
        Assert.Empty(resultDefault.Granulated);
        Assert.Equal(WatercolorPalettes.Defaults[0], resultDefault.SelectedPalette);

        ColorTransferProfile chosenPalette = WatercolorPalettes.Defaults[^1];
        WatercolorResult resultCustom = WatercolorPipeline.RenderDetailed(empty, options: new WatercolorOptions(TargetPalette: chosenPalette));
        Assert.Same(chosenPalette, resultCustom.SelectedPalette);
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
    /// Verifies stage progress reporting order, empty-image non-invocation, and 14-element result deconstruction.
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
            Matrix<DoublePixel> saliencyMap,
            MaskMatrix salientMask,
            Matrix<DoublePixel> distanceField,
            Matrix<Rgb8> meanShiftFiltered,
            RegionGraph graph,
            Matrix<Rgb8> lodAbstracted,
            WetInWetResult wetInWet,
            Matrix<Rgb8> handTremor,
            Matrix<Rgb8> densityAdjusted,
            Matrix<Rgb8> toneAdjusted,
            Matrix<Rgb8> granulated
        ) = result;

        Assert.Same(result.Image, image);
        Assert.Same(result.ColorTransferred, colorTransferred);
        Assert.Same(result.SelectedPalette, selectedPalette);
        Assert.Same(result.SaliencyMap, saliencyMap);
        Assert.Same(result.SalientMask, salientMask);
        Assert.Same(result.SaliencyDistanceField, distanceField);
        Assert.Same(result.MeanShiftFiltered, meanShiftFiltered);
        Assert.Same(result.SegmentationGraph, graph);
        Assert.Same(result.LodAbstracted, lodAbstracted);
        Assert.Same(result.WetInWet, wetInWet);
        Assert.Same(result.HandTremor, handTremor);
        Assert.Same(result.DensityAdjusted, densityAdjusted);
        Assert.Same(result.ToneAdjusted, toneAdjusted);
        Assert.Same(result.Granulated, granulated);

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
        MatrixAssert.Equal(result.HandTremor, result.DensityAdjusted);
        MatrixAssert.Equal(result.ToneAdjusted, result.Granulated);
        MatrixAssert.Equal(result.Granulated, result.Image);
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
        Assert.Equal(empty.Size, detailed.SaliencyMap.Size);
        Assert.Equal(empty.Size, detailed.SalientMask.Size);
        Assert.Equal(empty.Size, detailed.SaliencyDistanceField.Size);
        Assert.Equal(empty.Size, detailed.MeanShiftFiltered.Size);
        Assert.Equal(empty.Size, detailed.SegmentationGraph.Size);
        Assert.Equal(empty.Size, detailed.LodAbstracted.Size);
        Assert.Equal(empty.Size, detailed.WetInWet.Image.Size);
        Assert.Equal(empty.Size, detailed.WetInWet.WetAreaMask.Size);
        Assert.Equal(empty.Size, detailed.HandTremor.Size);
        Assert.Equal(empty.Size, detailed.DensityAdjusted.Size);
        Assert.Equal(empty.Size, detailed.ToneAdjusted.Size);
        Assert.Equal(empty.Size, detailed.Granulated.Size);
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
        Assert.Equal(fromExplicit.SegmentationGraph.RegionCount, fromDefault.SegmentationGraph.RegionCount);
        MatrixAssert.Equal(fromExplicit.ColorTransferred, fromDefault.ColorTransferred);
        MatrixAssert.Equal(fromExplicit.SalientMask, fromDefault.SalientMask);
        MatrixAssert.Equal(fromExplicit.MeanShiftFiltered, fromDefault.MeanShiftFiltered);
        MatrixAssert.Equal(fromExplicit.LodAbstracted, fromDefault.LodAbstracted);
        MatrixAssert.Equal(fromExplicit.WetInWet.Image, fromDefault.WetInWet.Image);
        MatrixAssert.Equal(fromExplicit.HandTremor, fromDefault.HandTremor);
        MatrixAssert.Equal(fromExplicit.DensityAdjusted, fromDefault.DensityAdjusted);
        MatrixAssert.Equal(fromExplicit.ToneAdjusted, fromDefault.ToneAdjusted);
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

        MatrixAssert.Equal(expectedTone, result.ToneAdjusted);
        MatrixAssert.Equal(expectedGranulated, result.Granulated);
        MatrixAssert.Equal(expectedGranulated, result.Image);
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
