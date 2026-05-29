using Leiter.Tests.TestUtils;
using Xunit;
using Leiter.Core;
using Leiter.Pixels;
using System.Collections.Generic;

namespace Leiter.Tests.Core;

/// <summary>
/// Provides unit tests or helpers for <see cref="IReadOnlyMatrix{T}" />.
/// </summary>
public class IReadOnlyMatrixTests
{
    /// <summary>
    /// Executes the test explicit interface counts operation.
    /// </summary>
    [Fact]
    public void TestExplicitInterfaceCounts()
    {
        var m = new DummyReadOnlyMatrix();
        IReadOnlyMatrix<DoublePixel> rom = m;
        IUntypedMatrix utm = rom;
        IReadOnlyCollection<DoublePixel> roc = rom;

        // Force call explicit interface default implementations in IReadOnlyMatrix
        Assert.Equal(4, utm.Count);
        Assert.Equal(4, roc.Count);
    }
}
