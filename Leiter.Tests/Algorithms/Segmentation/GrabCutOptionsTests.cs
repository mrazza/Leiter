namespace Leiter.Tests.Algorithms.Segmentation;

using Leiter.Algorithms.Segmentation;
using Leiter.Core;
using Leiter.Pixels;
using Leiter.Tests.TestUtils;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GrabCutOptions"/>.
/// </summary>
public class GrabCutOptionsTests
{
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

        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(3, 3), new Size(4000, 80), new Size(12000, 9000) })
        {
            GrabCutOptions.ForSize(size).Validate();
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCutOptions.ForSize(new Size(-1, 10)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GrabCutOptions.ForSize(new Size(10, 10), 0.0));
        Assert.Throws<ArgumentNullException>(() => GrabCutOptions.ForImage(null!));
    }
}
