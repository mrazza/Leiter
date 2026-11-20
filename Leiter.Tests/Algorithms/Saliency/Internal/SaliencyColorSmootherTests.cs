namespace Leiter.Tests.Algorithms.Saliency.Internal;

using System;
using System.Collections.Generic;
using System.Linq;
using Leiter.Algorithms.Saliency.Internal;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="SaliencyColorSmoother"/> and <see cref="PairwiseLabDistanceTable"/>.
/// </summary>
public class SaliencyColorSmootherTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public void SmoothColorSaliency_InvalidNeighborDivisor_ShouldThrowArgumentOutOfRangeException(int neighborDivisor)
    {
        var map = new Dictionary<Rgb8, double>
        {
            [new Rgb8(0, 0, 0)] = 0.5
        };
        var labLookup = new Dictionary<Rgb8, Lab32>
        {
            [new Rgb8(0, 0, 0)] = new Lab32(0f, 0f, 0f)
        };
        PairwiseLabDistanceTable table = PairwiseLabDistanceTable.Build(labLookup);
        double[] dense = [0.5];

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SaliencyColorSmoother.SmoothColorSaliency(map, labLookup, neighborDivisor));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SaliencyColorSmoother.SmoothColorSaliency(map, table, neighborDivisor));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SaliencyColorSmoother.SmoothColorSaliency(dense, table, neighborDivisor));
    }

    [Fact]
    public void SmoothColorSaliency_NullAndMismatchedLengthArguments_ShouldThrowExpectedExceptions()
    {
        var map = new Dictionary<Rgb8, double>
        {
            [new Rgb8(0, 0, 0)] = 0.5
        };
        var labLookup = new Dictionary<Rgb8, Lab32>
        {
            [new Rgb8(0, 0, 0)] = new Lab32(0f, 0f, 0f)
        };
        PairwiseLabDistanceTable table = PairwiseLabDistanceTable.Build(labLookup);

        Assert.Throws<ArgumentNullException>(() =>
            SaliencyColorSmoother.SmoothColorSaliency(null!, labLookup, 2));
        Assert.Throws<ArgumentNullException>(() =>
            SaliencyColorSmoother.SmoothColorSaliency(map, (IReadOnlyDictionary<Rgb8, Lab32>)null!, 2));
        Assert.Throws<ArgumentNullException>(() =>
            SaliencyColorSmoother.SmoothColorSaliency((Dictionary<Rgb8, double>)null!, table, 2));
        Assert.Throws<ArgumentNullException>(() =>
            SaliencyColorSmoother.SmoothColorSaliency(map, (PairwiseLabDistanceTable)null!, 2));
        Assert.Throws<ArgumentNullException>(() =>
            SaliencyColorSmoother.SmoothColorSaliency(new double[1], null!, 2));
        Assert.Throws<ArgumentException>("colorSaliencies", () =>
            SaliencyColorSmoother.SmoothColorSaliency(new double[2], table, 2));
    }

    [Fact]
    public void SmoothColorSaliency_NearestColorCountLessThanOrEqualToOne_ShouldReturnWithoutModification()
    {
        var map = new Dictionary<Rgb8, double>
        {
            [new Rgb8(0, 0, 0)] = 0.2,
            [new Rgb8(255, 0, 0)] = 0.8
        };
        var labLookup = new Dictionary<Rgb8, Lab32>
        {
            [new Rgb8(0, 0, 0)] = new Lab32(0f, 0f, 0f),
            [new Rgb8(255, 0, 0)] = new Lab32(53f, 80f, 67f)
        };

        // Count = 2, neighborDivisor = 2 -> nearestColorCount = 1 (<= 1), returns early
        SaliencyColorSmoother.SmoothColorSaliency(map, labLookup, neighborDivisor: 2);

        Assert.Equal(0.2, map[new Rgb8(0, 0, 0)], 6);
        Assert.Equal(0.8, map[new Rgb8(255, 0, 0)], 6);
    }

    [Fact]
    public void SmoothColorSaliency_ZeroDistanceNeighbors_ShouldPreserveValuesWithoutNaN()
    {
        var map = new Dictionary<Rgb8, double>
        {
            [new Rgb8(0, 0, 0)] = 0.2,
            [new Rgb8(1, 0, 0)] = 0.4,
            [new Rgb8(255, 0, 0)] = 0.8,
            [new Rgb8(0, 255, 0)] = 0.6
        };
        var labLookup = new Dictionary<Rgb8, Lab32>
        {
            [new Rgb8(0, 0, 0)] = new Lab32(0f, 0f, 0f),
            [new Rgb8(1, 0, 0)] = new Lab32(0f, 0f, 0f),
            [new Rgb8(255, 0, 0)] = new Lab32(53f, 80f, 67f),
            [new Rgb8(0, 255, 0)] = new Lab32(87f, -86f, 83f)
        };

        // For neighborDivisor: 2 -> nearestColorCount = 2; the two zero-Lab keys have T == 0 and remain unchanged.
        SaliencyColorSmoother.SmoothColorSaliency(map, labLookup, neighborDivisor: 2);

        Assert.Equal(0.2, map[new Rgb8(0, 0, 0)], 6);
        Assert.Equal(0.4, map[new Rgb8(1, 0, 0)], 6);
        Assert.All(map.Values, value => Assert.False(double.IsNaN(value)));
    }

    [Fact]
    public void SmoothColorSaliency_ActiveSmoothing_ShouldInterpolateNearestNeighbors_AcrossAllOverloads()
    {
        // 6 distinct colors positioned along the L* axis in CIELAB
        var c1 = new Rgb8(10, 0, 0);
        var c2 = new Rgb8(20, 0, 0);
        var c3 = new Rgb8(30, 0, 0);
        var c4 = new Rgb8(70, 0, 0);
        var c5 = new Rgb8(80, 0, 0);
        var c6 = new Rgb8(90, 0, 0);

        var mapFromDict = new Dictionary<Rgb8, double>
        {
            [c1] = 0.1,
            [c2] = 0.2,
            [c3] = 0.3,
            [c4] = 0.7,
            [c5] = 0.8,
            [c6] = 0.9
        };
        var mapFromTable = new Dictionary<Rgb8, double>(mapFromDict);

        var labLookup = new Dictionary<Rgb8, Lab32>
        {
            [c1] = new Lab32(10f, 0f, 0f),
            [c2] = new Lab32(20f, 0f, 0f),
            [c3] = new Lab32(30f, 0f, 0f),
            [c4] = new Lab32(70f, 0f, 0f),
            [c5] = new Lab32(80f, 0f, 0f),
            [c6] = new Lab32(90f, 0f, 0f)
        };

        // 6 colors / 2 = 3 nearest neighbors; distances use CIEDE2000 (Lab32.Distance).
        double d12 = labLookup[c1].Distance(labLookup[c2]);
        double d13 = labLookup[c1].Distance(labLookup[c3]);
        double t1 = d12 + d13;
        double expectedC1 = ((t1 * 0.1) + ((t1 - d12) * 0.2) + ((t1 - d13) * 0.3)) / (2.0 * t1);

        double d21 = labLookup[c2].Distance(labLookup[c1]);
        double d23 = labLookup[c2].Distance(labLookup[c3]);
        double t2 = d21 + d23;
        double expectedC2 = ((t2 * 0.2) + ((t2 - d21) * 0.1) + ((t2 - d23) * 0.3)) / (2.0 * t2);

        PairwiseLabDistanceTable exactTable = PairwiseLabDistanceTable.Build(labLookup);
        var denseSaliencies = new double[exactTable.Count];
        for (int i = 0; i < exactTable.Count; i++)
        {
            denseSaliencies[i] = mapFromDict[exactTable.GetColor(i)];
        }

        SaliencyColorSmoother.SmoothColorSaliency(denseSaliencies, exactTable, neighborDivisor: 2);
        SaliencyColorSmoother.SmoothColorSaliency(mapFromDict, labLookup, neighborDivisor: 2);

        var extendedLabLookup = new Dictionary<Rgb8, Lab32>(labLookup)
        {
            [new Rgb8(15, 0, 0)] = new Lab32(15f, 0f, 0f)
        };
        PairwiseLabDistanceTable extendedTable = PairwiseLabDistanceTable.Build(extendedLabLookup);
        SaliencyColorSmoother.SmoothColorSaliency(mapFromTable, extendedTable, neighborDivisor: 2);

        Assert.Equal(expectedC1, mapFromDict[c1], 10);
        Assert.Equal(expectedC2, mapFromDict[c2], 10);
        foreach (Rgb8 key in mapFromDict.Keys)
        {
            Assert.Equal(mapFromDict[key], mapFromTable[key], 12);
            Assert.Equal(mapFromDict[key], denseSaliencies[exactTable.GetIndex(key)], 12);
        }

        Assert.All(mapFromDict.Values, value =>
        {
            Assert.False(double.IsNaN(value));
            Assert.InRange(value, 0.0, 1.0);
        });
    }

    [Fact]
    public void SmoothColorSaliency_PackedRgbTieBreaking_ShouldBeDeterministicForEqualDistances()
    {
        // Two distinct RGB colors mapped to identical Lab32 values test deterministic packed-RGB tie-breaking
        var lowRgb = new Rgb8(10, 0, 0);
        var midRgb = new Rgb8(20, 0, 0);
        var highRgb = new Rgb8(200, 0, 0);
        var farRgb = new Rgb8(250, 0, 0);

        var labLookup = new Dictionary<Rgb8, Lab32>
        {
            [highRgb] = new Lab32(50f, 0f, 0f),
            [lowRgb] = new Lab32(50f, 0f, 0f),
            [midRgb] = new Lab32(0f, 0f, 0f),
            [farRgb] = new Lab32(90f, 0f, 0f)
        };

        var mapForward = new Dictionary<Rgb8, double>
        {
            [midRgb] = 0.0,
            [lowRgb] = 0.2,
            [highRgb] = 0.8,
            [farRgb] = 1.0
        };
        var mapReverse = new Dictionary<Rgb8, double>
        {
            [farRgb] = 1.0,
            [highRgb] = 0.8,
            [lowRgb] = 0.2,
            [midRgb] = 0.0
        };

        SaliencyColorSmoother.SmoothColorSaliency(mapForward, labLookup, neighborDivisor: 2);
        SaliencyColorSmoother.SmoothColorSaliency(mapReverse, labLookup, neighborDivisor: 2);

        foreach (Rgb8 key in mapForward.Keys)
        {
            Assert.Equal(mapForward[key], mapReverse[key], 12);
        }
    }

    [Fact]
    public void PairwiseLabDistanceTable_Build_ShouldComputeSymmetricZeroDiagonalDistances()
    {
        Assert.Throws<ArgumentNullException>(() => PairwiseLabDistanceTable.Build(null!));

        PairwiseLabDistanceTable empty = PairwiseLabDistanceTable.Build(new Dictionary<Rgb8, Lab32>());
        Assert.Equal(0, empty.Count);

        var c1 = new Rgb8(20, 40, 60);
        var c2 = new Rgb8(180, 50, 30);
        var c3 = new Rgb8(40, 200, 120);
        var lookup = new Dictionary<Rgb8, Lab32>
        {
            [c1] = new Lab32(25f, -5f, -15f),
            [c2] = new Lab32(55f, 55f, 35f),
            [c3] = new Lab32(75f, -60f, 30f)
        };

        PairwiseLabDistanceTable table = PairwiseLabDistanceTable.Build(lookup);
        Assert.Equal(3, table.Count);

        Rgb8[] colors = [c1, c2, c3];
        foreach (Rgb8 left in colors)
        {
            int i = table.GetIndex(left);
            Assert.Equal(left, table.GetColor(i));

            ReadOnlySpan<double> row = table.GetRow(i);
            Assert.Equal(3, row.Length);
            Assert.Equal(0.0, row[i]);

            foreach (Rgb8 right in colors)
            {
                int j = table.GetIndex(right);
                Assert.Equal(row[j], table.GetRow(j)[i], 12);
                if (i < j)
                {
                    Assert.Equal(lookup[left].Distance(lookup[right]), row[j], 12);
                }
            }
        }
    }

    [Fact]
    public void PairwiseLabDistanceTable_Build_LargePaletteParallelPath_ShouldMatchSequentialDistances()
    {
        var lookup = new Dictionary<Rgb8, Lab32>(40);
        for (int i = 0; i < 40; i++)
        {
            lookup[new Rgb8((byte)(i * 6), (byte)((i * 13) % 255), (byte)(255 - (i * 5)))] =
                new Lab32(10f + (i * 2f), -30f + (i * 1.5f), 25f - (i * 1.2f));
        }

        PairwiseLabDistanceTable table = PairwiseLabDistanceTable.Build(lookup);
        Assert.Equal(40, table.Count);

        for (int i = 0; i < table.Count; i++)
        {
            ReadOnlySpan<double> rowI = table.GetRow(i);
            Lab32 labI = lookup[table.GetColor(i)];
            Assert.Equal(0.0, rowI[i]);

            for (int j = i + 1; j < table.Count; j++)
            {
                double expected = labI.Distance(lookup[table.GetColor(j)]);
                Assert.Equal(expected, rowI[j], 12);
                Assert.Equal(expected, table.GetRow(j)[i], 12);
            }
        }
    }

    [Fact]
    public void TryGetMinMaxRange_SpanAndEnumerableOverloads_ShouldValidateAndComputeRange()
    {
        Assert.Throws<ArgumentNullException>("values", () =>
            SaliencyColorSmoother.TryGetMinMaxRange((IEnumerable<double>)null!, out _, out _));

        Assert.False(SaliencyColorSmoother.TryGetMinMaxRange(ReadOnlySpan<double>.Empty, out double emptySpanMin, out double emptySpanRange));
        Assert.Equal(0.0, emptySpanMin);
        Assert.Equal(0.0, emptySpanRange);

        Assert.False(SaliencyColorSmoother.TryGetMinMaxRange(Enumerable.Empty<double>(), out double emptySeqMin, out double emptySeqRange));
        Assert.Equal(0.0, emptySeqMin);
        Assert.Equal(0.0, emptySeqRange);

        double[] constantArray = [3.5, 3.5, 3.5];
        Assert.False(SaliencyColorSmoother.TryGetMinMaxRange(constantArray.AsSpan(), out double constMin, out double constRange));
        Assert.Equal(3.5, constMin);
        Assert.Equal(0.0, constRange);
        Assert.False(SaliencyColorSmoother.TryGetMinMaxRange(constantArray.Select(v => v), out double constSeqMin, out double constSeqRange));
        Assert.Equal(3.5, constSeqMin);
        Assert.Equal(0.0, constSeqRange);

        double[] valuesArray = [4.0, -2.5, 7.25, 1.0];
        var valuesList = new List<double>(valuesArray);
        IEnumerable<double> lazyValues = valuesArray.Select(v => v);

        Assert.True(SaliencyColorSmoother.TryGetMinMaxRange(valuesArray.AsSpan(), out double spanMin, out double spanRange));
        Assert.Equal(-2.5, spanMin, 12);
        Assert.Equal(9.75, spanRange, 12);

        Assert.True(SaliencyColorSmoother.TryGetMinMaxRange((IEnumerable<double>)valuesArray, out double arrMin, out double arrRange));
        Assert.Equal(-2.5, arrMin, 12);
        Assert.Equal(9.75, arrRange, 12);

        Assert.True(SaliencyColorSmoother.TryGetMinMaxRange(valuesList, out double listMin, out double listRange));
        Assert.Equal(-2.5, listMin, 12);
        Assert.Equal(9.75, listRange, 12);

        Assert.True(SaliencyColorSmoother.TryGetMinMaxRange(lazyValues, out double lazyMin, out double lazyRange));
        Assert.Equal(-2.5, lazyMin, 12);
        Assert.Equal(9.75, lazyRange, 12);
    }
}
