namespace Leiter.Tests.Algorithms.Saliency;

using System;
using Leiter.Algorithms.Saliency;
using Leiter.Tests.TestUtils;
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
        Assert.Equal(9.0, options.CenterPriorWeight, 6);
        Assert.Equal(RegionalContrastSaliencyOptions.Default, options);

        options.Validate();
        RegionalContrastSaliencyOptions.Default.Validate();
    }

    [Fact]
    public void RecordEquality_ShouldCompareFieldsByValue()
    {
        var opt1 = new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: true, CenterPriorWeight: 0.0);
        var opt2 = new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: true, CenterPriorWeight: 0.0);
        var opt3 = new RegionalContrastSaliencyOptions(EnableSmoothing: false, ComputeBorderRegions: false, CenterPriorWeight: 9.0);

        Assert.Equal(opt1, opt2);
        Assert.Equal(opt1.GetHashCode(), opt2.GetHashCode());
        Assert.NotEqual(opt1, opt3);
    }

    [Theory]
    [MemberData(nameof(OptionValidationData.InvalidNonNegativeDoubles), MemberType = typeof(OptionValidationData))]
    public void Validate_InvalidCenterPriorWeight_ShouldThrowArgumentOutOfRangeException(double centerPriorWeight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RegionalContrastSaliencyOptions(CenterPriorWeight: centerPriorWeight).Validate());
    }
}
