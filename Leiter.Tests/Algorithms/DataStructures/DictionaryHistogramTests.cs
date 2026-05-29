using Xunit;
using Leiter.Algorithms.DataStructures;
using System;
using System.Collections.Generic;

namespace Leiter.Tests.Algorithms.DataStructures;

/// <summary>
/// Provides unit tests for <see cref="DictionaryHistogram{T}" />.
/// </summary>
public class DictionaryHistogramTests
{
    /// <summary>
    /// Verifies that the dictionary histogram should count correctly behaves correctly.
    /// </summary>
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

    /// <summary>
    /// Executes the test dictionary histogram enumerable operation.
    /// </summary>
    [Fact]
    public void TestDictionaryHistogram_IEnumerable()
    {
        var hist = new DictionaryHistogram<string>();
        hist.Increment("test");
        var enumerable = (System.Collections.IEnumerable)hist;
        var enumerator = enumerable.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);
    }
}
