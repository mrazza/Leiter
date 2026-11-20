namespace Leiter.Tests.Algorithms.Segmentation;

using Leiter.Algorithms;
using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.Algorithms;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GrabCutOptions"/>.
/// </summary>
public class GrabCutOptionsTests : SizeTunableOptionsContractTestsBase<GrabCutOptions>
{
    /// <inheritdoc/>
    protected override GrabCutOptions CreateValidCustomOptions() =>
        new(Iterations: 7, MixtureComponentCount: 4, SmoothnessWeight: 65.0);

    /// <inheritdoc/>
    protected override GrabCutOptions CreateInvalidOptions() =>
        new(Iterations: 0);
    [Fact]
    public void Defaults_ShouldHaveExpectedValues()
    {
        var options = new GrabCutOptions();

        Assert.Equal(5, options.Iterations);
        Assert.Equal(5, options.MixtureComponentCount);
        Assert.Equal(50.0, options.SmoothnessWeight);

        options.Validate();
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveInts), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidIterations_ShouldThrowArgumentOutOfRangeException(int iterations)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrabCutOptions(Iterations: iterations).Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidPositiveInts), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidMixtureComponentCount_ShouldThrowArgumentOutOfRangeException(int componentCount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrabCutOptions(MixtureComponentCount: componentCount).Validate());
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidSmoothnessWeight_ShouldThrowArgumentOutOfRangeException(double smoothnessWeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrabCutOptions(SmoothnessWeight: smoothnessWeight).Validate());
    }

    [Fact]
    public void ForSize_And_ForImage_ShouldScaleSmoothnessAndAdaptGmmComponents()
    {
        GrabCutOptions refOpts = GrabCutOptions.ForSize(new Size(800, 800));
        Assert.Equal(50.0, refOpts.SmoothnessWeight, 6);
        Assert.Equal(refOpts, GrabCutOptions.ForSize(new Size(1000, 640)));

        GrabCutOptions small = GrabCutOptions.ForSize(new Size(200, 200));
        GrabCutOptions large = GrabCutOptions.ForSize(new Size(3200, 2400));
        Assert.Equal(12.5, small.SmoothnessWeight, 6);
        Assert.True(large.SmoothnessWeight > refOpts.SmoothnessWeight);

        SequentialMatrix<Rgb8> flat = TestMatrices.Constant(64, 64, new Rgb8(100, 120, 140));
        SequentialMatrix<Rgb8> rich = TestMatrices.Checkerboard(64, 64, new Rgb8(250, 240, 30), new Rgb8(15, 20, 220));

        GrabCutOptions flatOpts = GrabCutOptions.ForImage(flat);
        GrabCutOptions richOpts = GrabCutOptions.ForImage(rich);
        Assert.Equal(4, flatOpts.MixtureComponentCount);
        Assert.Equal(6, richOpts.MixtureComponentCount);

        ImageAnalysis richAnalysis = ImageAnalysis.Analyze(rich);
        Assert.Equal(richOpts, GrabCutOptions.ForAnalysis(richAnalysis));

        GrabCutOptions customBaseline = CreateValidCustomOptions();
        GrabCutOptions customHalf = GrabCutOptions.ForSize(new Size(400, 400), customBaseline);
        Assert.Equal(7, customHalf.Iterations);
        Assert.Equal(4, customHalf.MixtureComponentCount);
        Assert.Equal(32.5, customHalf.SmoothnessWeight, 6);

        GrabCutOptions customFlat = GrabCutOptions.ForImage(flat, customBaseline);
        GrabCutOptions customRich = GrabCutOptions.ForImage(rich, customBaseline);
        Assert.Equal(7, customFlat.Iterations);
        Assert.Equal(3, customFlat.MixtureComponentCount);
        Assert.Equal(5, customRich.MixtureComponentCount);
        Assert.Equal(customRich, GrabCutOptions.ForAnalysis(richAnalysis, customBaseline));

        // Extreme scaling must clamp to finite values rather than overflowing to Infinity.
        var hugeBaseline = new GrabCutOptions(SmoothnessWeight: double.MaxValue / 2.0);
        GrabCutOptions hugeScaled = GrabCutOptions.ForSize(new Size(3200, 3200), hugeBaseline, referenceDimension: 1e-200);
        hugeScaled.Validate();
        Assert.True(double.IsFinite(hugeScaled.SmoothnessWeight));

        Assert.Throws<ArgumentNullException>(() => GrabCutOptions.ForImage(null!));
        Assert.Throws<ArgumentNullException>(() => GrabCutOptions.ForImage(flat, null!));
        Assert.Throws<ArgumentNullException>("analysis", () => GrabCutOptions.ForAnalysis(null!));
        Assert.Throws<ArgumentNullException>("baseline", () => GrabCutOptions.ForAnalysis(richAnalysis, null!));
        Assert.Throws<ArgumentOutOfRangeException>("referenceDimension", () => GrabCutOptions.ForAnalysis(richAnalysis, 0.0));
    }
}
