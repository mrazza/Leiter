
using Xunit;
using Leiter.Algorithms.DataStructures;
using System.Collections.Generic;

namespace Leiter.Tests.Algorithms.DataStructures;

/// <summary>
/// Provides unit tests or helpers for <see cref="BucketingHistogramExtraTests" />.
/// </summary>
public class BucketingHistogramExtraTests
{
    /// <summary>
    /// Executes the test bucketing histogram enumerable operation.
    /// </summary>
    [Fact]
    public void TestBucketingHistogram_IEnumerable()
    {
        var hist = new DictionaryHistogram<string>();
        hist.Increment("test");
        var enumerable = (System.Collections.IEnumerable)hist;
        var enumerator = enumerable.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.NotNull(enumerator.Current);
    }
}
