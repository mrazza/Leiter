namespace Leiter.Tests.Algorithms.Segmentation;

using System;
using Leiter.Algorithms;
using Leiter.Algorithms.DataStructures;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.Algorithms;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="EgbiOptions"/>.
/// </summary>
public class EgbiOptionsTests : SizeTunableOptionsContractTestsBase<EgbiOptions>
{
    /// <inheritdoc/>
    protected override EgbiOptions CreateValidCustomOptions() =>
        new(KFactor: 75.0, Epsilon: 2.0, MinRegionSize: 150, Connectivity: GridConnectivity.FourConnected);

    /// <inheritdoc/>
    protected override EgbiOptions CreateInvalidOptions() =>
        new(KFactor: -1.0);
    /// <summary>
    /// Verifies that default EGBI options match expected values and pass validation.
    /// </summary>
    [Fact]
    public void Defaults_ShouldHaveExpectedValuesAndPassValidation()
    {
        var options = new EgbiOptions();

        Assert.Equal(100.0, options.KFactor);
        Assert.Equal(1.0, options.Epsilon);
        Assert.Equal(100, options.MinRegionSize);
        Assert.Equal(GridConnectivity.EightConnected, options.Connectivity);

        options.Validate();
    }

    /// <summary>
    /// Verifies that invalid <see cref="EgbiOptions.KFactor"/> values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="kFactor">The invalid k-factor value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidKFactor_ShouldThrowArgumentOutOfRangeException(double kFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EgbiOptions(KFactor: kFactor).Validate());
    }

    /// <summary>
    /// Verifies that invalid <see cref="EgbiOptions.Epsilon"/> values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="epsilon">The invalid epsilon value.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidEpsilon_ShouldThrowArgumentOutOfRangeException(double epsilon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EgbiOptions(Epsilon: epsilon).Validate());
    }

    /// <summary>
    /// Verifies that negative <see cref="EgbiOptions.MinRegionSize"/> values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    /// <param name="minRegionSize">The invalid minimum region size.</param>
    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeInts), MemberType = typeof(OptionValidationData))]
    public void Validate_NegativeMinRegionSize_ShouldThrowArgumentOutOfRangeException(int minRegionSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EgbiOptions(MinRegionSize: minRegionSize).Validate());
    }

    /// <summary>
    /// Verifies that undefined <see cref="EgbiOptions.Connectivity"/> values throw <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    [Fact]
    public void Validate_InvalidConnectivity_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new EgbiOptions(Connectivity: (GridConnectivity)99).Validate());
    }

    /// <summary>
    /// Verifies that boundary zero values and alternate connectivity settings pass validation.
    /// </summary>
    [Fact]
    public void Validate_ValidBoundaryAndCustomOptions_ShouldSucceed()
    {
        var zeroOptions = new EgbiOptions(KFactor: 0.0, Epsilon: 0.0, MinRegionSize: 0, Connectivity: GridConnectivity.FourConnected);
        zeroOptions.Validate();

        Assert.Equal(0.0, zeroOptions.KFactor);
        Assert.Equal(0.0, zeroOptions.Epsilon);
        Assert.Equal(0, zeroOptions.MinRegionSize);
        Assert.Equal(GridConnectivity.FourConnected, zeroOptions.Connectivity);

        var custom = new EgbiOptions(KFactor: 50.0, Epsilon: 2.5, MinRegionSize: 200, Connectivity: GridConnectivity.EightConnected);
        custom.Validate();

        Assert.Equal(50.0, custom.KFactor);
        Assert.Equal(2.5, custom.Epsilon);
        Assert.Equal(200, custom.MinRegionSize);
        Assert.Equal(GridConnectivity.EightConnected, custom.Connectivity);
    }

    /// <summary>
    /// Verifies that <see cref="EgbiOptions.ForSize(Size, double)"/> matches defaults at reference dimensions and scales across resolutions.
    /// </summary>
    [Fact]
    public void ForSize_ShouldMatchBaselineAtReferenceAndScaleAcrossSizes()
    {
        EgbiOptions refOpts = EgbiOptions.ForSize(new Size(800, 800));
        Assert.Equal(100.0, refOpts.KFactor, 6);
        Assert.Equal(1.0, refOpts.Epsilon, 6);
        Assert.Equal(100, refOpts.MinRegionSize);
        Assert.Equal(refOpts, EgbiOptions.ForSize(new Size(1000, 640)));

        EgbiOptions small = EgbiOptions.ForSize(new Size(200, 200));
        EgbiOptions large = EgbiOptions.ForSize(new Size(3200, 2400));
        Assert.True(small.KFactor < refOpts.KFactor);
        Assert.True(large.KFactor > refOpts.KFactor);
        Assert.True(small.MinRegionSize < refOpts.MinRegionSize);
        Assert.True(large.MinRegionSize > refOpts.MinRegionSize);
        Assert.True(large.Epsilon < refOpts.Epsilon);

        var zeroDisable = new EgbiOptions(KFactor: 50.0, Epsilon: 0.0, MinRegionSize: 0);
        EgbiOptions scaledDisable = EgbiOptions.ForSize(new Size(1600, 1600), zeroDisable);
        Assert.Equal(0.0, scaledDisable.Epsilon, 6);
        Assert.Equal(0, scaledDisable.MinRegionSize);

        // Scale-1.0 identity for sub-floor Epsilon (0 < Epsilon < 0.15) and decay floor clamping.
        var lowEpsilon = new EgbiOptions(KFactor: 50.0, Epsilon: 0.05, MinRegionSize: 100);
        EgbiOptions lowEpsilonRef = EgbiOptions.ForSize(new Size(800, 800), lowEpsilon);
        Assert.Equal(lowEpsilon, lowEpsilonRef);
        EgbiOptions lowEpsilonLarge = EgbiOptions.ForSize(new Size(3200, 3200), lowEpsilon);
        Assert.Equal(0.05, lowEpsilonLarge.Epsilon, 12);

        // Extreme scaling must remain finite and valid rather than overflowing to Infinity.
        var hugeBaseline = new EgbiOptions(KFactor: double.MaxValue / 2.0, Epsilon: double.MaxValue / 2.0, MinRegionSize: 100);
        EgbiOptions hugeScaled = EgbiOptions.ForSize(new Size(3200, 3200), hugeBaseline, referenceDimension: 1e-200);
        hugeScaled.Validate();
        Assert.True(double.IsFinite(hugeScaled.KFactor));
        Assert.True(double.IsFinite(hugeScaled.Epsilon));

        // Two-hop ForSize scaling (800 -> native -> preview) must compose without edge-attenuation drift.
        Size nativeSize = new(4000, 3000);
        Size previewSize = new(1024, 768);
        EgbiOptions nativeOpts = EgbiOptions.ForSize(nativeSize);
        EgbiOptions previewViaTwoHops = EgbiOptions.ForSize(previewSize, nativeOpts, nativeSize.LinearDimension);
        EgbiOptions previewDirect = EgbiOptions.ForSize(previewSize);
        Assert.Equal(previewDirect.KFactor, previewViaTwoHops.KFactor, 10);
        Assert.Equal(previewDirect.Epsilon, previewViaTwoHops.Epsilon, 10);
    }

    /// <summary>
    /// Verifies that <see cref="EgbiOptions.ForImage(IReadOnlyMatrix{Rgb8}, double)"/>, CIELAB overloads, and <see cref="EgbiOptions.ForAnalysis"/> adapt to image content and validate inputs.
    /// </summary>
    [Fact]
    public void ForImage_And_ForAnalysis_RgbAndLabOverloads_ShouldAdaptToContentAndValidate()
    {
        SequentialMatrix<Rgb8> flatImage = TestMatrices.Constant(256, 256, new Rgb8(120, 125, 130));
        SequentialMatrix<Rgb8> busyImage = TestMatrices.Checkerboard(256, 256, new Rgb8(245, 40, 30), new Rgb8(20, 60, 230), cellSize: 4);

        EgbiOptions flatOpts = EgbiOptions.ForImage(flatImage);
        EgbiOptions busyOpts = EgbiOptions.ForImage(busyImage);
        Assert.True(busyOpts.MinRegionSize > flatOpts.MinRegionSize);
        Assert.True(busyOpts.KFactor > flatOpts.KFactor);

        ImageAnalysis busyAnalysis = ImageAnalysis.Analyze(busyImage);
        Assert.Equal(busyOpts, EgbiOptions.ForAnalysis(busyAnalysis));
        var customBaseline = new EgbiOptions(KFactor: 50.0, Epsilon: 1.0, MinRegionSize: 200);
        Assert.Equal(EgbiOptions.ForImage(busyImage, customBaseline), EgbiOptions.ForAnalysis(busyAnalysis, customBaseline));

        // Single-rounding check for MinRegionSize: 256x256 flat image has areaScale=0.1024, clutterFactor=0.75.
        // 45 * 0.1024 * 0.75 = 3.456 -> rounds to 3 (whereas double-rounding Round(4.608)=5 -> Round(3.75)=4).
        EgbiOptions singleRounded = EgbiOptions.ForImage(flatImage, new EgbiOptions(MinRegionSize: 45));
        Assert.Equal(3, singleRounded.MinRegionSize);

        SequentialMatrix<Lab32> labImage = TestMatrices.Constant(64, 64, new Lab32(60f, 10f, -5f));
        EgbiOptions labOpts = EgbiOptions.ForImage(labImage);
        labOpts.Validate();
        EgbiOptions.ForImage(labImage, customBaseline).Validate();
        Assert.Equal(EgbiOptions.ForImage(labImage, customBaseline), EgbiOptions.ForAnalysis(ImageAnalysis.Analyze(labImage), customBaseline));

        Assert.Throws<ArgumentNullException>(() => EgbiOptions.ForImage((IReadOnlyMatrix<Rgb8>)null!));
        Assert.Throws<ArgumentNullException>(() => EgbiOptions.ForImage((IReadOnlyMatrix<Lab32>)null!));
        Assert.Throws<ArgumentNullException>(() => EgbiOptions.ForImage(flatImage, null!));
        Assert.Throws<ArgumentNullException>(() => EgbiOptions.ForImage(labImage, null!));
        Assert.Throws<ArgumentNullException>("analysis", () => EgbiOptions.ForAnalysis(null!));
        Assert.Throws<ArgumentNullException>("baseline", () => EgbiOptions.ForAnalysis(busyAnalysis, null!));
        Assert.Throws<ArgumentOutOfRangeException>("referenceDimension", () => EgbiOptions.ForAnalysis(busyAnalysis, 0.0));
    }
}
