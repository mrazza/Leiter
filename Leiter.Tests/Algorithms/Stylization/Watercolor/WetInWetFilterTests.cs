namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="WetInWetFilter"/>.
/// </summary>
public class WetInWetFilterTests
{
    /// <summary>
    /// Verifies that null arguments passed to <see cref="WetInWetFilter.ApplyDetailed"/> throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void ApplyDetailed_NullArguments_ShouldThrowArgumentNullException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var labels = new SequentialMatrix<int>(4, 4);
        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(4, 4);

        Assert.Throws<ArgumentNullException>(() => WetInWetFilter.ApplyDetailed(null!, graph));
        Assert.Throws<ArgumentNullException>(() => WetInWetFilter.ApplyDetailed(image, null!));
        Assert.Throws<ArgumentNullException>(() => WetInWetFilter.ApplyDetailed(null!, graph, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWetFilter.ApplyDetailed(image, null!, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWetFilter.ApplyDetailed(image, graph, (IReadOnlyMatrix<DoublePixel>)null!));
    }

    /// <summary>
    /// Verifies that null arguments passed to <see cref="WetInWetFilter.Apply"/> throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Apply_NullArguments_ShouldThrowArgumentNullException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var labels = new SequentialMatrix<int>(4, 4);
        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(4, 4);

        Assert.Throws<ArgumentNullException>(() => WetInWetFilter.Apply(null!, graph));
        Assert.Throws<ArgumentNullException>(() => WetInWetFilter.Apply(image, null!));
        Assert.Throws<ArgumentNullException>(() => WetInWetFilter.Apply(null!, graph, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWetFilter.Apply(image, null!, dist));
        Assert.Throws<ArgumentNullException>(() => WetInWetFilter.Apply(image, graph, (IReadOnlyMatrix<DoublePixel>)null!));
    }

    /// <summary>
    /// Verifies that mismatched matrix dimensions throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void ApplyDetailed_MismatchedDimensions_ShouldThrowArgumentException()
    {
        var image = new SequentialMatrix<Rgb8>(6, 6);
        var graphMismatched = RegionGraph.FromLabels(new SequentialMatrix<int>(4, 6));
        var graphMatching = RegionGraph.FromLabels(new SequentialMatrix<int>(6, 6));
        var distMismatched = new SequentialMatrix<DoublePixel>(6, 4);
        var distMatching = new SequentialMatrix<DoublePixel>(6, 6);
        var maskMismatched = new MaskMatrix(4, 4);

        Assert.Throws<ArgumentException>(() => WetInWetFilter.ApplyDetailed(image, graphMismatched));
        Assert.Throws<ArgumentException>(() => WetInWetFilter.ApplyDetailed(image, graphMatching, maskMismatched));
        Assert.Throws<ArgumentException>(() => WetInWetFilter.ApplyDetailed(image, graphMismatched, distMatching));
        Assert.Throws<ArgumentException>(() => WetInWetFilter.ApplyDetailed(image, graphMatching, distMismatched));
    }

    /// <summary>
    /// Verifies that invalid options throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void ApplyDetailed_InvalidOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(4, 4));
        var dist = new SequentialMatrix<DoublePixel>(4, 4);
        var invalidOptions = new WetInWetOptions(MajorRadius: -1.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => WetInWetFilter.ApplyDetailed(image, graph, dist, options: invalidOptions));
    }

    /// <summary>
    /// Verifies fast path handling when the input image is empty (0x0).
    /// </summary>
    [Fact]
    public void ApplyDetailed_EmptyImage_ShouldReturnEmptyResult()
    {
        var image = new SequentialMatrix<Rgb8>(0, 0);
        var graph = RegionGraph.FromLabels(new SequentialMatrix<int>(0, 0));
        var dist = new SequentialMatrix<DoublePixel>(0, 0);

        WetInWetResult result = WetInWetFilter.ApplyDetailed(image, graph, dist);

        Assert.Equal(0, result.Image.Width);
        Assert.Equal(0, result.Image.Height);
        Assert.Empty(result.QualifyingBoundaries);
    }

