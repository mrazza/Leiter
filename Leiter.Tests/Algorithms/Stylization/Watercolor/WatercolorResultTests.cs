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
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(2, 2));
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);

        var result = new WatercolorResult(
            output,
            transferred,
            palette,
            saliency,
            salientMask,
            distanceField,
            graph,
            lod,
            wet,
            tremor);

        Assert.Same(output, result.Image);
        Assert.Same(transferred, result.ColorTransferred);
        Assert.Same(palette, result.SelectedPalette);
        Assert.Same(saliency, result.SaliencyMap);
        Assert.Same(salientMask, result.SalientMask);
        Assert.Same(distanceField, result.SaliencyDistanceField);
        Assert.Same(graph, result.SegmentationGraph);
        Assert.Same(lod, result.LodAbstracted);
        Assert.Same(wet, result.WetInWet);
        Assert.Same(tremor, result.HandTremor);
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
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(2, 2));
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);

        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(null!, transferred, palette, saliency, salientMask, distanceField, graph, lod, wet, tremor));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, null!, palette, saliency, salientMask, distanceField, graph, lod, wet, tremor));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, null!, saliency, salientMask, distanceField, graph, lod, wet, tremor));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, null!, salientMask, distanceField, graph, lod, wet, tremor));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, null!, distanceField, graph, lod, wet, tremor));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, null!, graph, lod, wet, tremor));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, null!, lod, wet, tremor));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, graph, null!, wet, tremor));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, graph, lod, null!, tremor));
        Assert.Throws<ArgumentNullException>(() => new WatercolorResult(output, transferred, palette, saliency, salientMask, distanceField, graph, lod, wet, null!));
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
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(2, 2));
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);

        var badRgb = new SequentialMatrix<Rgb8>(3, 2);
        var badDouble = new SequentialMatrix<DoublePixel>(3, 2);
        var badMask = new MaskMatrix(3, 2);
        var badGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(3, 2));
        var badWet = new WetInWetResult(badRgb, badMask, []);

        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, badRgb, palette, saliency, salientMask, distanceField, graph, lod, wet, tremor));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, badDouble, salientMask, distanceField, graph, lod, wet, tremor));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, badMask, distanceField, graph, lod, wet, tremor));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, badDouble, graph, lod, wet, tremor));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, badGraph, lod, wet, tremor));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, graph, badRgb, wet, tremor));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, graph, lod, badWet, tremor));
        Assert.Throws<ArgumentException>(() => new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, graph, lod, wet, badRgb));
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
        var zeroBoundaryGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(2, 2));
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);

        var extraBoundaryWet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), [true]);
        Assert.Throws<ArgumentException>(() =>
            new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, zeroBoundaryGraph, lod, extraBoundaryWet, tremor));

        var labels = new SequentialMatrix<int>(2, 2);
        labels[1, 0] = 1;
        labels[1, 1] = 1;
        var oneBoundaryGraph = RegionGraph.FromLabels(labels);
        var emptyBoundaryWet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        Assert.Throws<ArgumentException>(() =>
            new WatercolorResult(image, transferred, palette, saliency, salientMask, distanceField, oneBoundaryGraph, lod, emptyBoundaryWet, tremor));
    }

    /// <summary>
    /// Verifies that <see cref="WatercolorResult.Deconstruct"/> unpacks all 10 artifacts in positional order.
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
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(2, 2));
        var lod = new SequentialMatrix<Rgb8>(2, 2);
        var wet = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        var tremor = new SequentialMatrix<Rgb8>(2, 2);

        var result = new WatercolorResult(
            output,
            transferred,
            palette,
            saliency,
            salientMask,
            distanceField,
            graph,
            lod,
            wet,
            tremor);

        (
            Matrix<Rgb8> dImage,
            Matrix<Rgb8> dTransferred,
            ColorTransferProfile dPalette,
            Matrix<DoublePixel> dSaliency,
            MaskMatrix dMask,
            Matrix<DoublePixel> dDistance,
            RegionGraph dGraph,
            Matrix<Rgb8> dLod,
            WetInWetResult dWet,
            Matrix<Rgb8> dTremor
        ) = result;

        Assert.Same(output, dImage);
        Assert.Same(transferred, dTransferred);
        Assert.Same(palette, dPalette);
        Assert.Same(saliency, dSaliency);
        Assert.Same(salientMask, dMask);
        Assert.Same(distanceField, dDistance);
        Assert.Same(graph, dGraph);
        Assert.Same(lod, dLod);
        Assert.Same(wet, dWet);
        Assert.Same(tremor, dTremor);
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
