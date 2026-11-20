namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="WatercolorResult"/>, <see cref="WatercolorStep"/>,
/// <see cref="WatercolorStepProgress"/>, <see cref="WatercolorStepTiming"/>, and <see cref="WatercolorStageProgress"/>.
/// </summary>
public class WatercolorResultTests
{
    /// <summary>
    /// Verifies that the constructor assigns all required and optional properties accurately.
    /// </summary>
    [Fact]
    public void Constructor_ValidInputs_ShouldAssignProperties()
    {
        var output = new SequentialMatrix<Rgb8>(2, 2);
        var transferred = new SequentialMatrix<Rgb8>(2, 2);
        ColorTransferProfile palette = WatercolorPalettes.Defaults[0];
        var egbiPreBlurred = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph egbiGraph = TestMatrices.SingleRegionGraph(2, 2);
        var saliency = new SequentialMatrix<DoublePixel>(2, 2);
        var salientMask = new MaskMatrix(2, 2);
        var rawDistanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var distanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var meanShift = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph graph = TestMatrices.SingleRegionGraph(2, 2);
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);
        var edgeDensity = new SequentialMatrix<DoublePixel>(2, 2);
        var turbulenceDensity = new SequentialMatrix<DoublePixel>(2, 2);
        var density = new SequentialMatrix<Rgb8>(2, 2);
        var tone = new SequentialMatrix<Rgb8>(2, 2);
        var paperHeightMap = new SequentialMatrix<DoublePixel>(2, 2);
        var granulated = new SequentialMatrix<Rgb8>(2, 2);
        var timings = new Dictionary<WatercolorStep, WatercolorStepTiming>
        {
            [WatercolorStep.ColorTransfer] = new(TimeSpan.FromMilliseconds(5), FromCache: false, Bypassed: false),
            [WatercolorStep.Fxaa] = new(TimeSpan.Zero, FromCache: false, Bypassed: true)
        };

        var result = new WatercolorResult(
            output,
            transferred,
            palette,
            egbiPreBlurred,
            egbiGraph,
            saliency,
            salientMask,
            rawDistanceField,
            distanceField,
            meanShift,
            graph,
            lod,
            wet,
            tremor,
            edgeDensity,
            turbulenceDensity,
            density,
            tone,
            paperHeightMap,
            granulated,
            timings);

        Assert.Same(output, result.Image);
        Assert.Same(transferred, result.ColorTransferred);
        Assert.Same(palette, result.SelectedPalette);
        Assert.Same(egbiPreBlurred, result.EgbiPreBlurred);
        Assert.Same(egbiGraph, result.EgbiGraph);
        Assert.Same(saliency, result.SaliencyMap);
        Assert.Same(salientMask, result.SalientMask);
        Assert.Same(rawDistanceField, result.RawSaliencyDistanceField);
        Assert.Same(distanceField, result.SaliencyDistanceField);
        Assert.Same(meanShift, result.MeanShiftFiltered);
        Assert.Same(graph, result.SegmentationGraph);
        Assert.Same(lod, result.LodAbstracted);
        Assert.Same(wet, result.WetInWet);
        Assert.Same(tremor, result.HandTremor);
        Assert.Same(edgeDensity, result.EdgeDensityField);
        Assert.Same(turbulenceDensity, result.TurbulenceDensityField);
        Assert.Same(density, result.DensityAdjusted);
        Assert.Same(tone, result.ToneAdjusted);
        Assert.Same(paperHeightMap, result.PaperHeightMap);
        Assert.Same(granulated, result.Granulated);
        Assert.Equal(2, result.StepTimings.Count);
        Assert.Equal(timings[WatercolorStep.ColorTransfer], result.StepTimings[WatercolorStep.ColorTransfer]);
        Assert.Equal(timings[WatercolorStep.Fxaa], result.StepTimings[WatercolorStep.Fxaa]);

