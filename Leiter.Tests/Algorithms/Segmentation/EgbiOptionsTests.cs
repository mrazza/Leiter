namespace Leiter.Tests.Algorithms.Segmentation;

using System;
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

        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(3, 3), new Size(4000, 80), new Size(12000, 9000) })
        {
            EgbiOptions.ForSize(size).Validate();
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => EgbiOptions.ForSize(new Size(-1, 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => EgbiOptions.ForSize(new Size(100, 100), double.NaN));
        Assert.Throws<ArgumentNullException>(() => EgbiOptions.ForSize(new Size(100, 100), null!));
    }

    /// <summary>
    /// Verifies that <see cref="EgbiOptions.ForImage(IReadOnlyMatrix{Rgb8}, double)"/> and CIELAB overloads adapt to image content and validate inputs.
    /// </summary>
    [Fact]
    public void ForImage_RgbAndLabOverloads_ShouldAdaptToContentAndValidate()
    {
        SequentialMatrix<Rgb8> flatImage = TestMatrices.Constant(256, 256, new Rgb8(120, 125, 130));
        SequentialMatrix<Rgb8> busyImage = TestMatrices.Checkerboard(256, 256, new Rgb8(245, 40, 30), new Rgb8(20, 60, 230), cellSize: 4);

        EgbiOptions flatOpts = EgbiOptions.ForImage(flatImage);
        EgbiOptions busyOpts = EgbiOptions.ForImage(busyImage);
        Assert.True(busyOpts.MinRegionSize > flatOpts.MinRegionSize);
        Assert.True(busyOpts.KFactor > flatOpts.KFactor);

        var labImage = new SequentialMatrix<Lab32>(64, 64);
        labImage.SetAll(new Lab32(60f, 10f, -5f));
        EgbiOptions labOpts = EgbiOptions.ForImage(labImage);
        labOpts.Validate();
        EgbiOptions.ForImage(labImage, new EgbiOptions(KFactor: 50.0, Epsilon: 1.0, MinRegionSize: 200)).Validate();

        Assert.Throws<ArgumentNullException>(() => EgbiOptions.ForImage((IReadOnlyMatrix<Rgb8>)null!));
        Assert.Throws<ArgumentNullException>(() => EgbiOptions.ForImage((IReadOnlyMatrix<Lab32>)null!));
        Assert.Throws<ArgumentNullException>(() => EgbiOptions.ForImage(flatImage, null!));
        Assert.Throws<ArgumentNullException>(() => EgbiOptions.ForImage(labImage, null!));
    }
}
