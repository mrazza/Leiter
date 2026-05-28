
using Xunit;
using Leiter.Algorithms.DataStructures;
using System;
using System.Collections.Generic;

namespace Leiter.Tests.Algorithms.DataStructures;

public class HistogramTests
{
    [Fact]
    public void DictionaryHistogram_ShouldCountCorrectly()
    {
        var hist = new DictionaryHistogram<string>();
        Assert.Equal(0, hist.BucketCount);
        Assert.Equal(0, hist.Total());
        Assert.Equal(0, hist["apple"]);
        // If total is 0, GetCount / 0 results in double.NaN in C# double division
        Assert.True(double.IsNaN(hist.GetDensity("apple")));

        hist.Increment("apple");
        hist.Increment("banana", 2);

        Assert.Equal(2, hist.BucketCount);
        Assert.Equal(3, hist.Total());
        Assert.Equal(1, hist["apple"]);
        Assert.Equal(2, hist["banana"]);
        Assert.Equal(1.0 / 3.0, hist.GetDensity("apple"), 5);

        // Buckets enumerator
        var buckets = new HashSet<string>(hist.Buckets);
        Assert.Contains("apple", buckets);
        Assert.Contains("banana", buckets);

        // ToString
        var str = hist.ToString();
        Assert.Contains("apple=1", str);
        Assert.Contains("banana=2", str);

        // KeyValuePair Enumerator
        var list = new List<KeyValuePair<string, int>>(hist);
        Assert.Equal(2, list.Count);
    }

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
}
