namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Algorithms.Stylization;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
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
        Assert.Equal(image.Size, detailed1.SegmentationGraph.Size);
        Assert.Equal(image.Size, detailed1.LodAbstracted.Size);
        Assert.Equal(image.Size, detailed1.WetInWet.Image.Size);
        Assert.Equal(image.Size, detailed1.HandTremor.Size);
        Assert.NotNull(detailed1.SelectedPalette);
        Assert.True(detailed1.SegmentationGraph.RegionCount >= 1);

        Assert.True(detailed1.Image.ContentEquals(detailed2.Image));
        Assert.True(detailed1.Image.ContentEquals(rendered));
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

        var flatHeightMap = new SequentialMatrix<DoublePixel>(16, 16);
        flatHeightMap.SetAll(0.5);

        ColorTransferProfile customPalette = WatercolorPalettes.Defaults[^1];
        var options = new WatercolorOptions(
            TargetPalette: customPalette,
            ColorTransferStrength: 0.5,
            Egbi: new EgbiOptions(KFactor: 25.0, Epsilon: 1.0, MinRegionSize: 4),
            ToneCurve: new PiecewiseToneCurveOptions(MinTone: 0.1, MaxTone: 0.9),
            PaperGranulation: new PaperGranulationOptions(DensityStrength: 0.0, WarpAmplitude: 0.0),
            EnableFxaa: false);

        WatercolorResult result = WatercolorPipeline.RenderDetailed(readOnlyScene, options, flatHeightMap);

        Assert.Same(customPalette, result.SelectedPalette);
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
        Assert.Equal(0, resultDefault.SegmentationGraph.RegionCount);
        Assert.Empty(resultDefault.WetInWet.QualifyingBoundaries);
        Assert.Equal(WatercolorPalettes.Defaults[0], resultDefault.SelectedPalette);

        ColorTransferProfile chosenPalette = WatercolorPalettes.Defaults[^1];
        WatercolorResult resultCustom = WatercolorPipeline.RenderDetailed(empty, new WatercolorOptions(TargetPalette: chosenPalette));
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
            WatercolorPipeline.RenderDetailed(validImage, new WatercolorOptions(ColorTransferStrength: 2.0)));

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

        WatercolorResult autoResult = WatercolorPipeline.RenderDetailed(scene, autoOptions);
        Assert.Equal(scene.Size, autoResult.Image.Size);
        Assert.Equal(autoOptions.TargetPalette, autoResult.SelectedPalette);

        WatercolorResult noBlurResult = WatercolorPipeline.RenderDetailed(scene, new WatercolorOptions(EgbiBlurSigma: 0.0));
        WatercolorResult recursiveBlurResult = WatercolorPipeline.RenderDetailed(scene, new WatercolorOptions(EgbiBlurSigma: 2.5));
        Assert.Equal(scene.Size, noBlurResult.Image.Size);
        Assert.Equal(scene.Size, recursiveBlurResult.Image.Size);
    }

    /// <summary>
    /// Verifies stage progress reporting order, empty-image non-invocation, and 10-element result deconstruction.
    /// </summary>
    [Fact]
    public void RenderDetailed_WithStageProgress_ShouldReportStagesInOrderAndSupportDeconstruct()
    {
        Matrix<Rgb8> scene = CreateTestScene(24, 24);
        var reported = new System.Collections.Generic.List<WatercolorStageProgress>();

        WatercolorResult result = WatercolorPipeline.RenderDetailed(
            scene,
            WatercolorOptions.ForImage(scene),
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
            RegionGraph graph,
            Matrix<Rgb8> lodAbstracted,
            WetInWetResult wetInWet,
            Matrix<Rgb8> handTremor
        ) = result;

        Assert.Same(result.Image, image);
        Assert.Same(result.ColorTransferred, colorTransferred);
        Assert.Same(result.SelectedPalette, selectedPalette);
        Assert.Same(result.SaliencyMap, saliencyMap);
        Assert.Same(result.SalientMask, salientMask);
        Assert.Same(result.SaliencyDistanceField, distanceField);
        Assert.Same(result.SegmentationGraph, graph);
        Assert.Same(result.LodAbstracted, lodAbstracted);
        Assert.Same(result.WetInWet, wetInWet);
        Assert.Same(result.HandTremor, handTremor);

        reported.Clear();
        WatercolorPipeline.RenderDetailed(
            new SequentialMatrix<Rgb8>(0, 0),
            onStageCompleted: reported.Add);
        Assert.Empty(reported);
    }

    private static Matrix<Rgb8> CreateTestScene(int width, int height)
    {
        var image = new SequentialMatrix<Rgb8>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool insideSubject = x >= width / 4 && x < (3 * width) / 4 && y >= height / 4 && y < (3 * height) / 4;
                if (insideSubject)
                {
                    image[x, y] = new Rgb8(220, 45, 35);
                }
                else if (x < width / 2)
                {
                    image[x, y] = new Rgb8(30, 80, 190);
                }
                else
                {
                    image[x, y] = new Rgb8(230, 235, 240);
                }
            }
        }

        return image;
    }
}
