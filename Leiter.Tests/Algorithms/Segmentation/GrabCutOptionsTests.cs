namespace Leiter.Tests.Algorithms.Segmentation;

using Leiter.Algorithms.Segmentation;
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

    [Fact]
    public void Validate_InvalidIterations_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrabCutOptions(Iterations: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrabCutOptions(Iterations: -1).Validate());
    }

    [Fact]
    public void Validate_InvalidMixtureComponentCount_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrabCutOptions(MixtureComponentCount: 0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrabCutOptions(MixtureComponentCount: -1).Validate());
    }

    [Fact]
    public void Validate_InvalidSmoothnessWeight_ShouldThrowArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrabCutOptions(SmoothnessWeight: -1.0).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrabCutOptions(SmoothnessWeight: double.NaN).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrabCutOptions(SmoothnessWeight: double.PositiveInfinity).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => new GrabCutOptions(SmoothnessWeight: double.NegativeInfinity).Validate());
    }
}
