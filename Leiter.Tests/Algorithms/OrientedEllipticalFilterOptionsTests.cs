namespace Leiter.Tests.Algorithms;

using System;
using Leiter.Algorithms;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="OrientedEllipticalFilterOptions"/>.
/// </summary>
public class OrientedEllipticalFilterOptionsTests : SizeTunableOptionsContractTestsBase<OrientedEllipticalFilterOptions>
{
    /// <inheritdoc/>
    protected override OrientedEllipticalFilterOptions CreateValidCustomOptions() =>
        new(MajorRadius: 8.0, MinorRadius: 3.0, SigmaScale: 0.7);

    /// <inheritdoc/>
    protected override OrientedEllipticalFilterOptions CreateInvalidOptions() =>
        new(MajorRadius: 0.0);
    /// <summary>
    /// Verifies that default options match expected default values and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new OrientedEllipticalFilterOptions();

        Assert.Equal(6.0, options.MajorRadius, 6);
        Assert.Equal(1.5, options.MinorRadius, 6);
        Assert.Equal(0.5, options.SigmaScale, 6);

        options.Validate();
    }

    /// <summary>
    /// Verifies that non-positive or non-finite MajorRadius values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="majorRadius">The invalid major radius value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMajorRadius_ShouldThrowArgumentOutOfRangeException(double majorRadius)
    {
        var options = new OrientedEllipticalFilterOptions(MajorRadius: majorRadius);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that non-positive or non-finite MinorRadius values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="minorRadius">The invalid minor radius value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMinorRadius_ShouldThrowArgumentOutOfRangeException(double minorRadius)
    {
        var options = new OrientedEllipticalFilterOptions(MinorRadius: minorRadius);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that non-positive or non-finite SigmaScale values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="sigmaScale">The invalid sigma scale value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidSigmaScale_ShouldThrowArgumentOutOfRangeException(double sigmaScale)
    {
        var options = new OrientedEllipticalFilterOptions(SigmaScale: sigmaScale);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that valid custom options pass validation without throwing.
    /// </summary>
    [Fact]
    public void Validate_CustomValidOptions_ShouldNotThrow()
    {
        var options = new OrientedEllipticalFilterOptions(
            MajorRadius: 4.0,
            MinorRadius: 2.0,
            SigmaScale: 0.3);

        options.Validate();
    }

    /// <summary>
    /// Verifies that <see cref="OrientedEllipticalFilterOptions.ForSize"/> and <see cref="OrientedEllipticalFilterOptions.ForImage{T}"/> scale radii linearly and validate inputs.
    /// </summary>
    [Fact]
    public void ForSize_And_ForImage_ShouldScaleRadiiLinearlyAndValidate()
    {
        OrientedEllipticalFilterOptions refOptions = OrientedEllipticalFilterOptions.ForSize(new Size(800, 800));
        Assert.Equal(6.0, refOptions.MajorRadius, 6);
        Assert.Equal(1.5, refOptions.MinorRadius, 6);
        Assert.Equal(refOptions, OrientedEllipticalFilterOptions.ForSize(new Size(1000, 640)));

        OrientedEllipticalFilterOptions small = OrientedEllipticalFilterOptions.ForSize(new Size(200, 200));
        OrientedEllipticalFilterOptions large = OrientedEllipticalFilterOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(1.5, small.MajorRadius, 6);
        Assert.Equal(0.375, small.MinorRadius, 6);
        Assert.True(large.MajorRadius > refOptions.MajorRadius);
        Assert.True(large.MinorRadius > refOptions.MinorRadius);

        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(3, 3), new Size(4000, 80), new Size(12000, 9000) })
        {
            OrientedEllipticalFilterOptions.ForSize(size).Validate();
        }

        var image = new SequentialMatrix<Rgb8>(400, 400);
        Assert.Equal(OrientedEllipticalFilterOptions.ForSize(image.Size), OrientedEllipticalFilterOptions.ForImage(image));

        var customBaseline = new OrientedEllipticalFilterOptions(MajorRadius: 10.0, MinorRadius: 4.0, SigmaScale: 0.8);
        OrientedEllipticalFilterOptions halfScaled = OrientedEllipticalFilterOptions.ForSize(new Size(400, 400), customBaseline);
        Assert.Equal(5.0, halfScaled.MajorRadius, 6);
        Assert.Equal(2.0, halfScaled.MinorRadius, 6);
        Assert.Equal(0.8, halfScaled.SigmaScale, 6);

        var scalarImage = new SequentialMatrix<DoublePixel>(400, 400);
        Assert.Equal(halfScaled, OrientedEllipticalFilterOptions.ForImage(scalarImage, customBaseline));

        Assert.Throws<ArgumentOutOfRangeException>(() => OrientedEllipticalFilterOptions.ForSize(new Size(-1, 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => OrientedEllipticalFilterOptions.ForSize(new Size(100, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => OrientedEllipticalFilterOptions.ForSize(new Size(100, 100), -5.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => OrientedEllipticalFilterOptions.ForSize(new Size(100, 100), double.PositiveInfinity));
        Assert.Throws<ArgumentNullException>(() => OrientedEllipticalFilterOptions.ForImage<Rgb8>(null!));
        Assert.Throws<ArgumentNullException>(() => OrientedEllipticalFilterOptions.ForImage<Rgb8>(null!, customBaseline));
        Assert.Throws<ArgumentNullException>(() => OrientedEllipticalFilterOptions.ForImage(image, null!));
    }
}
