namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="WetInWetResult"/>.
/// </summary>
public class WetInWetResultTests
{
    /// <summary>
    /// Verifies that passing a null image throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullImage_ShouldThrowArgumentNullException()
    {
        var mask = new MaskMatrix(4, 4);
        var qualifying = new bool[] { true };

        Assert.Throws<ArgumentNullException>(() => new WetInWetResult(null!, mask, qualifying));
    }

    /// <summary>
    /// Verifies that passing a null wet area mask throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullWetAreaMask_ShouldThrowArgumentNullException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var qualifying = new bool[] { true };

        Assert.Throws<ArgumentNullException>(() => new WetInWetResult(image, null!, qualifying));
    }

    /// <summary>
    /// Verifies that passing a null qualifying boundaries list throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullQualifyingBoundaries_ShouldThrowArgumentNullException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var mask = new MaskMatrix(4, 4);

        Assert.Throws<ArgumentNullException>(() => new WetInWetResult(image, mask, null!));
    }

    /// <summary>
    /// Verifies that mismatched image and wet area mask dimensions throw <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Constructor_MismatchedDimensions_ShouldThrowArgumentException()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var mask = new MaskMatrix(5, 4);
        var qualifying = new bool[] { true };

        Assert.Throws<ArgumentException>(() => new WetInWetResult(image, mask, qualifying));
    }

    /// <summary>
    /// Verifies that valid arguments populate properties correctly.
    /// </summary>
    [Fact]
    public void Constructor_ValidArguments_ShouldSetProperties()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var mask = new MaskMatrix(4, 4);
        var qualifying = new bool[] { true, false };

        var result = new WetInWetResult(image, mask, qualifying);

        Assert.Same(image, result.Image);
        Assert.Same(mask, result.WetAreaMask);
        Assert.NotSame(qualifying, result.QualifyingBoundaries);
        Assert.Equal(qualifying, result.QualifyingBoundaries);
        Assert.True(result.QualifyingBoundaries is not bool[]);
    }

    /// <summary>
    /// Verifies that mutating the caller's source list or attempting to mutate <see cref="WetInWetResult.QualifyingBoundaries"/>
    /// does not corrupt the stored boundary flags.
    /// </summary>
    [Fact]
    public void QualifyingBoundaries_ShouldBeDefensivelyCopiedAndReadOnly()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var mask = new MaskMatrix(4, 4);
        var qualifying = new bool[] { true, false };

        var result = new WetInWetResult(image, mask, qualifying);
        qualifying[0] = false;
        qualifying[1] = true;

        Assert.Equal([true, false], result.QualifyingBoundaries);
        Assert.Throws<NotSupportedException>(() => ((IList<bool>)result.QualifyingBoundaries)[0] = false);
    }

    /// <summary>
    /// Verifies that deconstruction outputs all constituent parts.
    /// </summary>
    [Fact]
    public void Deconstruct_ShouldReturnConstituentParts()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var mask = new MaskMatrix(4, 4);
        var qualifying = new bool[] { true, false };

        var result = new WetInWetResult(image, mask, qualifying);
        var (img, m, q) = result;

        Assert.Same(image, img);
        Assert.Same(mask, m);
        Assert.NotSame(qualifying, q);
        Assert.Equal(qualifying, q);
    }

    /// <summary>
    /// Verifies record equality and hash code consistency.
    /// </summary>
    [Fact]
    public void RecordEquality_IdenticalProperties_ShouldBeEqual()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var mask = new MaskMatrix(4, 4);

        var res1 = new WetInWetResult(image, mask, [true, false]);
        var res2 = new WetInWetResult(image, mask, [true, false]);

        var otherImage = new SequentialMatrix<Rgb8>(4, 4);
        var res3 = new WetInWetResult(otherImage, mask, [true, false]);
        var res4 = new WetInWetResult(image, mask, [false, true]);

        Assert.Equal(res1, res2);
        Assert.True(res1 == res2);
        Assert.False(res1 == res3);
        Assert.False(res1 == res4);
        Assert.Equal(res1.GetHashCode(), res2.GetHashCode());
    }

    /// <summary>
    /// Verifies that <see cref="WetInWetResult.ValidateAgainst"/> validates null, dimensions, and boundary counts.
    /// </summary>
    [Fact]
    public void ValidateAgainst_ShouldValidateRegionGraphDimensionsAndBoundaryCount()
    {
        var labels = new SequentialMatrix<int>(2, 2);
        labels[1, 0] = 1;
        labels[1, 1] = 1;
        var graph = RegionGraph.FromLabels(labels);

        var valid = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), [true]);
        valid.ValidateAgainst(graph, "wetInWet");

        Assert.Throws<ArgumentNullException>(() => valid.ValidateAgainst(null!, "wetInWet"));

        var wrongSize = new WetInWetResult(new SequentialMatrix<Rgb8>(3, 2), new MaskMatrix(3, 2), [true]);
        ArgumentException sizeEx = Assert.Throws<ArgumentException>(() => wrongSize.ValidateAgainst(graph, "wetInWet"));
        Assert.Equal("wetInWet", sizeEx.ParamName);

        var wrongBoundaryCount = new WetInWetResult(new SequentialMatrix<Rgb8>(2, 2), new MaskMatrix(2, 2), []);
        ArgumentException boundaryEx = Assert.Throws<ArgumentException>(() => wrongBoundaryCount.ValidateAgainst(graph, "wetInWet"));
        Assert.Equal("wetInWet", boundaryEx.ParamName);
    }
}
