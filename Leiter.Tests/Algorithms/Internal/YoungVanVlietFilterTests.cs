namespace Leiter.Tests.Algorithms.Internal;

using System;
using System.Runtime.Intrinsics;
using Leiter.Algorithms.Internal;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="YoungVanVlietFilter"/>.
/// </summary>
public class YoungVanVlietFilterTests
{
    [Fact]
    public void Create_WithDifferentSigmas_ShouldInstantiateFilter()
    {
        // Tests both branches in YoungVanVlietFilter.Create:
        // sigma < 2.5 (sigma = 1.2) and sigma >= 2.5 (sigma = 4.0)
        var filterMedium = YoungVanVlietFilter.Create(1.2);
        var filterLarge = YoungVanVlietFilter.Create(4.0);

        Assert.NotEqual(default, filterMedium);
        Assert.NotEqual(default, filterLarge);
    }

    [Theory]
    [InlineData(1.2)]
    [InlineData(4.0)]
    public void FilterLine_ConstantSpan_ShouldPreserveConstantValues(double sigma)
    {
        var filter = YoungVanVlietFilter.Create(sigma);
        const int length = 32;
        const double constantVal = 42.5;
        var line = new Vector256<double>[length];
        Array.Fill(line, Vector256.Create(constantVal));

        filter.FilterLine(line);

        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < Vector256<double>.Count; j++)
            {
                Assert.Equal(constantVal, line[i][j], 10);
            }
        }
    }

    [Theory]
    [InlineData(1.2)]
    [InlineData(4.0)]
    public void FilterLine_CenteredImpulseSpan_ShouldExhibitZeroPhaseSymmetryAndDecay(double sigma)
    {
        var filter = YoungVanVlietFilter.Create(sigma);
        const int length = 61;
        const int center = length / 2;
        var line = new Vector256<double>[length];
        Array.Fill(line, Vector256<double>.Zero);
        line[center] = Vector256.Create(1000.0);

        filter.FilterLine(line);

        // Check zero-phase symmetry around center
        for (int i = 1; i <= center; i++)
        {
            for (int j = 0; j < Vector256<double>.Count; j++)
            {
                Assert.Equal(line[center - i][j], line[center + i][j], 10);
            }
        }

        // Check monotonic decay away from center
        for (int i = 0; i < center; i++)
        {
            Assert.True(line[center - i][0] > line[center - i - 1][0]);
            Assert.True(line[center + i][0] > line[center + i + 1][0]);
        }
    }
}
