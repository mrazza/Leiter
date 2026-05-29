using Xunit;
using Leiter.Algorithms.DataStructures;
using System;
using System.Collections.Generic;

namespace Leiter.Tests.Algorithms.DataStructures;

/// <summary>
/// Provides unit tests for <see cref="BucketingHistogram{T}" />.
/// </summary>
public class BucketingHistogramTests
{
    /// <summary>
    /// Verifies that the bucketing histogram should bucket correctly behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Executes the test bucketing histogram enumerable operation.
    /// </summary>
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
}
