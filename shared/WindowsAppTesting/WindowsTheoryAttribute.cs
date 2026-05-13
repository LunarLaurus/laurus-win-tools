using Xunit;

namespace WindowsAppTesting;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class WindowsTheoryAttribute : TheoryAttribute
{
    public WindowsTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Windows-only";
    }
}
