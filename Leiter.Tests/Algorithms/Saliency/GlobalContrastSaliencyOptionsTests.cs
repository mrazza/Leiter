namespace Leiter.Tests.Algorithms.Saliency;

using Leiter.Algorithms.Saliency;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="GlobalContrastSaliencyOptions"/>.
/// </summary>
public class GlobalContrastSaliencyOptionsTests
{
    [Fact]
    public void Constructor_DefaultProperties_ShouldMatchDefaultInstanceAndPassValidation()
    {
        var options = new GlobalContrastSaliencyOptions();

        Assert.True(options.EnableColorSpaceSmoothing);
        Assert.Equal(GlobalContrastSaliencyOptions.Default, options);

        options.Validate();
        GlobalContrastSaliencyOptions.Default.Validate();
        Assert.Same(GlobalContrastSaliencyOptions.Default, OptionsResolver.Resolve<GlobalContrastSaliencyOptions>(null));
        Assert.Same(options, OptionsResolver.Resolve(options));
    }

    [Fact]
    public void RecordEquality_ShouldCompareFieldsByValue()
    {
        var opt1 = new GlobalContrastSaliencyOptions(EnableColorSpaceSmoothing: false);
        var opt2 = new GlobalContrastSaliencyOptions(EnableColorSpaceSmoothing: false);
        var opt3 = new GlobalContrastSaliencyOptions(EnableColorSpaceSmoothing: true);

        Assert.Equal(opt1, opt2);
        Assert.Equal(opt1.GetHashCode(), opt2.GetHashCode());
        Assert.NotEqual(opt1, opt3);
    }
}
