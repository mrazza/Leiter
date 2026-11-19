namespace Leiter.Tests.Algorithms.Stylization;

using System;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Stylization;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="LodAbstraction"/>.
/// </summary>
public class LodAbstractionTests
{
    /// <summary>
    /// Verifies that salient segments smooth internal noise while strictly preserving crisp boundaries against adjacent similarly-colored segments.
    /// </summary>
    [Fact]
    public void Apply_SalientSegments_ShouldSmoothInternalNoiseWhilePreservingCrispBoundaries()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph regionGraph) = TestMatrices.VerticalSplitWithGraph(
            8,
            8,
            new Rgb8(100, 100, 100),
            new Rgb8(102, 102, 102));
        var salientMask = new MaskMatrix(8, 8);
        salientMask.SetAll(true);
        var distanceField = new SequentialMatrix<DoublePixel>(8, 8);

        // Add internal noise inside Region 0 at (0, 0) (outside the 5x5 window of the (3, 3) boundary pixel)
        image[0, 0] = new Rgb8(150, 150, 150);

        Matrix<Rgb8> result = LodAbstraction.Apply(image, regionGraph, distanceField, salientMask);

        // Internal noise smoothed towards surrounding color
        Assert.True(result[0, 0].R < 150);
        Assert.True(result[0, 0].R > 100);

        // Boundary between Region 0 (x=3) and Region 1 (x=4) must remain strictly crisp
        Assert.Equal((byte)100, result[3, 3].R);
        Assert.Equal((byte)102, result[4, 3].R);
    }

    /// <summary>
    /// Verifies that non-salient segments smooth across adjacent neighbors when color distance is below the distance-scaled threshold.
    /// </summary>
    [Fact]
    public void Apply_NonSalientSegments_ShouldSmoothAcrossNeighborsWhenColorDifferenceBelowThreshold()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph regionGraph) = TestMatrices.VerticalSplitWithGraph(
            8,
            8,
            new Rgb8(100, 100, 100),
            new Rgb8(110, 110, 110));
        var salientMask = new MaskMatrix(8, 8);
        SequentialMatrix<DoublePixel> distanceField = TestMatrices.Constant(8, 8, new DoublePixel(0.6));

        Matrix<Rgb8> result = LodAbstraction.Apply(image, regionGraph, distanceField, salientMask);

        // Boundary pixel in Region 0 at (3, 3) should blend with neighboring Region 1 pixels
        Assert.True(result[3, 3].R > 100);
        Assert.True(result[3, 3].R < 110);
    }

    /// <summary>
    /// Verifies that non-salient segments do not smooth across neighbors when color distance is at or above the threshold.
    /// </summary>
    [Fact]
    public void Apply_NonSalientSegments_ShouldNotBleedAcrossNeighborsWhenColorDifferenceAtOrAboveThreshold()
    {
        (SequentialMatrix<Rgb8> image, RegionGraph regionGraph) = TestMatrices.VerticalSplitWithGraph(
            8,
            8,
            new Rgb8(100, 100, 100),
            new Rgb8(180, 180, 180));
        var salientMask = new MaskMatrix(8, 8);
        SequentialMatrix<DoublePixel> distanceField = TestMatrices.Constant(8, 8, new DoublePixel(0.6));

        Matrix<Rgb8> result = LodAbstraction.Apply(image, regionGraph, distanceField, salientMask);

        // Region 0 boundary pixel at (3, 3) must NOT blend with Region 1
        Assert.Equal((byte)100, result[3, 3].R);
        Assert.Equal((byte)180, result[4, 3].R);
    }

    /// <summary>
    /// Verifies that non-salient segments do not bleed across non-adjacent segments even if their color difference is small.
    /// </summary>
    [Fact]
    public void Apply_NonSalientSegments_ShouldNotBleedAcrossNonAdjacentSegments()
    {
        int width = 11;
        int height = 3;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);
        var salientMask = new MaskMatrix(width, height);
        var distanceField = new SequentialMatrix<DoublePixel>(width, height);

        salientMask.SetAll(false);
        distanceField.SetAll(1.0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x < 3)
                {
                    labels[x, y] = 0;
                    image[x, y] = new Rgb8(100, 100, 100);
                }
                else if (x < 6)
                {
                    labels[x, y] = 1;
                    image[x, y] = new Rgb8(200, 200, 200); // Barrier segment
                }
                else if (x < 9)
                {
                    labels[x, y] = 2;
                    image[x, y] = new Rgb8(105, 105, 105); // Non-adjacent to Region 0, similar color
                }
                else
                {
                    labels[x, y] = 3;
                    image[x, y] = new Rgb8(200, 200, 200);
                }
            }
        }

        var regionGraph = RegionGraph.FromLabels(labels);
        Assert.False(regionGraph.AreNeighbors(0, 2));

        Matrix<Rgb8> result = LodAbstraction.Apply(image, regionGraph, distanceField, salientMask);

        // Pixel at (2, 1) in Region 0 must only average with Region 0 pixels
        Assert.Equal((byte)100, result[2, 1].R);
    }

    /// <summary>
    /// Verifies kernel size growth from near-salient (d = 0 -> size 4) to image border (d = 1 -> size 9).
    /// </summary>
    [Fact]
    public void Apply_KernelGrowth_NearSalientToBorder_ShouldMatchExpectedRadius()
    {
        int width = 15;
        int height = 15;
        var labels = new SequentialMatrix<int>(width, height);
        var salientMask = new MaskMatrix(width, height);
        salientMask.SetAll(false);

        var regionGraph = RegionGraph.FromLabels(labels);

        // Test near-salient (d = 0.0 -> kernel size 4, radius 2)
        var imageNear = new SequentialMatrix<Rgb8>(width, height);
        imageNear[7, 7] = new Rgb8(255, 255, 255);
        var distanceNear = new SequentialMatrix<DoublePixel>(width, height);
        distanceNear.SetAll(0.0);

        Matrix<Rgb8> resultNear = LodAbstraction.Apply(imageNear, regionGraph, distanceNear, salientMask);
        Assert.True(resultNear[9, 7].R > 0);       // distance 2 receives weight
        Assert.Equal((byte)0, resultNear[10, 7].R); // distance 3 is outside kernel

        // Test border (d = 1.0 -> kernel size 9, radius 4)
        var imageFar = new SequentialMatrix<Rgb8>(width, height);
        imageFar[7, 7] = new Rgb8(255, 255, 255);
        var distanceFar = new SequentialMatrix<DoublePixel>(width, height);
        distanceFar.SetAll(1.0);

        Matrix<Rgb8> resultFar = LodAbstraction.Apply(imageFar, regionGraph, distanceFar, salientMask);
        Assert.True(resultFar[10, 7].R > 0);      // distance 3 receives weight
        Assert.True(resultFar[11, 7].R > 0);      // distance 4 receives weight
        Assert.Equal((byte)0, resultFar[12, 7].R); // distance 5 is outside kernel
    }

    /// <summary>
    /// Verifies that passing a null salient mask falls back to using distanceField &lt;= 1e-6 to identify salient regions.
    /// </summary>
    [Fact]
    public void Apply_SalientMaskNull_ShouldFallbackToDistanceFieldThreshold()
    {
        int width = 8;
        int height = 8;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var labels = new SequentialMatrix<int>(width, height);
        var distanceField = new SequentialMatrix<DoublePixel>(width, height);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x < 4)
                {
                    labels[x, y] = 0;
                    distanceField[x, y] = 0.0; // Salient fallback
                    image[x, y] = new Rgb8(200, 0, 0);
                }
                else
                {
                    labels[x, y] = 1;
                    distanceField[x, y] = 0.8; // Non-salient
                    image[x, y] = new Rgb8(205, 0, 0);
                }
            }
        }

        var regionGraph = RegionGraph.FromLabels(labels);
        Matrix<Rgb8> result = LodAbstraction.Apply(image, regionGraph, distanceField, salientMask: null);

        // Region 0 detected as salient: intra-segment only, no cross-segment bleeding
        Assert.Equal((byte)200, result[3, 3].R);
        Assert.Equal((byte)0, result[3, 3].G);
        Assert.Equal((byte)0, result[3, 3].B);
    }

    /// <summary>
    /// Verifies that non-finite or NaN distance values are clamped to 0.0 without errors.
    /// </summary>
    [Fact]
    public void Apply_NaNDistanceClamping_ShouldTreatAsZeroDistanceWithoutErrors()
    {
        int width = 6;
        int height = 6;
        var image = new SequentialMatrix<Rgb8>(width, height);
        image[2, 2] = new Rgb8(255, 255, 255);

        var labels = new SequentialMatrix<int>(width, height);
        var regionGraph = RegionGraph.FromLabels(labels);
        var salientMask = new MaskMatrix(width, height);
        salientMask.SetAll(false);

        var distanceField = new SequentialMatrix<DoublePixel>(width, height);
        distanceField.SetAll(double.NaN);

        Matrix<Rgb8> result = LodAbstraction.Apply(image, regionGraph, distanceField, salientMask);

        Assert.Equal(36, result.Count);
        Assert.True(result[4, 2].R > 0);       // distance 2 (kernel size 4, radius 2)
        Assert.Equal((byte)0, result[5, 2].R); // distance 3 outside kernel
    }

    /// <summary>
    /// Verifies that the IDisjointSet overload produces identical results to the RegionGraph overload.
    /// </summary>
    [Fact]
    public void Apply_DisjointSetOverload_ShouldProduceIdenticalResultToRegionGraph()
    {
        int width = 8;
        int height = 8;
        var image = new SequentialMatrix<Rgb8>(width, height);
        var dummy = new SequentialMatrix<int>(width, height);
        var disjointSet = new DisjointSet<int>(dummy, i => i);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (x < 4)
                {
                    image[x, y] = new Rgb8(80, 120, 160);
                    if (x != 0 || y != 0)
                        disjointSet.Union(new Coord(0, 0), new Coord(x, y), 0);
                }
                else
                {
                    image[x, y] = new Rgb8(160, 120, 80);
                    if (x != 4 || y != 0)
                        disjointSet.Union(new Coord(4, 0), new Coord(x, y), 1);
                }
            }
        }

        var distanceField = new SequentialMatrix<DoublePixel>(width, height);
        distanceField.SetAll(0.3);

        Matrix<Rgb8> resultOverload = LodAbstraction.Apply(image, disjointSet, distanceField);
        var regionGraph = RegionGraph.FromDisjointSet(disjointSet);
        Matrix<Rgb8> resultDirect = LodAbstraction.Apply(image, regionGraph, distanceField);

        MatrixAssert.Equal(resultDirect, resultOverload);
    }

    /// <summary>
    /// Verifies that SalientKernelSize = 1 leaves salient regions completely unchanged.
    /// </summary>
    [Fact]
    public void Apply_SalientKernelSizeOne_ShouldLeaveSalientRegionUnchanged()
    {
        SequentialMatrix<Rgb8> image = TestMatrices.Create(
            4,
            4,
            (x, y) => new Rgb8((byte)(x * 40), (byte)(y * 40), (byte)((x + y) * 20)));
        var labels = new SequentialMatrix<int>(4, 4);
        var salientMask = new MaskMatrix(4, 4);
        salientMask.SetAll(true);
        var distanceField = new SequentialMatrix<DoublePixel>(4, 4);

        var regionGraph = RegionGraph.FromLabels(labels);
        var options = new LodAbstractionOptions(SalientKernelSize: 1);
        Matrix<Rgb8> result = LodAbstraction.Apply(image, regionGraph, distanceField, salientMask, options);

        MatrixAssert.Equal(image, result);
    }

    /// <summary>
    /// Verifies that even kernel sizes apply center-symmetric half-border weights.
    /// </summary>
    [Fact]
    public void Apply_EvenKernelSize_ComputesSymmetricHalfBorderWeights()
    {
        int width = 5;
        int height = 1;
        var image = new SequentialMatrix<Rgb8>(width, height);
        image[2, 0] = new Rgb8(255, 255, 255);

        var labels = new SequentialMatrix<int>(width, height);
        var regionGraph = RegionGraph.FromLabels(labels);
        var salientMask = new MaskMatrix(width, height);
        salientMask.SetAll(true);
        var distanceField = new SequentialMatrix<DoublePixel>(width, height);

        var options = new LodAbstractionOptions(SalientKernelSize: 4);
        Matrix<Rgb8> result = LodAbstraction.Apply(image, regionGraph, distanceField, salientMask, options);

        // Center pixel receives 255 / 4.0 = 63.75 -> rounded byte 64
        Assert.Equal((byte)64, result[2, 0].R);

        // Inner symmetric pixels: 255 / 3.5 = 72.857... -> rounded byte 73
        Assert.Equal((byte)73, result[1, 0].R);
        Assert.Equal((byte)73, result[3, 0].R);

        // Outer symmetric pixels (51)
        Assert.Equal((byte)51, result[0, 0].R);
        Assert.Equal((byte)51, result[4, 0].R);
    }

    /// <summary>
    /// Verifies that an empty 0x0 image returns an empty matrix.
    /// </summary>
    [Fact]
    public void Apply_EmptyImage_ShouldReturnEmptyMatrix()
    {
        var emptyImage = new SequentialMatrix<Rgb8>(0, 0);
        var emptyLabels = new SequentialMatrix<int>(0, 0);
        var emptyDistanceField = new SequentialMatrix<DoublePixel>(0, 0);

        var regionGraph = RegionGraph.FromLabels(emptyLabels);
        Matrix<Rgb8> result = LodAbstraction.Apply(emptyImage, regionGraph, emptyDistanceField);

        Assert.Empty(result);
    }

    /// <summary>
    /// Verifies that null arguments throw <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Apply_NullArguments_ShouldThrowArgumentNullException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var labels = new SequentialMatrix<int>(4, 4);
        var regionGraph = RegionGraph.FromLabels(labels);
        var distanceField = new SequentialMatrix<DoublePixel>(4, 4);
        var dummy = new SequentialMatrix<int>(4, 4);
        var segmentation = new DisjointSet<int>(dummy, i => i);

        Assert.Throws<ArgumentNullException>(() => LodAbstraction.Apply(null!, regionGraph, distanceField));
        Assert.Throws<ArgumentNullException>(() => LodAbstraction.Apply(image, (RegionGraph)null!, distanceField));
        Assert.Throws<ArgumentNullException>(() => LodAbstraction.Apply(image, regionGraph, (IReadOnlyMatrix<DoublePixel>)null!));
        Assert.Throws<ArgumentNullException>(() => LodAbstraction.Apply(null!, segmentation, distanceField));
        Assert.Throws<ArgumentNullException>(() => LodAbstraction.Apply(image, (IDisjointSet)null!, distanceField));
        Assert.Throws<ArgumentNullException>(() => LodAbstraction.Apply(image, segmentation, (IReadOnlyMatrix<DoublePixel>)null!));
    }

    /// <summary>
    /// Verifies that dimension mismatches throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Apply_MismatchedDimensions_ShouldThrowArgumentException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var regionGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(4, 4));
        var distanceField = new SequentialMatrix<DoublePixel>(4, 4);

        var badGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(5, 5));
        var badDistance = new SequentialMatrix<DoublePixel>(5, 5);
        var badMask = new MaskMatrix(5, 5);
        var badSegmentation = new DisjointSet<int>(new SequentialMatrix<int>(5, 5), i => i);

        Assert.Throws<ArgumentException>(() => LodAbstraction.Apply(image, badGraph, distanceField));
        Assert.Throws<ArgumentException>(() => LodAbstraction.Apply(image, regionGraph, badDistance));
        Assert.Throws<ArgumentException>(() => LodAbstraction.Apply(image, regionGraph, distanceField, badMask));
        Assert.Throws<ArgumentException>(() => LodAbstraction.Apply(image, badSegmentation, distanceField));
    }

    /// <summary>
    /// Verifies that invalid options throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Apply_InvalidOptions_ShouldThrowArgumentOutOfRangeException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var regionGraph = RegionGraph.FromLabels(new SequentialMatrix<int>(4, 4));
        var distanceField = new SequentialMatrix<DoublePixel>(4, 4);

        var invalidOptions = new LodAbstractionOptions(SalientKernelSize: 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => LodAbstraction.Apply(image, regionGraph, distanceField, options: invalidOptions));
    }
}
