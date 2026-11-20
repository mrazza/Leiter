namespace Leiter.Tests.Core;

using System;
using Leiter.Core;
using Xunit;

/// <summary>
/// Provides unit tests for <see cref="OptionsResolver"/>.
/// </summary>
public class OptionsResolverTests
{
    [Fact]
    public void Resolve_NullOptions_ShouldReturnDefaultWithoutValidating()
    {
        var defaultOptions = new FakeOptions(isValid: false);

        FakeOptions resolved = OptionsResolver.Resolve<FakeOptions>(null, defaultOptions);

        Assert.Same(defaultOptions, resolved);
        Assert.Equal(0, defaultOptions.ValidateCalls);
    }

    [Fact]
    public void Resolve_NonNullValidOptions_ShouldValidateAndReturnProvidedInstance()
    {
        var defaultOptions = new FakeOptions();
        var customOptions = new FakeOptions(isValid: true);

        FakeOptions resolved = OptionsResolver.Resolve(customOptions, defaultOptions);

        Assert.Same(customOptions, resolved);
        Assert.Equal(1, customOptions.ValidateCalls);
        Assert.Equal(0, defaultOptions.ValidateCalls);
    }

    [Fact]
    public void Resolve_NonNullInvalidOptions_ShouldThrowValidationException()
    {
        var defaultOptions = new FakeOptions();
        var invalidOptions = new FakeOptions(isValid: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => OptionsResolver.Resolve(invalidOptions, defaultOptions));
        Assert.Equal(1, invalidOptions.ValidateCalls);
        Assert.Equal(0, defaultOptions.ValidateCalls);
    }

    [Fact]
    public void Resolve_SingleArg_NullOptions_ShouldReturnStaticDefaultWithoutValidating()
    {
        FakeAlgorithmOptions resolved = OptionsResolver.Resolve<FakeAlgorithmOptions>(null);

        Assert.Same(FakeAlgorithmOptions.Default, resolved);
        Assert.Equal(0, FakeAlgorithmOptions.Default.ValidateCalls);
    }

    [Fact]
    public void Resolve_SingleArg_NonNullValidOptions_ShouldValidateAndReturnProvidedInstance()
    {
        var customOptions = new FakeAlgorithmOptions(scale: 2.0, isValid: true);

        FakeAlgorithmOptions resolved = OptionsResolver.Resolve(customOptions);

        Assert.Same(customOptions, resolved);
        Assert.Equal(1, customOptions.ValidateCalls);
        Assert.Equal(0, FakeAlgorithmOptions.Default.ValidateCalls);
    }

    [Fact]
    public void Resolve_SingleArg_NonNullInvalidOptions_ShouldThrowValidationException()
    {
        var invalidOptions = new FakeAlgorithmOptions(scale: -1.0, isValid: false);

        Assert.Throws<ArgumentOutOfRangeException>(() => OptionsResolver.Resolve(invalidOptions));
        Assert.Equal(1, invalidOptions.ValidateCalls);
        Assert.Equal(0, FakeAlgorithmOptions.Default.ValidateCalls);
    }

    [Fact]
    public void ISizeTunableOptions_DefaultOneArgForSize_ShouldDelegateToBaselineOverload()
    {
        var size = new Size(400, 400);
        FakeSizeTunableOptions tuned = InvokeOneArgForSize<FakeSizeTunableOptions>(size, 800.0);

        Assert.Equal(0.5, tuned.Scale, 12);
        Assert.Same(FakeSizeTunableOptions.Default, tuned.BaselineUsed);
    }

    private static T InvokeOneArgForSize<T>(Size size, double referenceDimension)
        where T : ISizeTunableOptions<T> =>
        T.ForSize(size, referenceDimension);

    private sealed class FakeOptions(bool isValid = true) : IValidatableOptions
    {
        public int ValidateCalls { get; private set; }

        public void Validate()
        {
            ValidateCalls++;
            if (!isValid)
                throw new ArgumentOutOfRangeException(nameof(isValid), "Options are invalid.");
        }
    }

    private sealed class FakeAlgorithmOptions(double scale = 1.0, bool isValid = true) : IAlgorithmOptions<FakeAlgorithmOptions>
    {
        public static FakeAlgorithmOptions Default { get; } = new();

        public double Scale { get; } = scale;

        public int ValidateCalls { get; private set; }

        public void Validate()
        {
            ValidateCalls++;
            if (!isValid)
                throw new ArgumentOutOfRangeException(nameof(isValid), "Options are invalid.");
        }
    }

    private sealed class FakeSizeTunableOptions(double scale = 1.0, FakeSizeTunableOptions? baselineUsed = null)
        : ISizeTunableOptions<FakeSizeTunableOptions>
    {
        public static FakeSizeTunableOptions Default { get; } = new(scale: 1.0);

        public double Scale { get; } = scale;

        public FakeSizeTunableOptions? BaselineUsed { get; } = baselineUsed;

        public void Validate()
        {
            Guard.ThrowIfNotFinitePositive(Scale);
        }

        public static FakeSizeTunableOptions ForSize(
            Size size,
            FakeSizeTunableOptions baseline,
            double referenceDimension = ISizeTunableOptions<FakeSizeTunableOptions>.DefaultReferenceDimension) =>
            new(baseline.Scale * (size.LinearDimension / referenceDimension), baseline);
    }
}
