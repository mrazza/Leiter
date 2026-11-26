namespace Leiter.Tests.Algorithms.Stylization.Watercolor;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.Stylization.Watercolor;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.Algorithms;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="EdgeDarkeningOptions"/>.
/// </summary>
public class EdgeDarkeningOptionsTests : SizeTunableOptionsContractTestsBase<EdgeDarkeningOptions>
{
    /// <inheritdoc/>
    protected override EdgeDarkeningOptions CreateValidCustomOptions() =>
        new(Intensity: 2.4, BlurSigma: 1.2, GradientOperator: GradientOperator.Scharr);

    /// <inheritdoc/>
    protected override EdgeDarkeningOptions CreateInvalidOptions() =>
        new(Intensity: -1.0);
    /// <summary>
    /// Verifies that default options match expected default values and pass validation.
    /// </summary>
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchExpectedValues()
    {
        var options = new EdgeDarkeningOptions();

        Assert.Equal(1.8, options.Intensity);
        Assert.Equal(0.8, options.BlurSigma);
        Assert.Equal(GradientOperator.Sobel, options.GradientOperator);

        options.Validate();
    }

    /// <summary>
    /// Verifies that valid custom options pass validation without throwing exceptions.
    /// </summary>
    [Fact]
    public void Validate_ValidCustomOptions_ShouldNotThrow()
    {
        var options = new EdgeDarkeningOptions(
            Intensity: 0.0,
            BlurSigma: 0.0,
            GradientOperator: GradientOperator.Scharr);

        options.Validate();

        Assert.Equal(0.0, options.Intensity);
        Assert.Equal(0.0, options.BlurSigma);
        Assert.Equal(GradientOperator.Scharr, options.GradientOperator);
    }

    /// <summary>
    /// Verifies that invalid Intensity values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="intensity">The invalid intensity value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidIntensity_ShouldThrowArgumentOutOfRangeException(double intensity)
    {
        var options = new EdgeDarkeningOptions(Intensity: intensity);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that invalid BlurSigma values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="blurSigma">The invalid blur sigma value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidBlurSigma_ShouldThrowArgumentOutOfRangeException(double blurSigma)
    {
        var options = new EdgeDarkeningOptions(BlurSigma: blurSigma);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that an undefined GradientOperator enum value throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidGradientOperator_ShouldThrowArgumentOutOfRangeException()
    {
        var options = new EdgeDarkeningOptions(GradientOperator: (GradientOperator)999);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    /// <summary>
    /// Verifies that <see cref="EdgeDarkeningOptions.ForSize"/> scales blur sigma and compensates edge intensity, and <see cref="EdgeDarkeningOptions.ForImage"/> adapts to color dispersion.
    /// </summary>
    [Fact]
    public void ForSize_And_ForImage_ShouldCompensateIntensityAndAdaptToSpread()
    {
        EdgeDarkeningOptions refOpts = EdgeDarkeningOptions.ForSize(new Size(800, 800));
        Assert.Equal(1.8, refOpts.Intensity, 6);
        Assert.Equal(0.8, refOpts.BlurSigma, 6);
        Assert.Equal(refOpts, EdgeDarkeningOptions.ForSize(new Size(1000, 640)));

        EdgeDarkeningOptions small = EdgeDarkeningOptions.ForSize(new Size(200, 200));
        EdgeDarkeningOptions large = EdgeDarkeningOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(0.2, small.BlurSigma, 6);
        Assert.True(small.Intensity < refOpts.Intensity);
        Assert.True(large.BlurSigma > refOpts.BlurSigma);
        Assert.True(large.Intensity > refOpts.Intensity);

        SequentialMatrix<Rgb8> flat = TestMatrices.Constant(64, 64, new Rgb8(120, 120, 120));
        SequentialMatrix<Rgb8> vibrant = TestMatrices.Checkerboard(64, 64, new Rgb8(255, 240, 20), new Rgb8(10, 20, 235));

        Assert.True(EdgeDarkeningOptions.ForImage(flat).Intensity > EdgeDarkeningOptions.ForImage(vibrant).Intensity);

        EdgeDarkeningOptions customBaseline = CreateValidCustomOptions();
        EdgeDarkeningOptions customHalf = EdgeDarkeningOptions.ForSize(new Size(400, 400), customBaseline);
        Assert.Equal(0.6, customHalf.BlurSigma, 6);
        Assert.Equal(GradientOperator.Scharr, customHalf.GradientOperator);
        Assert.True(customHalf.Intensity < customBaseline.Intensity);
        Assert.Equal(GradientOperator.Scharr, EdgeDarkeningOptions.ForImage(vibrant, customBaseline).GradientOperator);
        Assert.True(EdgeDarkeningOptions.ForImage(flat, customBaseline).Intensity > EdgeDarkeningOptions.ForImage(vibrant, customBaseline).Intensity);

        ImageAnalysis vibrantAnalysis = ImageAnalysis.Analyze(vibrant);
        Assert.Equal(EdgeDarkeningOptions.ForImage(vibrant), EdgeDarkeningOptions.ForAnalysis(vibrantAnalysis));
        Assert.Equal(EdgeDarkeningOptions.ForImage(vibrant, customBaseline), EdgeDarkeningOptions.ForAnalysis(vibrantAnalysis, customBaseline));

        var overflowBaseline = new EdgeDarkeningOptions(Intensity: 1e300, BlurSigma: 1e300);
        EdgeDarkeningOptions overflowScaled = EdgeDarkeningOptions.ForSize(new Size(100_000, 100_000), overflowBaseline, referenceDimension: 1.0);
        overflowScaled.Validate();
        Assert.True(double.IsFinite(overflowScaled.Intensity));
        Assert.True(double.IsFinite(overflowScaled.BlurSigma));

        var zeroIntensityBaseline = new EdgeDarkeningOptions(Intensity: 0.0);
        Assert.Equal(0.0, EdgeDarkeningOptions.ForSize(new Size(1600, 1600), zeroIntensityBaseline).Intensity);

        // Two-hop ForSize scaling (800 -> native -> preview) must compose multiplicatively without double-scaling bias.
        Size nativeSize = new(4000, 3000);
        Size previewSize = new(1024, 768);
        EdgeDarkeningOptions nativeOpts = EdgeDarkeningOptions.ForSize(nativeSize);
        EdgeDarkeningOptions previewViaTwoHops = EdgeDarkeningOptions.ForSize(previewSize, nativeOpts, nativeSize.LinearDimension);
        EdgeDarkeningOptions previewDirect = EdgeDarkeningOptions.ForSize(previewSize);
        Assert.Equal(previewDirect.Intensity, previewViaTwoHops.Intensity, 12);
        Assert.Equal(previewDirect.BlurSigma, previewViaTwoHops.BlurSigma, 12);

        Assert.Throws<ArgumentNullException>(() => EdgeDarkeningOptions.ForImage(null!));
        Assert.Throws<ArgumentNullException>(() => EdgeDarkeningOptions.ForImage(flat, null!));
        Assert.Throws<ArgumentNullException>(() => EdgeDarkeningOptions.ForAnalysis(null!));
        Assert.Throws<ArgumentNullException>(() => EdgeDarkeningOptions.ForAnalysis(vibrantAnalysis, null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => EdgeDarkeningOptions.ForAnalysis(vibrantAnalysis, 0.0));
    }
}
