namespace Leiter.Tests.TestUtils;

using System;
using System.Collections.Generic;
using Leiter.Core;
using Leiter.Pixels;
using Xunit;

/// <summary>
/// Provides element-wise assertion helpers for <see cref="IReadOnlyMatrix{T}"/> values in tests.
/// </summary>
public static class MatrixAssert
{
    /// <summary>
    /// Asserts that two matrices have identical dimensions and identical element values at every coordinate.
    /// </summary>
    public static void Equal<T>(IReadOnlyMatrix<T> expected, IReadOnlyMatrix<T> actual)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        Assert.Equal(expected.Size, actual.Size);
        var comparer = EqualityComparer<T>.Default;
        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                if (!comparer.Equals(expected[x, y], actual[x, y]))
                {
                    Assert.Fail($"Element mismatch at ({x}, {y}): expected {expected[x, y]}, actual {actual[x, y]}.");
                }
            }
        }
    }

    /// <summary>
    /// Asserts that two matrices differ in either dimensions or at least one element value.
    /// </summary>
    public static void NotEqual<T>(IReadOnlyMatrix<T> first, IReadOnlyMatrix<T> second)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Size != second.Size)
        {
            return;
        }

        var comparer = EqualityComparer<T>.Default;
        for (int y = 0; y < first.Height; y++)
        {
            for (int x = 0; x < first.Width; x++)
            {
                if (!comparer.Equals(first[x, y], second[x, y]))
                {
                    return;
                }
            }
        }

        Assert.Fail($"Matrices of size {first.Size} were unexpectedly equal at every coordinate.");
    }

    /// <summary>
    /// Asserts that two <see cref="DoublePixel"/> matrices match within <paramref name="tolerance"/> at every coordinate.
    /// </summary>
    public static void ApproxEqual(
        IReadOnlyMatrix<DoublePixel> expected,
        IReadOnlyMatrix<DoublePixel> actual,
        double tolerance = 1e-6)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        Assert.Equal(expected.Size, actual.Size);
        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                double diff = Math.Abs(expected[x, y].Value - actual[x, y].Value);
                if (!(diff <= tolerance))
                {
                    Assert.Fail(
                        $"Value mismatch at ({x}, {y}): expected {expected[x, y].Value}, actual {actual[x, y].Value} (diff {diff} > {tolerance}).");
                }
            }
        }
    }

    /// <summary>
    /// Asserts that two <see cref="Rgb8"/> matrices match within <paramref name="maxChannelDiff"/> per channel at every coordinate.
    /// </summary>
    public static void ApproxEqual(
        IReadOnlyMatrix<Rgb8> expected,
        IReadOnlyMatrix<Rgb8> actual,
        int maxChannelDiff = 1)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);

        Assert.Equal(expected.Size, actual.Size);
        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
            {
                Rgb8 e = expected[x, y];
                Rgb8 a = actual[x, y];
                if (Math.Abs(e.R - a.R) > maxChannelDiff ||
                    Math.Abs(e.G - a.G) > maxChannelDiff ||
                    Math.Abs(e.B - a.B) > maxChannelDiff)
                {
                    Assert.Fail($"Channel mismatch at ({x}, {y}): expected {e}, actual {a} (max diff {maxChannelDiff}).");
                }
            }
        }
    }

    /// <summary>
    /// Asserts that every element in a <see cref="DoublePixel"/> matrix falls within [<paramref name="min"/>, <paramref name="max"/>].
    /// </summary>
    public static void AllInRange(IReadOnlyMatrix<DoublePixel> matrix, double min, double max)
    {
        ArgumentNullException.ThrowIfNull(matrix);

        for (int y = 0; y < matrix.Height; y++)
        {
            for (int x = 0; x < matrix.Width; x++)
            {
                double val = matrix[x, y].Value;
                Assert.InRange(val, min, max);
            }
        }
    }
}
