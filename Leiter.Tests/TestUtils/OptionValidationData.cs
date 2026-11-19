namespace Leiter.Tests.TestUtils;

using Xunit;

/// <summary>
/// Shared invalid-domain <see cref="TheoryData{T}"/> sets for options validation unit tests.
/// </summary>
public static class OptionValidationData
{
    /// <summary>
    /// Invalid values for parameters constrained to <c>[0, +infinity)</c>.
    /// </summary>
    public static TheoryData<double> InvalidNonNegativeDoubles =>
        [-0.01, -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity];

    /// <summary>
    /// Invalid values for parameters constrained to <c>(0, +infinity)</c>.
    /// </summary>
    public static TheoryData<double> InvalidPositiveDoubles =>
        [0.0, -0.01, -1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity];

    /// <summary>
    /// Invalid values for parameters constrained to <c>[0, 1]</c>.
    /// </summary>
    public static TheoryData<double> InvalidUnitIntervalDoubles =>
        [-0.01, 1.01, double.NaN, double.PositiveInfinity, double.NegativeInfinity];

    /// <summary>
    /// Invalid values for parameters constrained to <c>(0, 1]</c>.
    /// </summary>
    public static TheoryData<double> InvalidPositiveUnitIntervalDoubles =>
        [0.0, -0.01, 1.01, double.NaN, double.PositiveInfinity, double.NegativeInfinity];

    /// <summary>
    /// Invalid values for integer parameters constrained to <c>&gt;= 1</c>.
    /// </summary>
    public static TheoryData<int> InvalidPositiveInts => [0, -1, -10];

    /// <summary>
    /// Invalid values for integer parameters constrained to <c>&gt;= 0</c>.
    /// </summary>
    public static TheoryData<int> InvalidNonNegativeInts => [-1, -10];
}
