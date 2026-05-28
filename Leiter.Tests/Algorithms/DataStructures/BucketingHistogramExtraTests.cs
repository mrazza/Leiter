
using Xunit;
using Leiter.Algorithms.DataStructures;
using System.Collections.Generic;

namespace Leiter.Tests.Algorithms.DataStructures;

public class BucketingHistogramExtraTests
{
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
