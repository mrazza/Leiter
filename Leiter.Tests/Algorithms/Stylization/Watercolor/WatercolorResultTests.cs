namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.Color;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="WatercolorResult"/>.
/// </summary>
public class WatercolorResultTests
{
    /// <summary>
    /// Verifies that constructor assigns all properties accurately.
    /// </summary>
    [Fact]
    public void Constructor_ValidInputs_ShouldAssignProperties()
    {
        var output = new SequentialMatrix<Rgb8>(2, 2);
        var transferred = new SequentialMatrix<Rgb8>(2, 2);
        ColorTransferProfile palette = WatercolorPalettes.Defaults[0];
        var saliency = new SequentialMatrix<DoublePixel>(2, 2);
        var salientMask = new MaskMatrix(2, 2);
        var distanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var meanShift = new SequentialMatrix<Rgb8>(2, 2);
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(2, 2));
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);
        var density = new SequentialMatrix<Rgb8>(2, 2);
        var tone = new SequentialMatrix<Rgb8>(2, 2);
        var granulated = new SequentialMatrix<Rgb8>(2, 2);

        var result = new WatercolorResult(
            output,
            transferred,
            palette,
            saliency,
            salientMask,
            distanceField,
            meanShift,
            graph,
            lod,
            wet,
            tremor,
            density,
            tone,
            granulated);

        Assert.Same(output, result.Image);
        Assert.Same(transferred, result.ColorTransferred);
        Assert.Same(palette, result.SelectedPalette);
        Assert.Same(saliency, result.SaliencyMap);
        Assert.Same(salientMask, result.SalientMask);
        Assert.Same(distanceField, result.SaliencyDistanceField);
        Assert.Same(meanShift, result.MeanShiftFiltered);
        Assert.Same(graph, result.SegmentationGraph);
        Assert.Same(lod, result.LodAbstracted);
        Assert.Same(wet, result.WetInWet);
        Assert.Same(tremor, result.HandTremor);
        Assert.Same(density, result.DensityAdjusted);
        Assert.Same(tone, result.ToneAdjusted);
        Assert.Same(granulated, result.Granulated);
    }

    /// <summary>
    /// Verifies that passing <see langword="null"/> for any constructor parameter throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullArguments_ShouldThrowArgumentNullException()
    {
        var output = new SequentialMatrix<Rgb8>(2, 2);
        var transferred = new SequentialMatrix<Rgb8>(2, 2);
        ColorTransferProfile palette = WatercolorPalettes.Defaults[0];
        var saliency = new SequentialMatrix<DoublePixel>(2, 2);
        var salientMask = new MaskMatrix(2, 2);
        var distanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var meanShift = new SequentialMatrix<Rgb8>(2, 2);
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(2, 2));
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);
        var density = new SequentialMatrix<Rgb8>(2, 2);
        var tone = new SequentialMatrix<Rgb8>(2, 2);
        var granulated = new SequentialMatrix<Rgb8>(2, 2);

        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(null!, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, null!, palette, saliency, salientMask, distanceField, meanShift, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, null!, saliency, salientMask, distanceField, meanShift, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, null!, salientMask, distanceField, meanShift, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, null!, distanceField, meanShift, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, null!, meanShift, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, null!, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, meanShift, null!, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, null!, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, lod, null!, tremor, density, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, lod, wet, null!, density, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, lod, wet, tremor, null!, tone, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, lod, wet, tremor, density, null!, granulated));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, lod, wet, tremor, density, tone, null!));
    }

    /// <summary>
    /// Verifies that mismatched dimensions across any artifact matrix or <see cref="RegionGraph"/> throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Constructor_DimensionMismatch_ShouldThrowArgumentException()
    {
        var image = new SequentialMatrix<Rgb8>(2, 2);
        var transferred = new SequentialMatrix<Rgb8>(2, 2);
        ColorTransferProfile palette = WatercolorPalettes.Defaults[0];
        var saliency = new SequentialMatrix<DoublePixel>(2, 2);
        var salientMask = new MaskMatrix(2, 2);
        var distanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var meanShift = new SequentialMatrix<Rgb8>(2, 2);
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(2, 2));
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);
        var density = new SequentialMatrix<Rgb8>(2, 2);
        var tone = new SequentialMatrix<Rgb8>(2, 2);
        var granulated = new SequentialMatrix<Rgb8>(2, 2);

        var badRgb = new SequentialMatrix<Rgb8>(3, 2);
        var badDouble = new SequentialMatrix<DoublePixel>(3, 2);
        var badMask = new MaskMatrix(3, 2);
        var badGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(3, 2));
        var badWet = new WetInWetResult(badRgb, badMask, []);

        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, badRgb, palette, saliency, salientMask, distanceField, meanShift, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, badDouble, salientMask, distanceField, meanShift, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, badMask, distanceField, meanShift, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, badDouble, meanShift, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, badRgb, graph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, meanShift, badGraph, lod, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, badRgb, wet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, lod, badWet, tremor, density, tone, granulated));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, lod, wet, badRgb, density, tone, granulated));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, lod, wet, tremor, badRgb, tone, granulated));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, lod, wet, tremor, density, badRgb, granulated));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, meanShift, graph, lod, wet, tremor, density, tone, badRgb));
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
        var saliency = new SequentialMatrix<DoublePixel>(2, 2);
        var salientMask = new MaskMatrix(2, 2);
        var distanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var meanShift = new SequentialMatrix<Rgb8>(2, 2);
        var zeroBoundaryGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(2, 2));
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);
        var density = new SequentialMatrix<Rgb8>(2, 2);
        var tone = new SequentialMatrix<Rgb8>(2, 2);
        var granulated = new SequentialMatrix<Rgb8>(2, 2);

        var extraBoundaryWet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), [true]);
        Assert.Throws<ArgumentException>(() =>
            new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, meanShift, zeroBoundaryGraph, lod, extraBoundaryWet, tremor, density, tone, granulated));

        var labels = new SequentialMatrix<int>(2, 2);
        labels[1, 0] = 1;
        labels[1, 1] = 1;
        var oneBoundaryGraph = RegionGraph.FromLabels(labels);
        var emptyBoundaryWet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        Assert.Throws<ArgumentException>(() =>
            new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, meanShift, oneBoundaryGraph, lod, emptyBoundaryWet, tremor, density, tone, granulated));
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorResult.Deconstruct"/> unpacks all 14 artifacts in positional order.
    /// </summary>
    [Fact]
    public void Deconstruct_ShouldReturnAllPropertiesInOrder()
    {
        var output = new SequentialMatrix<Rgb8>(2, 2);
        var transferred = new SequentialMatrix<Rgb8>(2, 2);
        ColorTransferProfile palette = WatercolorPalettes.Defaults[0];
        var saliency = new SequentialMatrix<DoublePixel>(2, 2);
        var salientMask = new MaskMatrix(2, 2);
        var distanceField = new SequentialMatrix<DoublePixel>(2, 2);
        var meanShift = new SequentialMatrix<Rgb8>(2, 2);
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(2, 2));
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);
        var density = new SequentialMatrix<Rgb8>(2, 2);
        var tone = new SequentialMatrix<Rgb8>(2, 2);
        var granulated = new SequentialMatrix<Rgb8>(2, 2);

        var result = new WatercolorResult(
            output,
            transferred,
            palette,
            saliency,
            salientMask,
            distanceField,
            meanShift,
            graph,
            lod,
            wet,
            tremor,
            density,
            tone,
            granulated);

        (
            Matrix<Rgb8> dImage,
            Matrix<Rgb8> dTransferred,
            ColorTransferProfile dPalette,
            Matrix<DoublePixel> dSaliency,
            MaskMatrix dMask,
            Matrix<DoublePixel> dDistance,
            Matrix<Rgb8> dMeanShift,
            RegionGraph dGraph,
            Matrix<Rgb8> dLod,
            WetInWetResult dWet,
            Matrix<Rgb8> dTremor,
            Matrix<Rgb8> dDensity,
            Matrix<Rgb8> dTone,
            Matrix<Rgb8> dGranulated
        ) = result;

        Assert.Same(output, dImage);
        Assert.Same(transferred, dTransferred);
        Assert.Same(palette, dPalette);
        Assert.Same(saliency, dSaliency);
        Assert.Same(salientMask, dMask);
        Assert.Same(distanceField, dDistance);
        Assert.Same(meanShift, dMeanShift);
        Assert.Same(graph, dGraph);
        Assert.Same(lod, dLod);
        Assert.Same(wet, dWet);
        Assert.Same(tremor, dTremor);
        Assert.Same(density, dDensity);
        Assert.Same(tone, dTone);
        Assert.Same(granulated, dGranulated);
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
}
