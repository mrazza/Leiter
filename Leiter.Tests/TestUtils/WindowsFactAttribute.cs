namespace Leiter.Tests.TestUtils;

using Xunit;

/// <summary>
/// Marks a test method that executes only on Windows and skips on non-Windows platforms.
/// </summary>
public sealed class WindowsFactAttribute : FactAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WindowsFactAttribute"/> class.
    /// </summary>
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "Windows platform tests run only on Windows.";
        }
    }
}
