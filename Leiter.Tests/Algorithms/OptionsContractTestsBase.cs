namespace Leiter.Tests.Algorithms;

using System;
using Leiter.Core;
using Xunit;

/// <summary>
/// Shared contract tests for <see cref="IAlgorithmOptions{TSelf}"/> implementations.
/// </summary>
/// <typeparam name="TOptions">The concrete options record type under test.</typeparam>
public abstract class AlgorithmOptionsContractTestsBase<TOptions>
    where TOptions : class, IAlgorithmOptions<TOptions>
{
    /// <summary>
    /// Creates a valid, non-default configuration distinct from <typeparamref name="TOptions"/>.<see cref="IAlgorithmOptions{TSelf}.Default"/>.
    /// </summary>
    protected abstract TOptions CreateValidCustomOptions();

    /// <summary>
    /// Creates an invalid configuration expected to fail <see cref="IValidatableOptions.Validate"/>.
    /// </summary>
    protected abstract TOptions CreateInvalidOptions();

    /// <summary>
    /// Verifies that <typeparamref name="TOptions"/>.<see cref="IAlgorithmOptions{TSelf}.Default"/> is non-null and valid.
    /// </summary>
    [Fact]
    public void Contract_Default_ShouldBeNonNullAndValid()
    {
        TOptions defaults = TOptions.Default;
        Assert.NotNull(defaults);
        defaults.Validate();
    }

    /// <summary>
    /// Verifies that custom valid options pass validation and differ from <see cref="IAlgorithmOptions{TSelf}.Default"/>.
    /// </summary>
    [Fact]
    public void Contract_CustomValidOptions_ShouldBeValidAndDistinctFromDefault()
    {
        TOptions custom = CreateValidCustomOptions();
        Assert.NotNull(custom);
        Assert.NotEqual(TOptions.Default, custom);
        custom.Validate();
    }

    /// <summary>
    /// Verifies that invalid options fail <see cref="IValidatableOptions.Validate"/>.
    /// </summary>
    [Fact]
    public void Contract_InvalidOptions_ShouldFailValidation()
    {
        TOptions invalid = CreateInvalidOptions();
        Assert.ThrowsAny<ArgumentException>(() => invalid.Validate());
    }

    /// <summary>
    /// Verifies that <see cref="OptionsResolver.Resolve{T}(T?)"/> returns defaults for <see langword="null"/>,
    /// validates and returns valid instances, and throws for invalid instances.
    /// </summary>
    [Fact]
    public void Contract_OptionsResolver_ShouldResolveNullValidAndInvalid()
    {
        Assert.Same(TOptions.Default, OptionsResolver.Resolve<TOptions>(null));

        TOptions custom = CreateValidCustomOptions();
        Assert.Same(custom, OptionsResolver.Resolve(custom));

        TOptions invalid = CreateInvalidOptions();
        Assert.ThrowsAny<ArgumentException>(() => OptionsResolver.Resolve(invalid));
    }
}

/// <summary>
/// Shared contract tests for <see cref="ISizeTunableOptions{TSelf}"/> implementations.
/// </summary>
/// <typeparam name="TOptions">The concrete size-tunable options record type under test.</typeparam>
public abstract class SizeTunableOptionsContractTestsBase<TOptions> : AlgorithmOptionsContractTestsBase<TOptions>
    where TOptions : class, ISizeTunableOptions<TOptions>
{
    /// <summary>
    /// Verifies that <see cref="ISizeTunableOptions{TSelf}.ForSize(Size, double)"/> and
    /// <see cref="ISizeTunableOptions{TSelf}.ForSize(Size, TSelf, double)"/> preserve the baseline for zero-area dimensions.
    /// </summary>
    [Fact]
    public void Contract_ForSize_EmptySizes_ShouldPreserveBaseline()
    {
        TOptions custom = CreateValidCustomOptions();

        foreach (Size emptySize in new[] { new Size(0, 0), new Size(0, 16), new Size(16, 0) })
        {
            Assert.Equal(TOptions.Default, TOptions.ForSize(emptySize));
            Assert.Equal(custom, TOptions.ForSize(emptySize, custom));
        }
    }

    /// <summary>
    /// Verifies that <see cref="ISizeTunableOptions{TSelf}.ForSize(Size, TSelf, double)"/> preserves custom baselines at
    /// the default reference area and produces valid options across degenerate and large sizes.
    /// </summary>
    [Fact]
    public void Contract_ForSize_ReferenceAreaAndExtremeSizes_ShouldStayValid()
    {
        TOptions custom = CreateValidCustomOptions();

        Assert.Equal(custom, TOptions.ForSize(new Size(800, 800), custom));
        Assert.Equal(custom, TOptions.ForSize(new Size(1000, 640), custom));

        foreach (Size size in new[] { new Size(0, 0), new Size(1, 1), new Size(3, 3), new Size(4000, 80), new Size(12000, 9000) })
        {
            TOptions.ForSize(size).Validate();
            TOptions.ForSize(size, custom).Validate();
        }
    }

    /// <summary>
    /// Verifies that <see cref="ISizeTunableOptions{TSelf}.ForSize(Size, double)"/> and
    /// <see cref="ISizeTunableOptions{TSelf}.ForSize(Size, TSelf, double)"/> reject invalid sizes, reference dimensions, and baselines.
    /// </summary>
    [Fact]
    public void Contract_ForSize_InvalidArguments_ShouldThrow()
    {
        TOptions custom = CreateValidCustomOptions();
        TOptions invalid = CreateInvalidOptions();

        Assert.Throws<ArgumentOutOfRangeException>(() => TOptions.ForSize(new Size(-1, 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TOptions.ForSize(new Size(100, -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => TOptions.ForSize(new Size(-1, 100), custom));
        Assert.Throws<ArgumentOutOfRangeException>(() => TOptions.ForSize(new Size(100, -1), custom));
        Assert.Throws<ArgumentOutOfRangeException>(() => TOptions.ForSize(new Size(100, 100), 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TOptions.ForSize(new Size(100, 100), -5.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TOptions.ForSize(new Size(100, 100), double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => TOptions.ForSize(new Size(100, 100), double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => TOptions.ForSize(new Size(100, 100), custom, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => TOptions.ForSize(new Size(100, 100), custom, double.NegativeInfinity));
        Assert.Throws<ArgumentNullException>(() => TOptions.ForSize(new Size(100, 100), baseline: null!));
        Assert.ThrowsAny<ArgumentException>(() => TOptions.ForSize(new Size(100, 100), invalid));
        Assert.ThrowsAny<ArgumentException>(() => TOptions.ForSize(new Size(0, 0), invalid));
    }
}