        var withNullOptionals = new WatercolorResult(
            output,
            transferred,
            palette,
            egbiPreBlurred,
            egbiGraph,
            saliency,
            salientMask,
            rawDistanceField,
            distanceField,
            meanShift,
            graph,
            lod,
            wet,
            tremor,
            edgeDensityField: null,
            turbulenceDensityField: null,
            density,
            tone,
            paperHeightMap: null,
            granulated,
            new Dictionary<WatercolorStep, WatercolorStepTiming>());

        Assert.Null(withNullOptionals.EdgeDensityField);
        Assert.Null(withNullOptionals.TurbulenceDensityField);
        Assert.Null(withNullOptionals.PaperHeightMap);
        Assert.Empty(withNullOptionals.StepTimings);
    }

    /// <summary>
    /// Verifies that passing <see langword="null"/> for any required constructor parameter throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullArguments_ShouldThrowArgumentNullException()
    {
        var output = new SequentialMatrix<Rgb8>(2, 2);
        var transferred = new SequentialMatrix<Rgb8>(2, 2);
        ColorTransferProfile palette = WatercolorPalettes.Defaults[0];
        var egbiPreBlurred = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph egbiGraph = TestMatrices.SingleRegionGraph(2, 2);
        var saliency = new SequentialMatrix<DoublePixel>(2, 2);
        var salientMask = new MaskMatrix(2, 2);
        var rawDistanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var distanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var meanShift = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph graph = TestMatrices.SingleRegionGraph(2, 2);
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);
        var edgeDensity = new SequentialMatrix<DoublePixel>(2, 2);
        var turbulenceDensity = new SequentialMatrix<DoublePixel>(2, 2);
        var density = new SequentialMatrix<Rgb8>(2, 2);
        var tone = new SequentialMatrix<Rgb8>(2, 2);
        var paperHeightMap = new SequentialMatrix<DoublePixel>(2, 2);
        var granulated = new SequentialMatrix<Rgb8>(2, 2);
        var timings = new Dictionary<WatercolorStep, WatercolorStepTiming>();

        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(null!, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, null!, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, null!, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, null!, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, null!, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, null!, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, null!, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, null!, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, null!, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, null!, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, null!, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, null!, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, null!, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, null!, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, null!, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, null!, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, null!, timings));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, null!));
    }

    /// <summary>
    /// Verifies that mismatched dimensions across any required or optional artifact matrix or <see cref="RegionGraph"/> throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Constructor_DimensionMismatch_ShouldThrowArgumentException()
    {
        var image = new SequentialMatrix<Rgb8>(2, 2);
        var transferred = new SequentialMatrix<Rgb8>(2, 2);
        ColorTransferProfile palette = WatercolorPalettes.Defaults[0];
        var egbiPreBlurred = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph egbiGraph = TestMatrices.SingleRegionGraph(2, 2);
        var saliency = new SequentialMatrix<DoublePixel>(2, 2);
        var salientMask = new MaskMatrix(2, 2);
        var rawDistanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var distanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var meanShift = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph graph = TestMatrices.SingleRegionGraph(2, 2);
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);
        var edgeDensity = new SequentialMatrix<DoublePixel>(2, 2);
        var turbulenceDensity = new SequentialMatrix<DoublePixel>(2, 2);
        var density = new SequentialMatrix<Rgb8>(2, 2);
        var tone = new SequentialMatrix<Rgb8>(2, 2);
        var paperHeightMap = new SequentialMatrix<DoublePixel>(2, 2);
        var granulated = new SequentialMatrix<Rgb8>(2, 2);
        var timings = new Dictionary<WatercolorStep, WatercolorStepTiming>();

        var badRgb = new SequentialMatrix<Rgb8>(3, 2);
        var badDouble = new SequentialMatrix<DoublePixel>(3, 2);
        var badMask = new MaskMatrix(3, 2);
        RegionGraph badGraph = TestMatrices.SingleRegionGraph(3, 2);
        var badWet = new WetInWetResult(badRgb, badMask, []);

        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, badRgb, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, badRgb, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, badGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, badDouble, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, badMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, badDouble, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, badDouble, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, badRgb, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, badGraph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, badRgb, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, badWet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, badRgb, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, badDouble, turbulenceDensity, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, badDouble, density, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, badRgb, tone, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, badRgb, paperHeightMap, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, badDouble, granulated, timings));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, badRgb, timings));
    }

    /// <summary>
    /// Verifies that mismatched boundary counts between <see cref="WetInWetResult.QualifyingBoundaries"/> and
    /// <see cref="RegionGraph.Boundaries"/> throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Constructor_BoundaryCountMismatch_ShouldThrowArgumentException()
    {
        var image = new SequentialMatrix<Rgb8>(2, 2);
        var transferred = new SequentialMatrix<Rgb8>(2, 2);
        ColorTransferProfile palette = WatercolorPalettes.Defaults[0];
        var egbiPreBlurred = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph egbiGraph = TestMatrices.SingleRegionGraph(2, 2);
        var saliency = new SequentialMatrix<DoublePixel>(2, 2);
        var salientMask = new MaskMatrix(2, 2);
        var rawDistanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var distanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var meanShift = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph zeroBoundaryGraph = TestMatrices.SingleRegionGraph(2, 2);
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);
        var density = new SequentialMatrix<Rgb8>(2, 2);
        var tone = new SequentialMatrix<Rgb8>(2, 2);
        var granulated = new SequentialMatrix<Rgb8>(2, 2);
        var timings = new Dictionary<WatercolorStep, WatercolorStepTiming>();

        var extraBoundaryWet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), [true]);
        Assert.Throws<ArgumentException>(() =>
            new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, zeroBoundaryGraph, lod, extraBoundaryWet, tremor, null, null, density, tone, null, granulated, timings));

        var labels = new SequentialMatrix<int>(2, 2);
        labels[1, 0] = 1;
        labels[1, 1] = 1;
        var oneBoundaryGraph = RegionGraph.FromLabels(labels);
        var emptyBoundaryWet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        Assert.Throws<ArgumentException>(() =>
            new WatercolorResult(image, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, oneBoundaryGraph, lod, emptyBoundaryWet, tremor, null, null, density, tone, null, granulated, timings));
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorResult.Deconstruct"/> unpacks all 21 artifacts and timings in positional order.
    /// </summary>
    [Fact]
    public void Deconstruct_ShouldReturnAllPropertiesInOrder()
    {
        var output = new SequentialMatrix<Rgb8>(2, 2);
        var transferred = new SequentialMatrix<Rgb8>(2, 2);
        ColorTransferProfile palette = WatercolorPalettes.Defaults[0];
        var egbiPreBlurred = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph egbiGraph = TestMatrices.SingleRegionGraph(2, 2);
        var saliency = new SequentialMatrix<DoublePixel>(2, 2);
        var salientMask = new MaskMatrix(2, 2);
        var rawDistanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var distanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var meanShift = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph graph = TestMatrices.SingleRegionGraph(2, 2);
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);
        var edgeDensity = new SequentialMatrix<DoublePixel>(2, 2);
        var turbulenceDensity = new SequentialMatrix<DoublePixel>(2, 2);
        var density = new SequentialMatrix<Rgb8>(2, 2);
        var tone = new SequentialMatrix<Rgb8>(2, 2);
        var paperHeightMap = new SequentialMatrix<DoublePixel>(2, 2);
        var granulated = new SequentialMatrix<Rgb8>(2, 2);
        var timings = new Dictionary<WatercolorStep, WatercolorStepTiming>
        {
            [WatercolorStep.ColorTransfer] = new(TimeSpan.FromMilliseconds(3), FromCache: false, Bypassed: false)
        };

        var result = new WatercolorResult(
            output,
            transferred,
            palette,
            egbiPreBlurred,
            egbiGraph,
            saliency,
            salientMask,
            rawDistanceField,
            distanceField,
            meanShift,
            graph,
            lod,
            wet,
            tremor,
            edgeDensity,
            turbulenceDensity,
            density,
            tone,
            paperHeightMap,
            granulated,
            timings);

        (
            Matrix<Rgb8> dImage,
            Matrix<Rgb8> dTransferred,
            ColorTransferProfile dPalette,
            Matrix<Rgb8> dEgbiPreBlurred,
            RegionGraph dEgbiGraph,
            Matrix<DoublePixel> dSaliency,
            MaskMatrix dMask,
            Matrix<DoublePixel> dRawDistance,
            Matrix<DoublePixel> dDistance,
            Matrix<Rgb8> dMeanShift,
            RegionGraph dGraph,
            Matrix<Rgb8> dLod,
            WetInWetResult dWet,
            Matrix<Rgb8> dTremor,
            Matrix<DoublePixel>? dEdgeDensity,
            Matrix<DoublePixel>? dTurbulenceDensity,
            Matrix<Rgb8> dDensity,
            Matrix<Rgb8> dTone,
            IReadOnlyMatrix<DoublePixel>? dPaperHeightMap,
            Matrix<Rgb8> dGranulated,
            IReadOnlyDictionary<WatercolorStep, WatercolorStepTiming> dTimings
        ) = result;

        Assert.Same(output, dImage);
        Assert.Same(transferred, dTransferred);
        Assert.Same(palette, dPalette);
        Assert.Same(egbiPreBlurred, dEgbiPreBlurred);
        Assert.Same(egbiGraph, dEgbiGraph);
        Assert.Same(saliency, dSaliency);
        Assert.Same(salientMask, dMask);
        Assert.Same(rawDistanceField, dRawDistance);
        Assert.Same(distanceField, dDistance);
        Assert.Same(meanShift, dMeanShift);
        Assert.Same(graph, dGraph);
        Assert.Same(lod, dLod);
        Assert.Same(wet, dWet);
        Assert.Same(tremor, dTremor);
        Assert.Same(edgeDensity, dEdgeDensity);
        Assert.Same(turbulenceDensity, dTurbulenceDensity);
        Assert.Same(density, dDensity);
        Assert.Same(tone, dTone);
        Assert.Same(paperHeightMap, dPaperHeightMap);
        Assert.Same(granulated, dGranulated);
        Assert.Same(result.StepTimings, dTimings);
        Assert.Equal(timings[WatercolorStep.ColorTransfer], dTimings[WatercolorStep.ColorTransfer]);
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorResult.StepTimings"/> is defensively copied and read-only.
    /// </summary>
    [Fact]
    public void StepTimings_ShouldBeDefensivelyCopiedAndReadOnly()
    {
        var timing = new WatercolorStepTiming(TimeSpan.FromMilliseconds(12), FromCache: false, Bypassed: false);
        var sourceTimings = new Dictionary<WatercolorStep, WatercolorStepTiming>
        {
            [WatercolorStep.ColorTransfer] = timing
        };

        WatercolorResult result = CreateResult(sourceTimings);
        sourceTimings[WatercolorStep.ColorTransfer] = new WatercolorStepTiming(TimeSpan.FromMilliseconds(99), FromCache: true, Bypassed: true);
        sourceTimings[WatercolorStep.Fxaa] = new WatercolorStepTiming(TimeSpan.FromMilliseconds(1), FromCache: false, Bypassed: false);

        Assert.Single(result.StepTimings);
        Assert.Equal(timing, result.StepTimings[WatercolorStep.ColorTransfer]);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<WatercolorStep, WatercolorStepTiming>)result.StepTimings)[WatercolorStep.Fxaa] = timing);
    }

    /// <summary>
    /// Verifies record value equality and hash code consistency for <see cref="WatercolorResult"/> including <see cref="WatercolorResult.StepTimings"/>.
    /// </summary>
    [Fact]
    public void RecordEquality_ShouldCompareArtifactsAndStepTimingsByValue()
    {
        var output = new SequentialMatrix<Rgb8>(2, 2);
        var transferred = new SequentialMatrix<Rgb8>(2, 2);
        ColorTransferProfile palette = WatercolorPalettes.Defaults[0];
        var egbiPreBlurred = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph egbiGraph = TestMatrices.SingleRegionGraph(2, 2);
        var saliency = new SequentialMatrix<DoublePixel>(2, 2);
        var salientMask = new MaskMatrix(2, 2);
        var rawDistanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var distanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var meanShift = new SequentialMatrix<Rgb8>(2, 2);
        RegionGraph graph = TestMatrices.SingleRegionGraph(2, 2);
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);
        var edgeDensity = new SequentialMatrix<DoublePixel>(2, 2);
        var turbulenceDensity = new SequentialMatrix<DoublePixel>(2, 2);
        var density = new SequentialMatrix<Rgb8>(2, 2);
        var tone = new SequentialMatrix<Rgb8>(2, 2);
        var paperHeightMap = new SequentialMatrix<DoublePixel>(2, 2);
        var granulated = new SequentialMatrix<Rgb8>(2, 2);

        var timingsA = new Dictionary<WatercolorStep, WatercolorStepTiming>
        {
            [WatercolorStep.ColorTransfer] = new(TimeSpan.FromMilliseconds(4), FromCache: false, Bypassed: false),
            [WatercolorStep.Fxaa] = new(TimeSpan.Zero, FromCache: true, Bypassed: false)
        };
        var timingsB = new Dictionary<WatercolorStep, WatercolorStepTiming>
        {
            [WatercolorStep.Fxaa] = new(TimeSpan.Zero, FromCache: true, Bypassed: false),
            [WatercolorStep.ColorTransfer] = new(TimeSpan.FromMilliseconds(4), FromCache: false, Bypassed: false)
        };
        var timingsDifferent = new Dictionary<WatercolorStep, WatercolorStepTiming>
        {
            [WatercolorStep.ColorTransfer] = new(TimeSpan.FromMilliseconds(4), FromCache: false, Bypassed: true),
            [WatercolorStep.Fxaa] = new(TimeSpan.Zero, FromCache: true, Bypassed: false)
        };

        var res1 = new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timingsA);
        var res2 = new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timingsB);
        var resDiffTimings = new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, edgeDensity, turbulenceDensity, density, tone, paperHeightMap, granulated, timingsDifferent);
        var resNullEdge = new WatercolorResult(output, transferred, palette, egbiPreBlurred, egbiGraph, saliency, salientMask, rawDistanceField, distanceField, meanShift, graph, lod, wet, tremor, null, turbulenceDensity, density, tone, paperHeightMap, granulated, timingsA);

        Assert.Equal(res1, res2);
        Assert.True(res1 == res2);
        Assert.Equal(res1.GetHashCode(), res2.GetHashCode());
        Assert.NotEqual(res1, resDiffTimings);
        Assert.NotEqual(res1, resNullEdge);
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorStep"/> defines all 18 pipeline DAG steps.
    /// </summary>
    [Fact]
    public void WatercolorStep_ShouldDefineAll18DagSteps()
    {
        WatercolorStep[] steps = Enum.GetValues<WatercolorStep>();

        Assert.Equal(18, steps.Length);
        Assert.Equal(
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
            ],
            steps);
    }

    /// <summary>
    /// Verifies property assignment and record value equality for <see cref="WatercolorStageProgress"/>.
    /// </summary>
    [Fact]
    public void WatercolorStageProgress_PropertiesAndEquality_ShouldBehaveAsValueRecord()
    {
        var elapsed = TimeSpan.FromMilliseconds(42);
        var first = new WatercolorStageProgress(WatercolorStage.Saliency, elapsed);
        var equal = new WatercolorStageProgress(WatercolorStage.Saliency, TimeSpan.FromMilliseconds(42));
        var differentStage = new WatercolorStageProgress(WatercolorStage.WetInWet, elapsed);
        var differentElapsed = new WatercolorStageProgress(WatercolorStage.Saliency, TimeSpan.FromMilliseconds(43));

        Assert.Equal(WatercolorStage.Saliency, first.Stage);
        Assert.Equal(elapsed, first.Elapsed);
        Assert.Equal(first, equal);
        Assert.True(first == equal);
        Assert.Equal(first.GetHashCode(), equal.GetHashCode());
        Assert.NotEqual(first, differentStage);
        Assert.NotEqual(first, differentElapsed);
        Assert.True(first != differentStage);
    }

    /// <summary>
    /// Verifies property assignment and record value equality for <see cref="WatercolorStepProgress"/> and <see cref="WatercolorStepTiming"/>.
    /// </summary>
    [Fact]
    public void WatercolorStepProgressAndTiming_ShouldBehaveAsValueRecords()
    {
        var elapsed = TimeSpan.FromMilliseconds(17);

        var progress1 = new WatercolorStepProgress(WatercolorStep.MeanShiftFiltering, WatercolorStage.Segmentation, elapsed, FromCache: true, Bypassed: false);
        var progress2 = new WatercolorStepProgress(WatercolorStep.MeanShiftFiltering, WatercolorStage.Segmentation, TimeSpan.FromMilliseconds(17), FromCache: true, Bypassed: false);
        var progressDiffCache = progress1 with { FromCache = false };
        var progressDiffBypass = progress1 with { Bypassed = true };

        Assert.Equal(WatercolorStep.MeanShiftFiltering, progress1.Step);
        Assert.Equal(WatercolorStage.Segmentation, progress1.Stage);
        Assert.Equal(elapsed, progress1.Elapsed);
        Assert.True(progress1.FromCache);
        Assert.False(progress1.Bypassed);
        Assert.Equal(progress1, progress2);
        Assert.Equal(progress1.GetHashCode(), progress2.GetHashCode());
        Assert.NotEqual(progress1, progressDiffCache);
        Assert.NotEqual(progress1, progressDiffBypass);

        var timing1 = new WatercolorStepTiming(elapsed, FromCache: false, Bypassed: true);
        var timing2 = new WatercolorStepTiming(TimeSpan.FromMilliseconds(17), FromCache: false, Bypassed: true);
        var timingDiffElapsed = timing1 with { Elapsed = TimeSpan.FromMilliseconds(18) };
        var timingDiffCache = timing1 with { FromCache = true };

        Assert.Equal(elapsed, timing1.Elapsed);
        Assert.False(timing1.FromCache);
        Assert.True(timing1.Bypassed);
        Assert.Equal(timing1, timing2);
        Assert.Equal(timing1.GetHashCode(), timing2.GetHashCode());
        Assert.NotEqual(timing1, timingDiffElapsed);
        Assert.NotEqual(timing1, timingDiffCache);
    }

    private static WatercolorResult CreateResult(IReadOnlyDictionary<WatercolorStep, WatercolorStepTiming> timings) =>
        new(
            new SequentialMatrix<Rgb8>(2, 2),
            new SequentialMatrix<Rgb8>(2, 2),
            WatercolorPalettes.Defaults[0],
            new SequentialMatrix<Rgb8>(2, 2),
            TestMatrices.SingleRegionGraph(2, 2),
            new SequentialMatrix<DoublePixel>(2, 2),
            new MaskMatrix(2, 2),
            new SequentialMatrix<DoublePixel>(2, 2),
            new SequentialMatrix<DoublePixel>(2, 2),
            new SequentialMatrix<Rgb8>(2, 2),
            TestMatrices.SingleRegionGraph(2, 2),
            new SequentialMatrix<Rgb8>(2, 2),
            new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []),
            new SequentialMatrix<Rgb8>(2, 2),
            edgeDensityField: null,
            turbulenceDensityField: null,
            new SequentialMatrix<Rgb8>(2, 2),
            new SequentialMatrix<Rgb8>(2, 2),
            paperHeightMap: null,
            new SequentialMatrix<Rgb8>(2, 2),
            timings);
}
