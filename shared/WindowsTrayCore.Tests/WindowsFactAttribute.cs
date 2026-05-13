using Xunit;

namespace WindowsTrayCore.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
            Skip = "Windows-only";
    }
}
