namespace Leiter.Tests.Algorithms.Saliency;

using Leiter.Algorithms.Saliency;
using Leiter.Tests.Algorithms;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GlobalContrastSaliencyOptions"/>.
/// </summary>
public class GlobalContrastSaliencyOptionsTests : AlgorithmOptionsContractTestsBase<GlobalContrastSaliencyOptions>
{
    /// <inheritdoc/>
    protected override GlobalContrastSaliencyOptions CreateValidCustomOptions() =>
        new(EnableSmoothing: false);

    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchDefaultInstance()
    {
        var options = new GlobalContrastSaliencyOptions();

        Assert.True(options.EnableSmoothing);
        Assert.Equal(GlobalContrastSaliencyOptions.Default, options);
    }

    [Fact]
    public void RecordEquality_ShouldCompareFieldsByValue()
    {
        var opt1 = new GlobalContrastSaliencyOptions(EnableSmoothing: false);
        var opt2 = new GlobalContrastSaliencyOptions(EnableSmoothing: false);
        var opt3 = new GlobalContrastSaliencyOptions(EnableSmoothing: true);

        Assert.Equal(opt1, opt2);
        Assert.Equal(opt1.GetHashCode(), opt2.GetHashCode());
        Assert.NotEqual(opt1, opt3);
    }
}
