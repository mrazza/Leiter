namespace Leiter.Core;

/// <summary>
/// Resolves optional algorithm configuration objects to validated instances or cached defaults.
/// </summary>
internal static class OptionsResolver
{
    /// <summary>
    /// Returns <paramref name="defaultOptions"/> when <paramref name="options"/> is <see langword="null"/>;
    /// otherwise validates <paramref name="options"/> and returns it.
    /// </summary>
    public static T Resolve<T>(T? options, T defaultOptions)
        where T : class, IValidatableOptions
    {
        if (options is null)
            return defaultOptions;

        options.Validate();
        return options;
    }
}
