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
        WatercolorPalette palette = WatercolorPalettes.Defaults[0];
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

        Assert.Same(output, result.Output);
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
        WatercolorPalette palette = WatercolorPalettes.Defaults[0];
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
}
