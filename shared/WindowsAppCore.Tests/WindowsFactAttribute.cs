using Xunit;

namespace WindowsAppCore.Tests;

/// <summary>
/// Marks a test that must run on Windows. The test is skipped on other platforms,
/// allowing the test project to build and run on cross-platform CI without failure.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Windows-only";
    }
}
