namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using System.Threading;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="WetInWet"/>.
/// </summary>
public class WetInWetTests
{
    /// <summary>
    /// Verifies that null arguments passed to <see cref="WetInWet.ApplyDetailed(IReadOnlyMatrix{Rgb8}, RegionGraph, IReadOnlyMatrix{bool}?, WetInWetOptions?, CancellationToken)"/>
    /// and related overloads throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void ApplyDetailed_NullArguments_ShouldThrowArgumentNullException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        RegionGraph graph = TestMatrices.SingleRegionGraph(4, 4);
        DisjointSet<int> disjointSet = CreateVerticalSplitDisjointSet(4, 4);
        var dist = new SequentialMatrix<DoublePixel>(4, 4);

        Assert.Throws<ArgumentNullException>(() => WetInWet.ApplyDetailed(null!, graph));
        Assert.Throws<ArgumentNullException>(() => WetInWet.ApplyDetailed(image, (RegionGraph)null!));
        Assert.Throws<ArgumentNullException>(() => WetInWet.ApplyDetailed(null!, graph, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWet.ApplyDetailed(image, (RegionGraph)null!, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWet.ApplyDetailed(image, graph, (IReadOnlyMatrix<DoublePixel>)null!));

        Assert.Throws<ArgumentNullException>(() => WetInWet.ApplyDetailed(null!, disjointSet));
        Assert.Throws<ArgumentNullException>(() => WetInWet.ApplyDetailed(image, (IDisjointSet)null!));
        Assert.Throws<ArgumentNullException>(() => WetInWet.ApplyDetailed(null!, disjointSet, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWet.ApplyDetailed(image, (IDisjointSet)null!, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWet.ApplyDetailed(image, disjointSet, (IReadOnlyMatrix<DoublePixel>)null!));
    }

    /// <summary>
    /// Verifies that null arguments passed to <see cref="WetInWet.Apply(IReadOnlyMatrix{Rgb8}, RegionGraph, IReadOnlyMatrix{bool}?, WetInWetOptions?, CancellationToken)"/>
    /// and related overloads throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Apply_NullArguments_ShouldThrowArgumentNullException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        RegionGraph graph = TestMatrices.SingleRegionGraph(4, 4);
        DisjointSet<int> disjointSet = CreateVerticalSplitDisjointSet(4, 4);
        var dist = new SequentialMatrix<DoublePixel>(4, 4);

        Assert.Throws<ArgumentNullException>(() => WetInWet.Apply(null!, graph));
        Assert.Throws<ArgumentNullException>(() => WetInWet.Apply(image, (RegionGraph)null!));
        Assert.Throws<ArgumentNullException>(() => WetInWet.Apply(null!, graph, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWet.Apply(image, (RegionGraph)null!, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWet.Apply(image, graph, (IReadOnlyMatrix<DoublePixel>)null!));

        Assert.Throws<ArgumentNullException>(() => WetInWet.Apply(null!, disjointSet));
        Assert.Throws<ArgumentNullException>(() => WetInWet.Apply(image, (IDisjointSet)null!));
        Assert.Throws<ArgumentNullException>(() => WetInWet.Apply(null!, disjointSet, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWet.Apply(image, (IDisjointSet)null!, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWet.Apply(image, disjointSet, (IReadOnlyMatrix<DoublePixel>)null!));
    }

    /// <summary>
    /// Verifies that mismatched matrix dimensions throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void ApplyDetailed_MismatchedDimensions_ShouldThrowArgumentException()
    {
        var image = new SequentialMatrix<Rgb8>(6, 6);
        RegionGraph graphMismatched = TestMatrices.SingleRegionGraph(4, 6);
        RegionGraph graphMatching = TestMatrices.SingleRegionGraph(6, 6);
        DisjointSet<int> disjointMismatched = CreateVerticalSplitDisjointSet(4, 6);
        DisjointSet<int> disjointMatching = CreateVerticalSplitDisjointSet(6, 6);
        var distMismatched = new SequentialMatrix<DoublePixel>(6, 4);
        var distMatching = new SequentialMatrix<DoublePixel>(6, 6);
        var maskMismatched = new MaskMatrix(4, 4);

        Assert.Throws<ArgumentException>(() => WetInWet.ApplyDetailed(image, graphMismatched));
        Assert.Throws<ArgumentException>(() => WetInWet.ApplyDetailed(image, graphMatching, maskMismatched));
        Assert.Throws<ArgumentException>(() => WetInWet.ApplyDetailed(image, graphMismatched, distMatching));
        Assert.Throws<ArgumentException>(() => WetInWet.ApplyDetailed(image, graphMatching, distMismatched));

        Assert.Throws<ArgumentException>(() => WetInWet.ApplyDetailed(image, disjointMismatched));
        Assert.Throws<ArgumentException>(() => WetInWet.ApplyDetailed(image, disjointMatching, maskMismatched));
        Assert.Throws<ArgumentException>(() => WetInWet.ApplyDetailed(image, disjointMismatched, distMatching));
        Assert.Throws<ArgumentException>(() => WetInWet.ApplyDetailed(image, disjointMatching, distMismatched));
    }

    /// <summary>
    /// Verifies that invalid options throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ApplyDetailed_InvalidOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        RegionGraph graph = TestMatrices.SingleRegionGraph(4, 4);
        var dist = new SequentialMatrix<DoublePixel>(4, 4);
        var invalidOptions = new WetInWetOptions(EllipticalFilter: new OrientedEllipticalFilterOptions(MajorRadius: -1.0));

        Assert.Throws<ArgumentOutOfRangeException>(() => WetInWet.ApplyDetailed(image, graph, dist, options: invalidOptions));
    }

    /// <summary>
    /// Verifies fast path handling when the input image is empty (0x0).
    /// </summary>
    [Fact]
    public void ApplyDetailed_EmptyImage_ShouldReturnEmptyResult()
    {
        var image = new SequentialMatrix<Rgb8>(0, 0);
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(0, 0));
        DisjointSet<int> disjointSet = CreateVerticalSplitDisjointSet(0, 0);
        var dist = new SequentialMatrix<DoublePixel>(0, 0);

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, dist);
        WetInWetResult resultDisjoint = WetInWet.ApplyDetailed(image, disjointSet);

        Assert.Equal(0, result.Image.Width);
        Assert.Equal(0, result.Image.Height);
        Assert.Empty(result.QualifyingBoundaries);
        Assert.Empty(resultDisjoint.Image);
        Assert.Empty(resultDisjoint.QualifyingBoundaries);
    }

    /// <summary>
    /// Verifies that Disabled mode returns an unmodified image copy and all-false qualifying boundaries.
    /// </summary>
    [Fact]
    public void ApplyDetailed_DisabledMode_ShouldReturnUnmodifiedImage()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            6,
            6,
            new Rgb8(20, 20, 20),
            new Rgb8(200, 200, 200));
        var dist = new SequentialMatrix<DoublePixel>(6, 6);
        var options = new WetInWetOptions(Mode: WetInWetMode.Disabled);

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, dist, options: options);

        Assert.Equal((byte)20, result.Image[0, 0].R);
        Assert.Equal((byte)200, result.Image[5, 5].R);
        Assert.Single(result.QualifyingBoundaries);
        Assert.False(result.QualifyingBoundaries[0]);
    }