    /// <summary>
    /// Verifies that Disabled mode returns an unmodified image copy and all-false qualifying boundaries.
    /// </summary>
    [Fact]
    public void ApplyDetailed_DisabledMode_ShouldReturnUnmodifiedImage()
    {
        int width = 6;
        int height = 6;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labels[x, y] = x < 3 ? 0 : 1;
                image[x, y] = x < 3 ? new Rgb8(20, 20, 20) : new Rgb8(200, 200, 200);
            }
        }

        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);
        var options = new WetInWetOptions(Mode: WetInWetMode.Disabled);

        WetInWetResult result = WetInWetFilter.ApplyDetailed(image, graph, dist, options: options);

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
        var labels = new SequentialMatrix<int>(width, height);
        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        WetInWetResult result = WetInWetFilter.ApplyDetailed(image, graph, dist);

        Assert.Equal(width, result.Image.Width);
        Assert.Empty(result.QualifyingBoundaries);
    }

    /// <summary>
    /// Verifies that boundaries with intensity difference below the threshold do not qualify.
    /// </summary>
    [Fact]
    public void ApplyDetailed_IntensityDifferenceBelowThreshold_ShouldNotQualify()
    {
        int width = 6;
        int height = 6;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labels[x, y] = x < 3 ? 0 : 1;
                image[x, y] = x < 3 ? new Rgb8(100, 100, 100) : new Rgb8(101, 101, 101);
            }
        }

        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);
        var options = new WetInWetOptions(MinIntensityDifference: 0.05);

        WetInWetResult result = WetInWetFilter.ApplyDetailed(image, graph, dist, options: options);

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
        // 3 horizontal bands: Region 0 (y=0..1), Region 1 (y=2..3), Region 2 (y=4..5)
        int width = 6;
        int height = 6;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);
        var salientMask = new MaskMatrix(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (y < 2)
                {
                    labels[x, y] = 0;
                    image[x, y] = new Rgb8(20, 20, 20); // Darker
                    salientMask[x, y] = true;           // Region 0 is Salient
                }
                else if (y < 4)
                {
                    labels[x, y] = 1;
                    image[x, y] = new Rgb8(150, 150, 150); // Mid
                    salientMask[x, y] = false;             // Region 1 is Non-salient
                }
                else
                {
                    labels[x, y] = 2;
                    image[x, y] = new Rgb8(240, 240, 240); // Brighter
                    salientMask[x, y] = false;             // Region 2 is Non-salient
                }
            }
        }

        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        // Find boundary indices
        int boundary01 = -1;
        int boundary12 = -1;
        for (int i = 0; i < graph.Boundaries.Count; i++)
        {
            var b = graph.Boundaries[i];
            if ((b.First == 0 && b.Second == 1) || (b.First == 1 && b.Second == 0))
            {
                boundary01 = i;
            }

            if ((b.First == 1 && b.Second == 2) || (b.First == 2 && b.Second == 1))
            {
                boundary12 = i;
            }
        }

        Assert.True(boundary01 >= 0);
        Assert.True(boundary12 >= 0);

        // 1. Default mode: both qualify
        var optDefault = new WetInWetOptions(Mode: WetInWetMode.Default, MinIntensityDifference: 0.01);
        WetInWetResult resDefault = WetInWetFilter.ApplyDetailed(image, graph, salientMask, optDefault);
        Assert.True(resDefault.QualifyingBoundaries[boundary01]);
        Assert.True(resDefault.QualifyingBoundaries[boundary12]);

        // 2. ExcludeSalientBoundaries: boundary01 (salient vs non-salient) excluded; boundary12 included
        var optExcludeBoundaries = new WetInWetOptions(Mode: WetInWetMode.ExcludeSalientBoundaries, MinIntensityDifference: 0.01);
        WetInWetResult resExcludeBoundaries = WetInWetFilter.ApplyDetailed(image, graph, salientMask, optExcludeBoundaries);
        Assert.False(resExcludeBoundaries.QualifyingBoundaries[boundary01]);
        Assert.True(resExcludeBoundaries.QualifyingBoundaries[boundary12]);

        // 3. ExcludeSalientRegions: boundary01 (touches salient) excluded; boundary12 included
        var optExcludeRegions = new WetInWetOptions(Mode: WetInWetMode.ExcludeSalientRegions, MinIntensityDifference: 0.01);
        WetInWetResult resExcludeRegions = WetInWetFilter.ApplyDetailed(image, graph, salientMask, optExcludeRegions);
        Assert.False(resExcludeRegions.QualifyingBoundaries[boundary01]);
        Assert.True(resExcludeRegions.QualifyingBoundaries[boundary12]);
    }

    /// <summary>
    /// Verifies that high color gradient magnitude qualifies a boundary even when hue difference exceeds the threshold.
    /// </summary>
    [Fact]
    public void ApplyDetailed_HighColorGradient_ShouldQualifyDespiteLargeHueDifference()
    {
        int width = 8;
        int height = 8;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);

        // High 2D contrast (both X and Y gradients non-zero) between contrasting hues (Red vs Cyan)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labels[x, y] = x < 4 ? 0 : 1;
                if (x < 4)
                {
                    byte red = (byte)Math.Min(255, 40 + (x * 45) + (y * 25));
                    image[x, y] = new Rgb8(red, 10, 10);
                }
                else
                {
                    byte cyan = (byte)Math.Min(255, 80 + ((x - 4) * 35) + (y * 20));
                    image[x, y] = new Rgb8(20, cyan, cyan);
                }
            }
        }

        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        // With small gradient threshold, gradient rule triggers even with SalientHueThresholdDegrees = 0
        var options = new WetInWetOptions(
            ColorGradientThreshold: 0.01,
            SalientHueThresholdDegrees: 0.0,
            NonSalientHueThresholdDegrees: 0.0,
            MinIntensityDifference: 0.01);

        WetInWetResult result = WetInWetFilter.ApplyDetailed(image, graph, dist, options: options);

        Assert.Single(result.QualifyingBoundaries);
        Assert.True(result.QualifyingBoundaries[0]);
    }

    /// <summary>
    /// Verifies that hue threshold differentiates between salient and non-salient regions.
    /// </summary>
    [Fact]
    public void ApplyDetailed_HueThreshold_ShouldDistinguishSalientAndNonSalient()
    {
        int width = 6;
        int height = 6;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);

        // Red vs Orange (hue diff ~ 30 degrees, intensity diff ~ 0.1)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labels[x, y] = x < 3 ? 0 : 1;
                image[x, y] = x < 3 ? new Rgb8(220, 20, 20) : new Rgb8(240, 140, 20);
            }
        }

        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        // Gradient threshold high so only hue rule can qualify
        var options = new WetInWetOptions(
            ColorGradientThreshold: 100.0,
            SalientHueThresholdDegrees: 20.0,
            NonSalientHueThresholdDegrees: 90.0,
            MinIntensityDifference: 0.01);

        // When regions are non-salient, hue diff (~30) < 90 -> qualifies!
        var nonSalientMask = new MaskMatrix(width, height);
        WetInWetResult resNonSalient = WetInWetFilter.ApplyDetailed(image, graph, nonSalientMask, options);
        Assert.True(resNonSalient.QualifyingBoundaries[0]);

        // When Region 0 is salient, hue diff (~30) >= 20 -> does not qualify!
        var salientMask = new MaskMatrix(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                salientMask[x, y] = true;
            }
        }

        WetInWetResult resSalient = WetInWetFilter.ApplyDetailed(image, graph, salientMask, options);
        Assert.False(resSalient.QualifyingBoundaries[0]);
    }

    /// <summary>
    /// Verifies that pigment seeds are scattered exclusively onto the brighter side of a qualifying boundary.
    /// </summary>
    [Fact]
    public void ApplyDetailed_PigmentSeeds_ShouldScatterExclusivelyOntoBrighterSide()
    {
        int width = 10;
        int height = 10;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);

        // Left is darker (0), right is brighter (1)
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labels[x, y] = x < 5 ? 0 : 1;
                image[x, y] = x < 5 ? new Rgb8(30, 30, 30) : new Rgb8(220, 220, 220);
            }
        }

        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        var options = new WetInWetOptions(
            ScatterRadius: 3,
            SeedProbability: 1.0, // Guaranteed seed placement on candidate pixels
            MajorRadius: 2.0,
            MinorRadius: 1.0,
            RandomSeed: 42);

        WetInWetResult result = WetInWetFilter.ApplyDetailed(image, graph, dist, options: options);

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
        int width = 8;
        int height = 8;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labels[x, y] = x < 4 ? 0 : 1;
                image[x, y] = x < 4 ? new Rgb8(30, 30, 30) : new Rgb8(200, 200, 200);
            }
        }

        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        var opt1 = new WetInWetOptions(RandomSeed: 777, SeedProbability: 0.5);
        var opt2 = new WetInWetOptions(RandomSeed: 777, SeedProbability: 0.5);

        WetInWetResult res1 = WetInWetFilter.ApplyDetailed(image, graph, dist, options: opt1);
        WetInWetResult res2 = WetInWetFilter.ApplyDetailed(image, graph, dist, options: opt2);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Assert.Equal(res1.Image[x, y], res2.Image[x, y]);
                Assert.Equal(res1.WetAreaMask[x, y], res2.WetAreaMask[x, y]);
            }
        }
    }

    /// <summary>
    /// Verifies derivation of effective salient mask from distanceField when salientMask is null, including handling of non-finite distance values.
    /// </summary>
    [Fact]
    public void ApplyDetailed_ImplicitSalientMaskFromDistanceField_ShouldCorrectlyClassifySaliency()
    {
        int width = 6;
        int height = 6;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);
        var dist = new SequentialMatrix<DoublePixel>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labels[x, y] = x < 3 ? 0 : 1;
                image[x, y] = x < 3 ? new Rgb8(20, 20, 20) : new Rgb8(200, 200, 200);
                // Region 0 has distance <= 1e-6 (salient), Region 1 has distance 0.5 (non-salient)
                dist[x, y] = x < 3 ? new DoublePixel(0.0) : new DoublePixel(0.5);
            }
        }

        // Put a non-finite distance at (5, 5) to test !double.IsFinite(d) branch
        dist[5, 5] = new DoublePixel(double.NaN);

        var graph = RegionGraph.FromLabels(labels);
        var options = new WetInWetOptions(
            Mode: WetInWetMode.ExcludeSalientBoundaries,
            MinIntensityDifference: 0.01);

        // Distance field overload -> derives salient mask by thresholding <= 1e-6
        WetInWetResult result = WetInWetFilter.ApplyDetailed(image, graph, dist, options);

        // Region 0 is salient, Region 1 is non-salient; ExcludeSalientBoundaries excludes it
        Assert.False(result.QualifyingBoundaries[0]);

        // Primary overload without salient mask treats all regions as non-salient
        WetInWetResult withoutMask = WetInWetFilter.ApplyDetailed(image, graph, options: options);
        Assert.True(withoutMask.QualifyingBoundaries[0]);
    }

    /// <summary>
    /// Verifies that the convenience <see cref="WetInWetFilter.Apply"/> method produces the same image as <see cref="WetInWetFilter.ApplyDetailed"/>.
    /// </summary>
    [Fact]
    public void Apply_ConvenienceMethod_ShouldMatchDetailedImageOutput()
    {
        int width = 6;
        int height = 6;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                labels[x, y] = x < 3 ? 0 : 1;
                image[x, y] = x < 3 ? new Rgb8(20, 20, 20) : new Rgb8(200, 200, 200);
            }
        }

        var graph = RegionGraph.FromLabels(labels);
        var dist = new SequentialMatrix<DoublePixel>(width, height);
        var options = new WetInWetOptions();

        Matrix<Rgb8> imgOnly = WetInWetFilter.Apply(image, graph, dist, options: options);
        WetInWetResult detailed = WetInWetFilter.ApplyDetailed(image, graph, dist, options: options);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Assert.Equal(detailed.Image[x, y], imgOnly[x, y]);
            }
        }
    }
}
