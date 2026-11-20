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
}