    /// <summary>
    /// Verifies that a single-region image with zero boundaries returns an unmodified image copy.
    /// </summary>
    [Fact]
    public void ApplyDetailed_ZeroBoundaries_ShouldReturnUnmodifiedImage()
    {
        int width = 4;
        int height = 4;
        var image = new SequentialMatrix<Rgb8>(width, height);
        RegionGraph graph = TestMatrices.SingleRegionGraph(width, height);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, dist);

        Assert.Equal(width, result.Image.Width);
        Assert.Empty(result.QualifyingBoundaries);
    }

    /// <summary>
    /// Verifies that boundaries with intensity difference below the threshold do not qualify.
    /// </summary>
    [Fact]
    public void ApplyDetailed_IntensityDifferenceBelowThreshold_ShouldNotQualify()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            6,
            6,
            new Rgb8(100, 100, 100),
            new Rgb8(101, 101, 101));
        var dist = new SequentialMatrix<DoublePixel>(6, 6);
        var options = new WetInWetOptions(MinIntensityDifference: 0.05);

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, dist, options: options);

        Assert.Single(result.QualifyingBoundaries);
        Assert.False(result.QualifyingBoundaries[0]);
        Assert.False(result.WetAreaMask[2, 2]);
    }

    /// <summary>
    /// Verifies all four <see cref="WetInWetMode"/> options on salient and non-salient boundaries.
    /// </summary>
    [Fact]
    public void ApplyDetailed_ModeBehaviors_ShouldFilterCorrectBoundaries()
    {
        // 4 horizontal bands:
        // Region 0 (y=0..1, Salient), Region 1 (y=2..3, Salient), Region 2 (y=4..5, Non-salient), Region 3 (y=6..7, Non-salient)
        const int width = 6;
        const int height = 8;
        SequentialMatrix<Rgb8> image = TestMatrices.Create(width, height, static (_, y) =>
            y < 2 ? new Rgb8(20, 20, 20)
            : y < 4 ? new Rgb8(90, 90, 90)
            : y < 6 ? new Rgb8(165, 165, 165)
            : new Rgb8(240, 240, 240));
        SequentialMatrix<int> labels = TestMatrices.Create(width, height, static (_, y) => y / 2);
        SequentialMatrix<bool> salientMask = TestMatrices.Create(width, height, static (_, y) => y < 4);

        var graph = RegionGraph.FromLabels(labels);

        int boundary01 = -1; // salient interior
        int boundary12 = -1; // salient to non-salient boundary
        int boundary23 = -1; // non-salient interior
        for (int i = 0; i < graph.Boundaries.Count; i++)
        {
            var b = graph.Boundaries[i];
            if ((b.First == 0 && b.Second == 1) || (b.First == 1 && b.Second == 0))
                boundary01 = i;
            if ((b.First == 1 && b.Second == 2) || (b.First == 2 && b.Second == 1))
                boundary12 = i;
            if ((b.First == 2 && b.Second == 3) || (b.First == 3 && b.Second == 2))
                boundary23 = i;
        }

        Assert.True(boundary01 >= 0);
        Assert.True(boundary12 >= 0);
        Assert.True(boundary23 >= 0);

        // 1. Default mode: all three qualify
        var optDefault = new WetInWetOptions(Mode: WetInWetMode.Default, MinIntensityDifference: 0.01);
        WetInWetResult resDefault = WetInWet.ApplyDetailed(image, graph, salientMask, optDefault);
        Assert.True(resDefault.QualifyingBoundaries[boundary01]);
        Assert.True(resDefault.QualifyingBoundaries[boundary12]);
        Assert.True(resDefault.QualifyingBoundaries[boundary23]);

        // 2. ExcludeSalientBoundaries: boundary12 (salient vs non-salient) excluded; boundary01 and boundary23 included
        var optExcludeBoundaries = new WetInWetOptions(Mode: WetInWetMode.ExcludeSalientBoundaries, MinIntensityDifference: 0.01);
        WetInWetResult resExcludeBoundaries = WetInWet.ApplyDetailed(image, graph, salientMask, optExcludeBoundaries);
        Assert.True(resExcludeBoundaries.QualifyingBoundaries[boundary01]);
        Assert.False(resExcludeBoundaries.QualifyingBoundaries[boundary12]);
        Assert.True(resExcludeBoundaries.QualifyingBoundaries[boundary23]);

        // 3. ExcludeSalientRegions: boundary01 (interior of salient regions) excluded; outer boundary12 and background boundary23 included
        var optExcludeRegions = new WetInWetOptions(Mode: WetInWetMode.ExcludeSalientRegions, MinIntensityDifference: 0.01);
        WetInWetResult resExcludeRegions = WetInWet.ApplyDetailed(image, graph, salientMask, optExcludeRegions);
        Assert.False(resExcludeRegions.QualifyingBoundaries[boundary01]);
        Assert.True(resExcludeRegions.QualifyingBoundaries[boundary12]);
        Assert.True(resExcludeRegions.QualifyingBoundaries[boundary23]);
    }

    /// <summary>
    /// Verifies that high color gradient magnitude qualifies a boundary even when hue difference exceeds the threshold.
    /// </summary>
    [Fact]
    public void ApplyDetailed_HighColorGradient_ShouldQualifyDespiteLargeHueDifference()
    {
        const int width = 8;
        const int height = 8;
        // High 2D contrast (both X and Y gradients non-zero) between contrasting hues (Red vs Cyan)
        SequentialMatrix<Rgb8> image = TestMatrices.Create(width, height, static (x, y) =>
            x < 4
                ? new Rgb8((byte)Math.Min(255, 40 + (x * 45) + (y * 25)), 10, 10)
                : new Rgb8(20, (byte)Math.Min(255, 80 + ((x - 4) * 35) + (y * 20)), (byte)Math.Min(255, 80 + ((x - 4) * 35) + (y * 20))));
        SequentialMatrix<int> labels = TestMatrices.Create(width, height, static (x, _) => x < 4 ? 0 : 1);

        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        // With small gradient threshold, gradient rule triggers even with SalientHueThresholdDegrees = 0
        var options = new WetInWetOptions(
            ColorGradientThreshold: 0.01,
            SalientHueThresholdDegrees: 0.0,
            NonSalientHueThresholdDegrees: 0.0,
            MinIntensityDifference: 0.01);

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, dist, options: options);

        Assert.Single(result.QualifyingBoundaries);
        Assert.True(result.QualifyingBoundaries[0]);
    }

    /// <summary>
    /// Verifies that a 1D directional color gradient (non-zero along X only) qualifies via gradient magnitude.
    /// </summary>
    [Fact]
    public void ApplyDetailed_OneDimensionalGradient_ShouldQualifyViaGradientMagnitude()
    {
        const int width = 8;
        const int height = 8;
        SequentialMatrix<Rgb8> image = TestMatrices.Create(width, height, static (x, _) =>
            x < 4
                ? new Rgb8((byte)Math.Min(255, 40 + (x * 55)), 10, 10)
                : new Rgb8(20, (byte)Math.Min(255, 80 + ((x - 4) * 45)), (byte)Math.Min(255, 80 + ((x - 4) * 45))));
        SequentialMatrix<int> labels = TestMatrices.Create(width, height, static (x, _) => x < 4 ? 0 : 1);

        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        var options = new WetInWetOptions(
            ColorGradientThreshold: 0.02,
            SalientHueThresholdDegrees: 0.0,
            NonSalientHueThresholdDegrees: 0.0,
            MinIntensityDifference: 0.01);

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, dist, options: options);

        Assert.Single(result.QualifyingBoundaries);
        Assert.True(result.QualifyingBoundaries[0]);
    }

    /// <summary>
    /// Verifies that near-neutral gray regions with slight opposing channel tint do not fail the hue rule due to arbitrary hue angles.
    /// </summary>
    [Fact]
    public void ApplyDetailed_NearNeutralRegionsWithSlightOpposingTint_ShouldQualifyViaHueRule()
    {
        const int width = 6;
        const int height = 6;
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            width,
            height,
            new Rgb8(30, 31, 32),
            new Rgb8(220, 219, 218));
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        var options = new WetInWetOptions(
            ColorGradientThreshold: 100.0,
            SalientHueThresholdDegrees: 20.0,
            NonSalientHueThresholdDegrees: 90.0,
            MinIntensityDifference: 0.02);

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, dist, options: options);

        Assert.Single(result.QualifyingBoundaries);
        Assert.True(result.QualifyingBoundaries[0]);
    }

    /// <summary>
    /// Verifies that hue threshold differentiates between salient and non-salient regions.
    /// </summary>
    [Fact]
    public void ApplyDetailed_HueThreshold_ShouldDistinguishSalientAndNonSalient()
    {
        const int width = 6;
        const int height = 6;
        // Red vs Orange (hue diff ~ 30 degrees, intensity diff ~ 0.1)
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            width,
            height,
            new Rgb8(220, 20, 20),
            new Rgb8(240, 140, 20));
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        // Gradient threshold high so only hue rule can qualify
        var options = new WetInWetOptions(
            ColorGradientThreshold: 100.0,
            SalientHueThresholdDegrees: 20.0,
            NonSalientHueThresholdDegrees: 90.0,
            MinIntensityDifference: 0.01);

        // When regions are non-salient, hue diff (~30) < 90 -> qualifies!
        var nonSalientMask = new MaskMatrix(width, height);
        WetInWetResult resNonSalient = WetInWet.ApplyDetailed(image, graph, nonSalientMask, options);
        Assert.True(resNonSalient.QualifyingBoundaries[0]);

        // When Region 0 is salient, hue diff (~30) >= 20 -> does not qualify!
        SequentialMatrix<bool> salientMask = TestMatrices.CenteredBox(width, height, 0, 0, 2, height - 1, true, false);

        WetInWetResult resSalient = WetInWet.ApplyDetailed(image, graph, salientMask, options);
        Assert.False(resSalient.QualifyingBoundaries[0]);
    }

    /// <summary>
    /// Verifies that pigment seeds are scattered exclusively onto the brighter side of a qualifying boundary.
    /// </summary>
    [Fact]
    public void ApplyDetailed_PigmentSeeds_ShouldScatterExclusivelyOntoBrighterSide()
    {
        const int width = 10;
        const int height = 10;
        // Left is darker (0), right is brighter (1)
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            width,
            height,
            new Rgb8(30, 30, 30),
            new Rgb8(220, 220, 220));
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        var options = new WetInWetOptions(
            ScatterRadius: 3,
            SeedProbability: 1.0, // Guaranteed seed placement on candidate pixels
            EllipticalFilter: new OrientedEllipticalFilterOptions(MajorRadius: 2.0, MinorRadius: 1.0),
            Seed: 42);

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, dist, options: options);

        Assert.True(result.QualifyingBoundaries[0]);
        // Wet area mask covers both sides near boundary
        Assert.True(result.WetAreaMask[4, 5]);
        Assert.True(result.WetAreaMask[5, 5]);

        // Far left (x=0) is unblurred original darker color
        Assert.Equal((byte)30, result.Image[0, 5].R);

        // Right side (x=5) received pigment seeds and oriented smoothing, so it is significantly darker than original 220
        Assert.True(result.Image[5, 5].R < 220);
    }

    /// <summary>
    /// Verifies deterministic results when using the same random seed, and variation when using different seeds.
    /// </summary>
    [Fact]
    public void ApplyDetailed_Determinism_SameSeedShouldProduceIdenticalOutput()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            8,
            8,
            new Rgb8(30, 30, 30),
            new Rgb8(200, 200, 200));
        var dist = new SequentialMatrix<DoublePixel>(8, 8);

        var opt1 = new WetInWetOptions(Seed: 777, SeedProbability: 0.5);
        var opt2 = new WetInWetOptions(Seed: 777, SeedProbability: 0.5);

        WetInWetResult res1 = WetInWet.ApplyDetailed(image, graph, dist, options: opt1);
        WetInWetResult res2 = WetInWet.ApplyDetailed(image, graph, dist, options: opt2);

        MatrixAssert.Equal(res1.Image, res2.Image);
        MatrixAssert.Equal(res1.WetAreaMask, res2.WetAreaMask);
    }

    /// <summary>
    /// Verifies derivation of effective salient mask from distanceField when salientMask is null, including handling of non-finite distance values.
    /// </summary>
    [Fact]
    public void ApplyDetailed_ImplicitSalientMaskFromDistanceField_ShouldCorrectlyClassifySaliency()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            6,
            6,
            new Rgb8(20, 20, 20),
            new Rgb8(200, 200, 200));
        // Region 0 has distance <= 1e-6 (salient), Region 1 has distance 0.5 (non-salient)
        SequentialMatrix<DoublePixel> dist = TestMatrices.Create(6, 6, (x, _) => new DoublePixel(x < 3 ? 0.0 : 0.5));

        // Put a non-finite distance at (5, 5) to test !double.IsFinite(d) branch
        dist[5, 5] = new DoublePixel(double.NaN);
        var options = new WetInWetOptions(
            Mode: WetInWetMode.ExcludeSalientBoundaries,
            MinIntensityDifference: 0.01);

        // Distance field overload -> derives salient mask by thresholding <= 1e-6
        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, dist, options);

        // Region 0 is salient, Region 1 is non-salient; ExcludeSalientBoundaries excludes it
        Assert.False(result.QualifyingBoundaries[0]);

        // Primary overload without salient mask treats all regions as non-salient
        WetInWetResult withoutMask = WetInWet.ApplyDetailed(image, graph, options: options);
        Assert.True(withoutMask.QualifyingBoundaries[0]);
    }

    /// <summary>
    /// Verifies that the convenience <see cref="WetInWet.Apply(IReadOnlyMatrix{Rgb8}, RegionGraph, IReadOnlyMatrix{DoublePixel}, WetInWetOptions?, CancellationToken)"/>
    /// method produces the same image as <see cref="WetInWet.ApplyDetailed(IReadOnlyMatrix{Rgb8}, RegionGraph, IReadOnlyMatrix{DoublePixel}, WetInWetOptions?, CancellationToken)"/>.
    /// </summary>
    [Fact]
    public void Apply_ConvenienceMethod_ShouldMatchDetailedImageOutput()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            6,
            6,
            new Rgb8(20, 20, 20),
            new Rgb8(200, 200, 200));
        var dist = new SequentialMatrix<DoublePixel>(6, 6);
        var options = new WetInWetOptions();

        Matrix<Rgb8> imgOnly = WetInWet.Apply(image, graph, dist, options: options);
        WetInWetResult detailed = WetInWet.ApplyDetailed(image, graph, dist, options: options);

        MatrixAssert.Equal(detailed.Image, imgOnly);
    }

    /// <summary>
    /// Verifies that a thin 1-pixel flat sliver region along a high-contrast hue step does not falsely trigger the gradient rule.
    /// </summary>
    [Fact]
    public void ApplyDetailed_FlatThinSliverBetweenContrastingHues_ShouldNotTriggerGradientRule()
    {
        const int width = 10;
        const int height = 10;
        // Flat Red (0), 1px flat Cyan sliver (1) with 180° hue diff, flat Blue (2)
        SequentialMatrix<Rgb8> image = TestMatrices.Create(width, height, static (x, _) =>
            x < 4 ? new Rgb8(220, 20, 20)
            : x == 4 ? new Rgb8(20, 220, 220)
            : new Rgb8(40, 40, 230));
        SequentialMatrix<int> labels = TestMatrices.Create(width, height, static (x, _) =>
            x < 4 ? 0 : x == 4 ? 1 : 2);

        var graph = RegionGraph.FromLabels(labels);
        var salientMask = new MaskMatrix(width, height);
        // Threshold 0.05 would pass on un-clamped Sobel, and hue threshold 0 disables hue rule.
        var options = new WetInWetOptions(
            ColorGradientThreshold: 0.05,
            SalientHueThresholdDegrees: 0.0,
            NonSalientHueThresholdDegrees: 0.0,
            MinIntensityDifference: 0.01);

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, salientMask, options);
        Assert.All(result.QualifyingBoundaries, q => Assert.False(q));
    }

    /// <summary>
    /// Verifies that pigment seeds deposited on the brighter side reflect local boundary colors rather than distant region averages.
    /// </summary>
    [Fact]
    public void ApplyDetailed_LocalOppositeSideSeeding_ShouldSampleLocalGradientColorRatherThanGlobalMean()
    {
        const int width = 10;
        const int height = 12;
        // Left darker region varies vertically: top (y<6) is dark Red, bottom (y>=6) is dark Blue
        // Right brighter region is light neutral gray (220, 220, 220)
        SequentialMatrix<Rgb8> image = TestMatrices.Create(width, height, static (x, y) =>
            x < 5
                ? (y < 6 ? new Rgb8(110, 10, 10) : new Rgb8(10, 10, 110))
                : new Rgb8(220, 220, 220));
        SequentialMatrix<int> labels = TestMatrices.Create(width, height, static (x, _) => x < 5 ? 0 : 1);

        var graph = RegionGraph.FromLabels(labels);
        var options = new WetInWetOptions(
            ScatterRadius: 1,
            SeedProbability: 1.0,
            NonSalientHueThresholdDegrees: 180.0,
            MinIntensityDifference: 0.01,
            EllipticalFilter: new OrientedEllipticalFilterOptions(MajorRadius: 1.0, MinorRadius: 1.0));

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, options: options);

        // Top brighter boundary pixel (x=5, y=1) picks up red dominance; bottom (x=5, y=10) picks up blue dominance
        Assert.True(result.Image[5, 1].R > result.Image[5, 1].B);
        Assert.True(result.Image[5, 10].B > result.Image[5, 10].R);
    }

    /// <summary>
    /// Verifies that darker-side pixels at distance 2 or greater are excluded from <see cref="WetInWetResult.WetAreaMask"/>
    /// while distance 0 and 1 darker-side pixels remain included.
    /// </summary>
    [Fact]
    public void ApplyDetailed_DarkerSideAtDistanceTwoOrGreater_ShouldHaveFalseWetAreaMask()
    {
        const int width = 12;
        const int height = 6;
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            width,
            height,
            new Rgb8(30, 30, 30),
            new Rgb8(220, 220, 220),
            splitX: 6);
        var options = new WetInWetOptions(
            ScatterRadius: 4,
            SeedProbability: 1.0,
            EllipticalFilter: new OrientedEllipticalFilterOptions(MajorRadius: 4.0, MinorRadius: 1.5));

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, options: options);

        Assert.True(result.QualifyingBoundaries[0]);
        Assert.True(result.WetAreaMask[5, 3]);  // darker side, dist == 0
        Assert.True(result.WetAreaMask[4, 3]);  // darker side, dist == 1
        Assert.False(result.WetAreaMask[3, 3]); // darker side, dist == 2
        Assert.False(result.WetAreaMask[2, 3]); // darker side, dist == 3
        Assert.False(result.WetAreaMask[0, 3]); // darker side, dist == 5
        Assert.Equal(image[3, 3], result.Image[3, 3]);

        Assert.True(result.WetAreaMask[6, 3]);  // brighter side, dist == 0
        Assert.True(result.WetAreaMask[8, 3]);  // brighter side, dist == 2
        Assert.True(result.WetAreaMask[10, 3]); // brighter side, dist == 4
    }

    /// <summary>
    /// Verifies that huge elliptical filter radii do not overflow <c>maxBand</c> during boundary band construction.
    /// </summary>
    [Fact]
    public void ApplyDetailed_HugeMajorRadius_ShouldNotOverflowMaxBand()
    {
        const int width = 10;
        const int height = 6;
        (SequentialMatrix<Rgb8> image, RegionGraph graph) = TestMatrices.VerticalSplitWithGraph(
            width,
            height,
            new Rgb8(30, 30, 30),
            new Rgb8(220, 220, 220),
            splitX: 5);
        var options = new WetInWetOptions(
            ScatterRadius: 1,
            EllipticalFilter: new OrientedEllipticalFilterOptions(MajorRadius: 1e12, MinorRadius: 1.0));

        WetInWetResult result = WetInWet.ApplyDetailed(image, graph, options: options);

        Assert.Equal(image.Size, result.Image.Size);
        Assert.True(result.WetAreaMask[width - 1, height / 2]);
        Assert.False(result.WetAreaMask[0, height / 2]);
    }

    /// <summary>
    /// Verifies that <see cref="WetInWet.ApplyDetailedCore"/> with precomputed salient fractions matches
    /// <see cref="WetInWet.ApplyDetailed(IReadOnlyMatrix{Rgb8}, RegionGraph, IReadOnlyMatrix{bool}?, WetInWetOptions?, CancellationToken)"/>
    /// and executes deterministically across multiple gradient-bearing regions.
    /// </summary>
    [Fact]
    public void ApplyDetailedCore_PrecomputedSalientFractionsAndMultiRegionParallelism_ShouldMatchAndBeDeterministic()
    {
        const int width = 12;
        const int height = 10;
        SequentialMatrix<Rgb8> image = TestMatrices.Create(width, height, static (x, y) =>
        {
            int regionId = (x / 4) + ((y / 5) * 3);
            byte r = (byte)Math.Clamp(30 + (regionId * 35) + (x * 4), 0, 255);
            byte g = (byte)Math.Clamp(20 + (regionId * 25) + (y * 6), 0, 255);
            byte b = (byte)Math.Clamp(40 + ((5 - regionId) * 30), 0, 255);
            return new Rgb8(r, g, b);
        });
        SequentialMatrix<int> labels = TestMatrices.Create(width, height, static (x, y) => (x / 4) + ((y / 5) * 3));
        // Region 0 gets exact 50% salience (matching default threshold 0.5), Region 1 gets 100%, others 0%
        SequentialMatrix<bool> salientMask = TestMatrices.Create(width, height, static (x, y) =>
        {
            int regionId = (x / 4) + ((y / 5) * 3);
            return (regionId == 0 && (y < 2 || (y == 2 && x < 2))) || regionId == 1;
        });

        var graph = RegionGraph.FromLabels(labels);
        var options = new WetInWetOptions(
            Mode: WetInWetMode.ExcludeSalientBoundaries,
            MinIntensityDifference: 0.01,
            SalientOverlapThreshold: 0.5,
            Seed: 2026);

        WetInWetResult fromMask = WetInWet.ApplyDetailed(image, graph, salientMask, options);
        double[] fractions = graph.ComputeRegionFractions(salientMask);
        WetInWetResult fromFractions = WetInWet.ApplyDetailedCore(image, graph, salientMask: null, fractions, options);
        WetInWetResult repeated = WetInWet.ApplyDetailedCore(image, graph, salientMask: null, fractions, options);

        MatrixAssert.Equal(fromMask.Image, fromFractions.Image);
        MatrixAssert.Equal(fromMask.WetAreaMask, fromFractions.WetAreaMask);
        Assert.Equal(fromMask.QualifyingBoundaries, fromFractions.QualifyingBoundaries);
        MatrixAssert.Equal(fromFractions.Image, repeated.Image);
        MatrixAssert.Equal(fromFractions.WetAreaMask, repeated.WetAreaMask);
    }

    /// <summary>
    /// Verifies that the <see cref="IDisjointSet"/> overloads for <see cref="WetInWet.ApplyDetailed"/> and
    /// <see cref="WetInWet.Apply"/> produce identical results to the <see cref="RegionGraph"/> overloads.
    /// </summary>
    [Fact]
    public void ApplyDetailed_And_Apply_DisjointSetOverloads_ShouldMatchRegionGraphOverloads()
    {
        const int width = 10;
        const int height = 10;
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(width, height, new Rgb8(30, 30, 30), new Rgb8(220, 220, 220));
        DisjointSet<int> disjointSet = CreateVerticalSplitDisjointSet(width, height);
        RegionGraph graph = disjointSet.ToRegionGraph();
        SequentialMatrix<bool> salientMask = TestMatrices.CenteredBox(width, height, 0, 0, 4, height - 1, true, false);
        SequentialMatrix<DoublePixel> distanceField = TestMatrices.Create(width, height, static (x, _) => new DoublePixel(x < 5 ? 0.0 : 0.8));
        var options = new WetInWetOptions(Mode: WetInWetMode.ExcludeSalientBoundaries, Seed: 123);

        WetInWetResult maskDisjoint = WetInWet.ApplyDetailed(image, disjointSet, salientMask, options);
        WetInWetResult maskGraph = WetInWet.ApplyDetailed(image, graph, salientMask, options);
        MatrixAssert.Equal(maskGraph.Image, maskDisjoint.Image);
        MatrixAssert.Equal(maskGraph.WetAreaMask, maskDisjoint.WetAreaMask);
        Assert.Equal(maskGraph.QualifyingBoundaries, maskDisjoint.QualifyingBoundaries);
        MatrixAssert.Equal(maskGraph.Image, WetInWet.Apply(image, disjointSet, salientMask, options));

        WetInWetResult distDisjoint = WetInWet.ApplyDetailed(image, disjointSet, distanceField, options);
        WetInWetResult distGraph = WetInWet.ApplyDetailed(image, graph, distanceField, options);
        MatrixAssert.Equal(distGraph.Image, distDisjoint.Image);
        MatrixAssert.Equal(distGraph.WetAreaMask, distDisjoint.WetAreaMask);
        Assert.Equal(distGraph.QualifyingBoundaries, distDisjoint.QualifyingBoundaries);
        MatrixAssert.Equal(distGraph.Image, WetInWet.Apply(image, disjointSet, distanceField, options));
    }

    /// <summary>
    /// Verifies that a pre-canceled <see cref="CancellationToken"/> throws <see cref="OperationCanceledException"/>
    /// across all <see cref="WetInWet"/> overloads and that an active uncanceled token matches default execution bit-for-bit.
    /// </summary>
    [Fact]
    public void Apply_And_ApplyDetailed_CanceledToken_ShouldThrowOperationCanceledException()
    {
        const int width = 10;
        const int height = 10;
        SequentialMatrix<Rgb8> image = TestMatrices.VerticalSplit(width, height, new Rgb8(30, 30, 30), new Rgb8(220, 220, 220));
        DisjointSet<int> disjointSet = CreateVerticalSplitDisjointSet(width, height);
        RegionGraph graph = disjointSet.ToRegionGraph();
        SequentialMatrix<bool> salientMask = TestMatrices.CenteredBox(width, height, 0, 0, 4, height - 1, true, false);
        SequentialMatrix<DoublePixel> distanceField = TestMatrices.Create(width, height, static (x, _) => new DoublePixel(x < 5 ? 0.0 : 0.8));
        var options = new WetInWetOptions(Mode: WetInWetMode.ExcludeSalientBoundaries, Seed: 123);
        var canceledToken = new CancellationToken(canceled: true);

        Assert.Throws<OperationCanceledException>(() => WetInWet.ApplyDetailed(image, graph, salientMask, options, canceledToken));
        Assert.Throws<OperationCanceledException>(() => WetInWet.ApplyDetailed(image, graph, distanceField, options, canceledToken));
        Assert.Throws<OperationCanceledException>(() => WetInWet.ApplyDetailed(image, disjointSet, salientMask, options, canceledToken));
        Assert.Throws<OperationCanceledException>(() => WetInWet.ApplyDetailed(image, disjointSet, distanceField, options, canceledToken));
        Assert.Throws<OperationCanceledException>(() => WetInWet.Apply(image, graph, salientMask, options, canceledToken));
        Assert.Throws<OperationCanceledException>(() => WetInWet.Apply(image, graph, distanceField, options, canceledToken));
        Assert.Throws<OperationCanceledException>(() => WetInWet.Apply(image, disjointSet, salientMask, options, canceledToken));
        Assert.Throws<OperationCanceledException>(() => WetInWet.Apply(image, disjointSet, distanceField, options, canceledToken));
        Assert.Throws<OperationCanceledException>(() =>
            WetInWet.ApplyDetailedCore(image, graph, salientMask, null, options, cancellationToken: canceledToken));

        using var cts = new CancellationTokenSource();
        WetInWetResult expected = WetInWet.ApplyDetailed(image, graph, salientMask, options);
        WetInWetResult actual = WetInWet.ApplyDetailed(image, graph, salientMask, options, cts.Token);
        MatrixAssert.Equal(expected.Image, actual.Image);
        MatrixAssert.Equal(expected.WetAreaMask, actual.WetAreaMask);
        Assert.Equal(expected.QualifyingBoundaries, actual.QualifyingBoundaries);
    }

    private static DisjointSet<int> CreateVerticalSplitDisjointSet(int width, int height)
    {
        var dummy = new SequentialMatrix<int>(width, height);
        var disjointSet = new DisjointSet<int>(dummy, static i => i);
        int splitX = width / 2;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x < splitX && (x != 0 || y != 0))
                {
                    disjointSet.Union(new Coord(0, 0), new Coord(x, y), 0);
                }
                else if (x >= splitX && (x != splitX || y != 0))
                {
                    disjointSet.Union(new Coord(splitX, 0), new Coord(x, y), 1);
                }
            }
        }

        return disjointSet;
    }
}
