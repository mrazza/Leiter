namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using System.Collections.Generic;
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
        Assert.Same(qualifying, result.QualifyingBoundaries);
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
        Assert.Same(qualifying, q);
    }

    /// <summary>
    /// Verifies record equality and hash code consistency.
    /// </summary>
    [Fact]
    public void RecordEquality_IdenticalProperties_ShouldBeEqual()
    {
        var image = new SequentialMatrix<Rgb8>(4, 4);
        var mask = new MaskMatrix(4, 4);
        var qualifying = new bool[] { true };

        var res1 = new WetInWetResult(image, mask, qualifying);
        var res2 = new WetInWetResult(image, mask, qualifying);

        var otherImage = new SequentialMatrix<Rgb8>(4, 4);
        var res3 = new WetInWetResult(otherImage, mask, qualifying);

        Assert.Equal(res1, res2);
        Assert.True(res1 == res2);
        Assert.False(res1 == res3);
        Assert.Equal(res1.GetHashCode(), res2.GetHashCode());
    }
}
