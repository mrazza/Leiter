namespace Leiter.Tests.Algorithms.DataStructures;

using System;
using System.Collections.Generic;
using Leiter.Algorithms.DataStructures;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="BucketingHistogram{T}" />.
/// </summary>
public class BucketingHistogramTests
{
    [Fact]
    public void BucketingHistogram_ShouldBucketCorrectly()
    {
        // Bucket integers by parity (even/odd)
        var hist = new BucketingHistogram<int>(val => val % 2);
        
        hist.Increment(1); // odd (1)
        hist.Increment(2); // even (0)
        hist.Increment(3); // odd (1)

        Assert.Equal(2, hist.BucketCount);
        Assert.Equal(3, hist.Total());
        Assert.Equal(2, hist[1]); // odd bucket has 2
        Assert.Equal(1, hist[2]); // even bucket has 1

        // MapBuckets
        var mapped = hist.MapBuckets(b => b == 0 ? "Even" : "Odd");
        Assert.Equal(2, mapped.BucketCount);
        Assert.Equal(3, mapped.Total());
        Assert.Equal(2, mapped["Odd"]);
        Assert.Equal(1, mapped["Even"]);
    }

    [Fact]
    public void TestBucketingHistogram_IEnumerable()
    {
        var hist = new BucketingHistogram<string>(val => val);
        hist.Increment("test");
        var enumerable = (System.Collections.IEnumerable)hist;
        var enumerator = enumerable.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);
    }

    [Fact]
    public void GetDensity_WhenEmpty_ShouldReturnZero_And_NullCallbacksShouldThrow()
    {
        var emptyHist = new BucketingHistogram<int>(static val => val);
        Assert.Equal(0.0, emptyHist.GetDensity(123));

        var exCtor = Assert.Throws<ArgumentNullException>(() => new BucketingHistogram<int>(null!));
        Assert.Equal("bucketer", exCtor.ParamName);

        var exMap = Assert.Throws<ArgumentNullException>(() => emptyHist.MapBuckets<string>(null!));
        Assert.Equal("mapFunc", exMap.ParamName);
    }
}
