namespace Leiter.Tests.Algorithms.Saliency;

using Leiter.Algorithms.Saliency;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="RegionalContrastSaliencyOptions"/>.
/// </summary>
public class RegionalContrastSaliencyOptionsTests
{
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchDefaultInstanceAndPassValidation()
    {
        var options = new RegionalContrastSaliencyOptions();

        Assert.True(options.EnableSmoothing);
        Assert.True(options.ComputeBorderRegions);
        Assert.Equal(RegionalContrastSaliencyOptions.Default, options);

        options.Validate();
        RegionalContrastSaliencyOptions.Default.Validate();
    }

    [Fact]
    public void RecordEquality_ShouldCompareFieldsByValue()
    {
        var opt1 = new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: true);
        var opt2 = new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: true);
        var opt3 = new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: false);

        Assert.Equal(opt1, opt2);
        Assert.Equal(opt1.GetHashCode(), opt2.GetHashCode());
        Assert.NotEqual(opt1, opt3);
    }
}
