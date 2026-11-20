namespace Leiter.Tests.Algorithms.Internal;

using Leiter.Algorithms.Internal;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="OptionTuning"/>.
/// </summary>
public class OptionTuningTests
{
    [Fact]
    public void GeometricMeanDimension_ShouldComputeSqrtWidthTimesHeight()
    {
        Assert.Equal(0.0, OptionTuning.GeometricMeanDimension(new Size(0, 100)), 12);
        Assert.Equal(256.0, OptionTuning.GeometricMeanDimension(new Size(256, 256)), 12);
        Assert.Equal(600.0, OptionTuning.GeometricMeanDimension(new Size(400, 900)), 12);
        Assert.Equal(50_000.0, OptionTuning.GeometricMeanDimension(new Size(50_000, 50_000)), 6);
    }
}
